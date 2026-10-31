using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models.ViewModels;

public class NotaDebitoCrearViewModel :
    IValidatableObject
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "La factura afectada no es válida.")]
    public int FacturaAfectadaId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de emisión")]
    public DateTime FechaEmision { get; set; }
        = DateTime.Today;

    [Required(
        ErrorMessage =
            "La razón de la nota de débito es obligatoria.")]
    [StringLength(
        90,
        MinimumLength = 5,
        ErrorMessage =
            "La razón debe contener entre 5 y 90 caracteres.")]
    [Display(Name = "Razón de la modificación")]
    public string RazonModificacion { get; set; }
        = string.Empty;

    public List<NotaDebitoDetalleInputViewModel>
        Detalles
    { get; set; } = new();

    public FacturaAfectadaNotaViewModel?
        FacturaAfectada
    { get; set; }

    public List<LineaDisponibleNotaDebitoViewModel>
        LineasDisponibles
    { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (FechaEmision == default)
        {
            yield return new ValidationResult(
                "La fecha de emisión es obligatoria.",
                new[]
                {
                    nameof(FechaEmision)
                });
        }
        else if (FechaEmision.Date > DateTime.Today)
        {
            yield return new ValidationResult(
                "La fecha de emisión no puede ser futura.",
                new[]
                {
                    nameof(FechaEmision)
                });
        }

        if (Detalles is null ||
            Detalles.Count == 0 ||
            !Detalles.Any(x => x.MontoBase > 0))
        {
            yield return new ValidationResult(
                "Agrega al menos un cargo a la nota de débito.",
                new[]
                {
                    nameof(Detalles)
                });
        }
    }
}

public class NotaDebitoDetalleInputViewModel
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage =
            "Selecciona una línea del comprobante original.")]
    public int FacturaDetalleAfectadoId { get; set; }

    [Required(
        ErrorMessage =
            "El concepto del cargo es obligatorio.")]
    [StringLength(
        80,
        MinimumLength = 3,
        ErrorMessage =
            "El concepto debe contener entre 3 y 80 caracteres.")]
    public string Concepto { get; set; }
        = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage =
            "El monto adicional debe ser mayor que cero.")]
    [Display(Name = "Monto adicional antes de ITBIS")]
    public decimal MontoBase { get; set; }
}

public class LineaDisponibleNotaDebitoViewModel
{
    public int FacturaDetalleId { get; set; }

    public int NumeroLinea { get; set; }

    public int ProductoId { get; set; }

    public string CodigoProducto { get; set; }
        = string.Empty;

    public string NombreItem { get; set; }
        = string.Empty;

    public TipoItemDgii IndicadorBienOServicio
    { get; set; }

    public byte IndicadorFacturacion { get; set; }

    public decimal TasaItbis { get; set; }

    public byte? UnidadMedidaCodigo { get; set; }

    public string UnidadMedida { get; set; }
        = string.Empty;
}
