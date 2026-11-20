using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Models.ViewModels;

public class DashboardViewModel
{
    public int CantidadClientes { get; set; }

    public int CantidadProductos { get; set; }

    public int CantidadFacturas { get; set; }

    public int ProductosBajoInventario { get; set; }

    public int CantidadFacturasHoy { get; set; }

    public int CantidadNotasDebitoHoy { get; set; }

    public int CantidadNotasCreditoHoy { get; set; }

    public decimal VentasHoy { get; set; }

    public List<DocumentoRecienteDashboardViewModel> DocumentosRecientes
    { get; set; } = new();

    public List<ProductoBajoInventarioDashboardViewModel>
        ProductosConBajoInventario
    { get; set; } = new();
}

public class DocumentoRecienteDashboardViewModel
{
    public int Id { get; set; }

    public string NumeroInterno { get; set; } = string.Empty;

    public string? ENCF { get; set; }

    public DateTime FechaEmision { get; set; }

    public string Comprador { get; set; } = string.Empty;

    public TipoComprobante TipoComprobante { get; set; }

    public EstadoFactura Estado { get; set; }

    public decimal Total { get; set; }
}

public class ProductoBajoInventarioDashboardViewModel
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public decimal Existencia { get; set; }
}
