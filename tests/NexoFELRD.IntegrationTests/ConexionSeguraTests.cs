using Microsoft.Data.SqlClient;
using NexoFELRD.IntegrationTests.Infraestructura;

namespace NexoFELRD.IntegrationTests;

public class ConexionSeguraTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=localhost;Database=NexoFELRD_Dev;Integrated Security=True")]
    [InlineData("Server=localhost;Database=NexoFELRD_Produccion;Integrated Security=True")]
    [InlineData("Server=servidor-externo.example;Database=master;Integrated Security=True")]
    [InlineData("Server=localhost;Database=master;AttachDBFilename=C:\\datos\\negocio.mdf;Integrated Security=True")]
    public void RechazaConexionesQuePuedanAfectarDatosAjenos(string? cadena)
    {
        Assert.Throws<InvalidOperationException>(() => ConexionPruebas.Crear(cadena));
    }

    [Fact]
    public void CadaCasoTieneUnaBaseNuevaYLaLimpiezaSoloAceptaSuPropiaConexion()
    {
        const string cadena = "Server=localhost;Database=master;Integrated Security=True";
        var una = ConexionPruebas.Crear(cadena);
        var otra = ConexionPruebas.Crear(cadena);
        Assert.NotEqual(una.NombreBase, otra.NombreBase);
        Assert.Matches("^NexoFELRD_Tests_[0-9a-f]{32}$", una.NombreBase);
        Assert.Equal(una.NombreBase, new SqlConnectionStringBuilder(una.Cadena).InitialCatalog);
        una.ValidarLimpieza(una.Cadena);
        Assert.Throws<InvalidOperationException>(() => una.ValidarLimpieza(otra.Cadena));
        Assert.Throws<InvalidOperationException>(() => una.ValidarLimpieza(cadena));
    }
}
