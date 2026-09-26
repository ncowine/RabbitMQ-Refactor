using Employees.Contracts;
using Messaging.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Handlers;
using WebApi.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<EmployeeCache>();

builder.Services.AddMessaging(messaging => messaging
    .AddBus("Legacy", builder.Configuration.GetSection("Messaging:Buses:Legacy"))
    .AddBus("Modern", builder.Configuration.GetSection("Messaging:Buses:Modern"))
    .Route<EmployeeUpdated>().To("Legacy")
    .Route<EmployeeCacheRefreshed>().To("Modern")
    .Handle<EmployeeUpdated, EmployeeUpdatedHandler>().From("Legacy")
    .Handle<EmployeeSaved, EmployeeSavedHandler>().From("Modern"));

builder.Services.AddHealthChecks().AddMessaging();

WebApplication app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
