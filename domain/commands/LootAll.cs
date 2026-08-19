// Domain/Commands/LootAll.cs
namespace dnd_game.Domain.Commands
{
    public class LootAll(Guid characterId) : ICommand
    {
        public Guid CharacterId { get; } = characterId;
    }
}