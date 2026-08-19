// Domain/Commands/UseItem.cs
namespace dnd_game.Domain.Commands
{
    public class UseItem(Guid characterId, string itemId) : ICommand
    {
        public Guid CharacterId { get; } = characterId;
        public string ItemId { get; } = itemId;
    }
}