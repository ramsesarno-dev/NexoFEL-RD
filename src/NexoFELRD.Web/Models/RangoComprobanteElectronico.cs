using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexoFELRD.Web.Models;

public class RangoComprobanteElectronico
{
    public int Id { get; set; }

    [Required]
    public TipoComprobante TipoComprobante { get; set; }

    [Range(
        typeof(long),
        "1",
        "9999999999",
        ErrorMessage =
            "La secuencia inicial debe estar entre 1 y 9,999,999,999.")]
    public long SecuenciaInicial { get; set; } = 1;

    [Range(
        typeof(long),
        "1",
        "9999999999",
        ErrorMessage =
            "La secuencia final debe estar entre 1 y 9,999,999,999.")]
    public long SecuenciaFinal { get; set; }

    [Range(
        typeof(long),
        "1",
        "10000000000",
        ErrorMessage = "La próxima secuencia no es válida.")]
    public long ProximaSecuencia { get; set; } = 1;

    [DataType(DataType.Date)]
    public DateTime FechaAutorizacion { get; set; }
        = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime FechaVencimiento { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
        = DateTime.UtcNow;

    [Required]
    [StringLength(450)]
    public string UsuarioId { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string UsuarioNombre { get; set; } = string.Empty;

    [Timestamp]
    public byte[] VersionFila { get; set; }
        = Array.Empty<byte>();

    [NotMapped]
    public string Prefijo =>
        $"E{(int)TipoComprobante:00}";

    [NotMapped]
    public bool Agotado =>
        ProximaSecuencia > SecuenciaFinal;

    [NotMapped]
    public bool Vencido =>
        FechaVencimiento.Date < DateTime.Today;

    [NotMapped]
    public bool Disponible =>
        Activo &&
        !Agotado &&
        !Vencido;

    [NotMapped]
    public long CantidadDisponible =>
        Agotado
            ? 0
            : SecuenciaFinal - ProximaSecuencia + 1;

    [NotMapped]
    public string? ProximoENCF =>
        Disponible
            ? $"{Prefijo}{ProximaSecuencia:D10}"
            : null;

    public ICollection<Factura> Facturas { get; set; }
    = new List<Factura>();
}
