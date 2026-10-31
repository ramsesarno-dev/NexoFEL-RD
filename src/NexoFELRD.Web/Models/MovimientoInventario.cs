using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models;

public class MovimientoInventario
{
    public int Id { get; set; }

    [Required]
    public int ProductoId { get; set; }

    public Producto Producto { get; set; } = null!;

    public int? FacturaId { get; set; }

    public Factura? Factura { get; set; }

    [Display(Name = "Tipo de movimiento")]
    public TipoMovimiento Tipo { get; set; }

    [Display(Name = "Motivo")]
    public MotivoMovimientoInventario Motivo { get; set; }

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99",
        ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; set; }

    [Display(Name = "Existencia anterior")]
    public decimal ExistenciaAnterior { get; set; }

    [Display(Name = "Existencia resultante")]
    public decimal ExistenciaResultante { get; set; }

    // Se almacena en UTC y se convierte al mostrarla.
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [StringLength(
        500,
        ErrorMessage = "La observación no puede superar los 500 caracteres.")]
    public string? Observacion { get; set; }

    [Required]
    [StringLength(450)]
    public string UsuarioId { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [Display(Name = "Registrado por")]
    public string UsuarioNombre { get; set; } = string.Empty;
}

public enum TipoMovimiento
{
    [Display(Name = "Entrada")]
    Entrada = 1,

    [Display(Name = "Salida")]
    Salida = 2,

    [Display(Name = "Ajuste positivo")]
    AjustePositivo = 3,

    [Display(Name = "Ajuste negativo")]
    AjusteNegativo = 4
}

public enum MotivoMovimientoInventario
{
    [Display(Name = "Inventario inicial")]
    InventarioInicial = 1,

    [Display(Name = "Compra")]
    Compra = 2,

    [Display(Name = "Devolución del cliente")]
    DevolucionCliente = 3,

    [Display(Name = "Venta")]
    Venta = 4,

    [Display(Name = "Anulación de factura")]
    AnulacionFactura = 5,

    [Display(Name = "Merma")]
    Merma = 6,

    [Display(Name = "Producto deteriorado")]
    Deterioro = 7,

    [Display(Name = "Devolución al proveedor")]
    DevolucionProveedor = 8,

    [Display(Name = "Conteo físico")]
    ConteoFisico = 9,

    [Display(Name = "Corrección")]
    Correccion = 10,

    [Display(Name = "Otro")]
    Otro = 99
}
