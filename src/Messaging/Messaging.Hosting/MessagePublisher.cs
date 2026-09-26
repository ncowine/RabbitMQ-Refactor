using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Messaging.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Hosting
{
    internal sealed class MessagePublisher : IMessagePublisher
    {
        private readonly IServiceProvider provider;
        private readonly MessagingRegistry registry;

        public MessagePublisher(IServiceProvider provider, MessagingRegistry registry)
        {
            this.provider = provider;
            this.registry = registry;
        }

        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Type messageType = message.GetType();
            IReadOnlyList<string> busNames = registry.GetRoute(messageType)
                ?? throw new InvalidOperationException($"{messageType.Name} has no route. Add messaging.Route<{messageType.Name}>().To(...).");
            string wireName = WireNames.Get(messageType);

            foreach (string busName in busNames)
            {
                provider.GetRequiredKeyedService<RabbitMQBus>(busName).Enqueue(wireName, message);
            }

            return Task.CompletedTask;
        }
    }
}
