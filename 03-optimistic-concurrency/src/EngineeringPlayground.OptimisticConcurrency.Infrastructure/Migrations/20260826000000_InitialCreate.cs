using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringPlayground.OptimisticConcurrency.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inventory_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_inventory_items", x => x.id);
                table.CheckConstraint("ck_inventory_items_quantity_non_negative", "quantity >= 0");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inventory_items");
    }
}
