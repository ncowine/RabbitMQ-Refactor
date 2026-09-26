using Employees.Contracts;

namespace WebApi.Models
{
    public static class EmployeeMappings
    {
        public static Employee ToEmployee(this EmployeeUpdated message)
        {
            return new Employee { Id = message.Id, Name = message.Name, Department = message.Department, UpdatedBy = message.UpdatedBy, UpdatedAt = message.UpdatedAt };
        }

        public static Employee ToEmployee(this EmployeeSaved message)
        {
            return new Employee { Id = message.Id, Name = message.Name, Department = message.Department, UpdatedBy = message.UpdatedBy, UpdatedAt = message.UpdatedAt };
        }

        public static EmployeeUpdated ToEmployeeUpdated(this Employee employee)
        {
            return new EmployeeUpdated { Id = employee.Id, Name = employee.Name, Department = employee.Department, UpdatedBy = employee.UpdatedBy, UpdatedAt = employee.UpdatedAt };
        }

        public static EmployeeCacheRefreshed ToEmployeeCacheRefreshed(this Employee employee)
        {
            return new EmployeeCacheRefreshed { Id = employee.Id, Name = employee.Name, Department = employee.Department, UpdatedBy = employee.UpdatedBy, UpdatedAt = employee.UpdatedAt };
        }
    }
}
