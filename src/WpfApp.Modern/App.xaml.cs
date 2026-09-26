using System;
using System.Threading.Tasks;
using System.Windows;
using Employees.Contracts;
using Messaging.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WpfApp.Modern.ViewModels;
using WpfApp.Modern.Views;

namespace WpfApp.Modern
{
    /// <summary>
    /// A WPF app with no Prism: the .NET Generic Host provides DI, configuration (appsettings.json) and logging, and
    /// starts messaging. It talks to the 472 apps on the Legacy bus and to the API on the Modern bus with the same plain
    /// message classes the server uses. The wire names match the legacy Prism events, so the other apps can't tell.
    /// </summary>
    public partial class App : Application
    {
        private IHost host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Content root = the exe's folder, so appsettings.json is found wherever the app is started from.
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = e.Args,
                ContentRootPath = AppContext.BaseDirectory,
            });

            builder.Services.AddMessaging(messaging => messaging
                .AddBus("Legacy", builder.Configuration.GetSection("Messaging:Buses:Legacy"))
                .AddBus("Modern", builder.Configuration.GetSection("Messaging:Buses:Modern"))
                .AddMessages(typeof(EmployeeUpdated).Assembly)
                .Route<EmployeeUpdated>().To("Legacy")
                .Route<EmployeeSaved>().To("Modern"));

            builder.Services.AddSingleton<MainWindowViewModel>();
            builder.Services.AddSingleton<MainWindow>();

            host = builder.Build();

            // Build the window (and its view model's subscriptions) before connecting, so no message is missed.
            MainWindow window = host.Services.GetRequiredService<MainWindow>();
            await host.StartAsync();
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (host != null)
            {
                // Run on the thread pool so the UI thread can't deadlock while the buses close.
                Task.Run(() => host.StopAsync()).GetAwaiter().GetResult();
                host.Dispose();
            }

            base.OnExit(e);
        }
    }
}
