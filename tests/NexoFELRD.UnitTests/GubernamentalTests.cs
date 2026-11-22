using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;
using NexoFELRD.Web.Services;
using NexoFELRD.Web.Services.Documentos;

namespace NexoFELRD.UnitTests;

public class GubernamentalTests
{
    [Theory]
    [InlineData(TipoComprobante.CreditoFiscal, true, true)]
    [InlineData(TipoComprobante.Consumo, true, false)]
    [InlineData(TipoComprobante.Gubernamental, true, true)]
    [InlineData(TipoComprobante.NotaDebito, false, false)]
    [InlineData(TipoComprobante.NotaCredito, false, false)]
    [InlineData(TipoComprobante.Compras, false, false)]
    [InlineData(TipoComprobante.GastosMenores, false, false)]
    [InlineData(TipoComprobante.RegimenesEspeciales, false, false)]
    [InlineData(TipoComprobante.Exportaciones, false, false)]
    [InlineData(TipoComprobante.PagosExterior, false, false)]
    [InlineData((TipoComprobante)999, false, false)]
    public void SoloSeHabilitanLosFlujosImplementados(
        TipoComprobante tipo, bool habilitado, bool identificado)
    {
        Assert.Equal(habilitado, ReglasEmisionVenta.EsTipoHabilitado(tipo));
        Assert.Equal(identificado, ReglasEmisionVenta.RequiereCompradorIdentificado(tipo));
    }

    [Theory]
    [InlineData(TipoComprobante.Gubernamental)]
    [InlineData(TipoComprobante.NotaCredito)]
    [InlineData(TipoComprobante.NotaDebito)]
    public void E45YSusNotasSeClasificanComoVenta(TipoComprobante tipo)
    {
        Assert.Equal(CategoriaReporteFiscal.Venta,
            ClasificadorComprobanteFiscal.ObtenerCategoria(tipo, TipoComprobante.Gubernamental));
        Assert.Equal(tipo == TipoComprobante.NotaCredito ? -1 : 1,
            ClasificadorComprobanteFiscal.ObtenerSigno(tipo));
    }

    [Fact]
    public void CambiarLaDeclaracionGubernamentalCambiaLaHuellaDelE45()
    {
        var modelo = Modelo(TipoComprobante.Gubernamental);
        var sinConfirmacion = ProteccionEmision.Huella(modelo);
        modelo.ConfirmarDestinatarioGubernamental = true;
        Assert.NotEqual(sinConfirmacion, ProteccionEmision.Huella(modelo));
    }

    [Theory]
    [InlineData(TipoComprobante.CreditoFiscal)]
    [InlineData(TipoComprobante.Consumo)]
    public void LaHuellaDeE31E32EsCompatibleConLaVersionAnterior(TipoComprobante tipo)
    {
        var modelo = Modelo(tipo);
        var antes = new
        {
            modelo.ClienteId, modelo.TipoComprobante, Fecha = modelo.FechaEmision.Date,
            modelo.TipoPago, Vencimiento = modelo.FechaLimitePago?.Date,
            modelo.TipoIngreso, modelo.IndicadorMontoGravado,
            Detalles = modelo.Detalles.OrderBy(x => x.ProductoId).ThenBy(x => x.Cantidad)
                .Select(x => new { x.ProductoId, Cantidad = x.Cantidad.ToString("G29", CultureInfo.InvariantCulture) }).ToArray()
        };
        var huellaAnterior = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(antes)));
        Assert.Equal(huellaAnterior, ProteccionEmision.Huella(modelo));
        modelo.ConfirmarDestinatarioGubernamental = true;
        Assert.Equal(huellaAnterior, ProteccionEmision.Huella(modelo));
    }

    [Fact]
    public void ElNombreDelPdfIdentificaElComprobanteGubernamental()
    {
        Assert.Equal("gubernamental-E450000000001.pdf", ServicioComprobantePdf.NombreArchivo(
            new Factura { TipoComprobante = TipoComprobante.Gubernamental, ENCF = "E450000000001" }));
    }

    private static FacturaCrearViewModel Modelo(TipoComprobante tipo) => new()
    {
        ClienteId = 7, TipoComprobante = tipo, FechaEmision = new DateTime(2026, 11, 22),
        TipoPago = TipoPagoDgii.Credito, FechaLimitePago = new DateTime(2026, 12, 22),
        TipoIngreso = TipoIngresoDgii.OperacionesNoFinancieras, IndicadorMontoGravado = 1,
        Detalles = [new() { ProductoId = 5, Cantidad = 2.00m }]
    };
}
