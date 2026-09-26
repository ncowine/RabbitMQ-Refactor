using System.Collections.Generic;
using Common.Events;
using Common.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prism.Events;

namespace WebApi.Hosting
{
    public static class RabbitMQServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a Prism <see cref="IEventAggregator"/> and a <see cref="RabbitMQServiceRouter"/> with one bus per
        /// entry in the "RabbitMQ:Buses" configuration section.
        /// </summary>
        public static IServiceCollection AddRabbitMQ(this IServiceCollection services, IConfiguration configuration)
        {
            List<RabbitMQConfig> configs = configuration.GetSection("RabbitMQ:Buses").Get<List<RabbitMQConfig>>() ?? new List<RabbitMQConfig>();

            // No UI thread here: subscribers must use PublisherThread or BackgroundThread.
            services.AddSingleton<IEventAggregator, EventAggregator>();

            services.AddSingleton(provider =>
            {
                RabbitMQServiceRouter router = new RabbitMQServiceRouter(
                    provider.GetRequiredService<IEventAggregator>(),
                    new RemoteEventRegistry(typeof(EmployeeUpdated).Assembly));

                foreach (RabbitMQConfig config in configs)
                {
                    router.AddBus(config);
                }

                // There is no Prism ContainerLocator in the API, so PublishRemote finds the service here.
                RabbitMQServiceProvider.Current = router;
                return router;
            });

            services.AddSingleton<IRabbitMQService>(provider => provider.GetRequiredService<RabbitMQServiceRouter>());

            return services;
        }
    }
}
