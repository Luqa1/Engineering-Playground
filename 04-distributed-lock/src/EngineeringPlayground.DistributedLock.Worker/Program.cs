using EngineeringPlayground.DistributedLock.Infrastructure;
using EngineeringPlayground.DistributedLock.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is required.");

builder.Services.AddDistributedLockInfrastructure(connectionString);

var migrateOnly = builder.Configuration.GetValue<bool>("MIGRATE_ONLY");
if (migrateOnly)
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("Automatic migrations are disabled in Production.");
    }

    using var migrationHost = builder.Build();
    var logger = migrationHost.Services
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigration");

    logger.LogInformation("Applying database migrations");
    await DatabaseMigrator.MigrateAsync(migrationHost.Services);
    logger.LogInformation("Database migrations applied");
    return;
}

var workerInstance = builder.Configuration["WORKER_INSTANCE"];
if (string.IsNullOrWhiteSpace(workerInstance))
{
    throw new InvalidOperationException("WORKER_INSTANCE is required.");
}

var jobExecutionKey = builder.Configuration["JOB_EXECUTION_KEY"];
if (string.IsNullOrWhiteSpace(jobExecutionKey))
{
    throw new InvalidOperationException("JOB_EXECUTION_KEY is required.");
}

var protectedWorkDurationSeconds = builder.Configuration.GetValue<int>(
    "PROTECTED_WORK_DURATION_SECONDS");
if (protectedWorkDurationSeconds < 0)
{
    throw new InvalidOperationException("PROTECTED_WORK_DURATION_SECONDS cannot be negative.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new WorkerOptions(
    workerInstance,
    jobExecutionKey,
    TimeSpan.FromSeconds(protectedWorkDurationSeconds)));
builder.Services.AddScoped<DailyReportJob>();
builder.Services.AddScoped<DailyReportJobRunner>();
builder.Services.AddHostedService<OneShotWorker>();

var host = builder.Build();
await host.RunAsync();
