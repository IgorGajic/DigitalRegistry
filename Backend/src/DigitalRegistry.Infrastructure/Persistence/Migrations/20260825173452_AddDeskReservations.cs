using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeskReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GuestId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Reservations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Reservations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TakenByUserId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_TakenByUserId",
                table: "Reservations",
                column: "TakenByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reservation_Booker",
                table: "Reservations",
                sql: "([GuestId] IS NOT NULL) OR ([ContactName] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_AspNetUsers_TakenByUserId",
                table: "Reservations",
                column: "TakenByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_AspNetUsers_TakenByUserId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_TakenByUserId",
                table: "Reservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Reservation_Booker",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "TakenByUserId",
                table: "Reservations");

            migrationBuilder.AlterColumn<Guid>(
                name: "GuestId",
                table: "Reservations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
