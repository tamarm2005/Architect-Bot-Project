using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPlotTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlotId",
                table: "Point3Ds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlotId1",
                table: "Point3Ds",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Plots",
                columns: table => new
                {
                    PlotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalArea = table.Column<double>(type: "float", nullable: false),
                    AbsoluteZeroLevel = table.Column<double>(type: "float", nullable: false),
                    BuildPercentage = table.Column<double>(type: "float", nullable: false),
                    FrontSetback = table.Column<double>(type: "float", nullable: false),
                    SideSetback = table.Column<double>(type: "float", nullable: false),
                    RearSetback = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plots", x => x.PlotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Point3Ds_PlotId",
                table: "Point3Ds",
                column: "PlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Point3Ds_PlotId1",
                table: "Point3Ds",
                column: "PlotId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Point3Ds_Plots_PlotId",
                table: "Point3Ds",
                column: "PlotId",
                principalTable: "Plots",
                principalColumn: "PlotId");

            migrationBuilder.AddForeignKey(
                name: "FK_Point3Ds_Plots_PlotId1",
                table: "Point3Ds",
                column: "PlotId1",
                principalTable: "Plots",
                principalColumn: "PlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Point3Ds_Plots_PlotId",
                table: "Point3Ds");

            migrationBuilder.DropForeignKey(
                name: "FK_Point3Ds_Plots_PlotId1",
                table: "Point3Ds");

            migrationBuilder.DropTable(
                name: "Plots");

            migrationBuilder.DropIndex(
                name: "IX_Point3Ds_PlotId",
                table: "Point3Ds");

            migrationBuilder.DropIndex(
                name: "IX_Point3Ds_PlotId1",
                table: "Point3Ds");

            migrationBuilder.DropColumn(
                name: "PlotId",
                table: "Point3Ds");

            migrationBuilder.DropColumn(
                name: "PlotId1",
                table: "Point3Ds");
        }
    }
}
