// domain/sagas/combat_saga.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Aggregates;
using dnd_game.infrastructure.message_bus;

namespace dnd_game.Domain.Sagas
{
    /// <summary>
    /// Сага, управляющая жизненным циклом боевой сцены в DnD:
    /// сбор инициативы, смена раундов и ходов, завершение боя при выполнении условий.
    /// </summary>
    /// <summary>
    /// РћРґРёРЅ РёРЅСЃС‚Р°РЅСЃ CombatSaga = РѕРґРёРЅ Р±РѕР№: SagaId = CombatId, Р·Р°РґР°С‘С‚СЃСЏ СЃСЂР°Р·Сѓ РІ РєРѕРЅСЃС‚СЂСѓРєС‚РѕСЂРµ.
    /// Р Р°РЅСЊС€Рµ SagaId СѓСЃС‚Р°РЅР°РІР»РёРІР°Р»СЃСЏ С‚РѕР»СЊРєРѕ РІРЅСѓС‚СЂРё OnCombatStarted (РёР»Рё С‡РµСЂРµР· LoadState), Р°
    /// РґРѕ СЌС‚РѕРіРѕ РјРѕРјРµРЅС‚Р° _state РѕСЃС‚Р°РІР°Р»СЃСЏ null вЂ” РЅРѕ SagaCoordinator.DispatchAsync РѕР±СЂР°С‰Р°РµС‚СЃСЏ
    /// Рє saga.SagaId СЃСЂР°Р·Сѓ РїРѕСЃР»Рµ СЃРѕР·РґР°РЅРёСЏ РёРЅСЃС‚Р°РЅСЃР°, Р”Рћ РІС‹Р·РѕРІР° Handle/LoadState, РїРѕСЌС‚РѕРјСѓ
    /// РїРµСЂРІС‹Р№ Р¶Рµ РґРёСЃРїР°С‚С‡ РїР°РґР°Р» СЃ NullReferenceException. РўРµРїРµСЂСЊ _state СЃРѕР·РґР°С‘С‚СЃСЏ СЃСЂР°Р·Сѓ.
    /// </summary>
    public class CombatSaga : ISaga, ICommandingSaga
    {
        private ICommandBus _commandBus;
        private CombatSagaState _state;

        public CombatSaga(Guid combatId, ICommandBus commandBus)
        {
            _commandBus = commandBus;
            _state = new CombatSagaState { SagaId = combatId, CorrelationId = combatId, CombatId = combatId };
        }

        public void LoadState(ISagaState state)
        {
            _state = state as CombatSagaState
                     ?? throw new ArgumentException("Invalid saga state type", nameof(state));
        }

        public Guid SagaId => _state.SagaId;
        public ISagaState State => _state;

        public async Task Handle(IDomainEvent @event, CancellationToken cancellationToken)
        {
            switch (@event)
            {
                case CombatStarted combatStarted:
                    await OnCombatStarted(combatStarted, cancellationToken);
                    break;

                case InitiativeRolled initiativeRolled:
                    await OnInitiativeRolled(initiativeRolled, cancellationToken);
                    break;

                case CombatRoundStarted roundStarted:
                    await OnRoundStarted(roundStarted, cancellationToken);
                    break;

                case CombatTurnEnded turnEnded:
                    await OnTurnEnded(turnEnded, cancellationToken);
                    break;

                case CharacterDied characterDied:
                    await OnCharacterDied(characterDied, cancellationToken);
                    break;

                case ParticipantRemovedFromCombat participantRemoved:
                    await OnParticipantRemoved(participantRemoved, cancellationToken);
                    break;

                // Другие события при необходимости можно добавить
                default:
                    break;
            }
        }

