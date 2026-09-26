using System.Threading;
using System.Threading.Tasks;
using Employees.Contracts;
using Messaging;
using Microsoft.Extensions.Logging;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Handlers
{
    /// <summary>An employee changed in a net472 app (Legacy bus): keep the cache in sync.</summary>
    public class EmployeeUpdatedHandler : IMessageHandler<EmployeeUpdated>
    {
        private readonly EmployeeCache employeeCache;
        private readonly ILogger<EmployeeUpdatedHandler> logger;

        public EmployeeUpdatedHandler(EmployeeCache employeeCache, ILogger<EmployeeUpdatedHandler> logger)
        {
            this.employeeCache = employeeCache;
            this.logger = logger;
        }

        public Task Handle(EmployeeUpdated message, MessageContext context, CancellationToken cancellationToken)
        {
            Employee employee = message.ToEmployee();
            logger.LogInformation("EmployeeUpdated {Employee}", employee);
            employeeCache.Upsert(employee);
            return Task.CompletedTask;
        }
    }
}
