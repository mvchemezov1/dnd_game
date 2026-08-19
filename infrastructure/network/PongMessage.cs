namespace dnd_game.Infrastructure.Network
{
    public class PongMessage : INetworkMessage
    {
        public MessageType Type => MessageType.Pong;
        public string? CorrelationId { get; set; }   // добавляем
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}