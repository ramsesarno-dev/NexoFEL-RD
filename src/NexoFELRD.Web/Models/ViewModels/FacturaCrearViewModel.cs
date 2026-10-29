using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace NexoFELRD.Web.Models.ViewModels;

public class FacturaCrearViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un cliente.")]
    public int ClienteId { get; set; }

    [Required(ErrorMessage = "Seleccione el tipo de comprobante.")]
    public TipoComprobante TipoComprobante { get; set; }
        = TipoComprobante.Consumo;

    [DataType(DataType.Date)]
    public DateTime FechaEmision { get; set; }
        = DateTime.Today;

    [MinLength(1, ErrorMessage = "La factura debe contener al menos un producto.")]
    public List<FacturaDetalleInputViewModel> Detalles { get; set; }
        = new();

    public IEnumerable<SelectListItem> Clientes { get; set; }
        = Enumerable.Empty<SelectListItem>();

    public IEnumerable<ProductoFacturaViewModel> Productos { get; set; }
        = Enumerable.Empty<ProductoFacturaViewModel>();
}

public class ProductoFacturaViewModel
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public decimal Precio { get; set; }

    public decimal TasaItbis { get; set; }

    public byte IndicadorFacturacion { get; set; }

    public int Existencia { get; set; }
}
