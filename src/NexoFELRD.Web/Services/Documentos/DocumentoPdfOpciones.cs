namespace NexoFELRD.Web.Services.Documentos;

/// <summary>
/// Datos de presentación del emisor. No se inventa una identificación fiscal.
/// Se pueden configurar mediante User Secrets o DocumentosPdf__NombreEmisor, etc.
/// No son una instantánea histórica: cambiar estas opciones cambia futuras descargas.
/// </summary>
public sealed class DocumentoPdfOpciones
{
    public const string Seccion = "DocumentosPdf";

    public string NombreEmisor { get; set; } = "NexoFEL RD (demostración)";
    public string? RncEmisor { get; set; }
    public string? DireccionEmisor { get; set; }
    public string? TelefonoEmisor { get; set; }
    public string? CorreoEmisor { get; set; }

    // Es deliberadamente una dirección configurada, nunca Request.Host.
    // En localhost el QR solamente es útil desde el mismo equipo.
    // Vacío: el PDF sigue disponible, pero no incluye un QR engañoso.
    public string? UrlBaseConsulta { get; set; } = "https://localhost:7184/";
}

public sealed class ConfiguracionDocumentoPdfException(string mensaje)
    : Exception(mensaje);
