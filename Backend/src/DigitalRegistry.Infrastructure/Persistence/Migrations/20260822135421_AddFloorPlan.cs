using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositionX",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositionY",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "Tables",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rotation",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Shape",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CanvasWidth = table.Column<int>(type: "int", nullable: false),
                    CanvasHeight = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.CheckConstraint("CK_Room_Canvas_Positive", "[CanvasWidth] > 0 AND [CanvasHeight] > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tables_RoomId",
                table: "Tables",
                column: "RoomId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Table_Rotation_Range",
                table: "Tables",
                sql: "[Rotation] >= 0 AND [Rotation] < 360");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Table_Size_Positive",
                table: "Tables",
                sql: "[Width] > 0 AND [Height] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RestaurantId_DisplayOrder",
                table: "Rooms",
                columns: new[] { "RestaurantId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_RestaurantId_Name",
                table: "Rooms",
                columns: new[] { "RestaurantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_Rooms_RoomId",
                table: "Tables",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Rooms_RoomId",
                table: "Tables");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Tables_RoomId",
                table: "Tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Table_Rotation_Range",
                table: "Tables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Table_Size_Positive",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PositionX",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "PositionY",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "Rotation",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "Shape",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Tables");
        }
    }
}
