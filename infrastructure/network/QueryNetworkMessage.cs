namespace dnd_game.Infrastructure.Network
{
    /// <summary>
    /// Сообщение, содержащее запрос (Query).
    /// </summary>
    public class QueryNetworkMessage : INetworkMessage
    {
        public MessageType Type => MessageType.Query;
        public string QueryTypeName { get; set; } = string.Empty;
        public string QueryJson { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
    }
}