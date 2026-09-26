namespace Messaging.RabbitMQ
{
    /// <summary>Header names on the wire. Frozen: receivers of every build read them by name (ADR 0001, section 3).</summary>
    public static class WireHeaders
    {
        /// <summary>The message's wire name. Receivers fall back to the routing key when it is missing.</summary>
        public const string MessageType = "event-type";

        /// <summary>The sending bus instance, used to drop the sender's own messages.</summary>
        public const string SourceId = "source-id";
    }
}
