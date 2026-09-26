using System;
using System.Collections.Generic;

namespace Messaging
{
    /// <summary>A message on its way out, as <see cref="IMessagingObserver"/> sees it.</summary>
    public sealed class PublishContext
    {
        public PublishContext(string wireName, string busName, string messageId, Type messageType)
        {
            WireName = wireName;
            BusName = busName;
            MessageId = messageId;
            MessageType = messageType;
        }

        public string WireName { get; }

        public string BusName { get; }

        /// <summary>Sent as the message-id property.</summary>
        public string MessageId { get; }

        /// <summary>The message's runtime type, or null for a null message.</summary>
        public Type MessageType { get; }

        /// <summary>
        /// Extra headers to send, added in <see cref="IMessagingObserver.OnPublishing"/>. Additive only: receivers of
        /// every build ignore headers they don't know, and the frozen headers can't be overwritten.
        /// </summary>
        public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>State an observer keeps between its callbacks for this message.</summary>
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }
}
