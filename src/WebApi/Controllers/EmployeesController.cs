using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Messaging;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/employees")]
    public class EmployeesController : ControllerBase
    {
        private const string ApplicationName = "WebApi";

        private readonly IMessagePublisher publisher;
        private readonly EmployeeCache employeeCache;

        public EmployeesController(IMessagePublisher publisher, EmployeeCache employeeCache)
        {
            this.publisher = publisher;
            this.employeeCache = employeeCache;
        }

        [HttpGet]
        public IReadOnlyList<Employee> GetAll()
        {
            return employeeCache.GetAll();
        }

        [HttpGet("{id:int}")]
        public ActionResult<Employee> Get(int id)
        {
            Employee employee = employeeCache.Get(id);
            return employee == null ? NotFound() : employee;
        }

        /// <summary>Updates the cache and notifies net8 clients (Modern bus).</summary>
        [HttpPut("{id:int}")]
        public async Task<Employee> Put(int id, Employee employee, CancellationToken cancellationToken)
        {
            Stamp(id, employee);
            employeeCache.Upsert(employee);

            await publisher.PublishAsync(employee.ToEmployeeCacheRefreshed(), cancellationToken);
            return employee;
        }

        /// <summary>Updates the cache and sends EmployeeUpdated to the 472 apps (Legacy bus).</summary>
        [HttpPost("{id:int}/legacy-update")]
        public async Task<Employee> PublishLegacyUpdate(int id, Employee employee, CancellationToken cancellationToken)
        {
            Stamp(id, employee);

            // PublishRemote also raised the event locally, which updated the cache through the API's own subscriber.
            employeeCache.Upsert(employee);

            await publisher.PublishAsync(employee.ToEmployeeUpdated(), cancellationToken);
            return employee;
        }

        private static void Stamp(int id, Employee employee)
        {
            employee.Id = id;
            employee.UpdatedBy = ApplicationName;
            employee.UpdatedAt = DateTime.Now;
        }
    }
}
