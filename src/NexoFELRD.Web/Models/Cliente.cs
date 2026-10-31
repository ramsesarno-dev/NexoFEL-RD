using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre o razón social es obligatorio.")]
    [StringLength(
        150,
        ErrorMessage = "El nombre o razón social no puede exceder 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public TipoIdentificacionFiscal? TipoIdentificacion { get; set; }

    [StringLength(
        20,
        ErrorMessage = "La identificación no puede exceder 20 caracteres.")]
    public string? IdentificacionFiscal { get; set; }

    [Phone(ErrorMessage = "El teléfono no tiene un formato válido.")]
    [StringLength(25)]
    public string? Telefono { get; set; }

    [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
    [StringLength(150)]
    public string? Correo { get; set; }

    [StringLength(250)]
    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Factura> Facturas { get; set; }
        = new List<Factura>();
}
