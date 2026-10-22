using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<FacturaDetalle> FacturaDetalles => Set<FacturaDetalle>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "13b40ca0-40ca-4b3b-8eb4-898d30ef95a2",
                Name = "Administrador",
                NormalizedName = "ADMINISTRADOR",
                ConcurrencyStamp = "22d42c6e-7e6f-4e88-a36e-0f87741760b4"
            },
            new IdentityRole
            {
                Id = "b2c36972-5f64-449f-a75c-cb58d710fb92",
                Name = "Facturador",
                NormalizedName = "FACTURADOR",
                ConcurrencyStamp = "54415f50-a854-417a-8c41-d67522708f15"
            });

        builder.Entity<Cliente>(entity =>
        {
            entity.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(x => x.IdentificacionFiscal).HasMaxLength(20);
            entity.Property(x => x.Correo).HasMaxLength(150);
            entity.HasIndex(x => x.IdentificacionFiscal);
        });

        builder.Entity<Producto>(entity =>
        {
            entity.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Precio).HasPrecision(18, 2);
            entity.Property(x => x.TasaItbis).HasPrecision(5, 2);
            entity.Property(x => x.IndicadorFacturacion).HasDefaultValue((byte)1);
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        builder.Entity<Factura>(entity =>
        {
            entity.Property(x => x.NumeroInterno).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.Itbis).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.HasIndex(x => x.NumeroInterno).IsUnique();
            entity.HasOne(x => x.Cliente)
                .WithMany(x => x.Facturas)
                .HasForeignKey(x => x.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FacturaDetalle>(entity =>
        {
            entity.Property(x => x.PrecioUnitario).HasPrecision(18, 2);
            entity.Property(x => x.Itbis).HasPrecision(18, 2);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.HasOne(x => x.Factura)
                .WithMany(x => x.Detalles)
                .HasForeignKey(x => x.FacturaId);
            entity.HasOne(x => x.Producto)
                .WithMany(x => x.DetallesFactura)
                .HasForeignKey(x => x.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MovimientoInventario>(entity =>
        {
            entity.HasOne(x => x.Producto)
                .WithMany(x => x.Movimientos)
                .HasForeignKey(x => x.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Factura)
                .WithMany()
                .HasForeignKey(x => x.FacturaId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
