using System.ComponentModel.DataAnnotations;

namespace NexoFELRD.Web.Models;

public class Cliente
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(20)]
    public string? IdentificacionFiscal { get; set; }

    [Phone, StringLength(25)]
    public string? Telefono { get; set; }

    [EmailAddress, StringLength(150)]
    public string? Correo { get; set; }

    [StringLength(250)]
    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();
}
