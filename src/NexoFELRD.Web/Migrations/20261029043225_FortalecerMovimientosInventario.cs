using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoFELRD.Web.Migrations
{
    /// <inheritdoc />
    public partial class FortalecerMovimientosInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_ProductoId",
                table: "MovimientosInventario");

            migrationBuilder.AlterColumn<string>(
                name: "Observacion",
                table: "MovimientosInventario",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Motivo",
                table: "MovimientosInventario",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "MovimientosInventario",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioNombre",
                table: "MovimientosInventario",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_Fecha",
                table: "MovimientosInventario",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_ProductoId_Fecha",
                table: "MovimientosInventario",
                columns: new[] { "ProductoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_UsuarioId",
                table: "MovimientosInventario",
                column: "UsuarioId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoInventario_CantidadPositiva",
                table: "MovimientosInventario",
                sql: "[Cantidad] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoInventario_ExistenciasNoNegativas",
                table: "MovimientosInventario",
                sql: "[ExistenciaAnterior] >= 0 AND [ExistenciaResultante] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoInventario_MotivoValido",
                table: "MovimientosInventario",
                sql: "[Motivo] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 99)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MovimientoInventario_TipoValido",
                table: "MovimientosInventario",
                sql: "[Tipo] IN (1, 2, 3, 4)");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_AspNetUsers_UsuarioId",
                table: "MovimientosInventario",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosInventario_AspNetUsers_UsuarioId",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_Fecha",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_ProductoId_Fecha",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_UsuarioId",
                table: "MovimientosInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoInventario_CantidadPositiva",
                table: "MovimientosInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoInventario_ExistenciasNoNegativas",
                table: "MovimientosInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoInventario_MotivoValido",
                table: "MovimientosInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MovimientoInventario_TipoValido",
                table: "MovimientosInventario");

            migrationBuilder.DropColumn(
                name: "Motivo",
                table: "MovimientosInventario");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "MovimientosInventario");

            migrationBuilder.DropColumn(
                name: "UsuarioNombre",
                table: "MovimientosInventario");

            migrationBuilder.AlterColumn<string>(
                name: "Observacion",
                table: "MovimientosInventario",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_ProductoId",
                table: "MovimientosInventario",
                column: "ProductoId");
        }
    }
}
