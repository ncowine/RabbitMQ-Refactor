using System;
using System.Collections.Generic;
using Common.Events;
using Common.RabbitMQ;
using Microsoft.AspNetCore.Mvc;
using Prism.Events;
using WebApi.Services;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/employees")]
    public class EmployeesController : ControllerBase
    {
        private const string ApplicationName = "WebApi";

        private readonly IEventAggregator eventAggregator;
        private readonly EmployeeCache employeeCache;

        public EmployeesController(IEventAggregator eventAggregator, EmployeeCache employeeCache)
        {
            this.eventAggregator = eventAggregator;
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
        public Employee Put(int id, Employee employee)
        {
            Stamp(id, employee);
            employeeCache.Upsert(employee);

            eventAggregator.GetEvent<EmployeeCacheRefreshed>().PublishRemote(employee);
            return employee;
        }

        /// <summary>Sends EmployeeUpdated to the 472 apps (Legacy bus).</summary>
        [HttpPost("{id:int}/legacy-update")]
        public Employee PublishLegacyUpdate(int id, Employee employee)
        {
            Stamp(id, employee);

            eventAggregator.GetEvent<EmployeeUpdated>().PublishRemote(employee);
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
