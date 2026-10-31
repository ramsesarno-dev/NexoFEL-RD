using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models;

public class FacturaDetalle
{
    public int Id { get; set; }

    public int FacturaId { get; set; }

    public Factura Factura { get; set; } = null!;

    public int ProductoId { get; set; }

    public Producto Producto { get; set; } = null!;

    /*
 * Línea original afectada por una nota de crédito.
 * Permite controlar devoluciones parciales y evitar
 * acreditar una cantidad mayor que la facturada.
 */
    public int? FacturaDetalleAfectadoId { get; set; }

    public FacturaDetalle? FacturaDetalleAfectado { get; set; }

    public ICollection<FacturaDetalle> DetallesRelacionados
    { get; set; } = new List<FacturaDetalle>();
    public int NumeroLinea { get; set; }

    [Required]
    [StringLength(30)]
    public string CodigoProducto { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string NombreItem { get; set; } = string.Empty;

    public TipoItemDgii IndicadorBienOServicio { get; set; }
        = TipoItemDgii.Bien;

    [Range(0, 4)]
    public byte IndicadorFacturacion { get; set; }

    public byte? UnidadMedidaCodigo { get; set; }

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.9999",
        ErrorMessage = "El precio unitario no puede ser negativo.")]
    public decimal PrecioUnitario { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal TasaItbis { get; set; }

    public decimal Descuento { get; set; }

    public decimal Recargo { get; set; }

    public decimal Itbis { get; set; }

    public decimal Subtotal { get; set; }
}
