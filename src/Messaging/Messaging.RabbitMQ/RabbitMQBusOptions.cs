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

        /// <summary>Per-instance queues (the default, and the legacy topology) or one shared quorum queue.</summary>
        public QueueMode QueueMode { get; set; } = QueueMode.PerInstance;

        /// <summary>Shared queues only: the queue name. Defaults to <c>clientname.busname</c>, lower case.</summary>
        public string SharedQueueName { get; set; }

        /// <summary>Shared queues only: how many times the handlers run for one delivery before it is dead-lettered.</summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>Shared queues only: the wait between attempts.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Shared queues only: the quorum queue's x-delivery-limit. Guards against a message that crashes the process
        /// every time: the broker counts deliveries lost with a connection, not requeues (RabbitMQ 4.x).
        /// </summary>
        public int DeliveryLimit { get; set; } = 5;
    }
}
