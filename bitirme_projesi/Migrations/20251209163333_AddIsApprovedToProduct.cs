using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace bitirme_projesi.Migrations
{
    /// <inheritdoc />
    public partial class AddIsApprovedToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Products");
        }
    }
}
