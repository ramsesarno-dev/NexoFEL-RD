using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoFELRD.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRangosComprobantesElectronicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RangoComprobanteElectronicoId",
                table: "Facturas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RangosComprobantesElectronicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    SecuenciaInicial = table.Column<long>(type: "bigint", nullable: false),
                    SecuenciaFinal = table.Column<long>(type: "bigint", nullable: false),
                    ProximaSecuencia = table.Column<long>(type: "bigint", nullable: false),
                    FechaAutorizacion = table.Column<DateTime>(type: "date", nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "date", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VersionFila = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RangosComprobantesElectronicos", x => x.Id);
                    table.CheckConstraint("CK_RangoENCF_FechasValidas", "[FechaVencimiento] >= [FechaAutorizacion]");
                    table.CheckConstraint("CK_RangoENCF_ProximaSecuenciaValida", "[ProximaSecuencia] >= [SecuenciaInicial] AND [ProximaSecuencia] <= [SecuenciaFinal] + 1");
                    table.CheckConstraint("CK_RangoENCF_SecuenciasValidas", "[SecuenciaInicial] >= 1 AND [SecuenciaInicial] <= 9999999999 AND [SecuenciaFinal] >= [SecuenciaInicial] AND [SecuenciaFinal] <= 9999999999");
                    table.CheckConstraint("CK_RangoENCF_TipoComprobanteValido", "[TipoComprobante] IN (31, 32, 33, 34, 41, 43, 44, 45, 46, 47)");
                    table.ForeignKey(
                        name: "FK_RangosComprobantesElectronicos_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_RangoComprobanteElectronicoId",
                table: "Facturas",
                column: "RangoComprobanteElectronicoId");

            migrationBuilder.CreateIndex(
                name: "IX_RangosComprobantesElectronicos_FechaVencimiento",
                table: "RangosComprobantesElectronicos",
                column: "FechaVencimiento");

            migrationBuilder.CreateIndex(
                name: "IX_RangosComprobantesElectronicos_TipoComprobante",
                table: "RangosComprobantesElectronicos",
                column: "TipoComprobante",
                unique: true,
                filter: "[Activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RangosComprobantesElectronicos_UsuarioId",
                table: "RangosComprobantesElectronicos",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Facturas_RangosComprobantesElectronicos_RangoComprobanteElectronicoId",
                table: "Facturas",
                column: "RangoComprobanteElectronicoId",
                principalTable: "RangosComprobantesElectronicos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facturas_RangosComprobantesElectronicos_RangoComprobanteElectronicoId",
                table: "Facturas");

            migrationBuilder.DropTable(
                name: "RangosComprobantesElectronicos");

            migrationBuilder.DropIndex(
                name: "IX_Facturas_RangoComprobanteElectronicoId",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "RangoComprobanteElectronicoId",
                table: "Facturas");
        }
    }
}
