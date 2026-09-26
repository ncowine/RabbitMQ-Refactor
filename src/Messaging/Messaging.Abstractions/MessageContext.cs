using System.Collections.Generic;

namespace Messaging
{
    /// <summary>Everything about a received message other than its payload.</summary>
    public sealed class MessageContext
    {
        public MessageContext(string wireName, string busName, string messageId, bool redelivered, IReadOnlyDictionary<string, string> headers)
        {
            WireName = wireName;
            BusName = busName;
            MessageId = messageId;
            Redelivered = redelivered;
            Headers = headers;
        }

        /// <summary>The message's stable identity on the wire.</summary>
        public string WireName { get; }

        public string BusName { get; }

        /// <summary>Null when the sender did not set one.</summary>
        public string MessageId { get; }

        /// <summary>True when the broker delivered this message before and it was not acknowledged.</summary>
        public bool Redelivered { get; }

        /// <summary>Header values decoded as UTF-8 strings.</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }
    }
}
