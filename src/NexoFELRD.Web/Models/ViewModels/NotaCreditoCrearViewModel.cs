using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models.ViewModels;

public class NotaCreditoCrearViewModel :
    IValidatableObject
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Seleccione la factura afectada.")]
    public int FacturaAfectadaId { get; set; }

    [DataType(DataType.Date)]
    public DateTime FechaEmision { get; set; }
        = DateTime.Today;

    [Required(
        ErrorMessage = "Seleccione el código de modificación.")]
    public CodigoModificacionDgii? CodigoModificacion
    { get; set; } =
        CodigoModificacionDgii.CorreccionMontos;

    [Required(
        ErrorMessage = "Indique la razón de la modificación.")]
    [StringLength(
        90,
        MinimumLength = 5,
        ErrorMessage =
            "La razón debe contener entre 5 y 90 caracteres.")]
    public string RazonModificacion { get; set; }
        = string.Empty;

    /*
     * Estos datos se cargan desde la base de datos.
     * Nunca se confiará en valores fiscales enviados
     * directamente desde el navegador.
     */

    public FacturaAfectadaNotaViewModel?
        FacturaAfectada
    { get; set; }

    public List<NotaCreditoDetalleViewModel> Detalles
    { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (FechaEmision == default)
        {
            yield return new ValidationResult(
                "La fecha de emisión es obligatoria.",
                new[] { nameof(FechaEmision) });
        }
        else if (FechaEmision.Date > DateTime.Today)
        {
            yield return new ValidationResult(
                "La fecha de emisión no puede ser futura.",
                new[] { nameof(FechaEmision) });
        }

        if (!CodigoModificacion.HasValue)
        {
            yield break;
        }

        var codigo = CodigoModificacion.Value;

        if (codigo !=
                CodigoModificacionDgii.AnulacionTotal &&
            codigo !=
                CodigoModificacionDgii.CorreccionTexto &&
            codigo !=
                CodigoModificacionDgii.CorreccionMontos)
        {
            yield return new ValidationResult(
                "El código seleccionado no aplica a una " +
                "nota de crédito electrónica.",
                new[] { nameof(CodigoModificacion) });

            yield break;
        }

        var detallesConCantidad = Detalles
            .Where(x => x.Cantidad > 0)
            .ToList();

        if (codigo ==
                CodigoModificacionDgii.CorreccionTexto &&
            detallesConCantidad.Count > 0)
        {
            yield return new ValidationResult(
                "La corrección de texto debe emitirse con " +
                "monto cero.",
                new[] { nameof(Detalles) });
        }

        if (codigo ==
                CodigoModificacionDgii.CorreccionTexto &&
            Detalles.Any(x => x.ReintegrarInventario))
        {
            yield return new ValidationResult(
                "Una corrección de texto no puede modificar " +
                "el inventario.",
                new[] { nameof(Detalles) });
        }

        if (codigo ==
                CodigoModificacionDgii.CorreccionMontos &&
            detallesConCantidad.Count == 0)
        {
            yield return new ValidationResult(
                "Seleccione al menos una línea y especifique " +
                "la cantidad que será acreditada.",
                new[] { nameof(Detalles) });
        }

        if (Detalles.Any(x =>
            x.ReintegrarInventario &&
            x.Cantidad <= 0))
        {
            yield return new ValidationResult(
                "Para reintegrar un bien al inventario debe " +
                "especificar una cantidad mayor que cero.",
                new[] { nameof(Detalles) });
        }
    }
}

public class FacturaAfectadaNotaViewModel
{
    public int Id { get; set; }

    public string NumeroInterno { get; set; }
        = string.Empty;

    public string ENCF { get; set; }
        = string.Empty;

    public DateTime FechaEmision { get; set; }

    public TipoComprobante TipoComprobante { get; set; }

    public string Comprador { get; set; }
        = string.Empty;

    public string IdentificacionFiscal { get; set; }
        = string.Empty;

    public decimal TotalOriginal { get; set; }

    public decimal MontoNoFacturableOriginal { get; set; }

    public decimal MontoAcreditado { get; set; }

    public decimal MontoPeriodoOriginal =>
        TotalOriginal + MontoNoFacturableOriginal;

    public decimal MontoDisponible =>
        Math.Max(
            0m,
            MontoPeriodoOriginal - MontoAcreditado);

    public bool MayorDeTreintaDias { get; set; }
}

public class NotaCreditoDetalleViewModel
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "La línea afectada no es válida.")]
    public int FacturaDetalleAfectadoId { get; set; }

    public int ProductoId { get; set; }

    public int NumeroLinea { get; set; }

    public string CodigoProducto { get; set; }
        = string.Empty;

    public string NombreItem { get; set; }
        = string.Empty;

    public TipoItemDgii IndicadorBienOServicio
    { get; set; }

    public byte? UnidadMedidaCodigo { get; set; }

    public string UnidadMedida { get; set; }
        = string.Empty;

    public byte IndicadorFacturacion { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal TasaItbis { get; set; }

    public decimal CantidadOriginal { get; set; }

    public decimal CantidadAcreditada { get; set; }

    public decimal CantidadDisponible { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99",
        ErrorMessage = "La cantidad no puede ser negativa.")]
    public decimal Cantidad { get; set; }

    public bool ReintegrarInventario { get; set; }

    public bool ControlaInventario =>
        IndicadorBienOServicio ==
        TipoItemDgii.Bien;
}
