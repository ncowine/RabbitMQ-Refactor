using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Common.Events;
using Common.RabbitMQ;
using Messaging.Hosting;
using Messaging.Prism;
using Messaging.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using ModernEmployeeCacheRefreshed = Employees.Contracts.EmployeeCacheRefreshed;
using ModernEmployeeSaved = Employees.Contracts.EmployeeSaved;

namespace WpfApp.Net8.ViewModels
{
    /// <summary>
    /// Uses both messaging styles through the one <see cref="IEventAggregator"/>: Prism events on the Legacy bus, plain
    /// message classes as <see cref="MessageEvent{TMessage}"/> on the Modern bus.
    /// </summary>
    public class MainWindowViewModel : BindableBase
    {
        private static readonly string applicationName = $"WpfApp.Net8 #{Process.GetCurrentProcess().Id}";

        private readonly IEventAggregator eventAggregator;
        private int employeeId = 1;
        private string employeeName = "Jane Doe";
        private string department = "Finance";

        public MainWindowViewModel(IEventAggregator eventAggregator, RabbitMQServiceRouter rabbitMQServiceRouter, MessagingClient messagingClient)
        {
            this.eventAggregator = eventAggregator;

            PublishEmployeeUpdatedCommand = new DelegateCommand(PublishEmployeeUpdated);
            PublishEmployeeSavedCommand = new DelegateCommand(PublishEmployeeSaved);
            PublishEmployeeSelectedCommand = new DelegateCommand(PublishEmployeeSelected);
            ClearCommand = new DelegateCommand(Messages.Clear);

            // Local event
            eventAggregator.GetEvent<EmployeeSelected>().Subscribe(OnEmployeeSelected, ThreadOption.UIThread);

            // Legacy bus: Prism events, the old way.
            eventAggregator.GetEvent<EmployeeUpdated>().Subscribe(OnEmployeeUpdated, ThreadOption.UIThread);

            // Modern bus: plain message classes, same Prism style.
            eventAggregator.GetEvent<MessageEvent<ModernEmployeeSaved>>().Subscribe(OnEmployeeSaved, ThreadOption.UIThread);
            eventAggregator.GetEvent<MessageEvent<ModernEmployeeCacheRefreshed>>().Subscribe(OnEmployeeCacheRefreshed, ThreadOption.UIThread);

            rabbitMQServiceRouter.Log += OnMessagingLog;
            messagingClient.Services.GetRequiredKeyedService<RabbitMQBus>("Modern").Log += OnMessagingLog;
        }

        public string Title => applicationName;

        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        public DelegateCommand PublishEmployeeUpdatedCommand { get; }

        public DelegateCommand PublishEmployeeSavedCommand { get; }

        public DelegateCommand PublishEmployeeSelectedCommand { get; }

        public DelegateCommand ClearCommand { get; }

        public int EmployeeId
        {
            get => employeeId;
            set => SetProperty(ref employeeId, value);
        }

        public string EmployeeName
        {
            get => employeeName;
            set => SetProperty(ref employeeName, value);
        }

        public string Department
        {
            get => department;
            set => SetProperty(ref department, value);
        }

        private void PublishEmployeeUpdated()
        {
            try
            {
                eventAggregator.GetEvent<EmployeeUpdated>().PublishRemote(CreateEmployee());
            }
            catch (Exception ex)
            {
                AddMessage($"PublishRemote failed: {ex.Message}");
            }
        }

        private void PublishEmployeeSaved()
        {
            try
            {
                Employee employee = CreateEmployee();
                ModernEmployeeSaved saved = new ModernEmployeeSaved
                {
                    Id = employee.Id,
                    Name = employee.Name,
                    Department = employee.Department,
                    UpdatedBy = employee.UpdatedBy,
                    UpdatedAt = employee.UpdatedAt,
                };

                // The publisher is resolved from Prism's container, like the legacy PublishRemote.
                eventAggregator.GetEvent<MessageEvent<ModernEmployeeSaved>>().PublishRemote(saved);
            }
            catch (Exception ex)
            {
                AddMessage($"PublishRemote failed: {ex.Message}");
            }
        }

        private void PublishEmployeeSelected()
        {
            // Plain Prism publish: only subscribers in this application are invoked.
            eventAggregator.GetEvent<EmployeeSelected>().Publish(CreateEmployee());
        }

        private Employee CreateEmployee()
        {
            return new Employee
            {
                Id = EmployeeId,
                Name = EmployeeName,
                Department = Department,
                UpdatedBy = applicationName,
                UpdatedAt = DateTime.Now,
            };
        }

        private void OnEmployeeSelected(Employee employee)
        {
            AddMessage($"EmployeeSelected (local) {employee}");
        }

        private void OnEmployeeUpdated(Employee employee)
        {
            AddMessage($"EmployeeUpdated {employee}");
        }

        private void OnEmployeeSaved(ModernEmployeeSaved saved)
        {
            AddMessage($"EmployeeSaved {Describe(saved.Id, saved.Name, saved.Department, saved.UpdatedBy, saved.UpdatedAt)}");
        }

        private void OnEmployeeCacheRefreshed(ModernEmployeeCacheRefreshed refreshed)
        {
            AddMessage($"EmployeeCacheRefreshed {Describe(refreshed.Id, refreshed.Name, refreshed.Department, refreshed.UpdatedBy, refreshed.UpdatedAt)}");
        }

        private void OnMessagingLog(object sender, string message)
        {
            string busName = sender is RabbitMQService service ? service.BusName : ((RabbitMQBus)sender).BusName;
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => AddMessage($"[{busName}] {message}")));
        }

        private static string Describe(int id, string name, string department, string updatedBy, DateTime updatedAt)
        {
            return $"#{id} {name} ({department}) by {updatedBy} at {updatedAt:HH:mm:ss}";
        }

        private void AddMessage(string message)
        {
            Messages.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        }
    }
}
