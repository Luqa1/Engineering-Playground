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

builder.Services.AddDistributedLockInfrastructure(connectionString);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new WorkerOptions(
    workerInstance,
    jobExecutionKey,
    TimeSpan.FromSeconds(protectedWorkDurationSeconds)));
builder.Services.AddScoped<DailyReportJob>();
builder.Services.AddScoped<DailyReportJobRunner>();
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
