using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Employees.Contracts;
using Messaging;
using Messaging.Hosting;
using Messaging.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace WpfApp.Modern.ViewModels
{
    /// <summary>
    /// Sends and receives on both buses with no Prism: <see cref="IMessagePublisher"/> to send, <see cref="IMessageSubscriber"/>
    /// to receive. Subscriptions pass the UI thread's <see cref="SynchronizationContext"/>, so handlers can update the UI
    /// directly, like Prism's <c>ThreadOption.UIThread</c>.
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        private static readonly string applicationName = $"WpfApp.Modern #{Process.GetCurrentProcess().Id}";

        private readonly IMessagePublisher publisher;
        private readonly SynchronizationContext uiContext;

        [ObservableProperty]
        private int employeeId = 1;

        [ObservableProperty]
        private string employeeName = "Jane Doe";

        [ObservableProperty]
        private string department = "Finance";

        /// <param name="services">Used only to show each bus's connection messages.</param>
        public MainWindowViewModel(IMessagePublisher publisher, IMessageSubscriber subscriber, IServiceProvider services)
        {
            this.publisher = publisher;

            // Created by the app on the UI thread, so this is the dispatcher's context.
            uiContext = SynchronizationContext.Current;

            // Legacy bus: from the 472 apps (and the net8 app's Legacy side).
            subscriber.Subscribe<EmployeeUpdated>(m => AddMessage($"EmployeeUpdated {Describe(m.Id, m.Name, m.Department, m.UpdatedBy, m.UpdatedAt)}"), uiContext);

            // Modern bus: from net8 apps and the API.
            subscriber.Subscribe<EmployeeSaved>(m => AddMessage($"EmployeeSaved {Describe(m.Id, m.Name, m.Department, m.UpdatedBy, m.UpdatedAt)}"), uiContext);
            subscriber.Subscribe<EmployeeCacheRefreshed>(m => AddMessage($"EmployeeCacheRefreshed {Describe(m.Id, m.Name, m.Department, m.UpdatedBy, m.UpdatedAt)}"), uiContext);

            foreach (string busName in new[] { "Legacy", "Modern" })
            {
                string bus = busName;
                services.GetRequiredKeyedService<RabbitMQBus>(bus).Log += (sender, message) => uiContext.Post(state => AddMessage($"[{bus}] {message}"), null);
            }
        }

        public string Title => applicationName;

        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        [RelayCommand]
        private async Task PublishEmployeeUpdated()
        {
            // Legacy bus: the 472 apps receive this as their Prism event Common.Events.EmployeeUpdated.
            await Publish(new EmployeeUpdated
            {
                Id = EmployeeId,
                Name = EmployeeName,
                Department = Department,
                UpdatedBy = applicationName,
                UpdatedAt = DateTime.Now,
            });
        }

        [RelayCommand]
        private async Task PublishEmployeeSaved()
        {
            // Modern bus: the API updates its cache and replies with EmployeeCacheRefreshed.
            await Publish(new EmployeeSaved
            {
                Id = EmployeeId,
                Name = EmployeeName,
                Department = Department,
                UpdatedBy = applicationName,
                UpdatedAt = DateTime.Now,
            });
        }

        [RelayCommand]
        private void Clear()
        {
            Messages.Clear();
        }

        /// <summary>Senders don't receive their own messages back, so the app notes what it sent.</summary>
        private async Task Publish<TMessage>(TMessage message)
        {
            try
            {
                await publisher.PublishAsync(message);
                AddMessage($"sent {typeof(TMessage).Name}");
            }
            catch (Exception ex)
            {
                AddMessage($"Publish failed: {ex.Message}");
            }
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
