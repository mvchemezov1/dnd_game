// presentation/api/rest_api.cs
using dnd_game.Application.Services;
using dnd_game.Domain.Aggregates;
using dnd_game.Domain.Commands;
using dnd_game.Domain.Queries;
using dnd_game.infrastructure.message_bus;
using dnd_game.Infrastructure.MessageBus;
using dnd_game.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static dnd_game.Presentation.Api.Schemas;

namespace dnd_game.Presentation.Api
{
    // --------------------------------------------------------------------------------
    // Базовый класс контроллера с извлечением контекста (userId, sessionId)
    // --------------------------------------------------------------------------------
    [ApiController]
    public abstract class GameControllerBase : ControllerBase
    {
        protected Guid UserId =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        protected Guid SessionId =>
            Guid.TryParse(HttpContext.Request.Headers["X-Session-Id"].FirstOrDefault(), out var sid) ? sid : Guid.Empty;

        protected CommandContext CreateContext() => new()
        {
            UserId = UserId,
            GameSessionId = SessionId,
            CancellationToken = HttpContext.RequestAborted
        };
    }

    // --------------------------------------------------------------------------------
    // Персонажи
    // --------------------------------------------------------------------------------
    [Route("api/[controller]")]
    public class CharactersController(ICommandBus commandBus, IQueryBus queryBus) : GameControllerBase
    {
        private readonly ICommandBus _commandBus = commandBus;
        private readonly IQueryBus _queryBus = queryBus;

