using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NexoFELRD.Web.Models.ViewModels;

public class InventarioIndexViewModel
{
    public string? Buscar { get; set; }

    public int? ProductoId { get; set; }

    public TipoMovimiento? Tipo { get; set; }

    [DataType(DataType.Date)]
    public DateTime? Desde { get; set; }

    [DataType(DataType.Date)]
    public DateTime? Hasta { get; set; }

    public List<InventarioProductoViewModel> Productos { get; set; }
        = new();

    public List<MovimientoInventarioListadoViewModel> Movimientos
    { get; set; } = new();

    public IEnumerable<SelectListItem> ProductosFiltro { get; set; }
        = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> TiposFiltro { get; set; }
        = Enumerable.Empty<SelectListItem>();
}

public class InventarioProductoViewModel
{
    public int Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string UnidadMedida { get; set; } = string.Empty;

    public decimal Existencia { get; set; }

    public bool Activo { get; set; }

    public bool ExistenciaBaja { get; set; }
}

public class MovimientoInventarioListadoViewModel
{
    public int Id { get; set; }

    public int ProductoId { get; set; }

    public string CodigoProducto { get; set; } = string.Empty;

    public string NombreProducto { get; set; } = string.Empty;

    public string UnidadMedida { get; set; } = string.Empty;

    public TipoMovimiento Tipo { get; set; }

    public MotivoMovimientoInventario Motivo { get; set; }

    public decimal Cantidad { get; set; }

    public decimal ExistenciaAnterior { get; set; }

    public decimal ExistenciaResultante { get; set; }

    public DateTime Fecha { get; set; }

    public string UsuarioNombre { get; set; } = string.Empty;

    public string? Observacion { get; set; }

    public int? FacturaId { get; set; }
}

public class MovimientoInventarioCrearViewModel
{
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Selecciona un producto.")]
    [Display(Name = "Producto")]
    public int ProductoId { get; set; }

    [Required(ErrorMessage = "Selecciona el tipo de movimiento.")]
    [Display(Name = "Tipo de movimiento")]
    public TipoMovimiento? Tipo { get; set; }

    [Required(ErrorMessage = "Selecciona el motivo.")]
    [Display(Name = "Motivo")]
    public MotivoMovimientoInventario? Motivo { get; set; }

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; set; }

    [StringLength(
        500,
        ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string? Observacion { get; set; }

    public IEnumerable<SelectListItem> Productos { get; set; }
        = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> Tipos { get; set; }
        = Enumerable.Empty<SelectListItem>();

    public IEnumerable<SelectListItem> Motivos { get; set; }
        = Enumerable.Empty<SelectListItem>();
}
