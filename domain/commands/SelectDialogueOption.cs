// Domain/Commands/SelectDialogueOption.cs
namespace dnd_game.Domain.Commands
{
    public class SelectDialogueOption(Guid dialogueId, int optionIndex) : ICommand
    {
        public Guid DialogueId { get; } = dialogueId;
        public int OptionIndex { get; } = optionIndex;
    }
}