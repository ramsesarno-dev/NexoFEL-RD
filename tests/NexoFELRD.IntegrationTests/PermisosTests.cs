using System.Net;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.IntegrationTests.Infraestructura;

namespace NexoFELRD.IntegrationTests;

public class PermisosTests : PruebaConSqlServer
{
    [Theory]
    [InlineData("/Inventario")]
    [InlineData("/Inventario/Registrar")]
    [InlineData("/Reportes")]
    [InlineData("/Secuencias")]
    [InlineData("/Secuencias/Crear")]
    [InlineData("/Productos/Crear")]
    public async Task Facturador_NoPuedeEntrarARutasAdministrativas(string ruta)
    {
        using var respuesta = await Facturador.GetAsync(ruta);
        AccesoDenegado(respuesta);
    }

    [Theory]
    [InlineData("/Facturas")]
    [InlineData("/Facturas/Crear")]
    [InlineData("/Clientes")]
    [InlineData("/Productos")]
    public async Task Facturador_PuedeConsultarSusPantallas(string ruta)
    {
        using var respuesta = await Facturador.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Facturador_PuedeRegistrarUnaVentaReal()
    {
        using var respuesta = await EnviarFormularioAsync(Facturador,
            "/Facturas/Crear", "/Facturas/Crear", DatosVenta(1m));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(1, await LeerDbAsync(db => db.Facturas.CountAsync()));
    }

    [Fact]
    public async Task Facturador_NoPuedeEnviarUnPostAdministrativoDirectamente()
    {
        var datos = await PrepararFormularioAsync(Facturador, "/Facturas/Crear", new Dictionary<string, string>());
        using var respuesta = await Facturador.PostAsync("/Inventario/Registrar", new FormUrlEncodedContent(datos));
        AccesoDenegado(respuesta);
        Assert.Equal(0, await LeerDbAsync(db => db.MovimientosInventario.CountAsync()));
    }

    [Fact]
    public async Task Anonimo_EsEnviadoAlLogin()
    {
        using var respuesta = await Anonimo.GetAsync("/Facturas");
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Contains("/Account/Login", respuesta.Headers.Location!.OriginalString);
    }
}
