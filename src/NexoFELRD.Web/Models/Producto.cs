using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexoFELRD.Web.Models;

public class Producto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(
        30,
        ErrorMessage = "El código no puede exceder 30 caracteres.")]
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
        typeof(decimal),
        "0",
        "100",
        ErrorMessage = "La tasa de ITBIS no es válida.")]
    [Display(Name = "Tasa de ITBIS")]
    public decimal TasaItbis { get; set; } = 18m;

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
            "La clasificación como bien o servicio no es válida.")]
    [Display(Name = "Tipo de ítem")]
    public TipoItemDgii IndicadorBienOServicio { get; set; }
        = TipoItemDgii.Bien;

    [Range(
        typeof(byte),
        "1",
        "62",
        ErrorMessage =
            "La unidad de medida debe corresponder al catálogo DGII.")]
    [Display(Name = "Unidad de medida")]
    public byte? UnidadMedidaCodigo { get; set; } = 43;

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99",
        ErrorMessage = "La existencia no puede ser negativa.")]
    [Display(Name = "Existencia actual")]
    public decimal Existencia { get; set; }

    public bool Activo { get; set; } = true;

    [NotMapped]
    public bool ControlaInventario =>
        IndicadorBienOServicio == TipoItemDgii.Bien;

    [NotMapped]
    public string ClasificacionItbis =>
        CatalogosDgii.IndicadoresFacturacion.TryGetValue(
            IndicadorFacturacion,
            out var descripcion)
                ? descripcion
                : "Clasificación inválida";

    [NotMapped]
    public string UnidadMedidaDescripcion =>
        UnidadMedidaCodigo.HasValue &&
        CatalogosDgii.UnidadesMedida.TryGetValue(
            UnidadMedidaCodigo.Value,
            out var descripcion)
                ? $"{UnidadMedidaCodigo.Value} - {descripcion}"
                : "No especificada";

    public ICollection<FacturaDetalle> DetallesFactura { get; set; }
        = new List<FacturaDetalle>();

    public ICollection<MovimientoInventario> Movimientos { get; set; }
        = new List<MovimientoInventario>();
}
