using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyalStamp.Data.Migrations;

/// <inheritdoc />
public partial class _20261117155613_AddStampCampaignsTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StampCampaigns",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                MerchantId = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                MinSpendAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                TargetStamps = table.Column<int>(type: "int", nullable: false),
                RewardItem = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StampCampaigns", x => x.Id);
                table.ForeignKey(
                    name: "FK_StampCampaigns_Merchants_MerchantId",
                    column: x => x.MerchantId,
                    principalTable: "Merchants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StampCampaigns_MerchantId",
            table: "StampCampaigns",
            column: "MerchantId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StampCampaigns");
    }
}
