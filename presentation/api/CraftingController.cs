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
public class CraftingController : GameControllerBase
{
    private readonly CraftingService _craftingService;
    private readonly ICommandBus _commandBus;

    public CraftingController(CraftingService craftingService, ICommandBus commandBus)
    {
        _craftingService = craftingService;
        _commandBus = commandBus;
    }

    /// <summary>
    /// Получить список доступных рецептов для персонажа.
    /// </summary>
    [HttpGet("recipes")]
    public async Task<IActionResult> GetRecipes([FromQuery] Guid characterId)
    {
        try
        {
            var recipes = await _craftingService.GetAvailableRecipes(characterId);
            return Ok(recipes);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Начать крафт предмета.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartCrafting([FromBody] StartCraftingRequest request)
    {
        try
        {
            var process = await _craftingService.StartCrafting(request.CharacterId, request.RecipeId);
            return Ok(process);
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
    /// Получить список активных процессов крафта для персонажа.
    /// </summary>
    [HttpGet("processes")]
    public async Task<IActionResult> GetProcesses([FromQuery] Guid characterId)
    {
        try
        {
            var processes = _craftingService.GetActiveCraftingProcesses(characterId);
            return Ok(processes);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Отменить активный процесс крафта.
    /// </summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelCrafting([FromBody] CancelCraftingRequest request)
    {
        try
        {
            await _craftingService.CancelCrafting(request.ProcessId);
            return Ok(new { message = "Crafting cancelled successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record StartCraftingRequest(Guid CharacterId, Guid RecipeId);
public record CancelCraftingRequest(Guid ProcessId);