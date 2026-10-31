using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models.ViewModels;

public class RangoComprobanteCrearViewModel :
    IValidatableObject
{
    [Required(
        ErrorMessage =
            "Selecciona el tipo de comprobante.")]
    public TipoComprobante? TipoComprobante { get; set; }

    [Range(
        typeof(long),
        "1",
        "9999999999",
        ErrorMessage =
            "La secuencia inicial debe estar entre " +
            "1 y 9,999,999,999.")]
    public long SecuenciaInicial { get; set; } = 1;

    [Range(
        typeof(long),
        "1",
        "9999999999",
        ErrorMessage =
            "La secuencia final debe estar entre " +
            "1 y 9,999,999,999.")]
    public long SecuenciaFinal { get; set; } = 1000;

    [Required(
        ErrorMessage =
            "La fecha de autorización es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime FechaAutorizacion { get; set; }
        = DateTime.Today;

    [Required(
        ErrorMessage =
            "La fecha de vencimiento es obligatoria.")]
    [DataType(DataType.Date)]
    public DateTime FechaVencimiento { get; set; }
        = DateTime.Today.AddYears(1);

    public bool ActivarInmediatamente { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!TipoComprobante.HasValue ||
            !Enum.IsDefined(
                typeof(TipoComprobante),
                TipoComprobante.Value))
        {
            yield return new ValidationResult(
                "El tipo de comprobante no es válido.",
                new[]
                {
                    nameof(TipoComprobante)
                });
        }

        if (SecuenciaFinal < SecuenciaInicial)
        {
            yield return new ValidationResult(
                "La secuencia final debe ser igual o mayor " +
                "que la secuencia inicial.",
                new[]
                {
                    nameof(SecuenciaFinal)
                });
        }

        if (FechaAutorizacion == default)
        {
            yield return new ValidationResult(
                "La fecha de autorización es obligatoria.",
                new[]
                {
                    nameof(FechaAutorizacion)
                });
        }
        else if (FechaAutorizacion.Date > DateTime.Today)
        {
            yield return new ValidationResult(
                "La fecha de autorización no puede ser futura.",
                new[]
                {
                    nameof(FechaAutorizacion)
                });
        }

        if (FechaVencimiento == default)
        {
            yield return new ValidationResult(
                "La fecha de vencimiento es obligatoria.",
                new[]
                {
                    nameof(FechaVencimiento)
                });
        }
        else if (
            FechaVencimiento.Date <
            FechaAutorizacion.Date)
        {
            yield return new ValidationResult(
                "La fecha de vencimiento debe ser igual o " +
                "posterior a la fecha de autorización.",
                new[]
                {
                    nameof(FechaVencimiento)
                });
        }

        if (ActivarInmediatamente &&
            FechaVencimiento.Date < DateTime.Today)
        {
            yield return new ValidationResult(
                "No se puede activar un rango que ya está vencido.",
                new[]
                {
                    nameof(FechaVencimiento)
                });
        }
    }
}
