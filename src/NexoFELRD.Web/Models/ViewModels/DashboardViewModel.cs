namespace NexoFELRD.Web.Models.ViewModels;

public class DashboardViewModel
{
    public int CantidadClientes { get; set; }

    public int CantidadProductos { get; set; }

    public int CantidadFacturas { get; set; }

    public int ProductosBajoInventario { get; set; }

    public decimal VentasHoy { get; set; }
}
