using System;
using System.Linq;
using System.Reflection;
using Messaging.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.Hosting
{
    /// <summary>
    /// Configures messaging in <see cref="MessagingServiceCollectionExtensions.AddMessaging"/>:
    /// <code>
    /// messaging.AddBus("Legacy", configuration.GetSection("Messaging:Buses:Legacy"));
    /// messaging.Route&lt;EmployeeUpdated&gt;().To("Legacy", "Modern");
    /// messaging.Handle&lt;EmployeeSaved, EmployeeSavedHandler&gt;().From("Modern");
    /// </code>
    /// </summary>
    public sealed class MessagingBuilder
    {
        internal MessagingBuilder(IServiceCollection services, MessagingRegistry registry)
        {
            Services = services;
            Registry = registry;
        }

        public IServiceCollection Services { get; }

        internal MessagingRegistry Registry { get; }

        /// <summary>Adds a bus whose <see cref="RabbitMQBusOptions"/> bind from <paramref name="section"/>.</summary>
        /// <param name="serializer">Null for System.Text.Json (<see cref="SystemTextJsonMessageSerializer"/>).</param>
        public MessagingBuilder AddBus(string name, IConfiguration section, IMessageSerializer serializer = null)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            AddBus(name, serializer);
            Services.AddOptions<RabbitMQBusOptions>(name).Bind(section);
            return this;
        }

        /// <param name="serializer">Null for System.Text.Json (<see cref="SystemTextJsonMessageSerializer"/>).</param>
        public MessagingBuilder AddBus(string name, Action<RabbitMQBusOptions> configure, IMessageSerializer serializer = null)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            AddBus(name, serializer);
            Services.AddOptions<RabbitMQBusOptions>(name).Configure(configure);
            return this;
        }

        /// <summary>Where <typeparamref name="TMessage"/> is published.</summary>
        public RouteBuilder Route<TMessage>()
        {
            return new RouteBuilder(this, typeof(TMessage));
        }

        /// <summary>
        /// Registers <typeparamref name="THandler"/> as a scoped handler. Choose the buses it receives from with
        /// <see cref="HandlerBuilder.From"/>.
        /// </summary>
        public HandlerBuilder Handle<TMessage, THandler>()
            where THandler : class, IMessageHandler<TMessage>
        {
            Services.AddScoped<THandler>();
            return new HandlerBuilder(this, typeof(TMessage), typeof(THandler));
        }

        private void AddBus(string name, IMessageSerializer serializer)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A bus name is required.", nameof(name));
            }

            Registry.AddBus(new BusRegistration(name, serializer));

            Services.AddOptions<RabbitMQBusOptions>(name).PostConfigure(options =>
            {
                options.BusName = name;
                if (string.IsNullOrWhiteSpace(options.ClientName))
                {
                    options.ClientName = Assembly.GetEntryAssembly()?.GetName().Name ?? "messaging";
                }
            });

            Services.AddKeyedSingleton(name, (provider, key) => MessagingServiceCollectionExtensions.CreateBus(provider, name));
        }

        internal static string[] CheckBusNames(string[] busNames)
        {
            if (busNames == null || busNames.Length == 0 || busNames.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("At least one bus name is required, and none may be empty.", nameof(busNames));
            }

            return busNames;
        }
    }
}