        public async Task Complete(bool success, string? reason = null, CancellationToken cancellationToken = default)
        {
            if (!_state.IsActive) return;
            _state.IsActive = false;
            _state.Status = success ? SagaStatus.Completed : SagaStatus.Failed;
            _state.CompletionReason = reason;
            // Р Р°РЅСЊС€Рµ Р·РґРµСЃСЊ СЃС‚РѕСЏР»Р° РїСЂРѕРІРµСЂРєР° `if (_state.IsActive)`, РєРѕС‚РѕСЂР°СЏ РїРѕСЃР»Рµ СЃС‚СЂРѕРєРё
            // РІС‹С€Рµ (_state.IsActive = false) Р±С‹Р»Р° РІСЃРµРіРґР° Р»РѕР¶РЅРѕР№ вЂ” EndCombat РЅРёРєРѕРіРґР° РЅРµ
            // РѕС‚РїСЂР°РІР»СЏР»СЃСЏ. РњС‹ СѓР¶Рµ Р·РЅР°РµРј, С‡С‚Рѕ Р±РѕР№ Р±С‹Р» Р°РєС‚РёРІРµРЅ (РёРЅР°С‡Рµ РІС‹С€Р»Рё Р±С‹ РЅР° РїРµСЂРІРѕР№
            // СЃС‚СЂРѕРєРµ), РїРѕСЌС‚РѕРјСѓ РѕС‚РїСЂР°РІР»СЏРµРј EndCombat Р±РµР·СѓСЃР»РѕРІРЅРѕ.
            await SendCommand(new EndCombat(_state.CombatId), cancellationToken);
        }

        public async Task SendCommand(ICommand command, CancellationToken cancellationToken = default)
        {
            await _commandBus.SendAsync(command, new CommandContext { CancellationToken = cancellationToken });
        }

        // ---------- Приватные методы-реакции на события ----------

        private async Task OnCombatStarted(CombatStarted e, CancellationToken cancellationToken)
        {
            _state = new CombatSagaState
            {
                SagaId = e.CombatId,
                CorrelationId = e.CombatId,
                CombatId = e.CombatId,
                Participants = e.Participants.ToDictionary(
                    id => id,
                    id => new CombatSagaParticipant { CharacterId = id }),
                IsActive = true,
                Status = SagaStatus.Started,
                CreatedAt = DateTime.UtcNow
            };

            // Отправляем команды на бросок инициативы для каждого участника
            foreach (var participantId in e.Participants)
            {
                await SendCommand(new RollInitiative(e.CombatId, participantId, 0, 0), cancellationToken);
            }
        }

        private async Task OnInitiativeRolled(InitiativeRolled e, CancellationToken cancellationToken)
        {
            if (_state == null || _state.CombatId != e.CombatId) return;

            if (_state.Participants.TryGetValue(e.CharacterId, out var participant))
            {
                participant.Initiative = e.Initiative;
                participant.DexterityModifier = e.DexterityModifier;
                participant.HasRolledInitiative = true;
            }

            // Проверяем, все ли бросили инициативу
            if (_state.Participants.Values.All(p => p.HasRolledInitiative))
            {
                await SendCommand(new StartRound(_state.CombatId), cancellationToken);
            }
        }

        private async Task OnRoundStarted(CombatRoundStarted e, CancellationToken cancellationToken)
        {
            if (_state == null || _state.CombatId != e.CombatId) return;

            _state.Round = e.Round;
            _state.CurrentTurnIndex = 0;

            // Сортируем участников по убыванию инициативы
            var sorted = _state.Participants.Values
                .OrderByDescending(p => p.Initiative)
                .ThenByDescending(p => p.DexterityModifier)
                .Select(p => p.CharacterId)
                .ToList();
            _state.TurnOrder = sorted;

            // Начинаем первый ход
            if (sorted.Count > 0)
                await SendCommand(new NextTurn(_state.CombatId), cancellationToken);
        }

