using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyalStamp.Data.Migrations;

/// <inheritdoc />
public partial class _20261118091426_AddStampTransactionsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StampTransactions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CustomerCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TransactionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                StampsChanged = table.Column<int>(type: "int", nullable: false),
                SpendAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StampTransactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_StampTransactions_CustomerCards_CustomerCardId",
                    column: x => x.CustomerCardId,
                    principalTable: "CustomerCards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StampTransactions_CustomerCardId",
            table: "StampTransactions",
            column: "CustomerCardId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StampTransactions");
    }
}
