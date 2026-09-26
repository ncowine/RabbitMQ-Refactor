using System;
using System.Collections.Generic;

namespace Messaging
{
    /// <summary>Everything about a received message other than its payload.</summary>
    public sealed class MessageContext
    {
        public MessageContext(string wireName, string busName, string messageId, string correlationId, bool redelivered, IReadOnlyDictionary<string, string> headers)
        {
            WireName = wireName;
            BusName = busName;
            MessageId = messageId;
            CorrelationId = correlationId;
            Redelivered = redelivered;
            Headers = headers;
        }

        /// <summary>The message's stable identity on the wire.</summary>
        public string WireName { get; }

        public string BusName { get; }

        /// <summary>Null when the sender did not set one.</summary>
        public string MessageId { get; }

        /// <summary>The correlation-id header. Null when the sender did not set one (legacy senders never do).</summary>
        public string CorrelationId { get; }

        /// <summary>True when the broker delivered this message before and it was not acknowledged.</summary>
        public bool Redelivered { get; }

        /// <summary>Header values decoded as UTF-8 strings.</summary>
        public IReadOnlyDictionary<string, string> Headers { get; }

        /// <summary>State an observer keeps between its callbacks for this message.</summary>
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }
}
