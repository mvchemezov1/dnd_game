using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using dnd_game.Domain.Exceptions;

namespace dnd_game.Infrastructure.Exceptions;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred.");

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path,
            Type = exception.GetType().Name
        };

        switch (exception)
        {
            case InvalidAction invalidAction:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Invalid action";
                problemDetails.Detail = invalidAction.Message;
                if (!string.IsNullOrEmpty(invalidAction.ActionName))
                    problemDetails.Extensions["actionName"] = invalidAction.ActionName;
                if (invalidAction.CharacterId.HasValue)
                    problemDetails.Extensions["characterId"] = invalidAction.CharacterId.Value;
                break;

            case RuleViolation ruleViolation:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Rule violation";
                problemDetails.Detail = ruleViolation.Message;
                problemDetails.Extensions["ruleName"] = ruleViolation.RuleName;
                if (!string.IsNullOrEmpty(ruleViolation.RuleReference))
                    problemDetails.Extensions["ruleReference"] = ruleViolation.RuleReference;
                break;

            case EntityNotFoundException notFound:
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Title = "Entity not found";
                problemDetails.Detail = notFound.Message;
                problemDetails.Extensions["entityType"] = notFound.EntityType;
                problemDetails.Extensions["entityId"] = notFound.EntityId;
                break;

            case StateConflictException stateConflict:
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Title = "State conflict";
                problemDetails.Detail = stateConflict.Message;
                problemDetails.Extensions["aggregateId"] = stateConflict.AggregateId;
                problemDetails.Extensions["expectedVersion"] = stateConflict.ExpectedVersion;
                problemDetails.Extensions["actualVersion"] = stateConflict.ActualVersion;
                break;

            case UnauthorizedActionException unauthorized:
                problemDetails.Status = StatusCodes.Status403Forbidden;
                problemDetails.Title = "Unauthorized action";
                problemDetails.Detail = unauthorized.Message;
                problemDetails.Extensions["userId"] = unauthorized.UserId;
                problemDetails.Extensions["action"] = unauthorized.Action;
                break;

            case DomainError domainError:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Domain error";
                problemDetails.Detail = domainError.Message;
                break;

            default:
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Title = "Internal server error";
                problemDetails.Detail = "An unexpected error occurred. Please try again later.";
                break;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}