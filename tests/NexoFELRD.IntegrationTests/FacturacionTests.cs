using System.Net;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.IntegrationTests.Infraestructura;
using NexoFELRD.Web.Models;

namespace NexoFELRD.IntegrationTests;

public class FacturacionTests : PruebaConSqlServer
{
    [Theory]
    [InlineData(2, 16, true, "139.66", "22.34", "162.00")]
    [InlineData(1, 18, true, "137.29", "24.71", "162.00")]
    [InlineData(2, 16, false, "162.00", "25.92", "187.92")]
    [InlineData(1, 18, false, "162.00", "29.16", "191.16")]
    public async Task VentaValida_GuardaTotalesInventarioYSecuencia(
        byte indicador, int tasa, bool incluido, string baseEsperada, string impuestoEsperado, string totalEsperado)
    {
        await ConDbAsync(async db =>
        {
            var producto = await db.Productos.SingleAsync();
            producto.IndicadorFacturacion = indicador;
            producto.TasaItbis = tasa;
            await db.SaveChangesAsync();
        });

        using var respuesta = await VenderAsync(6m, precioIncluyeItbis: incluido);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        await ConDbAsync(async db =>
        {
            var factura = await db.Facturas.Include(x => x.Detalles).SingleAsync();
            Assert.Equal(decimal.Parse(baseEsperada, System.Globalization.CultureInfo.InvariantCulture), factura.Subtotal);
            Assert.Equal(decimal.Parse(impuestoEsperado, System.Globalization.CultureInfo.InvariantCulture), factura.Itbis);
            Assert.Equal(decimal.Parse(totalEsperado, System.Globalization.CultureInfo.InvariantCulture), factura.Total);
            Assert.Equal(factura.Total, factura.Subtotal + factura.Itbis);
            Assert.Equal(factura.Total, factura.Detalles.Single().Subtotal + factura.Detalles.Single().Itbis);
            Assert.Equal("E320000000001", factura.ENCF);
            Assert.Equal(EstadoFactura.Confirmada, factura.Estado);
            Assert.Equal(14m, (await db.Productos.SingleAsync()).Existencia);
            var movimiento = await db.MovimientosInventario.SingleAsync();
            Assert.Equal(TipoMovimiento.Salida, movimiento.Tipo);
            Assert.Equal(6m, movimiento.Cantidad);
            Assert.Equal(20m, movimiento.ExistenciaAnterior);
            Assert.Equal(14m, movimiento.ExistenciaResultante);
            Assert.Equal(factura.Id, movimiento.FacturaId);
            Assert.Equal(2L, (await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.Consumo)).ProximaSecuencia);
        });
    }

    [Fact]
    public async Task SinExistencias_NoGuardaNadaNiConsumeSecuencia()
    {
        using var respuesta = await VenderAsync(21m);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("existencia", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await VerificarVentaRechazadaAsync();
    }

    [Theory]
    [InlineData("agotado")]
    [InlineData("vencido")]
    [InlineData("inactivo")]
    [InlineData("sin-rango")]
    public async Task RangoNoDisponible_NoGuardaNadaNiDescuentaInventario(string estado)
    {
        await ConDbAsync(async db =>
        {
            var rango = await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.Consumo);
            switch (estado)
            {
                case "agotado": rango.ProximaSecuencia = rango.SecuenciaFinal + 1; break;
                case "vencido": rango.FechaVencimiento = Hoy.AddDays(-1); break;
                case "inactivo": rango.Activo = false; break;
                case "sin-rango": db.RangosComprobantesElectronicos.Remove(rango); break;
            }
            await db.SaveChangesAsync();
        });
        var siguiente = await LeerDbAsync(db => db.RangosComprobantesElectronicos
            .Where(x => x.TipoComprobante == TipoComprobante.Consumo)
            .Select(x => (long?)x.ProximaSecuencia).SingleOrDefaultAsync());

        using var respuesta = await VenderAsync(1m);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("No existe un rango E32 activo", await respuesta.Content.ReadAsStringAsync());
        await ConDbAsync(async db =>
        {
            Assert.Equal(0, await db.Facturas.CountAsync());
            Assert.Equal(0, await db.MovimientosInventario.CountAsync());
            Assert.Equal(20m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(siguiente, await db.RangosComprobantesElectronicos
                .Where(x => x.TipoComprobante == TipoComprobante.Consumo)
                .Select(x => (long?)x.ProximaSecuencia).SingleOrDefaultAsync());
        });
    }

    [Fact]
    public async Task UltimaSecuenciaValida_SeUsaUnaVezYElRangoSeAgota()
    {
        await ConDbAsync(async db =>
        {
            var rango = await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.Consumo);
            rango.SecuenciaFinal = 1;
            await db.SaveChangesAsync();
        });
        await VentaConfirmadaAsync(1m);
        using var segunda = await VenderAsync(1m);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        await ConDbAsync(async db =>
        {
            Assert.Equal(1, await db.Facturas.CountAsync());
            Assert.Equal(19m, (await db.Productos.SingleAsync()).Existencia);
            var rango = await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.Consumo);
            Assert.False(rango.Activo);
            Assert.Equal(2L, rango.ProximaSecuencia);
        });
    }

    [Fact]
    public async Task PostSinTokenAntifalsificacion_SeRechazaSinCambiarLaBase()
    {
        using var respuesta = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(DatosVenta(1m)));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await VerificarVentaRechazadaAsync();
    }

    private Task VerificarVentaRechazadaAsync() => ConDbAsync(async db =>
    {
        Assert.Equal(0, await db.Facturas.CountAsync());
        Assert.Equal(0, await db.FacturaDetalles.CountAsync());
        Assert.Equal(0, await db.MovimientosInventario.CountAsync());
        Assert.Equal(20m, (await db.Productos.SingleAsync()).Existencia);
        Assert.Equal(1L, (await db.RangosComprobantesElectronicos.SingleAsync(
            x => x.TipoComprobante == TipoComprobante.Consumo)).ProximaSecuencia);
    });
}
