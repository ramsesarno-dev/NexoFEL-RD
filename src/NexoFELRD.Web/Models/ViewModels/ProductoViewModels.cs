using System.ComponentModel.DataAnnotations;
using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Models.ViewModels;

public abstract class ProductoFormularioViewModel
    : IValidatableObject
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(
        30,
        ErrorMessage =
            "El código no puede exceder 30 caracteres.")]
    [Display(Name = "Código")]
    public string Codigo { get; set; } = string.Empty;

    [Required(
        ErrorMessage =
            "El nombre del producto o servicio es obligatorio.")]
    [StringLength(
        80,
        ErrorMessage =
            "El nombre no puede exceder 80 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(
        1000,
        ErrorMessage =
            "La descripción no puede exceder 1,000 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.9999",
        ErrorMessage = "El precio no puede ser negativo.")]
    [Display(Name = "Precio")]
    public decimal Precio { get; set; }

    [Range(
        0,
        4,
        ErrorMessage =
            "El indicador de facturación debe estar entre 0 y 4.")]
    [Display(Name = "Indicador de facturación DGII")]
    public byte IndicadorFacturacion { get; set; } = 1;

    [EnumDataType(
        typeof(TipoItemDgii),
        ErrorMessage =
            "La clasificación del ítem no es válida.")]
    [Display(Name = "Tipo de ítem")]
    public TipoItemDgii IndicadorBienOServicio { get; set; }
        = TipoItemDgii.Bien;

    [Range(
        typeof(byte),
        "1",
        "62",
        ErrorMessage =
            "La unidad de medida no pertenece al catálogo DGII.")]
    [Display(Name = "Unidad de medida")]
    public byte? UnidadMedidaCodigo { get; set; } = 43;

    public virtual IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!CatalogosDgii.EsIndicadorFacturacionValido(
                IndicadorFacturacion))
        {
            yield return new ValidationResult(
                "El indicador de facturación no es válido.",
                new[]
                {
                    nameof(IndicadorFacturacion)
                });
        }

        if (!Enum.IsDefined(
                typeof(TipoItemDgii),
                IndicadorBienOServicio))
        {
            yield return new ValidationResult(
                "Debe seleccionar Bien o Servicio.",
                new[]
                {
                    nameof(IndicadorBienOServicio)
                });
        }

        if (!CatalogosDgii.EsUnidadMedidaValida(
                UnidadMedidaCodigo))
        {
            yield return new ValidationResult(
                "La unidad de medida seleccionada no es válida.",
                new[]
                {
                    nameof(UnidadMedidaCodigo)
                });
        }

        if (IndicadorBienOServicio == TipoItemDgii.Bien &&
            UnidadMedidaCodigo is null)
        {
            yield return new ValidationResult(
                "Los bienes controlados en inventario deben tener " +
                "una unidad de medida.",
                new[]
                {
                    nameof(UnidadMedidaCodigo)
                });
        }

        if (decimal.Round(Precio, 4) != Precio)
        {
            yield return new ValidationResult(
                "El precio admite un máximo de cuatro decimales.",
                new[]
                {
                    nameof(Precio)
                });
        }
    }
}

public class ProductoCrearViewModel
    : ProductoFormularioViewModel
{
    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99",
        ErrorMessage =
            "La existencia inicial no puede ser negativa.")]
    [Display(Name = "Existencia inicial")]
    public decimal ExistenciaInicial { get; set; }

    public override IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        foreach (var resultado in base.Validate(validationContext))
        {
            yield return resultado;
        }

        if (decimal.Round(ExistenciaInicial, 2) !=
            ExistenciaInicial)
        {
            yield return new ValidationResult(
                "La existencia inicial admite un máximo de " +
                "dos decimales.",
                new[]
                {
                    nameof(ExistenciaInicial)
                });
        }

        if (IndicadorBienOServicio == TipoItemDgii.Servicio &&
            ExistenciaInicial != 0)
        {
            yield return new ValidationResult(
                "Los servicios no controlan existencia. " +
                "La existencia inicial debe ser cero.",
                new[]
                {
                    nameof(ExistenciaInicial)
                });
        }
    }
}

public class ProductoEditarViewModel
    : ProductoFormularioViewModel
{
    public int Id { get; set; }

    [Display(Name = "Existencia actual")]
    public decimal ExistenciaActual { get; set; }
}
