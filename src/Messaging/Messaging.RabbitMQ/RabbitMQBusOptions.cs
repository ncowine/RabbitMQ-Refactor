using System;

namespace Messaging.RabbitMQ
{
    /// <summary>Settings for one <see cref="RabbitMQBus"/>: one virtual host and exchange.</summary>
    public sealed class RabbitMQBusOptions
    {
        /// <summary>Logical bus name, used in the queue name and connection names.</summary>
        public string BusName { get; set; }

        public string HostName { get; set; } = "localhost";

        public int Port { get; set; } = 5672;

        public string VirtualHost { get; set; } = "/";

        public string UserName { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        /// <summary>A durable topic exchange, declared on connect.</summary>
        public string ExchangeName { get; set; }

        /// <summary>Shown in the RabbitMQ management UI, sent as the app-id property and used as the queue name prefix.</summary>
        public string ClientName { get; set; }

        /// <summary>Identifies this bus instance on the wire. Generated when not set.</summary>
        public string InstanceId { get; set; }

        /// <summary>How often the connect loops check for a dropped connection, and the wait before reconnecting.</summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>How long the outgoing buffer waits when there is nothing to send (or no connection).</summary>
        public TimeSpan OutstandingPollInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        public ushort PrefetchCount { get; set; } = 50;

        public TimeSpan Heartbeat { get; set; } = TimeSpan.FromSeconds(30);
    }
}
