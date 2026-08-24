using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftAssignmentId",
                table: "Shifts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ShiftTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTemplates", x => x.Id);
                    table.CheckConstraint("CK_ShiftTemplate_Period", "[StartTime] <> [EndTime]");
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WaiterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedByManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.CheckConstraint("CK_ShiftAssignment_Period", "[ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]");
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_AspNetUsers_WaiterId",
                        column: x => x.WaiterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_ShiftTemplates_ShiftTemplateId",
                        column: x => x.ShiftTemplateId,
                        principalTable: "ShiftTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ShiftAssignmentId",
                table: "Shifts",
                column: "ShiftAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_RestaurantId_ValidFrom_ValidTo",
                table: "ShiftAssignments",
                columns: new[] { "RestaurantId", "ValidFrom", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftTemplateId",
                table: "ShiftAssignments",
                column: "ShiftTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_WaiterId",
                table: "ShiftAssignments",
                column: "WaiterId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftTemplates_RestaurantId_Name",
                table: "ShiftTemplates",
                columns: new[] { "RestaurantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_ShiftAssignments_ShiftAssignmentId",
                table: "Shifts",
                column: "ShiftAssignmentId",
                principalTable: "ShiftAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_ShiftAssignments_ShiftAssignmentId",
                table: "Shifts");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "ShiftTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_ShiftAssignmentId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "ShiftAssignmentId",
                table: "Shifts");
        }
    }
}