        // ── CRUD ──────────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CreateCharacter([FromBody] CreateCharacter command)
        {
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCharacter(Guid id, [FromBody] UpdateCharacterRequest request)
        {
            var command = new UpdateCharacter(id, request.Name, request.MaxHitPoints);
            await _commandBus.SendAsync(command, CreateContext());
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCharacter(Guid id) =>
            OkOrNotFound(await _queryBus.QueryAsync(new GetCharacterById(id)));

        [HttpGet]
        public async Task<IActionResult> GetAllCharacters() =>
            Ok(await _queryBus.QueryAsync(new GetAllCharacters()));

        // ── Здоровье ──────────────────────────────────────────────────────────────
        [HttpPost("{id}/damage")]
        public async Task<IActionResult> DealDamage(Guid id, [FromBody] DealDamage command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        [HttpPost("{id}/heal")]
        public async Task<IActionResult> Heal(Guid id, [FromBody] HealCharacter command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        [HttpPut("{id}/temporary-hit-points")]
        public async Task<IActionResult> SetTemporaryHitPoints(Guid id, [FromBody] SetTemporaryHitPoints command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        [HttpGet("{id}/hit-points")]
        public async Task<IActionResult> GetHitPoints(Guid id) =>
            OkOrNotFound(await _queryBus.QueryAsync(new GetCharacterHitPoints(id)));

        // ── Характеристики ──────────────────────────────────────────────────────
        [HttpPut("{id}/ability-scores/{ability}")]
        public async Task<IActionResult> SetAbilityScore(Guid id, string ability, [FromBody] SetAbilityScoreRequest request)
        {
            var command = new SetAbilityScore(id, ability, request.Score);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ── Раса, класс, предыстория ─────────────────────────────────────────────
        [HttpPost("{id}/race")]
        public async Task<IActionResult> ChooseRace(Guid id, [FromBody] ChooseRace command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        [HttpPost("{id}/class")]
        public async Task<IActionResult> ChooseClass(Guid id, [FromBody] ChooseClass command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        [HttpPost("{id}/background")]
        public async Task<IActionResult> ChooseBackground(Guid id, [FromBody] ChooseBackground command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        // ── Владения ────────────────────────────────────────────────────────────
        [HttpPost("{id}/skills/{skill}")]
        public async Task<IActionResult> AddSkill(Guid id, string skill)
        {
            await _commandBus.SendAsync(new AddSkillProficiency(id, skill), CreateContext());
            return Ok();
        }
        [HttpDelete("{id}/skills/{skill}")]
        public async Task<IActionResult> RemoveSkill(Guid id, string skill)
        {
            await _commandBus.SendAsync(new RemoveSkillProficiency(id, skill), CreateContext());
            return Ok();
        }

        [HttpPost("{id}/saving-throws/{ability}")]
        public async Task<IActionResult> AddSavingThrow(Guid id, string ability)
        {
            await _commandBus.SendAsync(new AddSavingThrowProficiency(id, ability), CreateContext());
            return Ok();
        }

        [HttpDelete("{id}/saving-throws/{ability}")]
        public async Task<IActionResult> RemoveSavingThrow(Guid id, string ability)
        {
            await _commandBus.SendAsync(new RemoveSavingThrowProficiency(id, ability), CreateContext());
            return Ok();
        }

        // ── Черты ───────────────────────────────────────────────────────────────
        [HttpPost("{id}/feats")]
        public async Task<IActionResult> AddFeat(Guid id, [FromBody] AddFeat command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpDelete("{id}/feats/{featName}")]
        public async Task<IActionResult> RemoveFeat(Guid id, string featName)
        {
            await _commandBus.SendAsync(new RemoveFeat(id, featName), CreateContext());
            return Ok();
        }

        // ── Заклинания ──────────────────────────────────────────────────────────
        [HttpPost("{id}/spells")]
        public async Task<IActionResult> AddSpell(Guid id, [FromBody] AddSpell command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpDelete("{id}/spells/{spellId}")]
        public async Task<IActionResult> RemoveSpell(Guid id, string spellId)
        {
            await _commandBus.SendAsync(new RemoveSpell(id, spellId), CreateContext());
            return Ok();
        }
        [HttpPost("{id}/spells/prepare")]
        public async Task<IActionResult> PrepareSpell(Guid id, [FromBody] PrepareSpell command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpPost("{id}/spell-slots/use")]
        public async Task<IActionResult> UseSpellSlot(Guid id, [FromBody] UseSpellSlot command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpPost("{id}/spell-slots/restore")]
        public async Task<IActionResult> RestoreAllSpellSlots(Guid id)
        {
            await _commandBus.SendAsync(new RestoreAllSpellSlots(id), CreateContext());
            return Ok();
        }
        [HttpGet("{id}/spells")]
        public async Task<IActionResult> GetSpells(Guid id) =>
            OkOrNotFound(await _queryBus.QueryAsync(new GetCharacterSpells(id)));

        // ── Инвентарь и экипировка ──────────────────────────────────────────────
        [HttpPost("{id}/inventory")]
        public async Task<IActionResult> AddInventoryItem(Guid id, [FromBody] AddInventoryItem command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpDelete("{id}/inventory/{itemId}")]
        public async Task<IActionResult> RemoveInventoryItem(Guid id, string itemId, [FromQuery] int quantity = 1)
        {
            await _commandBus.SendAsync(new RemoveInventoryItem(id, itemId, quantity), CreateContext());
            return Ok();
        }
        [HttpGet("{id}/inventory")]
        public async Task<IActionResult> GetInventory(Guid id) =>
            Ok(await _queryBus.QueryAsync(new GetCharacterInventory(id)));

        [HttpPost("{id}/equip")]
        public async Task<IActionResult> EquipItem(Guid id, [FromBody] EquipItem command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpPost("{id}/unequip")]
        public async Task<IActionResult> UnequipItem(Guid id, [FromBody] UnequipItem command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpGet("{id}/equipment")]
        public async Task<IActionResult> GetEquipment(Guid id) =>
            Ok(await _queryBus.QueryAsync(new GetCharacterEquipment(id)));

        // ── Состояния ───────────────────────────────────────────────────────────
        [HttpPost("{id}/conditions")]
        public async Task<IActionResult> ApplyCondition(Guid id, [FromBody] ApplyCondition command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpDelete("{id}/conditions/{condition}")]
        public async Task<IActionResult> RemoveCondition(Guid id, string condition)
        {
            await _commandBus.SendAsync(new RemoveCondition(id, condition), CreateContext());
            return Ok();
        }
        [HttpGet("{id}/conditions")]
        
        [HttpPost("{id}/conditions/clear")]
        // Очистить все состояния (условия) персонажа.
        public async Task<IActionResult> ClearAllConditions(Guid id)
        {
            var command = new ClearAllConditionsCommand(id);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }
        public async Task<IActionResult> GetConditions(Guid id) =>
            OkOrNotFound(await _queryBus.QueryAsync(new GetCharacterConditions(id)));

        // ── Смерть и спасброски ────────────────────────────────────────────────
        [HttpPost("{id}/death-saves")]
        public async Task<IActionResult> DeathSavingThrow(Guid id, [FromBody] DeathSavingThrow command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpPost("{id}/stabilize")]
        public async Task<IActionResult> Stabilize(Guid id)
        {
            await _commandBus.SendAsync(new StabilizeCharacter(id), CreateContext());
            return Ok();
        }
        [HttpGet("{id}/death-status")]
        public async Task<IActionResult> GetDeathStatus(Guid id) =>
            OkOrNotFound(await _queryBus.QueryAsync(new GetCharacterDeathStatus(id)));

        // ── Опыт и уровень ────────────────────────────────────────────────────
        [HttpPost("{id}/experience")]
        public async Task<IActionResult> GainExperience(Guid id, [FromBody] GainExperience command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        [HttpPost("{id}/level-up")]
        public async Task<IActionResult> LevelUp(Guid id, [FromBody] LevelUpCharacter command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        // ── Отдых ───────────────────────────────────────────────────────────────
        [HttpPost("{id}/rest/start")]
        public async Task<IActionResult> StartRest(Guid id, [FromBody] StartRest command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }
        [HttpPost("{id}/rest/end")]
        public async Task<IActionResult> EndRest(Guid id)
        {
            await _commandBus.SendAsync(new EndRest(id), CreateContext());
            return Ok();
        }

        // ── Перемещение ─────────────────────────────────────────────────────────
        [HttpPost("{id}/move")]
        public async Task<IActionResult> Move(Guid id, [FromBody] MoveCharacter command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        // ── Боевые параметры ────────────────────────────────────────────────────
        [HttpGet("{id}/combat-stats")]
        public async Task<IActionResult> GetCombatStats(Guid id) =>
            OkOrNotFound(await _queryBus.QueryAsync(new GetCharacterCombatStats(id)));

        [HttpPut("{id}/armor-class")]
        public async Task<IActionResult> UpdateArmorClass(Guid id, [FromBody] UpdateArmorClass command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        [HttpPut("{id}/speed")]
        public async Task<IActionResult> UpdateSpeed(Guid id, [FromBody] UpdateSpeed command)
        {
            await _commandBus.SendAsync(command with { CharacterId = id }, CreateContext());
            return Ok();
        }

        // ── Защиты ──────────────────────────────────────────────────────────────
        [HttpGet("{id}/defenses")]
        public async Task<IActionResult> GetDefenses(Guid id) =>
            OkOrNotFound(await _queryBus.QueryAsync(new GetCharacterDefenses(id)));

        // ── Поиск персонажей ────────────────────────────────────────────────────
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? name, [FromQuery] string? className,
            [FromQuery] string? race, [FromQuery] bool? alive, [FromQuery] int? minLvl, [FromQuery] int? maxLvl)
        {
            var query = new SearchCharacters(name, className, race, alive, minLvl, maxLvl);
            var result = await _queryBus.QueryAsync(query);
            return Ok(result);
        }

        // -- Управление золотом --------------------------------------------------
        /// <summary>
        /// Добавить золото персонажу.
        /// </summary>
        [HttpPost("{id}/gold/add")]
        public async Task<IActionResult> AddGold(Guid id, [FromBody] AddGoldRequest request)
        {
            var command = new AddGold(id, request.Amount);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>
        /// Потратить золото персонажа.
        /// </summary>
        [HttpPost("{id}/gold/spend")]
        public async Task<IActionResult> SpendGold(Guid id, [FromBody] SpendGoldRequest request)
        {
            var command = new SpendGold(id, request.Amount);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>
        /// Установить точное количество золота (только для администраторов).
        /// </summary>
        [HttpPut("{id}/gold")]
        [Authorize(Roles = "Admin,GameMaster")]
        public async Task<IActionResult> SetGold(Guid id, [FromBody] SetGoldRequest request)
        {
            var command = new SetGoldCommand(id, request.Amount);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ── Вспомогательные методы ──────────────────────────────────────────────
        private IActionResult OkOrNotFound<T>(T? value) where T : class =>
            value is null ? NotFound() : Ok(value);

        public record UpdateCharacterRequest(string? Name, int? MaxHitPoints);
        public record SetAbilityScoreRequest(int Score);
    }

    // --------------------------------------------------------------------------------
    // Бой
    // --------------------------------------------------------------------------------
    // presentation/api/rest_api.cs (дополнение/замена)

    [Route("api/[controller]")]
    [ApiController]
    public class CombatController(ICommandBus commandBus, IQueryBus queryBus) : GameControllerBase
    {
        private readonly ICommandBus _commandBus = commandBus;
        private readonly IQueryBus _queryBus = queryBus;

        // ---------- Управление боем ----------

        /// <summary>Создать новый бой</summary>
        [HttpPost]
        public async Task<IActionResult> StartCombat([FromBody] StartCombatRequest request)
        {
            var command = new StartCombat(request.CombatId, request.Participants);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Завершить бой</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> EndCombat(Guid id)
        {
            await _commandBus.SendAsync(new EndCombat(id), CreateContext());
            return NoContent();
        }

        // ---------- Инициатива и раунды ----------

        /// <summary>Бросок инициативы для участника</summary>
        [HttpPost("{id}/initiative")]
        public async Task<IActionResult> RollInitiative(Guid id, [FromBody] RollInitiativeRequest request)
        {
            var command = new RollInitiative(id, request.ParticipantId, request.InitiativeRoll, request.DexterityModifier);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Начать новый раунд</summary>
        [HttpPost("{id}/rounds")]
        public async Task<IActionResult> StartRound(Guid id)
        {
            await _commandBus.SendAsync(new StartRound(id), CreateContext());
            return Ok();
        }

        /// <summary>Перейти к следующему ходу</summary>
        [HttpPost("{id}/turns/next")]
        public async Task<IActionResult> NextTurn(Guid id)
        {
            await _commandBus.SendAsync(new NextTurn(id), CreateContext());
            return Ok();
        }

        /// <summary>Завершить текущий раунд</summary>
        [HttpPost("{id}/rounds/end")]
        public async Task<IActionResult> EndRound(Guid id)
        {
            await _commandBus.SendAsync(new EndRound(id), CreateContext());
            return Ok();
        }

        // ---------- Участники ----------

        /// <summary>Добавить участника в бой</summary>
        [HttpPost("{id}/participants")]
        public async Task<IActionResult> AddParticipant(Guid id, [FromBody] AddParticipantRequest request)
        {
            var command = new AddParticipantToCombat(id, request.ParticipantId, request.Initiative);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Удалить участника из боя</summary>
        [HttpDelete("{id}/participants/{participantId}")]
        public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId)
        {
            await _commandBus.SendAsync(new RemoveParticipantFromCombat(id, participantId), CreateContext());
            return NoContent();
        }

        // ---------- Действия ----------

        /// <summary>Перемещение (движение)</summary>
        [HttpPost("{id}/actions/move")]
        public async Task<IActionResult> TakeMoveAction(Guid id, [FromBody] TakeMoveActionRequest request)
        {
            var command = new TakeMoveAction(id, request.ParticipantId, request.DistanceFeet);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Стандартное действие (атака, заклинание, рывок и т.д.)</summary>
        [HttpPost("{id}/actions/standard")]
        public async Task<IActionResult> TakeStandardAction(Guid id, [FromBody] TakeStandardActionRequest request)
        {
            var command = new TakeStandardAction(
                id,
                request.ParticipantId,
                request.ActionType,
                request.TargetId,
                request.ActionData
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Бонусное действие</summary>
        [HttpPost("{id}/actions/bonus")]
        public async Task<IActionResult> TakeBonusAction(Guid id, [FromBody] TakeBonusActionRequest request)
        {
            var command = new TakeBonusAction(
                id,
                request.ParticipantId,
                request.ActionType,
                request.TargetId,
                request.ActionData
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Реакция</summary>
        [HttpPost("{id}/actions/reaction")]
        public async Task<IActionResult> TakeReaction(Guid id, [FromBody] TakeReactionRequest request)
        {
            var command = new TakeReaction(
                id,
                request.ParticipantId,
                request.ReactionType,
                request.TriggerDescription,
                request.TargetId
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Готовое действие</summary>
        [HttpPost("{id}/actions/ready")]
        public async Task<IActionResult> ReadyAction(Guid id, [FromBody] ReadyActionRequest request)
        {
            var command = new ReadyAction(id, request.ParticipantId, request.ActionToReady, request.TriggerCondition);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Срабатывание готового действия</summary>
        [HttpPost("{id}/actions/trigger")]
        public async Task<IActionResult> TriggerReadyAction(Guid id, [FromBody] TriggerReadyActionRequest request)
        {
            var command = new TriggerReadyAction(id, request.ParticipantId);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ---------- Урон и лечение ----------

        /// <summary>Нанести урон цели</summary>
        [HttpPost("{id}/damage")]
        public async Task<IActionResult> DealDamage(Guid id, [FromBody] DealDamageRequest request)
        {
            var command = new DealDamageToTarget(
                id,
                request.SourceParticipantId,
                request.TargetParticipantId,
                request.DamageAmount,
                request.DamageType
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Исцелить цель</summary>
        [HttpPost("{id}/heal")]
        public async Task<IActionResult> HealTarget(Guid id, [FromBody] HealTargetRequest request)
        {
            var command = new HealTarget(id, request.SourceParticipantId, request.TargetParticipantId, request.HealingAmount);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ---------- Состояния ----------

        /// <summary>Наложить состояние на участника</summary>
        [HttpPost("{id}/conditions")]
        public async Task<IActionResult> ApplyCondition(Guid id, [FromBody] ApplyConditionRequest request)
        {
            var command = new ApplyConditionToTarget(
                id,
                request.TargetParticipantId,
                request.ConditionType,
                request.DurationRounds
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Снять состояние с участника</summary>
        [HttpDelete("{id}/conditions")]
        public async Task<IActionResult> RemoveCondition(Guid id, [FromBody] RemoveConditionRequest request)
        {
            var command = new RemoveConditionFromTarget(
                id,
                request.TargetParticipantId,
                request.ConditionType
            );
            await _commandBus.SendAsync(command, CreateContext());
            return NoContent();
        }

        // ---------- Спасброски ----------

        /// <summary>Сделать спасбросок в бою</summary>
        [HttpPost("{id}/saving-throws")]
        public async Task<IActionResult> MakeSavingThrow(Guid id, [FromBody] MakeSavingThrowRequest request)
        {
            var command = new MakeSavingThrowInCombat(
                id,
                request.ParticipantId,
                request.Ability,
                request.DifficultyClass,
                request.RollResult,
                request.Modifiers
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Сделать спасбросок от смерти</summary>
        [HttpPost("{id}/death-saves")]
        public async Task<IActionResult> MakeDeathSavingThrow(Guid id, [FromBody] MakeDeathSavingThrowRequest request)
        {
            var command = new MakeDeathSavingThrowInCombat(id, request.ParticipantId, request.RollResult);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Стабилизировать участника</summary>
        [HttpPost("{id}/stabilize")]
        public async Task<IActionResult> Stabilize(Guid id, [FromBody] StabilizeRequest request)
        {
            var command = new StabilizeInCombat(id, request.ParticipantId, request.StabilizedByParticipantId);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ---------- Концентрация ----------

        /// <summary>Проверка концентрации</summary>
        [HttpPost("{id}/concentration")]
        public async Task<IActionResult> MakeConcentrationCheck(Guid id, [FromBody] MakeConcentrationCheckRequest request)
        {
            var command = new MakeConcentrationCheck(
                id,
                request.ParticipantId,
                request.DifficultyClass,
                request.RollResult,
                request.ConstitutionModifier
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ---------- Прочие действия ----------

        /// <summary>Отложить ход</summary>
        [HttpPost("{id}/delay")]
        public async Task<IActionResult> DelayTurn(Guid id, [FromBody] DelayTurnRequest request)
        {
            var command = new DelayTurn(id, request.ParticipantId);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Сдаться в бою</summary>
        [HttpPost("{id}/surrender")]
        public async Task<IActionResult> Surrender(Guid id, [FromBody] SurrenderRequest request)
        {
            var command = new SurrenderInCombat(id, request.ParticipantId);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ---------- Универсальное действие ----------

        /// <summary>Выполнить любое действие (общий эндпоинт)</summary>
        [HttpPost("{id}/actions")]
        public async Task<IActionResult> PerformAction(Guid id, [FromBody] PerformActionRequest request)
        {
            var command = new PerformAction(
                id,
                request.ParticipantId,
                request.ActionType,
                request.TargetId,
                request.ActionData
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ---------- Запросы (чтение) ----------

        /// <summary>Получить статус боя</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCombatStatus(Guid id)
        {
            var result = await _queryBus.QueryAsync(new GetCombatStatus(id));
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Получить список участников</summary>
        [HttpGet("{id}/participants")]
        public async Task<IActionResult> GetParticipants(Guid id)
        {
            var result = await _queryBus.QueryAsync(new GetCombatParticipants(id));
            return Ok(result);
        }

        /// <summary>Получить текущего участника (чей ход)</summary>
        [HttpGet("{id}/current")]
        public async Task<IActionResult> GetCurrentParticipant(Guid id)
        {
            var result = await _queryBus.QueryAsync(new GetCurrentCombatParticipant(id));
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Получить номер текущего раунда</summary>
        [HttpGet("{id}/round")]
        public async Task<IActionResult> GetRound(Guid id)
        {
            var result = await _queryBus.QueryAsync(new GetCombatRound(id));
            return Ok(result);
        }

        /// <summary>Получить порядок ходов (по инициативе)</summary>
        [HttpGet("{id}/turn-order")]
        public async Task<IActionResult> GetTurnOrder(Guid id)
        {
            var result = await _queryBus.QueryAsync(new GetCombatTurnOrder(id));
            return Ok(result);
        }

        /// <summary>Проверить, активен ли бой</summary>
        [HttpGet("{id}/active")]
        public async Task<IActionResult> IsActive(Guid id)
        {
            var result = await _queryBus.QueryAsync(new IsCombatActive(id));
            return Ok(result);
        }
    }

    // --------------------------------------------------------------------------------
    // Кампания
    // --------------------------------------------------------------------------------
    [Route("api/[controller]")]
    [ApiController]
    public class CampaignController : GameControllerBase
    {
        private readonly ICommandBus _commandBus;
        private readonly IQueryBus _queryBus;

        public CampaignController(ICommandBus commandBus, IQueryBus queryBus)
        {
            _commandBus = commandBus;
            _queryBus = queryBus;
        }

        // ---------- Получение информации о кампании ----------

        /// <summary>Получить состояние кампании</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCampaignState(Guid id)
        {
            var result = await _queryBus.QueryAsync(new GetCampaignState(id));
            return result is null ? NotFound() : Ok(result);
        }

        /// <summary>Получить список активных квестов</summary>
        [HttpGet("{id}/quests/active")]
        public async Task<IActionResult> GetActiveQuests(Guid id)
        {
            var result = await _queryBus.QueryAsync(new GetActiveQuests(id));
            return Ok(result);
        }

        /// <summary>Получить список квестов с фильтром по статусу</summary>
        [HttpGet("{id}/quests")]
        public async Task<IActionResult> GetQuests(Guid id, [FromQuery] string? status = null)
        {
            var parsedStatus = ParseStatus(status);
            var result = await _queryBus.QueryAsync(new GetQuestsByStatus(id, (Application.Projections.QuestStatus?)parsedStatus));
            return Ok(result);
        }

        // ---------- Управление квестами (команды) ----------

        /// <summary>Принять квест (начать выполнение)</summary>
        [HttpPost("{campaignId}/quests/{questId}/accept")]
        public async Task<IActionResult> AcceptQuest(Guid campaignId, Guid questId)
        {
            var command = new AcceptQuestCommand(campaignId, questId);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Завершить квест (успешно)</summary>
        [HttpPost("{campaignId}/quests/{questId}/complete")]
        public async Task<IActionResult> CompleteQuest(Guid campaignId, Guid questId)
        {
            var command = new CompleteQuestCommand(campaignId, questId);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>Провалить квест (неудачно)</summary>
        [HttpPost("{campaignId}/quests/{questId}/fail")]
        public async Task<IActionResult> FailQuest(Guid campaignId, Guid questId)
        {
            var command = new FailQuestCommand(campaignId, questId);
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        // ---------- Вспомогательные методы ----------

        private static QuestStatus? ParseStatus(string? status)
        {
            if (string.IsNullOrEmpty(status)) return null;
            return Enum.TryParse<QuestStatus>(status, true, out var s) ? s : null;
        }

        private IActionResult OkOrNotFound<T>(T? value) where T : class =>
            value is null ? NotFound() : Ok(value);

        /// <summary>
        /// Создать новый квест в кампании.
        /// </summary>
        [HttpPost("{campaignId}/quests")]
        public async Task<IActionResult> CreateQuest(Guid campaignId, [FromBody] CreateQuestRequest request)
        {
            var command = new CreateQuestCommand(
                campaignId,
                request.QuestId,
                request.Title,
                request.Objectives,
                request.Rewards,
                request.ParticipantIds
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }

        /// <summary>
        /// Обновить цель квеста (прогресс или завершение).
        /// </summary>
        [HttpPut("{campaignId}/quests/{questId}/objectives")]
        public async Task<IActionResult> UpdateQuestObjective(Guid campaignId, Guid questId, [FromBody] UpdateQuestObjectiveRequest request)
        {
            var command = new UpdateQuestObjectiveCommand(
                campaignId,
                questId,
                request.ObjectiveIndex,
                request.IsCompleted,
                request.CurrentProgress
            );
            await _commandBus.SendAsync(command, CreateContext());
            return Ok();
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthProvider _authProvider;

        public AuthController(IAuthProvider authProvider)
        {
            _authProvider = authProvider;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthRequest request)
        {
            var result = await _authProvider.RegisterAsync(request);
            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage });
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthRequest request)
        {
            var result = await _authProvider.LoginAsync(request);
            if (!result.Success)
                return Unauthorized(new { error = result.ErrorMessage });
            return Ok(result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            var result = await _authProvider.RefreshTokenAsync(request.RefreshToken);
            if (!result.Success)
                return Unauthorized(new { error = result.ErrorMessage });
            return Ok(result);
        }
    }

    public record RefreshRequest(string RefreshToken);
}