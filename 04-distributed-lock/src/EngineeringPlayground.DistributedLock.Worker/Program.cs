using EngineeringPlayground.DistributedLock.Infrastructure;
using EngineeringPlayground.DistributedLock.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is required.");

var workerInstance = builder.Configuration["WORKER_INSTANCE"];
if (string.IsNullOrWhiteSpace(workerInstance))
{
    throw new InvalidOperationException("WORKER_INSTANCE is required.");
}

builder.Services.AddDistributedLockInfrastructure(connectionString);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new WorkerOptions(workerInstance));
builder.Services.AddScoped<DailyReportJob>();
builder.Services.AddHostedService<OneShotWorker>();

var host = builder.Build();

var applyMigrations = builder.Configuration.GetValue<bool>("APPLY_MIGRATIONS");
if (applyMigrations && !builder.Environment.IsProduction())
{
    await DatabaseMigrator.MigrateAsync(host.Services);
}
else if (applyMigrations)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
    logger.LogWarning("Automatic migrations are disabled in Production");
}

await host.RunAsync();
