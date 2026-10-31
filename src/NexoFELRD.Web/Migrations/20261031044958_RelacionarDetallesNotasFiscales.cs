using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoFELRD.Web.Migrations
{
    /// <inheritdoc />
    public partial class RelacionarDetallesNotasFiscales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FacturaDetalleAfectadoId",
                table: "FacturaDetalles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacturaDetalles_FacturaDetalleAfectadoId",
                table: "FacturaDetalles",
                column: "FacturaDetalleAfectadoId");

            migrationBuilder.AddForeignKey(
                name: "FK_FacturaDetalles_FacturaDetalles_FacturaDetalleAfectadoId",
                table: "FacturaDetalles",
                column: "FacturaDetalleAfectadoId",
                principalTable: "FacturaDetalles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FacturaDetalles_FacturaDetalles_FacturaDetalleAfectadoId",
                table: "FacturaDetalles");

            migrationBuilder.DropIndex(
                name: "IX_FacturaDetalles_FacturaDetalleAfectadoId",
                table: "FacturaDetalles");

            migrationBuilder.DropColumn(
                name: "FacturaDetalleAfectadoId",
                table: "FacturaDetalles");
        }
    }
}
