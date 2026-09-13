using EngineeringPlayground.DistributedLock.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EngineeringPlayground.DistributedLock.Infrastructure;

public sealed class JobExecutionConfiguration : IEntityTypeConfiguration<JobExecution>
{
    public void Configure(EntityTypeBuilder<JobExecution> builder)
    {
        builder.ToTable("JobExecutions");

        builder.HasKey(jobExecution => jobExecution.Id);

        builder.Property(jobExecution => jobExecution.Id)
            .ValueGeneratedNever();

        builder.Property(jobExecution => jobExecution.JobName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(jobExecution => jobExecution.ExecutionKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(jobExecution => jobExecution.WorkerInstance)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(jobExecution => jobExecution.StartedAtUtc)
            .IsRequired();
    }
}
