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
public class DialogController : GameControllerBase
{
    private readonly DialogService _dialogService;
    private readonly ICommandBus _commandBus;

    public DialogController(DialogService dialogService, ICommandBus commandBus)
    {
        _dialogService = dialogService;
        _commandBus = commandBus;
    }

    /// <summary>
    /// Начать диалог между персонажем и NPC.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartDialog([FromBody] StartDialogRequest request)
    {
        try
        {
            var state = await _dialogService.StartDialogue(request.DialogueId, request.NpcId, request.CharacterId);
            return Ok(state);
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
    /// Выбрать вариант ответа в диалоге.
    /// </summary>
    [HttpPost("option")]
    public async Task<IActionResult> SelectOption([FromBody] SelectOptionRequest request)
    {
        try
        {
            var state = await _dialogService.SelectOption(request.DialogueId, request.OptionId);
            return Ok(state);
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
    /// Получить текущее состояние диалога (текст NPC, варианты ответов).
    /// </summary>
    [HttpGet("state/{dialogueId}")]
    public async Task<IActionResult> GetState(Guid dialogueId)
    {
        var node = _dialogService.GetCurrentDialogueNode(dialogueId);
        if (node == null)
            return NotFound(new { error = "Dialogue not found or not active." });

        return Ok(node);
    }

    /// <summary>
    /// Принудительно завершить диалог.
    /// </summary>
    [HttpPost("end")]
    public async Task<IActionResult> EndDialog([FromBody] EndDialogRequest request)
    {
        try
        {
            await _dialogService.EndDialogue(request.DialogueId);
            return Ok(new { message = "Dialogue ended." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record StartDialogRequest(Guid DialogueId, Guid NpcId, Guid CharacterId);
public record SelectOptionRequest(Guid DialogueId, Guid OptionId);
public record EndDialogRequest(Guid DialogueId);