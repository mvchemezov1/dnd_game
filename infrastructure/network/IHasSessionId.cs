namespace dnd_game.Infrastructure.Network
{
    /// <summary>
    /// Сообщение, содержащее идентификатор игровой сессии.
    /// </summary>
    public interface IHasSessionId
    {
        Guid SessionId { get; }
    }
}