using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOpeningMaterialRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpeningId",
                table: "Materials",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_OpeningId",
                table: "Materials",
                column: "OpeningId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_Openings_OpeningId",
                table: "Materials",
                column: "OpeningId",
                principalTable: "Openings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_Openings_OpeningId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_OpeningId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "OpeningId",
                table: "Materials");
        }
    }
}
