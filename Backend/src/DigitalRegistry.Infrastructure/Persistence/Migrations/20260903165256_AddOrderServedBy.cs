using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderServedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ServedByWaiterId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ServedByWaiterId",
                table: "Orders",
                column: "ServedByWaiterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_ServedByWaiterId",
                table: "Orders",
                column: "ServedByWaiterId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_ServedByWaiterId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ServedByWaiterId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ServedByWaiterId",
                table: "Orders");
        }
    }
}
