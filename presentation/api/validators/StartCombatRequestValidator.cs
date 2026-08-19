using FluentValidation;
using static dnd_game.Presentation.Api.Schemas;

namespace dnd_game.Presentation.Api.Validators;

public class StartCombatRequestValidator : AbstractValidator<StartCombatRequest>
{
    public StartCombatRequestValidator()
    {
        RuleFor(x => x.CombatId)
            .NotEmpty().WithMessage("CombatId is required.");

        RuleFor(x => x.Participants)
            .NotNull().WithMessage("Participants list is required.")
            .Must(p => p.Count >= 2).WithMessage("At least two participants required.")
            .ForEach(participant => participant.NotEmpty().WithMessage("Participant ID cannot be empty."));
    }
}