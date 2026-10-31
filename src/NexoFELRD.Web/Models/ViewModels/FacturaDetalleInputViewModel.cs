using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models.ViewModels;

public class FacturaDetalleInputViewModel
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Selecciona un producto o servicio.")]
    public int ProductoId { get; set; }

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; set; } = 1m;
}
