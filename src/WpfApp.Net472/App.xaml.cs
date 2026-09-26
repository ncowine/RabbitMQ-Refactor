using System.Windows;
using Common.Events;
using Common.RabbitMQ;
using Common.RabbitMQ.Configuration;
using Prism.DryIoc;
using Prism.Events;
using Prism.Ioc;
using WpfApp.Net472.Views;

namespace WpfApp.Net472
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
            // Fire and forget: connects in the background, messages are queued until then.
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

            foreach (RabbitMQConfig config in RabbitMQConfigLoader.Load())
            {
                router.AddBus(config);
            }

            return router;
        }
    }
}
