using System;
using System.Threading;
using System.Threading.Tasks;
using Compat.Events;
using Messaging;
using Messaging.RabbitMQ;

namespace Common.RabbitMQ.Tests.Broker
{
    /// <summary>A bare <see cref="RabbitMQBus"/> subscribed to <see cref="CompatEvent"/>'s wire name, with an optional observer.</summary>
    public sealed class CoreEndpoint : ICompatEndpoint, IInboundDispatcher
    {
        public static readonly string WireName = typeof(CompatEvent).FullName;

        private readonly RabbitMQBus bus;
        private readonly string queueName;

        public CoreEndpoint(BrokerSettings broker, string exchangeName, string clientName, IMessagingObserver observer)
        {
            RabbitMQBusOptions options = new RabbitMQBusOptions
            {
                BusName = CompatBus.Name,
                HostName = broker.HostName,
                Port = broker.Port,
                VirtualHost = broker.VirtualHost,
                UserName = broker.UserName,
                Password = broker.Password,
                ExchangeName = exchangeName,
                ClientName = clientName,
                ReconnectDelay = TimeSpan.FromSeconds(1),
                OutstandingPollInterval = TimeSpan.FromMilliseconds(20),
            };

            bus = new RabbitMQBus(options, TestJsonSerializer.Instance, this, observer);
            bus.Start(new[] { new MessageRegistration(WireName, typeof(CompatPayload)) });
            queueName = $"{clientName}.{CompatBus.Name}.{bus.InstanceId}".ToLowerInvariant();
        }

        public CompatBuild Build => CompatBuild.Core;

        public string InstanceId => bus.InstanceId;

        public string QueueName => queueName;

        public bool IsConnected => bus.IsConsumerConnected && bus.IsPublisherConnected;

        public ReceivedPayloads Received { get; } = new ReceivedPayloads();

        /// <summary>When set, the dispatcher throws it instead of receiving the message.</summary>
        public Exception HandlerException { get; set; }

        /// <summary>Only sends: there are no local subscribers.</summary>
        public void PublishRemote(CompatPayload payload)
        {
            bus.Enqueue(WireName, payload);
        }

        public Task Dispatch(MessageRegistration registration, object message, MessageContext context, CancellationToken cancellationToken)
        {
            if (HandlerException != null)
            {
                throw HandlerException;
            }

            Received.Add((CompatPayload)message);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            bus.Dispose();
        }
    }
}
