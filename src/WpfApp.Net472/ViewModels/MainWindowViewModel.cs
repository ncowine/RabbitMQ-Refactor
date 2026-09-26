using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Common.Events;
using Common.RabbitMQ;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace WpfApp.Net472.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private static readonly string applicationName = $"WpfApp.Net472 #{Process.GetCurrentProcess().Id}";

        private readonly IEventAggregator eventAggregator;
        private int employeeId = 1;
        private string employeeName = "Jane Doe";
        private string department = "Finance";

        public MainWindowViewModel(IEventAggregator eventAggregator, RabbitMQServiceRouter rabbitMQServiceRouter)
        {
            this.eventAggregator = eventAggregator;

            PublishEmployeeUpdatedCommand = new DelegateCommand(PublishEmployeeUpdated);
            PublishEmployeeSelectedCommand = new DelegateCommand(PublishEmployeeSelected);
            ClearCommand = new DelegateCommand(Messages.Clear);

            // Local event
            eventAggregator.GetEvent<EmployeeSelected>().Subscribe(OnEmployeeSelected, ThreadOption.UIThread);

            // Legacy bus
            eventAggregator.GetEvent<EmployeeUpdated>().Subscribe(OnEmployeeUpdated, ThreadOption.UIThread);
            rabbitMQServiceRouter.Log += OnRabbitMQLog;
        }

        public string Title => applicationName;

        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        public DelegateCommand PublishEmployeeUpdatedCommand { get; }

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

        private void OnRabbitMQLog(object sender, string message)
        {
            string busName = ((RabbitMQService)sender).BusName;
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => AddMessage($"[{busName}] {message}")));
        }

        private void AddMessage(string message)
        {
            Messages.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        }
    }
}
