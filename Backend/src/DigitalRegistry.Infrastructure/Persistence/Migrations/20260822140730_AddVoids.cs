using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalRegistry.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_OrderId",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transaction_Amount_NonNegative",
                table: "Transactions");

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesTransactionId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VoidRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VoidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoidRecords", x => x.Id);
                    table.CheckConstraint("CK_VoidRecord_Amount_NonNegative", "[Amount] >= 0");
                    table.CheckConstraint("CK_VoidRecord_Quantity_NonNegative", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_VoidRecords_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoidRecords_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoidRecords_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoidRecords_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OrderId",
                table: "Transactions",
                column: "OrderId",
                unique: true,
                filter: "[ReversesTransactionId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ReversesTransactionId",
                table: "Transactions",
                column: "ReversesTransactionId",
                unique: true,
                filter: "[ReversesTransactionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transaction_Amount_Sign",
                table: "Transactions",
                sql: "([ReversesTransactionId] IS NULL AND [Amount] >= 0) OR ([ReversesTransactionId] IS NOT NULL AND [Amount] <= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRecords_ApprovedByUserId",
                table: "VoidRecords",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRecords_MenuItemId",
                table: "VoidRecords",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRecords_OrderId",
                table: "VoidRecords",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRecords_PerformedByUserId",
                table: "VoidRecords",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRecords_RestaurantId_PerformedByUserId_VoidedAtUtc",
                table: "VoidRecords",
                columns: new[] { "RestaurantId", "PerformedByUserId", "VoidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VoidRecords_RestaurantId_VoidedAtUtc",
                table: "VoidRecords",
                columns: new[] { "RestaurantId", "VoidedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_ReversesTransactionId",
                table: "Transactions",
                column: "ReversesTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_ReversesTransactionId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "VoidRecords");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_OrderId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ReversesTransactionId",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transaction_Amount_Sign",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ReversesTransactionId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OrderId",
                table: "Transactions",
                column: "OrderId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transaction_Amount_NonNegative",
                table: "Transactions",
                sql: "[Amount] >= 0");
        }
    }
}
