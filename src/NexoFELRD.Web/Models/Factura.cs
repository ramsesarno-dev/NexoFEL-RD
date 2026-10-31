using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models;

public class Factura
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string NumeroInterno { get; set; } = string.Empty;

    [StringLength(13, MinimumLength = 13)]
    public string? ENCF { get; set; }
    public int? RangoComprobanteElectronicoId { get; set; }

    public RangoComprobanteElectronico?
        RangoComprobanteElectronico
    { get; set; }

    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    /*
     * Datos históricos del comprador.
     * Se guardan en la factura para que no cambien si posteriormente
     * se modifica el cliente.
     */

    [StringLength(11)]
    public string? RncComprador { get; set; }

    [StringLength(20)]
    public string? IdentificadorExtranjero { get; set; }

    [StringLength(150)]
    public string? RazonSocialComprador { get; set; }

    [EmailAddress]
    [StringLength(80)]
    public string? CorreoComprador { get; set; }

    [StringLength(100)]
    public string? DireccionComprador { get; set; }

    [StringLength(60)]
    public string? PaisComprador { get; set; }

    public TipoComprobante TipoComprobante { get; set; }
        = TipoComprobante.Consumo;

    public EstadoFactura Estado { get; set; }
        = EstadoFactura.Borrador;

    public DateTime FechaEmision { get; set; }
        = DateTime.Now;

    public DateTime? FechaVencimientoSecuencia { get; set; }

    public TipoPagoDgii TipoPago { get; set; }
        = TipoPagoDgii.Contado;

    public DateTime? FechaLimitePago { get; set; }

    [StringLength(15)]
    public string? TerminoPago { get; set; }

    public TipoIngresoDgii TipoIngreso { get; set; }
        = TipoIngresoDgii.OperacionesNoFinancieras;

    [Range(0, 1)]
    public byte IndicadorMontoGravado { get; set; }

    /*
     * Información de referencia para notas de débito,
     * notas de crédito y reemplazos.
     */

    public int? FacturaAfectadaId { get; set; }

    public Factura? FacturaAfectada { get; set; }

    [StringLength(19, MinimumLength = 11)]
    public string? NCFModificado { get; set; }

    [StringLength(11, MinimumLength = 9)]
    public string? RncOtroContribuyente { get; set; }

    public DateTime? FechaNCFModificado { get; set; }

    public CodigoModificacionDgii? CodigoModificacion { get; set; }

    [StringLength(90)]
    public string? RazonModificacion { get; set; }

    [Range(0, 1)]
    public byte? IndicadorNotaCreditoMayor30Dias { get; set; }

    public string UsuarioId { get; set; } = string.Empty;

    public decimal MontoGravado18 { get; set; }

    public decimal MontoGravado16 { get; set; }

    public decimal MontoGravado0 { get; set; }

    public decimal MontoExento { get; set; }

    public decimal MontoNoFacturable { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Itbis { get; set; }

    public decimal Total { get; set; }

    public ICollection<FacturaDetalle> Detalles { get; set; }
        = new List<FacturaDetalle>();

    public ICollection<FacturaFormaPago> FormasPago { get; set; }
        = new List<FacturaFormaPago>();

    public ICollection<Factura> DocumentosRelacionados { get; set; }
        = new List<Factura>();
}

public enum TipoComprobante
{
    CreditoFiscal = 31,
    Consumo = 32,
    NotaDebito = 33,
    NotaCredito = 34,
    Compras = 41,
    GastosMenores = 43,
    RegimenesEspeciales = 44,
    Gubernamental = 45,
    Exportaciones = 46,
    PagosExterior = 47
}

public enum CodigoModificacionDgii
{
    AnulacionTotal = 1,
    CorreccionTexto = 2,
    CorreccionMontos = 3,
    ReemplazoContingencia = 4,
    ReferenciaFacturaConsumo = 5
}

public enum EstadoFactura
{
    Borrador = 1,
    Confirmada = 2,
    Generada = 3,
    Anulada = 4
}
