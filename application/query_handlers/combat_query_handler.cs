// application/query_handlers/combat_query_handler.cs
using dnd_game.Domain.Queries;
using dnd_game.Application.Projections;

namespace dnd_game.application.query_handlers;

public class CombatQueryHandler(CombatProjection projection) : IQueryHandler<GetCombatStatus, CombatStatusDto?>,
                                 IQueryHandler<GetCombatParticipants, List<CombatParticipantDto>>,
                                 IQueryHandler<GetCurrentCombatParticipant, CombatParticipantDto?>,
                                 IQueryHandler<GetCombatRound, int>,
                                 IQueryHandler<GetCombatTurnOrder, List<Guid>>,
                                 IQueryHandler<IsCombatActive, bool>
{

    // ѕолучить полный статус бо€
    public Task<CombatStatusDto?> Handle(GetCombatStatus query, CancellationToken cancellationToken)
        => projection.GetStatus(query.CombatId);

    // ѕолучить список всех участников с их детальными параметрами
    public async Task<List<CombatParticipantDto>> Handle(GetCombatParticipants query, CancellationToken cancellationToken)
        => await projection.GetParticipants(query.CombatId);

    // ѕолучить текущего активного участника (чей сейчас ход)
    public async Task<CombatParticipantDto?> Handle(GetCurrentCombatParticipant query, CancellationToken cancellationToken)
        => await projection.GetCurrentParticipant(query.CombatId);

    // ѕолучить номер текущего раунда
    public async Task<int> Handle(GetCombatRound query, CancellationToken cancellationToken)
    {
        var status = await projection.GetStatus(query.CombatId);
        return status?.Round ?? 0;
    }

    // ѕолучить пор€док ходов: список идентификаторов персонажей в пор€дке инициативы
    public async Task<List<Guid>> Handle(GetCombatTurnOrder query, CancellationToken cancellationToken)
    {
        var participants = await projection.GetParticipants(query.CombatId);
        return participants.Select(p => p.CharacterId).ToList();
    }

    // ѕроверить, активен ли бой
    public async Task<bool> Handle(IsCombatActive query, CancellationToken cancellationToken)
    {
        var status = await projection.GetStatus(query.CombatId);
        return status?.IsActive ?? false;
    }
}