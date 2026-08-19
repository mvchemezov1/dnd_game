// application/projections/combat_projection.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Queries;
using dnd_game.Infrastructure.Caching;

namespace dnd_game.Application.Projections;

/// <summary>
/// Проекция боевых сцен, отвечающая за построение read-модели на основе событий предметной области.
/// Хранит текущее состояние боя в памяти (в реальном приложении — в БД) и предоставляет методы чтения с кешированием.
/// </summary>
/// <remarks>
/// Инициализирует новый экземпляр проекции боевых сцен.
/// </remarks>
/// <param name="cache">Провайдер кеша.</param>
/// <param name="cacheTtl">Время жизни кеша; по умолчанию 1 минута.</param>
public class CombatProjection(ICacheProvider cache, TimeSpan? cacheTtl = null)
{
    /// <summary>Хранилище DTO боевых сцен: идентификатор боя → состояние.</summary>
    private readonly Dictionary<Guid, CombatStatusDto> _state = [];

    /// <summary>Провайдер кеша для ускорения повторных чтений.</summary>
    private readonly ICacheProvider _cache = cache;

    /// <summary>Время жизни записей в кеше.</summary>
    private readonly TimeSpan _cacheTtl = cacheTtl ?? TimeSpan.FromMinutes(1);

    /// <summary>
    /// Инвалидирует кеш, связанный с конкретным боем.
    /// Выполняется асинхронно в фоновом потоке, чтобы не блокировать обработку события.
    /// </summary>
    /// <param name="combatId">Идентификатор боя, кеш которого требуется сбросить.</param>
    private void InvalidateCache(Guid combatId)
    {
        _ = Task.Run(async () =>
        {
            await _cache.RemoveAsync($"combat:{combatId}");
            await _cache.RemoveAsync($"combat:participants:{combatId}");
            await _cache.RemoveAsync($"combat:current:{combatId}");
        });
    }

    /// <summary>
    /// Обрабатывает событие начала боя (<see cref="CombatStarted"/>).
    /// Создаёт DTO боя с начальными значениями и сбрасывает кеш.
    /// </summary>
    /// <param name="e">Событие начала боя, содержащее идентификатор боя и список участников.</param>
    public void Apply(CombatStarted e)
    {
        var participants = e.Participants
            .Select(id => new CombatParticipantDto(
                CharacterId: id,
                Initiative: 0,
                MovementRemaining: 30
            ))
            .ToList();
        _state[e.CombatId] = new CombatStatusDto(e.CombatId, true, participants, Round: 0, TurnIndex: -1);
        InvalidateCache(e.CombatId);
    }

