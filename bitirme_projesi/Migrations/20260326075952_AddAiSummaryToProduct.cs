using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bitirme_projesi.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSummaryToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AiSummaryUpdatedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "AiSummaryUpdatedAt",
                table: "Products");
        }
    }
}
