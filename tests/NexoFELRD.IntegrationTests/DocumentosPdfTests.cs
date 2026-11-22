using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoFELRD.IntegrationTests.Infraestructura;
using NexoFELRD.Web.Models;
using UglyToad.PdfPig;

namespace NexoFELRD.IntegrationTests;

public class DocumentosPdfTests : PruebaConSqlServer
{
    [Theory]
    [InlineData("Administrador")]
    [InlineData("Facturador")]
    public async Task RolAutorizado_DescargaElDocumentoConCabecerasPrivadas(string rol)
    {
        var factura = await VentaConfirmadaAsync(1);
        var cliente = rol == "Administrador" ? Administrador : Facturador;
        using var respuesta = await cliente.GetAsync($"/Facturas/DescargarPdf/{factura.Id}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", respuesta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains(factura.ENCF!, respuesta.Content.Headers.ContentDisposition?.FileNameStar
            ?? respuesta.Content.Headers.ContentDisposition?.FileName ?? "");
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        Assert.True(respuesta.Headers.CacheControl?.NoCache);
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        using var pdf = PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync());
        var texto = Texto(pdf);
        Assert.Contains(factura.ENCF!, texto);
        Assert.Contains(factura.NumeroInterno, texto);
        Assert.Contains("27.00", texto);
        Assert.Contains("3.72", texto);
        Assert.Contains("23.28", texto);
        Assert.Contains("QA-PROD-01", texto);
    }

