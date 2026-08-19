// Domain/Commands/DialogueTextInput.cs
namespace dnd_game.Domain.Commands
{
    public class DialogueTextInput(Guid dialogueId, string text) : ICommand
    {
        public Guid DialogueId { get; } = dialogueId;
        public string Text { get; } = text;
    }
}