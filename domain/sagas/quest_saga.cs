// domain/sagas/quest_saga.cs
using dnd_game.Application.Projections;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Events;
using dnd_game.Domain.Interfaces;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Domain.Sagas
{
    public class QuestSaga : ISaga
    {
        private readonly ICommandBus _commandBus;
        private readonly CampaignProjection _campaignProjection;
        private readonly CharacterProjection _characterProjection;
        private readonly IQuestTrackingStore _trackingStore;
        private QuestSagaState _state;

        public QuestSaga(Guid questId, ICommandBus commandBus, CampaignProjection campaignProjection, CharacterProjection characterProjection, IQuestTrackingStore trackingStore)
        {
            _commandBus = commandBus;
            _campaignProjection = campaignProjection;
            _characterProjection = characterProjection;
            _trackingStore = trackingStore;
            _state = new QuestSagaState { SagaId = questId, CorrelationId = questId, QuestId = questId };
        }

        public Guid SagaId => _state.SagaId;
        public ISagaState State => _state;

        public void LoadState(ISagaState state) => _state = (QuestSagaState)state;

        public async Task Handle(IDomainEvent @event, CancellationToken cancellationToken = default)
        {
            switch (@event)
            {
                case QuestAccepted accepted:
                    await OnQuestAccepted(accepted);
                    break;
                case QuestObjectiveUpdated objectiveUpdated:
                    await OnObjectiveUpdated(objectiveUpdated, cancellationToken);
                    break;
                case QuestCompleted completed:
                    await OnQuestCompleted(completed, cancellationToken);
                    break;
                case QuestFailed failed:
                    OnQuestFailed(failed);
                    break;
                case CharacterDied died:
                    await OnCharacterDied(died);
                    break;
                case ItemAcquired acquired:
                    await OnItemAcquired(acquired);
                    break;
                default:
                    break;
            }
        }

        public Task Complete(bool success, string? reason = null, CancellationToken cancellationToken = default)
        {
            _state.Status = success ? SagaStatus.Completed : SagaStatus.Failed;
            return Task.CompletedTask;
        }

        private async Task OnQuestAccepted(QuestAccepted e)
        {
            var questInfo = await _campaignProjection.GetQuestDetails(e.CampaignId, e.QuestId);
            if (questInfo == null) return;

            _state.CampaignId = e.CampaignId;
            _state.QuestStatus = QuestSagaStatus.InProgress;
            _state.Status = SagaStatus.InProgress;
            _state.Objectives = questInfo.Objectives.Select(o => new TrackedObjective
            {
                Description = o.Description,
                RequiredProgress = o.RequiredProgress,
                CurrentProgress = 0
            }).ToList();
            _state.Rewards = questInfo.Rewards.Select(r => new QuestRewardData
            {
                Description = r.Description,
                ExperiencePoints = r.ExperiencePoints,
                ItemIds = r.ItemIds,
                Gold = r.Gold,
                FactionReputationChange = r.FactionReputationChange
            }).ToList();

            // Добавляем участников в tracking store
            foreach (var participantId in e.ParticipantIds)
            {
                _trackingStore.AddParticipant(e.QuestId, participantId);
            }

            // Запоминаем кампанию квеста — понадобится обработчикам событий вроде
            // CharacterDied, которые не несут CampaignId напрямую и обрабатываются
            // "одноразовым" инстансом QuestSaga без загруженного состояния (см. OnCharacterDied).
            _trackingStore.SetCampaign(e.QuestId, e.CampaignId);
        }

        private async Task OnCharacterDied(CharacterDied e)
        {
            // ВАЖНО: этот обработчик запускается на "одноразовом" инстансе QuestSaga,
            // созданном специально под CharacterDied (SagaId = CharacterId, не QuestId —
            // см. saga_registrations.cs), поэтому _state здесь НЕ содержит реального
            // состояния ни одного квеста. Раньше код проверял _state.CampaignId, который
            // в этом инстансе всегда Guid.Empty — FailQuestCommand из-за этого никогда не
            // отправлялся. CampaignId для каждого найденного квеста теперь ищется отдельно
            // через _trackingStore (заполняется в OnQuestAccepted).
            var questIds = _trackingStore.GetQuestsForCharacter(e.CharacterId).ToList();
            foreach (var questId in questIds)
            {
                var campaignId = _trackingStore.GetCampaign(questId);
                if (campaignId is { } campaign)
                {
                    await _commandBus.SendAsync(new FailQuestCommand(campaign, questId));
                }
            }
        }

        private async Task OnItemAcquired(ItemAcquired e)
        {
            // ВАЖНО: этот обработчик пока намеренно не реализован до конца — он находит
            // квесты, требующие предмет e.ItemId, но не обновляет их прогресс. Причина:
            // непонятно, какую конкретно цель квеста (TrackedObjective) должно продвигать
            // получение предмета — нужен явный маппинг "предмет -> индекс цели", которого
            // сейчас нет ни в модели квеста, ни в IQuestTrackingStore. Это отдельная задача
            // проектирования, а не однострочный фикс — реализовывать не стал, чтобы не
            // выдавать частичную/неверную логику начисления прогресса за готовую фичу.
            var questIds = _trackingStore.GetQuestsForItem(e.ItemId).ToList();
            _ = questIds; // намеренно не используется дальше — см. комментарий выше
            await Task.CompletedTask;
        }

        private async Task OnObjectiveUpdated(QuestObjectiveUpdated e, CancellationToken cancellationToken)
        {
            if (_state.QuestStatus != QuestSagaStatus.InProgress) return;

            if (e.ObjectiveIndex >= 0 && e.ObjectiveIndex < _state.Objectives.Count)
            {
                var obj = _state.Objectives[e.ObjectiveIndex];
                obj.CurrentProgress = e.CurrentProgress;
                obj.IsCompleted = e.IsCompleted;
            }

            if (_state.Objectives.Count > 0 && _state.Objectives.All(o => o.IsCompleted))
            {
                await _commandBus.SendAsync(new CompleteQuestCommand(_state.CampaignId, e.QuestId), new CommandContext { CancellationToken = cancellationToken });
            }
        }

        private async Task OnQuestCompleted(QuestCompleted e, CancellationToken cancellationToken)
        {
            if (_state.QuestStatus == QuestSagaStatus.Completed) return;

            var characters = await _characterProjection.GetAll();
            foreach (var character in characters)
            {
                await GrantRewards(character.Id, _state.Rewards, cancellationToken);
            }

            _state.QuestStatus = QuestSagaStatus.Completed;
            _state.Status = SagaStatus.Completed;
            _trackingStore.RemoveQuest(e.QuestId);
        }

        private void OnQuestFailed(QuestFailed e)
        {
            _state.QuestStatus = QuestSagaStatus.Failed;
            _state.Status = SagaStatus.Failed;
            _trackingStore.RemoveQuest(e.QuestId);
        }

        private async Task GrantRewards(Guid characterId, List<QuestRewardData> rewards, CancellationToken cancellationToken)
        {
            foreach (var reward in rewards)
            {
                if (reward.ExperiencePoints > 0)
                    await _commandBus.SendAsync(new GainExperience(characterId, reward.ExperiencePoints), new CommandContext { CancellationToken = cancellationToken });

                if (reward.Gold > 0)
                    await _commandBus.SendAsync(new AddGold(characterId, reward.Gold), new CommandContext { CancellationToken = cancellationToken });

                foreach (var itemId in reward.ItemIds)
                    await _commandBus.SendAsync(new AddInventoryItem(characterId, itemId, itemId, 1), new CommandContext { CancellationToken = cancellationToken });

                if (!string.IsNullOrEmpty(reward.FactionReputationChange))
                {
                    var parts = reward.FactionReputationChange.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int change))
                        await _commandBus.SendAsync(new ChangeFactionReputation(characterId, parts[0], change), new CommandContext { CancellationToken = cancellationToken });
                }
            }
        }

        private class QuestSagaState : ISagaState
        {
            public Guid SagaId { get; set; }
            public Guid CorrelationId { get; set; }
            public SagaStatus Status { get; set; } = SagaStatus.Started;
            public int Version { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime? UpdatedAt { get; set; }
            public Guid QuestId { get; set; }
            public Guid CampaignId { get; set; }
            public QuestSagaStatus QuestStatus { get; set; } = QuestSagaStatus.InProgress;
            public List<TrackedObjective> Objectives { get; set; } = [];
            public List<QuestRewardData> Rewards { get; set; } = [];
        }

        private class TrackedObjective
        {
            public string Description { get; set; } = string.Empty;
            public bool IsCompleted { get; set; }
            public int CurrentProgress { get; set; }
            public int RequiredProgress { get; set; }
        }

        private enum QuestSagaStatus
        {
            InProgress,
            Completed,
            Failed
        }
    }
}