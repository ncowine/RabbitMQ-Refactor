using System.Windows;
using Common.Events;
using Common.RabbitMQ;
using Common.RabbitMQ.Configuration;
using Prism.DryIoc;
using Prism.Events;
using Prism.Ioc;
using WpfApp.Net8.Views;

namespace WpfApp.Net8
{
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton(typeof(RabbitMQServiceRouter), CreateRabbitMQServiceRouter);
            containerRegistry.RegisterSingleton(typeof(IRabbitMQService), container => container.Resolve<RabbitMQServiceRouter>());
        }

        protected override void OnInitialized()
        {
            // Fire and forget: both buses connect in the background, messages are queued until then.
            Container.Resolve<RabbitMQServiceRouter>().Init();

            base.OnInitialized();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Container.Resolve<RabbitMQServiceRouter>().Dispose();

            base.OnExit(e);
        }

        private static object CreateRabbitMQServiceRouter(IContainerProvider container)
        {
            RabbitMQServiceRouter router = new RabbitMQServiceRouter(
                container.Resolve<IEventAggregator>(),
                new RemoteEventRegistry(typeof(EmployeeUpdated).Assembly));

            // Legacy + Modern from App.config.
            foreach (RabbitMQConfig config in RabbitMQConfigLoader.Load())
            {
                router.AddBus(config);
            }

            return router;
        }
    }
}
