using Common.Events;
using Common.RabbitMQ;
using Microsoft.Extensions.Logging;
using Prism.Events;
using WebApi.Services;

namespace WebApi.Subscribers
{
    /// <summary>
    /// Keeps the employee cache in sync with events from both buses and tells clients when it changed.
    /// Registered as a singleton, so Prism's weak subscription references stay alive.
    /// </summary>
    public class EmployeeEventsSubscriber
    {
        private readonly IEventAggregator eventAggregator;
        private readonly IRabbitMQService rabbitMQService;
        private readonly EmployeeCache employeeCache;
        private readonly ILogger<EmployeeEventsSubscriber> logger;

        public EmployeeEventsSubscriber(
            IEventAggregator eventAggregator,
            IRabbitMQService rabbitMQService,
            EmployeeCache employeeCache,
            ILogger<EmployeeEventsSubscriber> logger)
        {
            this.eventAggregator = eventAggregator;
            this.rabbitMQService = rabbitMQService;
            this.employeeCache = employeeCache;
            this.logger = logger;
        }

        public void Subscribe()
        {
            // Legacy bus
            eventAggregator.GetEvent<EmployeeUpdated>().Subscribe(OnEmployeeUpdated, ThreadOption.BackgroundThread);

            // Modern bus
            eventAggregator.GetEvent<EmployeeSaved>().Subscribe(OnEmployeeSaved, ThreadOption.BackgroundThread);
        }

        private void OnEmployeeUpdated(Employee employee)
        {
            logger.LogInformation("EmployeeUpdated {Employee}", employee);
            employeeCache.Upsert(employee);
        }

        private void OnEmployeeSaved(Employee employee)
        {
            logger.LogInformation("EmployeeSaved {Employee}", employee);
            employeeCache.Upsert(employee);

            // Service passed explicitly here; PublishRemote(employee) alone would resolve it via RabbitMQServiceProvider.
            eventAggregator.GetEvent<EmployeeCacheRefreshed>().PublishRemote(employee, rabbitMQService);
        }
    }
}
