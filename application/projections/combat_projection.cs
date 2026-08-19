// application/projections/combat_projection.cs
using dnd_game.Domain.Events;
using dnd_game.Domain.Queries;
using dnd_game.Infrastructure.Caching;

namespace dnd_game.Application.Projections;

public class CombatProjection
{
    private readonly Dictionary<Guid, CombatStatusDto> _state = new();
    private readonly ICacheProvider _cache;
    private readonly TimeSpan _cacheTtl;

    public CombatProjection(ICacheProvider cache, TimeSpan? cacheTtl = null)
    {
        _cache = cache;
        _cacheTtl = cacheTtl ?? TimeSpan.FromMinutes(1);
    }

    private void InvalidateCache(Guid combatId)
    {
        _ = Task.Run(async () =>
        {
            await _cache.RemoveAsync($"combat:{combatId}");
            await _cache.RemoveAsync($"combat:participants:{combatId}");
            await _cache.RemoveAsync($"combat:current:{combatId}");
        });
    }

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

    public void Apply(CombatEnded e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            _state[e.CombatId] = dto with { IsActive = false };
            InvalidateCache(e.CombatId);
        }
    }

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

    public void Apply(ConditionAppliedToCombatant e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId
                    ? p with { Conditions = p.Conditions.Append(e.Condition).ToList() }
                    : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

    public void Apply(ConditionRemovedFromCombatant e)
    {
        if (_state.TryGetValue(e.CombatId, out var dto))
        {
            var participants = dto.Participants.Select(p =>
                p.CharacterId == e.CharacterId
                    ? p with { Conditions = p.Conditions.Where(c => c != e.Condition).ToList() }
                    : p
            ).ToList();
            _state[e.CombatId] = dto with { Participants = participants };
            InvalidateCache(e.CombatId);
        }
    }

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
        return new List<CombatParticipantDto>();
    }

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