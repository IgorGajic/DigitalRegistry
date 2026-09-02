using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Theme",
                table: "Restaurants",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Theme",
                table: "Restaurants");
        }
    }
}
