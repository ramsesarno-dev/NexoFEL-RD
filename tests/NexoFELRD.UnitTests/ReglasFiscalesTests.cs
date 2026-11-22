using NexoFELRD.Web.Models;
using NexoFELRD.Web.Services;

namespace NexoFELRD.UnitTests;

public class ReglasFiscalesTests
{
    [Theory]
    [InlineData(TipoComprobante.CreditoFiscal, 1)]
    [InlineData(TipoComprobante.Consumo, 1)]
    [InlineData(TipoComprobante.NotaDebito, 1)]
    [InlineData(TipoComprobante.NotaCredito, -1)]
    public void CadaDocumentoTieneElSignoEsperado(TipoComprobante tipo, int signo)
    {
        Assert.Equal(signo, ClasificadorComprobanteFiscal.ObtenerSigno(tipo));
    }

    [Theory]
    [InlineData(TipoComprobante.CreditoFiscal, CategoriaReporteFiscal.Venta)]
    [InlineData(TipoComprobante.Consumo, CategoriaReporteFiscal.Venta)]
    [InlineData(TipoComprobante.Compras, CategoriaReporteFiscal.Compra)]
    [InlineData(TipoComprobante.GastosMenores, CategoriaReporteFiscal.Gasto)]
    [InlineData(TipoComprobante.PagosExterior, CategoriaReporteFiscal.PagoExterior)]
    public void LasNotasHeredanLaCategoriaDelOriginal(
        TipoComprobante original, CategoriaReporteFiscal categoria)
    {
        Assert.Equal(categoria, ClasificadorComprobanteFiscal.ObtenerCategoria(
            TipoComprobante.NotaCredito, original));
        Assert.Equal(categoria, ClasificadorComprobanteFiscal.ObtenerCategoria(
            TipoComprobante.NotaDebito, original));
    }

    [Fact]
    public void LaAnulacionConE34ConservaElEfectoHistoricoDeLaFactura()
    {
        var original = new Factura
        {
            Id = 1, TipoComprobante = TipoComprobante.Consumo, Estado = EstadoFactura.Anulada,
            DocumentosRelacionados = new List<Factura>
            {
                new()
                {
                    TipoComprobante = TipoComprobante.NotaCredito,
                    CodigoModificacion = CodigoModificacionDgii.AnulacionTotal,
                    Estado = EstadoFactura.Confirmada
                }
            }
        };

        Assert.Single(new[] { original }.AsQueryable().ConEfectoFiscal());
        original.DocumentosRelacionados.First().Estado = EstadoFactura.Anulada;
        Assert.Empty(new[] { original }.AsQueryable().ConEfectoFiscal());
    }

    [Theory]
    [InlineData(EstadoFactura.Borrador, false)]
    [InlineData(EstadoFactura.Confirmada, true)]
    [InlineData(EstadoFactura.Generada, true)]
    [InlineData(EstadoFactura.Anulada, false)]
    public void SoloLosEstadosValidosAportanImportes(EstadoFactura estado, bool incluido)
    {
        var consulta = new[] { new Factura { Estado = estado } }.AsQueryable();
        Assert.Equal(incluido, consulta.ConEfectoFiscal().Any());
    }

    [Theory]
    [InlineData(CodigoModificacionDgii.AnulacionTotal, true)]
    [InlineData(CodigoModificacionDgii.CorreccionMontos, true)]
    [InlineData(CodigoModificacionDgii.CorreccionTexto, false)]
    public void UnaCorreccionDeTextoNoEsUnReintegro(CodigoModificacionDgii codigo, bool incluido)
    {
        var movimiento = new MovimientoInventario
        {
            Tipo = TipoMovimiento.Entrada,
            Motivo = MotivoMovimientoInventario.DevolucionCliente,
            Cantidad = 1m,
            Factura = new Factura
            {
                TipoComprobante = TipoComprobante.NotaCredito,
                Estado = EstadoFactura.Confirmada,
                CodigoModificacion = codigo
            }
        };
        Assert.Equal(incluido, new[] { movimiento }.AsQueryable().ReintegrosDeNotasCredito().Any());
    }
}
