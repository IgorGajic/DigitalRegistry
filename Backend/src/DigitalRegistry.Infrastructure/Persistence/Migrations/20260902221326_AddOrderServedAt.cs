using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderServedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ServedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServedAtUtc",
                table: "Orders");
        }
    }
}
