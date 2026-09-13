using System;
using EngineeringPlayground.DistributedLock.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringPlayground.DistributedLock.Infrastructure.Migrations;

[DbContext(typeof(DistributedLockDbContext))]
[Migration("20260913000000_InitialCreate")]
partial class InitialCreate
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.12")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("EngineeringPlayground.DistributedLock.Domain.JobExecution", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<DateTimeOffset?>("CompletedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("JobName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<DateTimeOffset>("StartedAtUtc")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("WorkerInstance")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.HasKey("Id");

            b.ToTable("JobExecutions");
        });
#pragma warning restore 612, 618
    }
}
