using FluentValidation;
using static dnd_game.Presentation.Api.Schemas;

namespace dnd_game.Presentation.Api.Validators;

public class CreateQuestRequestValidator : AbstractValidator<CreateQuestRequest>
{
    public CreateQuestRequestValidator()
    {
        RuleFor(x => x.QuestId)
            .NotEmpty().WithMessage("QuestId is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Objectives)
            .NotNull().WithMessage("Objectives list is required.")
            .Must(o => o.Count > 0).WithMessage("At least one objective is required.");

        RuleFor(x => x.ParticipantIds)
            .NotNull().WithMessage("ParticipantIds list is required.");
    }
}