using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace bitirme_projesi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "ImageVector",
                table: "Products",
                type: "vector(512)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageVector",
                table: "Products");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
