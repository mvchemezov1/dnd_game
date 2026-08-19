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
public class TradeController : GameControllerBase
{
    private readonly TradeService _tradeService;
    private readonly ICommandBus _commandBus;

    public TradeController(TradeService tradeService, ICommandBus commandBus)
    {
        _tradeService = tradeService;
        _commandBus = commandBus;
    }

    /// <summary>
    /// Создать предложение обмена между двумя персонажами.
    /// </summary>
    [HttpPost("offer")]
    public async Task<IActionResult> ProposeTrade([FromBody] ProposeTradeRequest request)
    {
        try
        {
            var offer = await _tradeService.ProposeTrade(
                request.FromCharacterId,
                request.ToCharacterId,
                request.OfferedItems,
                request.OfferedGold,
                request.RequestedItems,
                request.RequestedGold
            );
            return Ok(offer);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Принять предложение обмена.
    /// </summary>
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptTrade([FromBody] AcceptTradeRequest request)
    {
        try
        {
            await _tradeService.AcceptTrade(request.OfferId);
            return Ok(new { message = "Trade accepted successfully." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Отклонить предложение обмена.
    /// </summary>
    [HttpPost("decline")]
    public async Task<IActionResult> DeclineTrade([FromBody] DeclineTradeRequest request)
    {
        try
        {
            await _tradeService.DeclineTrade(request.OfferId);
            return Ok(new { message = "Trade declined." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Отменить своё предложение обмена.
    /// </summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelTradeOffer([FromBody] CancelTradeOfferRequest request)
    {
        try
        {
            await _tradeService.CancelTradeOffer(request.OfferId);
            return Ok(new { message = "Trade offer cancelled." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record ProposeTradeRequest(
    Guid FromCharacterId,
    Guid ToCharacterId,
    List<TradeItem> OfferedItems,
    int OfferedGold,
    List<TradeItem> RequestedItems,
    int RequestedGold
);

public record AcceptTradeRequest(Guid OfferId);
public record DeclineTradeRequest(Guid OfferId);
public record CancelTradeOfferRequest(Guid OfferId);

// TradeItem уже определён в domain/events/trade_events.cs
// Если нет, можно использовать локальный record:
// public record TradeItem(string ItemId, string ItemName, int Quantity);