        private async Task OnTurnEnded(CombatTurnEnded e, CancellationToken cancellationToken)
        {
            if (_state == null || _state.CombatId != e.CombatId) return;

            int nextIndex = _state.CurrentTurnIndex + 1;
            if (nextIndex < _state.TurnOrder.Count)
            {
                _state.CurrentTurnIndex = nextIndex;
                await SendCommand(new NextTurn(_state.CombatId), cancellationToken);
            }
            else
            {
                // Конец раунда
                await SendCommand(new EndRound(_state.CombatId), cancellationToken);
                // Если бой ещё активен, начнётся новый раунд (StartRound) – его вызовет обработчик EndRound или внешняя логика
                // Обычно EndRound сам запускает следующий раунд, но мы можем отправить StartRound.
                if (_state.IsActive)
                    await SendCommand(new StartRound(_state.CombatId), cancellationToken);
            }
        }

        private async Task OnCharacterDied(CharacterDied e, CancellationToken cancellationToken)
        {
            if (_state == null) return;

            // Если умерший участник есть в бою, удаляем его
            if (_state.Participants.ContainsKey(e.CharacterId))
            {
                await SendCommand(new RemoveParticipantFromCombat(_state.CombatId, e.CharacterId), cancellationToken);
            }
        }

        private async Task OnParticipantRemoved(ParticipantRemovedFromCombat e, CancellationToken cancellationToken)
        {
            if (_state == null || _state.CombatId != e.CombatId) return;

            _state.Participants.Remove(e.CharacterId);

            // Проверяем условие завершения боя: все оставшиеся участники принадлежат одной стороне
            if (IsCombatOver())
            {
                await Complete(true, "All opponents defeated", cancellationToken);
            }
            else if (_state.Participants.Count == 0)
            {
                await Complete(true, "No participants left", cancellationToken);
            }
        }

        private bool IsCombatOver()
        {
            if (_state.Participants.Count == 0) return true;

            // Пример: бой завершается, если все оставшиеся участники — игровые персонажи (или все NPC).
            // Для этого необходимо знать, кто является игроком, а кто NPC. Здесь используем заглушку,
            // предполагая, что у нас есть список PlayerCharacterIds в состоянии саги.
            // В реальном коде информацию о том, какие персонажи являются врагами, нужно хранить
            // в состоянии саги при старте боя (например, команды StartCombat передают фракции).
            bool hasPlayers = _state.Participants.Values.Any(p => _state.PlayerCharacterIds.Contains(p.CharacterId));
            bool hasEnemies = _state.Participants.Values.Any(p => !_state.PlayerCharacterIds.Contains(p.CharacterId));

            return !hasPlayers || !hasEnemies;
        }

        // Внутреннее состояние саги
        private class CombatSagaState : ISagaState
        {
            public Guid SagaId { get; set; }
            public Guid CorrelationId { get; set; }
            public SagaStatus Status { get; set; }
            public int Version { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }

            public Guid CombatId { get; set; }
            public bool IsActive { get; set; }
            public int Round { get; set; }
            public int CurrentTurnIndex { get; set; }
            public List<Guid> TurnOrder { get; set; } = [];
            public Dictionary<Guid, CombatSagaParticipant> Participants { get; set; } = [];
            public HashSet<Guid> PlayerCharacterIds { get; set; } = [];
            public string? CompletionReason { get; set; }
        }

        private class CombatSagaParticipant
        {
            public Guid CharacterId { get; set; }
            public int Initiative { get; set; }
            public int DexterityModifier { get; set; }
            public bool HasRolledInitiative { get; set; }
        }

        public void SetCommandBus(ICommandBus commandBus)
        {
            // CombatSaga РІСЃРµРіРґР° РїРѕР»СѓС‡Р°РµС‚ commandBus С‡РµСЂРµР· РєРѕРЅСЃС‚СЂСѓРєС‚РѕСЂ; СЌС‚РѕС‚ РјРµС‚РѕРґ вЂ” С‡Р°СЃС‚СЊ
            // РєРѕРЅС‚СЂР°РєС‚Р° ICommandingSaga РґР»СЏ СЃР°Рі, РєРѕС‚РѕСЂС‹Рµ РјРѕРіСѓС‚ Р±С‹С‚СЊ СЃРѕР·РґР°РЅС‹ Р±РµР· РЅРµРіРѕ.
            _commandBus = commandBus;
        }
    }
}