using System.Globalization;
using System.Net;
using AngleSharp.Dom;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.IntegrationTests.Infraestructura;
using NexoFELRD.Web.Models;

namespace NexoFELRD.IntegrationTests;

public class NotasCreditoTests : PruebaConSqlServer
{
    [Theory]
    [InlineData(false, "14.00", "0.00", "6.00")]
    [InlineData(true, "15.00", "1.00", "5.00")]
    public async Task CreditoParcial_DistingueReduccionFinancieraDeReintegroFisico(
        bool reintegrar, string stockEsperado, string devueltas, string netas)
    {
        var original = await VentaConfirmadaAsync();
        using var respuesta = await AcreditarAsync(original, 1m, reintegrar);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var nota = await UltimaNotaAsync();
        Assert.Equal(23.28m, nota.Subtotal);
        Assert.Equal(3.72m, nota.Itbis);
        Assert.Equal(27m, nota.Total);
        Assert.Equal(original.Id, nota.FacturaAfectadaId);
        Assert.Equal(original.Detalles.Single().Id, nota.Detalles.Single().FacturaDetalleAfectadoId);
        Assert.Equal(1m, nota.Detalles.Single().Cantidad);
        await ConDbAsync(async db =>
        {
            Assert.Equal(EstadoFactura.Confirmada, (await db.Facturas.FindAsync(original.Id))!.Estado);
            Assert.Equal(decimal.Parse(stockEsperado, CultureInfo.InvariantCulture), (await db.Productos.SingleAsync()).Existencia);
            var entradas = await db.MovimientosInventario.Where(x => x.Tipo == TipoMovimiento.Entrada).ToListAsync();
            Assert.Equal(reintegrar ? 1 : 0, entradas.Count);
            if (reintegrar)
            {
                Assert.Equal(nota.Id, entradas.Single().FacturaId);
                Assert.Equal(1m, entradas.Single().Cantidad);
                Assert.Equal(MotivoMovimientoInventario.DevolucionCliente, entradas.Single().Motivo);
            }
            Assert.Equal(2L, (await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.NotaCredito)).ProximaSecuencia);
        });
        var reporte = await ReporteAsync();
        Assert.Equal(135m, Dinero(reporte.QuerySelector(".report-net-card h2")));
        Assert.Equal(18.62m, Dinero(reporte.QuerySelectorAll(".report-net-details strong")[1]));
        ComprobarCantidades(reporte, "6.00", devueltas, netas);
    }

    [Fact]
    public async Task CantidadSuperiorAlSaldo_SeRechazaSinConsumirOtraSecuencia()
    {
        var original = await VentaConfirmadaAsync();
        using var primera = await AcreditarAsync(original, 1m, true);
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        using var excesiva = await AcreditarAsync(original, 6m, true);
        Assert.Equal(HttpStatusCode.OK, excesiva.StatusCode);
        Assert.Contains("supera", await excesiva.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await ConDbAsync(async db =>
        {
            Assert.Equal(1, await db.Facturas.CountAsync(x => x.TipoComprobante == TipoComprobante.NotaCredito));
            Assert.Equal(15m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(1, await db.MovimientosInventario.CountAsync(x => x.Tipo == TipoMovimiento.Entrada));
            Assert.Equal(2L, (await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.NotaCredito)).ProximaSecuencia);
        });
    }

    [Fact]
    public async Task AnulacionTotal_ConservaOriginalYCompensaImportesEInventario()
    {
        var original = await VentaConfirmadaAsync(4m);
        using var respuesta = await AcreditarAsync(original, 4m, true, CodigoModificacionDgii.AnulacionTotal);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var nota = await UltimaNotaAsync();
        Assert.Equal(108m, nota.Total);
        await ConDbAsync(async db =>
        {
            Assert.Equal(EstadoFactura.Anulada, (await db.Facturas.FindAsync(original.Id))!.Estado);
            Assert.Equal(20m, (await db.Productos.SingleAsync()).Existencia);
        });
        var reporte = await ReporteAsync();
        Assert.Equal(0m, Dinero(reporte.QuerySelector(".report-net-card h2")));
        Assert.Equal(0m, Dinero(reporte.QuerySelectorAll(".report-net-details strong")[1]));
        Assert.Contains("Revertida por E34", reporte.Body!.TextContent);
        Assert.Contains(original.NumeroInterno, reporte.Body.TextContent);
        ComprobarCantidades(reporte, "4.00", "4.00", "0.00");
    }

    [Fact]
    public async Task AnulacionEnOtraFecha_NoBorraLaVentaDelPeriodoOriginal()
    {
        var ayer = Hoy.AddDays(-1);
        var original = await VentaConfirmadaAsync(4m, ayer);
        using var respuesta = await AcreditarAsync(original, 4m, true, CodigoModificacionDgii.AnulacionTotal);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var reporteOriginal = await ReporteAsync(ayer, ayer);
        Assert.Equal(108m, Dinero(reporteOriginal.QuerySelector(".report-net-card h2")));
        var reporteNota = await ReporteAsync(Hoy, Hoy);
        Assert.Equal(-108m, Dinero(reporteNota.QuerySelector(".report-net-card h2")));
        var reporteCompleto = await ReporteAsync(ayer, Hoy);
        Assert.Equal(0m, Dinero(reporteCompleto.QuerySelector(".report-net-card h2")));
    }

    [Fact]
    public async Task CorreccionTexto_NoReduceImportesNiReintegraCantidadesDeReferencia()
    {
        var original = await VentaConfirmadaAsync();
        using var respuesta = await AcreditarAsync(original, 0m, false, CodigoModificacionDgii.CorreccionTexto);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var nota = await UltimaNotaAsync();
        Assert.Equal(0m, nota.Total);
        Assert.Equal(0m, nota.Itbis);
        await ConDbAsync(async db =>
        {
            Assert.Equal(14m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(0, await db.MovimientosInventario.CountAsync(x => x.Tipo == TipoMovimiento.Entrada));
            Assert.Equal(EstadoFactura.Confirmada, (await db.Facturas.FindAsync(original.Id))!.Estado);
        });
        var reporte = await ReporteAsync();
        Assert.Equal(162m, Dinero(reporte.QuerySelector(".report-net-card h2")));
        ComprobarCantidades(reporte, "6.00", "0.00", "6.00");
    }

    [Fact]
    public async Task CorreccionTextoConReintegro_SeRechazaSinCrearLaNota()
    {
        var original = await VentaConfirmadaAsync();
        using var respuesta = await AcreditarAsync(original, 1m, true, CodigoModificacionDgii.CorreccionTexto);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        await ConDbAsync(async db =>
        {
            Assert.Equal(0, await db.Facturas.CountAsync(x => x.TipoComprobante == TipoComprobante.NotaCredito));
            Assert.Equal(14m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(1L, (await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.NotaCredito)).ProximaSecuencia);
        });
    }

    [Fact]
    public async Task NotaDebito_AumentaImportesSinInventarUnaSalidaDeBienes()
    {
        var original = await VentaConfirmadaAsync();
        var datos = new Dictionary<string, string>
        {
            ["FacturaAfectadaId"] = original.Id.ToString(CultureInfo.InvariantCulture),
            ["FechaEmision"] = Hoy.ToString("yyyy-MM-dd"),
            ["RazonModificacion"] = "Cargo adicional para prueba automatizada.",
            ["Detalles[0].FacturaDetalleAfectadoId"] = original.Detalles.Single().Id.ToString(CultureInfo.InvariantCulture),
            ["Detalles[0].Concepto"] = "Cargo simulado",
            ["Detalles[0].MontoBase"] = "100"
        };
        using var respuesta = await EnviarFormularioAsync(Administrador,
            $"/Facturas/CrearNotaDebito/{original.Id}", "/Facturas/CrearNotaDebito", datos);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        await ConDbAsync(async db =>
        {
            var nota = await db.Facturas.SingleAsync(x => x.TipoComprobante == TipoComprobante.NotaDebito);
            Assert.Equal(116m, nota.Total);
            Assert.Equal(16m, nota.Itbis);
            Assert.Equal(14m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(1, await db.MovimientosInventario.CountAsync());
        });
        var reporte = await ReporteAsync();
        Assert.Equal(278m, Dinero(reporte.QuerySelector(".report-net-card h2")));
    }

    private static void ComprobarCantidades(IDocument reporte, string facturadas, string reintegradas, string netas)
    {
        var tabla = reporte.QuerySelectorAll("table").Single(t =>
            t.QuerySelectorAll("th").Any(th => th.TextContent.Contains("Cantidad reintegrada", StringComparison.Ordinal)));
        var celdas = tabla.QuerySelectorAll("tbody tr").Single().QuerySelectorAll("td");
        Assert.Equal(decimal.Parse(facturadas, CultureInfo.InvariantCulture), Dinero(celdas[1]));
        Assert.Equal(decimal.Parse(reintegradas, CultureInfo.InvariantCulture), Dinero(celdas[2]));
        Assert.Equal(decimal.Parse(netas, CultureInfo.InvariantCulture), Dinero(celdas[3]));
    }
}
