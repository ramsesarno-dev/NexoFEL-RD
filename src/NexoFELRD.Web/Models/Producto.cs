using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexoFELRD.Web.Models;

public class Producto
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal Precio { get; set; }

    [Range(0, 100)]
    public decimal TasaItbis { get; set; } = 18;

    [Range(0, 4)]
    public byte IndicadorFacturacion { get; set; } = 1;

    [NotMapped]
    public string ClasificacionItbis =>
        IndicadorFacturacion switch
        {
            0 => "No facturable",
            1 => "ITBIS 18%",
            2 => "ITBIS 16%",
            3 => "ITBIS 0%",
            4 => "Exento",
            _ => "Sin clasificación"
        };

    [Range(0, int.MaxValue)]
    public int Existencia { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<FacturaDetalle> DetallesFactura { get; set; } = new List<FacturaDetalle>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