    /// <summary>
    /// Обрабатывает событие окончания боя (<see cref="CombatEnded"/>).
    /// Помечает бой как неактивный.
    /// </summary>
    /// <param name="e">Событие окончания боя.</param>
    public void Apply(CombatEnded e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            _state[e.CombatId] = dto with { IsActive = false };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие броска инициативы (<see cref="InitiativeRolled"/>).
    /// Обновляет инициативу участника.
    /// </summary>
    /// <param name="e">Событие броска инициативы, содержащее идентификатор боя, персонажа и значение инициативы.</param>
    public void Apply(InitiativeRolled e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var idx = dto.Participants.FindIndex(p => p.CharacterId == e.CharacterId);
            if (idx >= 0)
            {
                var updated = dto.Participants[idx] with { Initiative = e.Initiative };
                dto.Participants[idx] = updated;
            }
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие начала раунда (<see cref="CombatRoundStarted"/>).
    /// Сортирует участников по инициативе (по убыванию) и устанавливает первый ход.
    /// </summary>
    /// <param name="e">Событие начала раунда с номером раунда.</param>
    public void Apply(CombatRoundStarted e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var sorted = dto.Participants
                .OrderByDescending(p => p.Initiative)
                .ToList();
            _state[e.CombatId] = dto with
            {
                Round = e.Round,
                Participants = sorted,
                TurnIndex = 0
            };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие начала хода (<see cref="CombatTurnStarted"/>).
    /// Обновляет состояние участника: устанавливает его текущим, сбрасывает доступные действия и движение.
    /// </summary>
    /// <param name="e">Событие начала хода с идентификатором персонажа.</param>
    public void Apply(CombatTurnStarted e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId
                    ? p with { IsCurrentTurn = true, HasAction = true, HasBonusAction = true, HasReaction = true, HasMovement = true, MovementRemaining = 30 }
                    : p with { IsCurrentTurn = false }
            ).ToList();
            int turnIndex = participants.FindIndex(p => p.CharacterId == e.CharacterId);
            _state[e.CombatId] = dto with { Participants = participants, TurnIndex = turnIndex };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие окончания хода (<see cref="CombatTurnEnded"/>).
    /// Снимает признак текущего хода с участника.
    /// </summary>
    /// <param name="e">Событие окончания хода.</param>
    public void Apply(CombatTurnEnded e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId ? p with { IsCurrentTurn = false } : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие использования основного действия (<see cref="CombatActionTaken"/>).
    /// Помечает, что основное действие участника недоступно.
    /// </summary>
    /// <param name="e">Событие использования основного действия.</param>
    public void Apply(CombatActionTaken e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId ? p with { HasAction = false } : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие использования бонусного действия (<see cref="CombatBonusActionTaken"/>).
    /// Помечает, что бонусное действие участника недоступно.
    /// </summary>
    /// <param name="e">Событие использования бонусного действия.</param>
    public void Apply(CombatBonusActionTaken e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId ? p with { HasBonusAction = false } : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие использования реакции (<see cref="CombatReactionUsed"/>).
    /// Помечает, что реакция участника недоступна.
    /// </summary>
    /// <param name="e">Событие использования реакции.</param>
    public void Apply(CombatReactionUsed e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId ? p with { HasReaction = false } : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие использования движения (<see cref="CombatMovementUsed"/>).
    /// Уменьшает оставшееся движение участника на указанное количество футов.
    /// </summary>
    /// <param name="e">Событие использования движения с количеством футов.</param>
    public void Apply(CombatMovementUsed e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId ? p with { MovementRemaining = Math.Max(0, p.MovementRemaining - e.Feet) } : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие наложения состояния на участника боя (<see cref="ConditionAppliedToCombatant"/>).
    /// Добавляет состояние в список активных состояний участника.
    /// </summary>
    /// <param name="e">Событие наложения состояния.</param>
    public void Apply(ConditionAppliedToCombatant e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId
                    ? p with { Conditions = [.. p.Conditions, e.Condition] }
                    : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие снятия состояния с участника боя (<see cref="ConditionRemovedFromCombatant"/>).
    /// Удаляет состояние из списка активных состояний.
    /// </summary>
    /// <param name="e">Событие снятия состояния.</param>
    public void Apply(ConditionRemovedFromCombatant e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId
                    ? p with { Conditions = [.. p.Conditions.Where(c => c != e.Condition)] }
                    : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие начала концентрации (<see cref="CombatConcentrationStarted"/>).
    /// Устанавливает признак концентрации для участника.
    /// </summary>
    /// <param name="e">Событие начала концентрации.</param>
    public void Apply(CombatConcentrationStarted e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId ? p with { Concentrating = true } : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие окончания концентрации (<see cref="CombatConcentrationEnded"/>).
    /// Снимает признак концентрации с участника.
    /// </summary>
    /// <param name="e">Событие окончания концентрации.</param>
    public void Apply(CombatConcentrationEnded e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId ? p with { Concentrating = false } : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие добавления участника в бой (<see cref="ParticipantAddedToCombat"/>).
    /// Добавляет нового участника и пересортировывает список по инициативе.
    /// </summary>
    /// <param name="e">Событие добавления участника.</param>
    public void Apply(ParticipantAddedToCombat e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var newParticipant = new CombatParticipantDto(e.CharacterId, e.Initiative, MovementRemaining: 30);
            var newList = dto.Participants.Append(newParticipant)
                                          .OrderByDescending(p => p.Initiative)
                                          .ToList();
            _state[e.CombatId] = dto with { Participants = newList };
            InvalidateCache(e.CombatId);
        }
    }

    /// <summary>
    /// Обрабатывает событие удаления участника из боя (<see cref="ParticipantRemovedFromCombat"/>).
    /// Удаляет участника из списка.
    /// </summary>
    /// <param name="e">Событие удаления участника.</param>
    public void Apply(ParticipantRemovedFromCombat e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var newList = dto.Participants.Where(p => p.CharacterId != e.CharacterId).ToList();
            _state[e.CombatId] = dto with { Participants = newList };
            InvalidateCache(e.CombatId);
        }
    }

    // ---------- Методы доступа (с кешем) ----------

    /// <summary>
    /// Получает полное состояние боя по идентификатору. Использует кеширование.
    /// </summary>
    /// <param name="combatId">Идентификатор боя.</param>
    /// <returns>Объект <see cref="CombatStatusDto"/> или null, если бой не найден.</returns>
    public async Task<CombatStatusDto?> GetStatus(Guid combatId)
    {
        var cacheKey = $"combat:{combatId}";
        var cached = await _cache.GetAsync<CombatStatusDto>(cacheKey);
        if (cached != null)
            return cached;

        if (_state.TryGetValue(combatId, out var dto))
        {
            await _cache.SetAsync(cacheKey, dto, _cacheTtl);
            return dto;
        }
        return null;
    }

    /// <summary>
    /// Получает список участников боя. Использует кеширование.
    /// </summary>
    /// <param name="combatId">Идентификатор боя.</param>
    /// <returns>Список DTO участников; пустой список, если бой не найден.</returns>
    public async Task<List<CombatParticipantDto>> GetParticipants(Guid combatId)
    {
        var cacheKey = $"combat:participants:{combatId}";
        var cached = await _cache.GetAsync<List<CombatParticipantDto>>(cacheKey);
        if (cached != null)
            return cached;

        if (_state.TryGetValue(combatId, out var dto))
        {
            await _cache.SetAsync(cacheKey, dto.Participants, _cacheTtl);
            return dto.Participants;
        }
        return [];
    }

    /// <summary>
    /// Получает текущего участника боя (того, чей ход сейчас). Использует кеширование.
    /// </summary>
    /// <param name="combatId">Идентификатор боя.</param>
    /// <returns>DTO текущего участника или null, если ход ничей или бой не найден.</returns>
    public async Task<CombatParticipantDto?> GetCurrentParticipant(Guid combatId)
    {
        var cacheKey = $"combat:current:{combatId}";
        var cached = await _cache.GetAsync<CombatParticipantDto>(cacheKey);
        if (cached != null)
            return cached;

        if (_state.TryGetValue(combatId, out var dto) && dto.TurnIndex >= 0 && dto.TurnIndex < dto.Participants.Count)
        {
            var current = dto.Participants[dto.TurnIndex];
            await _cache.SetAsync(cacheKey, current, _cacheTtl);
            return current;
        }
        return null;
    }
}