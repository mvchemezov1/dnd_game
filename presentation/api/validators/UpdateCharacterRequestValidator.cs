using FluentValidation;
using static dnd_game.Presentation.Api.Schemas;

namespace dnd_game.Presentation.Api.Validators;

public class UpdateCharacterRequestValidator : AbstractValidator<UpdateCharacterRequest>
{
    public UpdateCharacterRequestValidator()
    {
        When(x => x.Name != null, () =>
        {
            RuleFor(x => x.Name)
                .MaximumLength(50).WithMessage("Name must not exceed 50 characters.");
        });

        When(x => x.MaxHitPoints.HasValue, () =>
        {
            RuleFor(x => x.MaxHitPoints)
                .GreaterThan(0).WithMessage("Max hit points must be greater than 0.")
                .LessThanOrEqualTo(1000).WithMessage("Max hit points cannot exceed 1000.");
        });
    }
}