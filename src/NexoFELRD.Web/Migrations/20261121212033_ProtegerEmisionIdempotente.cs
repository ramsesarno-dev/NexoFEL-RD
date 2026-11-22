using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexoFELRD.Web.Migrations
{
    /// <inheritdoc />
    public partial class ProtegerEmisionIdempotente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClaveOperacion",
                table: "Facturas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HuellaSolicitud",
                table: "Facturas",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facturas_ClaveOperacion",
                table: "Facturas",
                column: "ClaveOperacion",
                unique: true,
                filter: "[ClaveOperacion] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Facturas_ClaveOperacion",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "ClaveOperacion",
                table: "Facturas");

            migrationBuilder.DropColumn(
                name: "HuellaSolicitud",
                table: "Facturas");
        }
    }
}
