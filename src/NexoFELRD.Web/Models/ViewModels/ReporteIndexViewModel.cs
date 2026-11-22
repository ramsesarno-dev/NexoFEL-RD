using System.ComponentModel.DataAnnotations;
using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Models.ViewModels;

public class ReporteIndexViewModel
{
    [DataType(DataType.Date)]
    [Display(Name = "Desde")]
    public DateTime Desde { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Hasta")]
    public DateTime Hasta { get; set; }

    [Display(Name = "Tipo de comprobante")]
    public TipoComprobante? TipoSeleccionado { get; set; }

    [Display(Name = "Mostrar anulados")]
    public bool IncluirAnuladas { get; set; }

    public decimal FacturacionBruta { get; set; }

    public decimal NotasDebito { get; set; }

    public decimal NotasCredito { get; set; }

    public decimal VentasNetas { get; set; }

    public decimal ItbisNeto { get; set; }

    public decimal MontoNoFacturableNeto { get; set; }

    public decimal ComprasYGastosNetos { get; set; }

    public int CantidadDocumentos { get; set; }

    public int CantidadAnulados { get; set; }

    public int CantidadRevertidos { get; set; }

    public int CantidadBorradores { get; set; }

    public int TotalResultados { get; set; }

    public bool ResultadosLimitados { get; set; }

    public List<ReporteDocumentoViewModel> Documentos
    { get; set; } = new();

    public List<ReporteTipoComprobanteViewModel> ResumenPorTipo
    { get; set; } = new();

    public List<ReporteDiarioViewModel> ResumenDiario
    { get; set; } = new();

    public List<ReporteProductoViewModel> Productos
    { get; set; } = new();
}

public class ReporteDocumentoViewModel
{
    public int Id { get; set; }

    public string NumeroInterno { get; set; } = string.Empty;

    public string? ENCF { get; set; }

    public DateTime FechaEmision { get; set; }

    public string Comprador { get; set; } = string.Empty;

    public TipoComprobante TipoComprobante { get; set; }

    public TipoComprobante? TipoComprobanteAfectado
    { get; set; }

    public int? FacturaAfectadaId { get; set; }

    public string? ENCFOriginal { get; set; }

    public EstadoFactura Estado { get; set; }

    public bool TieneEfectoFiscal { get; set; }

    public bool RevertidaPorNotaCredito =>
        Estado == EstadoFactura.Anulada && TieneEfectoFiscal;

    public decimal Subtotal { get; set; }

    public decimal Itbis { get; set; }

    public decimal MontoNoFacturable { get; set; }

    public decimal Total { get; set; }

    public decimal MontoFirmado { get; set; }
}

public class ReporteTipoComprobanteViewModel
{
    public TipoComprobante TipoComprobante { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Categoria { get; set; } = string.Empty;

    public int Cantidad { get; set; }

    public decimal Monto { get; set; }
}

public class ReporteDiarioViewModel
{
    public DateTime Fecha { get; set; }

    public decimal FacturacionBruta { get; set; }

    public decimal NotasDebito { get; set; }

    public decimal NotasCredito { get; set; }

    public decimal VentasNetas { get; set; }
}

public class ReporteProductoViewModel
{
    public int ProductoId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public decimal CantidadFacturada { get; set; }

    public decimal CantidadDevuelta { get; set; }

    public decimal CantidadNeta { get; set; }
}
