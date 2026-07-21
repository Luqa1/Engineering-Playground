using EngineeringPlayground.Outbox.Infrastructure;
using EngineeringPlayground.Outbox.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOptions<OutboxProcessorOptions>()
    .BindConfiguration(OutboxProcessorOptions.SectionName)
    .Validate(options => options.MaxRetriesCount > 0, "MaxRetriesCount must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddHostedService<OutboxProcessor>();

var host = builder.Build();
host.Run();
