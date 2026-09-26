using System;

namespace Messaging.RabbitMQ
{
    /// <summary>A serialized message waiting in the outgoing buffer.</summary>
    internal sealed class PendingMessage
    {
        public PendingMessage(string wireName, byte[] body)
        {
            WireName = wireName;
            Body = body;
            MessageId = Guid.NewGuid().ToString("N");
            CreatedUtc = DateTimeOffset.UtcNow;
        }

        public string WireName { get; }

        public byte[] Body { get; }

        public string MessageId { get; }

        public DateTimeOffset CreatedUtc { get; }
    }
}
