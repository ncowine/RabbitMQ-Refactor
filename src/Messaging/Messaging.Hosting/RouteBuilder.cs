using System;

namespace Messaging.Hosting
{
    public sealed class RouteBuilder
    {
        private readonly MessagingBuilder builder;
        private readonly Type messageType;

        internal RouteBuilder(MessagingBuilder builder, Type messageType)
        {
            this.builder = builder;
            this.messageType = messageType;
        }

        /// <summary>Publishes the message to every one of <paramref name="busNames"/>.</summary>
        public MessagingBuilder To(params string[] busNames)
        {
            builder.Registry.AddRoute(messageType, MessagingBuilder.CheckBusNames(busNames));
            return builder;
        }
    }
}