    [Fact]
    public async Task Anonimo_NoRecibeElPdfYDebeIniciarSesion()
    {
        var factura = await VentaConfirmadaAsync(1);
        using var respuesta = await Anonimo.GetAsync($"/Facturas/DescargarPdf/{factura.Id}");
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Contains("/Account/Login", respuesta.Headers.Location!.OriginalString);
        Assert.NotEqual("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UsuarioAutenticadoSinRol_NoPuedeDescargar()
    {
        var factura = await VentaConfirmadaAsync(1);
        using (var scope = App.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var crear = await users.CreateAsync(new IdentityUser
            {
                UserName = "sinrol@nexofel.test", Email = "sinrol@nexofel.test", EmailConfirmed = true
            }, "SoloPruebas!2026");
            Assert.True(crear.Succeeded);
        }
        using var cliente = App.CrearCliente();
        using var login = await EnviarFormularioAsync(cliente, "/Identity/Account/Login", "/Identity/Account/Login",
            new Dictionary<string, string> { ["Input.Email"] = "sinrol@nexofel.test", ["Input.Password"] = "SoloPruebas!2026" });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var respuesta = await cliente.GetAsync($"/Facturas/DescargarPdf/{factura.Id}");
        AccesoDenegado(respuesta);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task DocumentoInexistente_Devuelve404SinCrearNada(int id)
    {
        var antes = await EstadoNegocioAsync();
        using var respuesta = await Administrador.GetAsync($"/Facturas/DescargarPdf/{id}");
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(antes, await EstadoNegocioAsync());
    }

    [Fact]
    public async Task DescargasSimultaneas_NoModificanFacturaInventarioMovimientosNiSecuencias()
    {
        var factura = await VentaConfirmadaAsync();
        var antes = await EstadoNegocioAsync();
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => Administrador.GetAsync($"/Facturas/DescargarPdf/{factura.Id}")));
        try
        {
            foreach (var respuesta in respuestas)
            {
                Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                var bytes = await respuesta.Content.ReadAsByteArrayAsync();
                Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
            }
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
        Assert.Equal(antes, await EstadoNegocioAsync());
    }

    [Fact]
    public async Task CreditoParcial_DescargaSoloLaUnidadAcreditadaYNoAnulaLaOriginal()
    {
        var original = await VentaConfirmadaAsync();
        using var credito = await AcreditarAsync(original, 1, true);
        Assert.Equal(HttpStatusCode.Redirect, credito.StatusCode);
        var nota = await UltimaNotaAsync();
        var antes = await EstadoNegocioAsync();
        using var respuesta = await Administrador.GetAsync($"/Facturas/DescargarPdf/{nota.Id}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var pdf = PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync());
        var texto = Texto(pdf);
        foreach (var esperado in new[] { "Nota de crédito", original.ENCF!, nota.ENCF!, "23.28", "3.72", "27.00", "1.00" })
            Assert.Contains(esperado, texto);
        Assert.Equal(EstadoFactura.Confirmada,
            await LeerDbAsync(db => db.Facturas.Where(f => f.Id == original.Id).Select(f => f.Estado).SingleAsync()));
        Assert.Equal(antes, await EstadoNegocioAsync());
    }

    [Fact]
    public async Task NotaDeDebito_ConservaReferenciaCargoEItbis()
    {
        var original = await VentaConfirmadaAsync();
        using var cargo = await EnviarFormularioAsync(Administrador, $"/Facturas/CrearNotaDebito/{original.Id}",
            "/Facturas/CrearNotaDebito", new Dictionary<string, string>
            {
                ["FacturaAfectadaId"] = original.Id.ToString(), ["FechaEmision"] = Hoy.ToString("yyyy-MM-dd"),
                ["RazonModificacion"] = "Cargo de prueba para PDF de débito.",
                ["Detalles[0].FacturaDetalleAfectadoId"] = original.Detalles.Single().Id.ToString(),
                ["Detalles[0].Concepto"] = "Cargo adicional simulado", ["Detalles[0].MontoBase"] = "100"
            });
        Assert.Equal(HttpStatusCode.Redirect, cargo.StatusCode);
        var nota = await LeerDbAsync(db => db.Facturas.AsNoTracking()
            .SingleAsync(x => x.TipoComprobante == TipoComprobante.NotaDebito));
        var antes = await EstadoNegocioAsync();
        using var respuesta = await Administrador.GetAsync($"/Facturas/DescargarPdf/{nota.Id}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var pdf = PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync());
        foreach (var esperado in new[] { "Nota de débito", original.ENCF!, "Cargo adicional simulado", "100.00", "16.00", "116.00" })
            Assert.Contains(esperado, Texto(pdf));
        Assert.Equal(antes, await EstadoNegocioAsync());
    }

    [Fact]
    public async Task FacturaAnulada_SePuedeConsultarSinCambiarSuEstado()
    {
        var original = await VentaConfirmadaAsync(1);
        using var credito = await AcreditarAsync(original, 1, true, CodigoModificacionDgii.AnulacionTotal);
        Assert.Equal(HttpStatusCode.Redirect, credito.StatusCode);
        var antes = await EstadoNegocioAsync();
        using var respuesta = await Administrador.GetAsync($"/Facturas/DescargarPdf/{original.Id}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var pdf = PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Contains("Anulada", Texto(pdf));
        Assert.Contains(original.ENCF!, Texto(pdf));
        Assert.Contains("27.00", Texto(pdf));
        Assert.Equal(antes, await EstadoNegocioAsync());
    }

    [Fact]
    public async Task CambiarCatalogos_NoAlteraLosDatosHistoricosDelPdf()
    {
        var factura = await VentaConfirmadaAsync(1);
        await ConDbAsync(async db =>
        {
            var cliente = new Cliente { Nombre = "COMPRADOR ACTUAL DISTINTO", IdentificacionFiscal = "000000000" };
            db.Clientes.Add(cliente);
            var venta = await db.Facturas.FindAsync(factura.Id);
            venta!.Cliente = cliente;
            venta.RazonSocialComprador = "COMPRADOR HISTORICO ORIGINAL";
            var producto = await db.Productos.FindAsync(ProductoId);
            producto!.Nombre = "PRODUCTO ACTUAL DISTINTO";
            producto.Precio = 99;
            await db.SaveChangesAsync();
        });
        using var respuesta = await Administrador.GetAsync($"/Facturas/DescargarPdf/{factura.Id}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var pdf = PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync());
        var texto = Texto(pdf);
        Assert.Contains("COMPRADOR HISTORICO ORIGINAL", texto);
        Assert.Contains("Producto simulado", texto);
        Assert.DoesNotContain("ACTUAL DISTINTO", texto);
        Assert.Contains("27.00", texto);
    }

    [Fact]
    public async Task HostDeLaPeticion_NoPuedeCambiarLaDireccionDelQr()
    {
        var factura = await VentaConfirmadaAsync(1);
        var conexion = await LeerDbAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
        await using var otraApp = new AplicacionDePruebas(conexion, new Dictionary<string, string?>
        {
            ["DocumentosPdf:UrlBaseConsulta"] = "https://consulta.ejemplo.test/nexofel/"
        });
        var cliente = await CrearClienteAutenticadoAsync("Administrador", otraApp);
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/Facturas/DescargarPdf/{factura.Id}");
        peticion.Headers.Host = "atacante.ejemplo.test";
        using var respuesta = await cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var pdf = PdfDocument.Open(await respuesta.Content.ReadAsByteArrayAsync());
        Assert.Contains($"https://consulta.ejemplo.test/nexofel/Facturas/Detalle/{factura.Id}", Texto(pdf));
        Assert.DoesNotContain("atacante.ejemplo.test", Texto(pdf));
    }

    [Fact]
    public async Task ConfiguracionIncorrecta_Devuelve503SinEscrituras()
    {
        var factura = await VentaConfirmadaAsync(1);
        var antes = await EstadoNegocioAsync();
        var conexion = await LeerDbAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
        await using var otraApp = new AplicacionDePruebas(conexion, new Dictionary<string, string?>
        {
            ["DocumentosPdf:UrlBaseConsulta"] = "javascript:alert(1)"
        });
        var cliente = await CrearClienteAutenticadoAsync("Administrador", otraApp);
        using var respuesta = await cliente.GetAsync($"/Facturas/DescargarPdf/{factura.Id}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal(antes, await EstadoNegocioAsync());
    }

    [Fact]
    public async Task DetalleIncluyeElBotonParaDescargarElDocumentoCorrecto()
    {
        var factura = await VentaConfirmadaAsync(1);
        using var respuesta = await Administrador.GetAsync($"/Facturas/Detalle/{factura.Id}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains($"/Facturas/DescargarPdf/{factura.Id}", html);
        Assert.Contains("Descargar PDF", html);
    }

    private Task<string> EstadoNegocioAsync() => LeerDbAsync(async db => JsonSerializer.Serialize(new
    {
        Facturas = await db.Facturas.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Detalles = await db.FacturaDetalles.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Pagos = await db.FacturaFormasPago.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Productos = await db.Productos.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Clientes = await db.Clientes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Movimientos = await db.MovimientosInventario.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Rangos = await db.RangosComprobantesElectronicos.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
    }));

    private static string Texto(PdfDocument pdf) => string.Join("\n", pdf.GetPages().Select(p => p.Text));
}
