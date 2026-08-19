using dnd_game.Application.Services;
using dnd_game.Domain.Commands;
using dnd_game.infrastructure.message_bus;
using dnd_game.Infrastructure.MessageBus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace dnd_game.Presentation.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TravelController : GameControllerBase
{
    private readonly TravelService _travelService;
    private readonly ICommandBus _commandBus;

    public TravelController(TravelService travelService, ICommandBus commandBus)
    {
        _travelService = travelService;
        _commandBus = commandBus;
    }

    /// <summary>
    /// Переместить персонажа на тактической карте (в футах).
    /// </summary>
    [HttpPost("move")]
    public async Task<IActionResult> MoveCharacter([FromBody] MoveCharacterRequest request)
    {
        await _travelService.MoveCharacter(request.CharacterId, request.TargetX, request.TargetY);
        return Ok(new { message = "Character moved successfully." });
    }

    /// <summary>
    /// Использовать действие Dash (удвоение скорости на текущий ход).
    /// </summary>
    [HttpPost("dash")]
    public async Task<IActionResult> Dash([FromBody] DashRequest request)
    {
        await _travelService.Dash(request.CharacterId);
        return Ok(new { message = "Dash used." });
    }

    /// <summary>
    /// Специальное перемещение (Climb, Swim, Fly, Burrow).
    /// </summary>
    [HttpPost("special-movement")]
    public async Task<IActionResult> SpecialMovement([FromBody] SpecialMovementRequest request)
    {
        await _travelService.SpecialMovement(request.CharacterId, request.DistanceFeet, request.MovementType);
        return Ok(new { message = $"Special movement ({request.MovementType}) completed." });
    }

    /// <summary>
    /// Начать путешествие группы по глобальной карте.
    /// </summary>
    [HttpPost("journey/start")]
    public async Task<IActionResult> StartJourney([FromBody] StartJourneyRequest request)
    {
        await _travelService.StartJourney(request.PartyId, request.RouteId, request.Pace);
        return Ok(new { message = "Journey started." });
    }

    /// <summary>
    /// Завершить путешествие (прибыли на место или прервали).
    /// </summary>
    [HttpPost("journey/end")]
    public async Task<IActionResult> EndJourney([FromBody] EndJourneyRequest request)
    {
        await _travelService.EndJourney(request.PartyId);
        return Ok(new { message = "Journey ended." });
    }

    /// <summary>
    /// Пройти один день пути (daily progress).
    /// </summary>
    [HttpPost("journey/day")]
    public async Task<IActionResult> TravelDay([FromBody] TravelDayRequest request)
    {
        await _travelService.TravelDay(request.PartyId, request.Terrain, request.HoursTraveled, request.NavigationCheckResult);
        return Ok(new { message = "Day progressed." });
    }

    /// <summary>
    /// Получить скорость персонажа.
    /// </summary>
    [HttpGet("speed/{characterId}")]
    public async Task<IActionResult> GetSpeed(Guid characterId)
    {
        var speed = await _travelService.GetCharacterSpeed(characterId);
        return Ok(new { characterId, speed });
    }
}

public record MoveCharacterRequest(Guid CharacterId, int TargetX, int TargetY);
public record DashRequest(Guid CharacterId);
public record SpecialMovementRequest(Guid CharacterId, int DistanceFeet, string MovementType);
public record StartJourneyRequest(Guid PartyId, Guid RouteId, TravelPace Pace);
public record EndJourneyRequest(Guid PartyId);
public record TravelDayRequest(Guid PartyId, TerrainType Terrain, int HoursTraveled, int NavigationCheckResult = 10);