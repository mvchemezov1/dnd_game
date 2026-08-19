// Domain/Commands/SpeakCommand.cs
namespace dnd_game.Domain.Commands
{
    public class SpeakCommand(Guid characterId, string message) : ICommand
    {
        public Guid CharacterId { get; } = characterId;
        public string Message { get; } = message;
    }
}