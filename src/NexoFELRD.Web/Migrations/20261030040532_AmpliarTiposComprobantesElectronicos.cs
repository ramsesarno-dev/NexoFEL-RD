using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoFELRD.Web.Migrations
{
    /// <inheritdoc />
    public partial class AmpliarTiposComprobantesElectronicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodigoModificacion",
                table: "Facturas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorreoComprador",
                table: "Facturas",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionComprador",
                table: "Facturas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FacturaAfectadaId",
                table: "Facturas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaNCFModificado",
                table: "Facturas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentificadorExtranjero",
                table: "Facturas",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "IndicadorNotaCreditoMayor30Dias",
                table: "Facturas",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NCFModificado",
                table: "Facturas",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaisComprador",
                table: "Facturas",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonModificacion",
                table: "Facturas",
                type: "nvarchar(90)",
                maxLength: 90,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocialComprador",
                table: "Facturas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RncComprador",
                table: "Facturas",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RncOtroContribuyente",
                table: "Facturas",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_FacturaAfectadaId",
                table: "Facturas",
                column: "FacturaAfectadaId");

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_NCFModificado",
                table: "Facturas",
                column: "NCFModificado");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_CodigoModificacionValido",
                table: "Facturas",
                sql: "[CodigoModificacion] IS NULL OR [CodigoModificacion] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_IndicadorMontoGravadoValido",
                table: "Facturas",
                sql: "[IndicadorMontoGravado] IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_IndicadorNotaCreditoValido",
                table: "Facturas",
                sql: "[IndicadorNotaCreditoMayor30Dias] IS NULL OR [IndicadorNotaCreditoMayor30Dias] IN (0, 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Factura_TipoComprobanteValido",
                table: "Facturas",
                sql: "[TipoComprobante] IN (31, 32, 33, 34, 41, 43, 44, 45, 46, 47)");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_Facturas_FacturaAfectadaId",
                table: "Facturas",
                column: "FacturaAfectadaId",
                principalTable: "Facturas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_Facturas_FacturaAfectadaId",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_FacturaAfectadaId",
                table: "Facturas");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_NCFModificado",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Factura_CodigoModificacionValido",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Factura_IndicadorMontoGravadoValido",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Factura_IndicadorNotaCreditoValido",
                table: "Facturas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Factura_TipoComprobanteValido",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "CodigoModificacion",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "CorreoComprador",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "DireccionComprador",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FacturaAfectadaId",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "FechaNCFModificado",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "IdentificadorExtranjero",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "IndicadorNotaCreditoMayor30Dias",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "NCFModificado",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "PaisComprador",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "RazonModificacion",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "RazonSocialComprador",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "RncComprador",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "RncOtroContribuyente",
                table: "Facturas");
        }
    }
}
