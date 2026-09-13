using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringPlayground.DistributedLock.Infrastructure.Migrations;

public partial class AddExecutionKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExecutionKey",
            table: "JobExecutions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "legacy");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExecutionKey",
            table: "JobExecutions");
    }
}
