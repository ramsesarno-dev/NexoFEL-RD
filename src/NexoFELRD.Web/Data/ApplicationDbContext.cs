using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Producto> Productos => Set<Producto>();

    public DbSet<Factura> Facturas => Set<Factura>();

    public DbSet<FacturaDetalle> FacturaDetalles =>
        Set<FacturaDetalle>();

    public DbSet<MovimientoInventario> MovimientosInventario =>
        Set<MovimientoInventario>();

    public DbSet<FacturaFormaPago> FacturaFormasPago =>
        Set<FacturaFormaPago>();

    public DbSet<RangoComprobanteElectronico>
    RangosComprobantesElectronicos =>
        Set<RangoComprobanteElectronico>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigurarRoles(builder);
        ConfigurarCliente(builder);
        ConfigurarProducto(builder);
        ConfigurarFactura(builder);
        ConfigurarFacturaDetalle(builder);
        ConfigurarMovimientoInventario(builder);
        ConfigurarFormaPago(builder);
        ConfigurarRangoComprobanteElectronico(builder);
    }

    private static void ConfigurarRoles(ModelBuilder builder)
    {
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "13b40ca0-40ca-4b3b-8eb4-898d30ef95a2",
                Name = "Administrador",
                NormalizedName = "ADMINISTRADOR",
                ConcurrencyStamp =
                    "22d42c6e-7e6f-4e88-a36e-0f87741760b4"
            },
            new IdentityRole
            {
                Id = "b2c36972-5f64-449f-a75c-cb58d710fb92",
                Name = "Facturador",
                NormalizedName = "FACTURADOR",
                ConcurrencyStamp =
                    "54415f50-a854-417a-8c41-d67522708f15"
            });
    }

    private static void ConfigurarCliente(ModelBuilder builder)
    {
        builder.Entity<Cliente>(entity =>
        {
            entity.Property(x => x.Nombre)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(x => x.IdentificacionFiscal)
                .HasMaxLength(20);

            entity.Property(x => x.Telefono)
                .HasMaxLength(25);

            entity.Property(x => x.Correo)
                .HasMaxLength(150);

            entity.Property(x => x.Direccion)
                .HasMaxLength(250);

            entity.HasIndex(x => x.IdentificacionFiscal);
        });
    }

    private static void ConfigurarProducto(ModelBuilder builder)
    {
        builder.Entity<Producto>(entity =>
        {
            entity.Property(x => x.Codigo)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Nombre)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(x => x.Descripcion)
                .HasMaxLength(1000);

            entity.Property(x => x.Precio)
                .HasPrecision(20, 4);

            entity.Property(x => x.TasaItbis)
                .HasPrecision(5, 2);

            entity.Property(x => x.Existencia)
                .HasPrecision(18, 2);

            entity.Property(x => x.IndicadorFacturacion)
                .HasDefaultValue((byte)1);

            entity.Property(x => x.IndicadorBienOServicio)
                .HasDefaultValue(TipoItemDgii.Bien);

            entity.Property(x => x.UnidadMedidaCodigo)
                .HasDefaultValue((byte)43);

            entity.HasIndex(x => x.Codigo)
                .IsUnique();
        });
    }

    private static void ConfigurarFactura(ModelBuilder builder)
    {
        builder.Entity<Factura>(entity =>
        {
            entity.ToTable(
                "Facturas",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_Factura_TipoComprobanteValido",
                        "[TipoComprobante] IN " +
                        "(31, 32, 33, 34, 41, 43, 44, 45, 46, 47)");

                    table.HasCheckConstraint(
                        "CK_Factura_IndicadorMontoGravadoValido",
                        "[IndicadorMontoGravado] IN (0, 1)");

                    table.HasCheckConstraint(
                        "CK_Factura_CodigoModificacionValido",
                        "[CodigoModificacion] IS NULL OR " +
                        "[CodigoModificacion] IN (1, 2, 3, 4, 5)");

                    table.HasCheckConstraint(
                        "CK_Factura_IndicadorNotaCreditoValido",
                        "[IndicadorNotaCreditoMayor30Dias] IS NULL OR " +
                        "[IndicadorNotaCreditoMayor30Dias] IN (0, 1)");
                });

            entity.Property(x => x.NumeroInterno)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.ENCF)
                .HasMaxLength(13);

            entity.Property(x => x.RncComprador)
                .HasMaxLength(11);

            entity.Property(x => x.IdentificadorExtranjero)
                .HasMaxLength(20);

            entity.Property(x => x.RazonSocialComprador)
                .HasMaxLength(150);

            entity.Property(x => x.CorreoComprador)
                .HasMaxLength(80);

            entity.Property(x => x.DireccionComprador)
                .HasMaxLength(100);

            entity.Property(x => x.PaisComprador)
                .HasMaxLength(60);

            entity.Property(x => x.NCFModificado)
                .HasMaxLength(19);

            entity.Property(x => x.RncOtroContribuyente)
                .HasMaxLength(11);

            entity.Property(x => x.RazonModificacion)
                .HasMaxLength(90);

            entity.Property(x => x.UsuarioId)
                .HasMaxLength(450);

            entity.Property(x => x.TerminoPago)
                .HasMaxLength(15);

            entity.Property(x => x.MontoGravado18)
                .HasPrecision(18, 2);

            entity.Property(x => x.MontoGravado16)
                .HasPrecision(18, 2);

            entity.Property(x => x.MontoGravado0)
                .HasPrecision(18, 2);

            entity.Property(x => x.MontoExento)
                .HasPrecision(18, 2);

            entity.Property(x => x.MontoNoFacturable)
                .HasPrecision(18, 2);

            entity.Property(x => x.Subtotal)
                .HasPrecision(18, 2);

            entity.Property(x => x.Itbis)
                .HasPrecision(18, 2);

            entity.Property(x => x.Total)
                .HasPrecision(18, 2);

            entity.Property(x => x.TipoPago)
                .HasDefaultValue(TipoPagoDgii.Contado);

            entity.Property(x => x.TipoIngreso)
                .HasDefaultValue(
                    TipoIngresoDgii.OperacionesNoFinancieras);

            entity.Property(x => x.IndicadorMontoGravado)
                .HasDefaultValue((byte)0);

            entity.HasIndex(x => x.NumeroInterno)
                .IsUnique();

            entity.HasIndex(x => x.ENCF)
                .IsUnique()
                .HasFilter("[ENCF] IS NOT NULL");

            entity.HasIndex(x => x.NCFModificado);

            entity.HasOne(x => x.Cliente)
                .WithMany(x => x.Facturas)
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FacturaAfectada)
                .WithMany(x => x.DocumentosRelacionados)
                .HasForeignKey(x => x.FacturaAfectadaId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarFacturaDetalle(
        ModelBuilder builder)
    {
        builder.Entity<FacturaDetalle>(entity =>
        {
            entity.Property(x => x.CodigoProducto)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.NombreItem)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(x => x.Cantidad)
                .HasPrecision(18, 2);

            entity.Property(x => x.PrecioUnitario)
                .HasPrecision(20, 4);

            entity.Property(x => x.TasaItbis)
                .HasPrecision(5, 2);

            entity.Property(x => x.Descuento)
                .HasPrecision(18, 2);

            entity.Property(x => x.Recargo)
                .HasPrecision(18, 2);

            entity.Property(x => x.Itbis)
                .HasPrecision(18, 2);

            entity.Property(x => x.Subtotal)
                .HasPrecision(18, 2);

            entity.HasOne(x => x.Factura)
                .WithMany(x => x.Detalles)
                .HasForeignKey(x => x.FacturaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Producto)
                .WithMany(x => x.DetallesFactura)
                .HasForeignKey(x => x.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FacturaDetalleAfectado)
                .WithMany(x => x.DetallesRelacionados)
                .HasForeignKey(x => x.FacturaDetalleAfectadoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurarMovimientoInventario(
        ModelBuilder builder)
    {
        builder.Entity<MovimientoInventario>(entity =>
        {
            entity.ToTable(
                "MovimientosInventario",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_MovimientoInventario_CantidadPositiva",
                        "[Cantidad] > 0");

                    table.HasCheckConstraint(
                        "CK_MovimientoInventario_ExistenciasNoNegativas",
                        "[ExistenciaAnterior] >= 0 AND " +
                        "[ExistenciaResultante] >= 0");

                    table.HasCheckConstraint(
                        "CK_MovimientoInventario_TipoValido",
                        "[Tipo] IN (1, 2, 3, 4)");

                    table.HasCheckConstraint(
                        "CK_MovimientoInventario_MotivoValido",
                        "[Motivo] IN " +
                        "(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 99)");
                });

            entity.Property(x => x.Cantidad)
                .HasPrecision(18, 2);

            entity.Property(x => x.ExistenciaAnterior)
                .HasPrecision(18, 2);

            entity.Property(x => x.ExistenciaResultante)
                .HasPrecision(18, 2);

            entity.Property(x => x.Fecha)
                .IsRequired();

            entity.Property(x => x.Observacion)
                .HasMaxLength(500);

            entity.Property(x => x.UsuarioId)
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(x => x.UsuarioNombre)
                .HasMaxLength(256)
                .IsRequired();

            entity.HasIndex(x => x.Fecha);

            entity.HasIndex(x => new
            {
                x.ProductoId,
                x.Fecha
            });

            entity.HasOne(x => x.Producto)
                .WithMany(x => x.Movimientos)
                .HasForeignKey(x => x.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Factura)
                .WithMany()
                .HasForeignKey(x => x.FacturaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
    private static void ConfigurarRangoComprobanteElectronico(
    ModelBuilder builder)
    {
        builder.Entity<RangoComprobanteElectronico>(entity =>
        {
            entity.ToTable(
                "RangosComprobantesElectronicos",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_RangoENCF_TipoComprobanteValido",
                        "[TipoComprobante] IN " +
                        "(31, 32, 33, 34, 41, 43, 44, 45, 46, 47)");

                    table.HasCheckConstraint(
                        "CK_RangoENCF_SecuenciasValidas",
                        "[SecuenciaInicial] >= 1 AND " +
                        "[SecuenciaInicial] <= 9999999999 AND " +
                        "[SecuenciaFinal] >= [SecuenciaInicial] AND " +
                        "[SecuenciaFinal] <= 9999999999");

                    table.HasCheckConstraint(
                        "CK_RangoENCF_ProximaSecuenciaValida",
                        "[ProximaSecuencia] >= [SecuenciaInicial] AND " +
                        "[ProximaSecuencia] <= [SecuenciaFinal] + 1");

                    table.HasCheckConstraint(
                        "CK_RangoENCF_FechasValidas",
                        "[FechaVencimiento] >= [FechaAutorizacion]");
                });

            entity.Property(x => x.FechaAutorizacion)
                .HasColumnType("date");

            entity.Property(x => x.FechaVencimiento)
                .HasColumnType("date");

            entity.Property(x => x.FechaCreacion)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(x => x.UsuarioId)
                .HasMaxLength(450)
                .IsRequired();

            entity.Property(x => x.UsuarioNombre)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(x => x.VersionFila)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasIndex(x => x.TipoComprobante)
                .IsUnique()
                .HasFilter("[Activo] = 1");

            entity.HasIndex(x => x.FechaVencimiento);

            entity.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Facturas)
                .WithOne(x => x.RangoComprobanteElectronico)
                .HasForeignKey(
                    x => x.RangoComprobanteElectronicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
    private static void ConfigurarFormaPago(
    ModelBuilder builder)
    {
        builder.Entity<FacturaFormaPago>(entity =>
        {
            entity.Property(x => x.Monto)
                .HasPrecision(18, 2);

            entity.HasOne(x => x.Factura)
                .WithMany(x => x.FormasPago)
                .HasForeignKey(x => x.FacturaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
