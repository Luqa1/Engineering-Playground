using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringPlayground.DistributedLock.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "JobExecutions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                WorkerInstance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobExecutions", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "JobExecutions");
    }
}
