using System.Threading;
using System.Threading.Tasks;
using Common.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebApi.Subscribers;

namespace WebApi.Hosting
{
    /// <summary>Starts the RabbitMQ buses with the application and closes them on shutdown.</summary>
    public class RabbitMQHostedService : IHostedService
    {
        private readonly RabbitMQServiceRouter rabbitMQServiceRouter;
        private readonly EmployeeEventsSubscriber employeeEventsSubscriber;
        private readonly ILogger<RabbitMQHostedService> logger;

        public RabbitMQHostedService(
            RabbitMQServiceRouter rabbitMQServiceRouter,
            EmployeeEventsSubscriber employeeEventsSubscriber,
            ILogger<RabbitMQHostedService> logger)
        {
            this.rabbitMQServiceRouter = rabbitMQServiceRouter;
            this.employeeEventsSubscriber = employeeEventsSubscriber;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            rabbitMQServiceRouter.Log += OnRabbitMQLog;
            employeeEventsSubscriber.Subscribe();

            // Fire and forget: connects in the background.
            rabbitMQServiceRouter.Init();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            rabbitMQServiceRouter.Dispose();
            rabbitMQServiceRouter.Log -= OnRabbitMQLog;

            return Task.CompletedTask;
        }

        private void OnRabbitMQLog(object sender, string message)
        {
            logger.LogInformation("[{Bus}] {Message}", ((RabbitMQService)sender).BusName, message);
        }
    }
}
