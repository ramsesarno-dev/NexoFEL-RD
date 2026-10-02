namespace NexoFELRD.Web.Models;

public class MovimientoInventario
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public int? FacturaId { get; set; }
    public Factura? Factura { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int Cantidad { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Observacion { get; set; }
}

public enum TipoMovimiento
{
    Entrada = 1,
    Salida = 2,
    Ajuste = 3
}
