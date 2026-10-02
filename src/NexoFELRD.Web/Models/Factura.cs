using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models;

public class Factura
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string NumeroInterno { get; set; } = string.Empty;

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public TipoComprobante TipoComprobante { get; set; } = TipoComprobante.Consumo;
    public EstadoFactura Estado { get; set; } = EstadoFactura.Borrador;
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public string UsuarioId { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Itbis { get; set; }
    public decimal Total { get; set; }

    public ICollection<FacturaDetalle> Detalles { get; set; } = new List<FacturaDetalle>();
}

public enum TipoComprobante
{
    CreditoFiscal = 31,
    Consumo = 32
}

public enum EstadoFactura
{
    Borrador = 1,
    Confirmada = 2,
    Generada = 3,
    Anulada = 4
}
