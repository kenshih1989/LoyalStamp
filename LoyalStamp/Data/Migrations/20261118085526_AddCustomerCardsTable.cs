using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyalStamp.Data.Migrations;

/// <inheritdoc />
public partial class _20261118085526_AddCustomerCardsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CustomerCards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                CampaignId = table.Column<int>(type: "int", nullable: false),
                CardCode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                CurrentStamps = table.Column<int>(type: "int", nullable: false),
                TotalRedeemedCount = table.Column<int>(type: "int", nullable: false),
                LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerCards", x => x.Id);
                table.ForeignKey(
                    name: "FK_CustomerCards_StampCampaigns_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "StampCampaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CustomerCards_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomerCards_CampaignId",
            table: "CustomerCards",
            column: "CampaignId");

        migrationBuilder.CreateIndex(
            name: "IX_CustomerCards_CardCode",
            table: "CustomerCards",
            column: "CardCode",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomerCards_UserId_CampaignId",
            table: "CustomerCards",
            columns: new[] { "UserId", "CampaignId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CustomerCards");
    }
}
