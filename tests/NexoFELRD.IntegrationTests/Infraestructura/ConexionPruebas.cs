using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace NexoFELRD.IntegrationTests.Infraestructura;

public sealed class ConexionPruebas
{
    public const string Variable = "NEXOFEL_TEST_SQL_CONNECTION";
    public string NombreBase { get; }
    public string Cadena { get; }
    public string CadenaMaestra { get; }

    private ConexionPruebas(string origen)
    {
        var builder = new SqlConnectionStringBuilder(origen);
        var servidor = builder.DataSource;
        var equipo = Environment.MachineName;
        var esLocal = servidor.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            servidor.StartsWith("localhost,", StringComparison.OrdinalIgnoreCase) ||
            servidor.StartsWith("localhost\\", StringComparison.OrdinalIgnoreCase) ||
            servidor.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            servidor.StartsWith("127.0.0.1,", StringComparison.OrdinalIgnoreCase) ||
            servidor.Equals(".", StringComparison.Ordinal) || servidor.StartsWith(".\\", StringComparison.Ordinal) ||
            servidor.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            servidor.Equals(equipo, StringComparison.OrdinalIgnoreCase) ||
            servidor.StartsWith(equipo + "\\", StringComparison.OrdinalIgnoreCase);

        if (!esLocal || (builder.InitialCatalog.Length > 0 &&
            !builder.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            throw new InvalidOperationException(
                "Seguridad: use un SQL Server local con Database=master y sin AttachDBFilename. " +
                "Nunca configure la base de desarrollo. Las bases temporales se crean con un nombre aleatorio.");
        }

        builder.InitialCatalog = "master";
        builder.ConnectTimeout = 3;
        CadenaMaestra = builder.ConnectionString;
        NombreBase = "NexoFELRD_Tests_" + Guid.NewGuid().ToString("N");
        builder.InitialCatalog = NombreBase;
        Cadena = builder.ConnectionString;
    }

    public static ConexionPruebas Crear(string? origen)
    {
        if (string.IsNullOrWhiteSpace(origen))
            throw new InvalidOperationException(
                $"Configure {Variable} para ejecutar integración. " +
                "Consulte tests/README.md. No se usará la conexión de la aplicación como alternativa.");

        return new ConexionPruebas(origen);
    }

    public void ValidarLimpieza(string cadena)
    {
        var nombre = new SqlConnectionStringBuilder(cadena).InitialCatalog;
        if (!Regex.IsMatch(nombre, "^NexoFELRD_Tests_[0-9a-f]{32}$") ||
            !string.Equals(nombre, NombreBase, StringComparison.Ordinal) ||
            !string.Equals(cadena, Cadena, StringComparison.Ordinal))
            throw new InvalidOperationException("Seguridad: se rechazó eliminar una base no creada por este caso.");
    }

    public async Task EsperarServidorAsync()
    {
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            try
            {
                await using var connection = new SqlConnection(CadenaMaestra);
                await connection.OpenAsync();
                return;
            }
            catch (SqlException) when (DateTime.UtcNow < limite)
            {
                await Task.Delay(1000);
            }
        }
    }
}
