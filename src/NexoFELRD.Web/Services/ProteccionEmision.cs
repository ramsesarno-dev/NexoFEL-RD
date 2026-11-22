using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;

namespace NexoFELRD.Web.Services;

// Una instancia por petición: nunca guarda claves en memoria entre solicitudes.
public sealed class ProteccionEmision(IDataProtectionProvider provider)
{
    public const string Venta = "Venta";
    public const string Credito = "NotaCredito";
    public const string Debito = "NotaDebito";

    private Guid? _clave;
    private string? _huella;

    public string CrearToken(string usuarioId, string flujo, int facturaId = 0) =>
        Protector(usuarioId, flujo, facturaId).Protect(Guid.NewGuid().ToString("N"));

    public bool ValidarToken(string? token, string usuarioId, string flujo,
        int facturaId, out Guid clave)
    {
        clave = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;
        try
        {
            return Guid.TryParseExact(Protector(usuarioId, flujo, facturaId).Unprotect(token),
                "N", out clave) && clave != Guid.Empty;
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
    }

    public void Preparar(Guid clave, string huella)
    {
        _clave = clave;
        _huella = huella;
    }

    // Se persiste junto a factura, secuencia y movimientos en la transacción existente.
    public void Vincular(Factura factura)
    {
        if (!_clave.HasValue || _huella is null)
            throw new InvalidOperationException("La emisión no tiene una solicitud validada.");
        factura.ClaveOperacion = _clave;
        factura.HuellaSolicitud = _huella;
    }

    private IDataProtector Protector(string usuarioId, string flujo, int facturaId) =>
        provider.CreateProtector("NexoFELRD.ProteccionEmision.v1", usuarioId, flujo,
            facturaId.ToString(CultureInfo.InvariantCulture));

    // Solo se consideran los datos de entrada que utiliza la operación, no importes
    // calculados en el navegador ni etiquetas informativas cargadas desde la base.
    public static string Huella(FacturaCrearViewModel modelo) => Calcular(new
    {
        modelo.ClienteId, modelo.TipoComprobante, Fecha = modelo.FechaEmision.Date,
        modelo.TipoPago, Vencimiento = modelo.FechaLimitePago?.Date,
        modelo.TipoIngreso, modelo.IndicadorMontoGravado,
        Detalles = (modelo.Detalles ?? []).OrderBy(x => x.ProductoId)
            .ThenBy(x => x.Cantidad).Select(x => new { x.ProductoId, Cantidad = Numero(x.Cantidad) }).ToArray()
    });

    public static string Huella(NotaCreditoCrearViewModel modelo) => Calcular(new
    {
        modelo.FacturaAfectadaId, Fecha = modelo.FechaEmision.Date, modelo.CodigoModificacion,
        Razon = modelo.RazonModificacion?.Trim(),
        Detalles = (modelo.Detalles ?? []).OrderBy(x => x.FacturaDetalleAfectadoId)
            .ThenBy(x => x.Cantidad).ThenBy(x => x.ReintegrarInventario).Select(x => new
            {
                x.FacturaDetalleAfectadoId, Cantidad = Numero(x.Cantidad), x.ReintegrarInventario
            }).ToArray()
    });

    public static string Huella(NotaDebitoCrearViewModel modelo) => Calcular(new
    {
        modelo.FacturaAfectadaId, Fecha = modelo.FechaEmision.Date,
        Razon = modelo.RazonModificacion?.Trim(),
        Detalles = (modelo.Detalles ?? []).OrderBy(x => x.FacturaDetalleAfectadoId)
            .ThenBy(x => x.Concepto?.Trim(), StringComparer.Ordinal).ThenBy(x => x.MontoBase)
            .Select(x => new
            {
                x.FacturaDetalleAfectadoId, Concepto = x.Concepto?.Trim(), Monto = Numero(x.MontoBase)
            }).ToArray()
    });

    private static string Numero(decimal valor) => valor.ToString("G29", CultureInfo.InvariantCulture);
    private static string Calcular<T>(T datos) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(datos)));
}
