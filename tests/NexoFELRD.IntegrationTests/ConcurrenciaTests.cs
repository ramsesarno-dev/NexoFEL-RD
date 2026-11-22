using System.Net;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.IntegrationTests.Infraestructura;
using NexoFELRD.Web.Models;

namespace NexoFELRD.IntegrationTests;

public class ConcurrenciaTests : PruebaConSqlServer
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task DosVentasPorLaUltimaUnidad_SoloUnaPuedeConfirmarse(int repeticion)
    {
        Assert.InRange(repeticion, 1, 3);
        await ConDbAsync(async db =>
        {
            (await db.Productos.SingleAsync()).Existencia = 1m;
            await db.SaveChangesAsync();
        });
        var segundo = await CrearClienteAutenticadoAsync("Administrador");
        var uno = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        var dos = await PrepararFormularioAsync(segundo, "/Facturas/Crear", DatosVenta(1m));
        var respuestas = await EnviarJuntosAsync(
            () => Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(uno)),
            () => segundo.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(dos)));
        try
        {
            Assert.Single(respuestas, x => x.StatusCode == HttpStatusCode.Redirect);
            await ConDbAsync(async db =>
            {
                Assert.Equal(1, await db.Facturas.CountAsync());
                Assert.Equal(0m, (await db.Productos.SingleAsync()).Existencia);
                Assert.Equal(1, await db.MovimientosInventario.CountAsync());
                Assert.Equal(2L, (await db.RangosComprobantesElectronicos.SingleAsync(
                    x => x.TipoComprobante == TipoComprobante.Consumo)).ProximaSecuencia);
            });
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Fact]
    public async Task DosCreditosSimultaneos_NoPuedenAcreditarMasQueElOriginal()
    {
        var original = await VentaConfirmadaAsync();
        var segundo = await CrearClienteAutenticadoAsync("Administrador");
        var uno = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{original.Id}", DatosCredito(original, 4m, true));
        var dos = await PrepararFormularioAsync(segundo,
            $"/Facturas/CrearNotaCredito/{original.Id}", DatosCredito(original, 4m, true));
        var respuestas = await EnviarJuntosAsync(
            () => Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(uno)),
            () => segundo.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(dos)));
        try
        {
            Assert.Single(respuestas, x => x.StatusCode == HttpStatusCode.Redirect);
            await ConDbAsync(async db =>
            {
                var notas = await db.Facturas.Include(x => x.Detalles)
                    .Where(x => x.TipoComprobante == TipoComprobante.NotaCredito).ToListAsync();
                Assert.Single(notas);
                Assert.Equal(4m, notas.SelectMany(x => x.Detalles).Sum(x => x.Cantidad));
                Assert.Equal(18m, (await db.Productos.SingleAsync()).Existencia);
                Assert.Equal(2L, (await db.RangosComprobantesElectronicos.SingleAsync(
                    x => x.TipoComprobante == TipoComprobante.NotaCredito)).ProximaSecuencia);
            });
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Fact]
    public async Task VentasConcurrentes_ConservanSecuenciasUnicasYMovimientosConsistentes()
    {
        var segundo = await CrearClienteAutenticadoAsync("Administrador");
        var uno = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        var dos = await PrepararFormularioAsync(segundo, "/Facturas/Crear", DatosVenta(1m));
        var respuestas = await EnviarJuntosAsync(
            () => Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(uno)),
            () => segundo.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(dos)));
        try
        {
            await ConDbAsync(async db =>
            {
                var facturas = await db.Facturas.OrderBy(x => x.ENCF).ToListAsync();
                // Un rechazo controlado por contención no debe dejar escrituras parciales.
                Assert.InRange(facturas.Count, 1, 2);
                Assert.Equal(facturas.Count, respuestas.Count(x => x.StatusCode == HttpStatusCode.Redirect));
                Assert.Equal(facturas.Count, facturas.Select(x => x.ENCF).Distinct().Count());
                Assert.Equal(facturas.Count, await db.MovimientosInventario.CountAsync());
                Assert.Equal(20m - facturas.Count, (await db.Productos.SingleAsync()).Existencia);
                Assert.Equal(1L + facturas.Count, (await db.RangosComprobantesElectronicos.SingleAsync(
                    x => x.TipoComprobante == TipoComprobante.Consumo)).ProximaSecuencia);
                for (var i = 0; i < facturas.Count; i++) Assert.Equal($"E32{i + 1:D10}", facturas[i].ENCF);
            });
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    internal static async Task<HttpResponseMessage[]> EnviarJuntosAsync(
        Func<Task<HttpResponseMessage>> uno, Func<Task<HttpResponseMessage>> dos)
    {
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> EnviarAsync(Func<Task<HttpResponseMessage>> accion)
        {
            await salida.Task;
            return await accion();
        }
        var tareaUno = EnviarAsync(uno);
        var tareaDos = EnviarAsync(dos);
        salida.SetResult();
        return await Task.WhenAll(tareaUno, tareaDos);
    }
}
