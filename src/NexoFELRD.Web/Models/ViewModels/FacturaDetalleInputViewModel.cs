using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models.ViewModels;

public class FacturaDetalleInputViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un producto.")]
    public int ProductoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int Cantidad { get; set; } = 1;
}
