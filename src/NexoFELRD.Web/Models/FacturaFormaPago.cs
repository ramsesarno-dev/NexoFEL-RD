using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models;

public class FacturaFormaPago
{
    public int Id { get; set; }

    public int FacturaId { get; set; }

    public Factura Factura { get; set; } = null!;

    public FormaPagoDgii FormaPago { get; set; }

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "El monto de pago debe ser mayor que cero.")]
    public decimal Monto { get; set; }
}
