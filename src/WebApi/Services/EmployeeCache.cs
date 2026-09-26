using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WebApi.Models;

namespace WebApi.Services
{
    public class EmployeeCache
    {
        private readonly ConcurrentDictionary<int, Employee> employees = new ConcurrentDictionary<int, Employee>();

        public IReadOnlyList<Employee> GetAll()
        {
            return employees.Values.OrderBy(e => e.Id).ToList();
        }

        public Employee Get(int id)
        {
            return employees.TryGetValue(id, out Employee employee) ? employee : null;
        }

        public void Upsert(Employee employee)
        {
            employees[employee.Id] = employee;
        }
    }
}
