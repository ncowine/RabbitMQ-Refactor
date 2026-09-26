using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Common.Events;
using Common.RabbitMQ;
using Common.RabbitMQ.Configuration;
using Messaging.Hosting;
using Messaging.Prism;
using Prism.DryIoc;
using Prism.Events;
using Prism.Ioc;
using WpfApp.Net8.Views;
using ModernEmployeeSaved = Employees.Contracts.EmployeeSaved;

namespace WpfApp.Net8
{
    /// <summary>
    /// Both ways of messaging side by side (ADR 0002, section 5):
    /// <list type="bullet">
    /// <item>Legacy bus: Prism events from Common.Events over the Common.RabbitMQ adapter, exactly as the 472 apps do.</item>
    /// <item>Modern bus: plain message classes from Employees.Contracts over Messaging.Hosting, raised on the same
    /// <see cref="IEventAggregator"/> as <see cref="MessageEvent{TMessage}"/>, so view models keep the Prism style.</item>
    /// </list>
    /// Both buses come from App.config.
    /// </summary>
    public partial class App : PrismApplication
    {
        private const string LegacyBus = "Legacy";
        private const string ModernBus = "Modern";

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            IReadOnlyList<RabbitMQConfig> configs = RabbitMQConfigLoader.Load();
            IEventAggregator eventAggregator = Container.Resolve<IEventAggregator>();

            RabbitMQServiceRouter router = CreateLegacyRouter(eventAggregator, configs.Single(c => c.BusName == LegacyBus));
            containerRegistry.RegisterInstance(router);
            containerRegistry.RegisterInstance<IRabbitMQService>(router);

            MessagingClient client = CreateModernClient(eventAggregator, configs.Single(c => c.BusName == ModernBus));
            containerRegistry.RegisterInstance(client);
            containerRegistry.RegisterInstance(client.Publisher);
        }

        protected override void OnInitialized()
        {
            // Fire and forget: both buses connect in the background, messages are buffered until then.
            Container.Resolve<RabbitMQServiceRouter>().Init();
            Task.Run(() => Container.Resolve<MessagingClient>().StartAsync()).GetAwaiter().GetResult();

            base.OnInitialized();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Container.Resolve<MessagingClient>().Dispose();
            Container.Resolve<RabbitMQServiceRouter>().Dispose();

            base.OnExit(e);
        }

        private static RabbitMQServiceRouter CreateLegacyRouter(IEventAggregator eventAggregator, RabbitMQConfig legacy)
        {
            RabbitMQServiceRouter router = new RabbitMQServiceRouter(eventAggregator, new RemoteEventRegistry(typeof(EmployeeUpdated).Assembly));
            router.AddBus(legacy);
            return router;
        }

        private static MessagingClient CreateModernClient(IEventAggregator eventAggregator, RabbitMQConfig modern)
        {
            return MessagingClient.Create(services => services.AddMessaging(messaging => messaging
                .AddBus(ModernBus, options =>
                {
                    options.HostName = modern.HostName;
                    options.Port = modern.Port;
                    options.VirtualHost = modern.VirtualHost;
                    options.UserName = modern.UserName;
                    options.Password = modern.Password;
                    options.ExchangeName = modern.ExchangeName;
                    options.ClientName = modern.ClientName;
                    options.ReconnectDelay = TimeSpan.FromSeconds(modern.ReconnectDelaySeconds);
                    options.PrefetchCount = (ushort)modern.PrefetchCount;
                    options.Heartbeat = TimeSpan.FromSeconds(modern.HeartbeatSeconds);
                })
                .AddMessages(typeof(ModernEmployeeSaved).Assembly)
                .Route<ModernEmployeeSaved>().And()
                .UseEventAggregator(eventAggregator)));
        }
    }
}
