using EngineeringPlayground.Outbox.Infrastructure;
using EngineeringPlayground.Outbox.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<OutboxProcessorOptions>(builder.Configuration.GetSection(OutboxProcessorOptions.SectionName));
builder.Services.AddHostedService<OutboxProcessor>();

var host = builder.Build();
host.Run();
