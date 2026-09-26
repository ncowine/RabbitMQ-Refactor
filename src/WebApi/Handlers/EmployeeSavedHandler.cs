using System.Threading;
using System.Threading.Tasks;
using Employees.Contracts;
using Messaging;
using Microsoft.Extensions.Logging;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Handlers
{
    /// <summary>A net8 app saved an employee (Modern bus): update the cache and tell clients it changed.</summary>
    public class EmployeeSavedHandler : IMessageHandler<EmployeeSaved>
    {
        private readonly EmployeeCache employeeCache;
        private readonly IMessagePublisher publisher;
        private readonly ILogger<EmployeeSavedHandler> logger;

        public EmployeeSavedHandler(EmployeeCache employeeCache, IMessagePublisher publisher, ILogger<EmployeeSavedHandler> logger)
        {
            this.employeeCache = employeeCache;
            this.publisher = publisher;
            this.logger = logger;
        }

        public async Task Handle(EmployeeSaved message, MessageContext context, CancellationToken cancellationToken)
        {
            Employee employee = message.ToEmployee();
            logger.LogInformation("EmployeeSaved {Employee}", employee);
            employeeCache.Upsert(employee);

            // Published inside the handler, so it carries the incoming message's correlation ID.
            await publisher.PublishAsync(employee.ToEmployeeCacheRefreshed(), cancellationToken);
        }
    }
}
