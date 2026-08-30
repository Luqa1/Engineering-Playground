using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringPlayground.OptimisticConcurrency.Infrastructure.Migrations;

public partial class AddInventoryItemVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "version",
            table: "inventory_items",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "version",
            table: "inventory_items");
    }
}
