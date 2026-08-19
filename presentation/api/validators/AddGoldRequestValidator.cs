using FluentValidation;
using static dnd_game.Presentation.Api.Schemas;

namespace dnd_game.Presentation.Api.Validators;

public class AddGoldRequestValidator : AbstractValidator<AddGoldRequest>
{
    public AddGoldRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be positive.");
    }
}