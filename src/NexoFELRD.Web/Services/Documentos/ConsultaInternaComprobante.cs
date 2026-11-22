using System.Globalization;
using QRCoder;

namespace NexoFELRD.Web.Services.Documentos;

public static class ConsultaInternaComprobante
{
    public static Uri? CrearUrl(string? urlBase, int facturaId)
    {
        if (facturaId <= 0)
            throw new ArgumentOutOfRangeException(nameof(facturaId));

        if (string.IsNullOrWhiteSpace(urlBase))
            return null;

        var texto = urlBase.Trim();
        if (texto.Length > 700 || texto.Contains('\\') || texto.Any(char.IsControl) ||
            !Uri.TryCreate(texto, UriKind.Absolute, out var origen) ||
            (origen.Scheme != Uri.UriSchemeHttps && origen.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(origen.UserInfo) || !string.IsNullOrEmpty(origen.Query) ||
            !string.IsNullOrEmpty(origen.Fragment))
        {
            throw new ConfiguracionDocumentoPdfException(
                "DocumentosPdf:UrlBaseConsulta debe ser una URL absoluta HTTP/HTTPS " +
                "sin credenciales, consulta ni fragmento, o quedar vacía para desactivar el QR.");
        }

        // Conserva un posible directorio virtual, por ejemplo /nexofel/.
        return new Uri(new Uri(origen.AbsoluteUri.TrimEnd('/') + "/"),
            "Facturas/Detalle/" + facturaId.ToString(CultureInfo.InvariantCulture));
    }

    public static byte[] CrearImagen(Uri url)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(url.AbsoluteUri, QRCodeGenerator.ECCLevel.Q);
        using var codigo = new PngByteQRCode(datos);
        return codigo.GetGraphic(8);
    }
}
