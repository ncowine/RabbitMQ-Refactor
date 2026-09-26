using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Hosting;
using WebApi.Services;
using WebApi.Subscribers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRabbitMQ(builder.Configuration);
builder.Services.AddSingleton<EmployeeCache>();
builder.Services.AddSingleton<EmployeeEventsSubscriber>();
builder.Services.AddHostedService<RabbitMQHostedService>();

WebApplication app = builder.Build();

app.MapControllers();

app.Run();
