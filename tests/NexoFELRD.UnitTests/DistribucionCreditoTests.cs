using System.Globalization;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Services;

namespace NexoFELRD.UnitTests;

public class DistribucionCreditoTests
{
    [Theory]
    [InlineData("46.55", "7.45", 2, 1, 2, "46.55", "7.45", "23.28", "3.72")]
    [InlineData("46.55", "7.45", 2, 1, 1, "23.27", "3.73", "23.27", "3.73")]
    [InlineData("139.66", "22.34", 6, 1, 6, "139.66", "22.34", "23.28", "3.72")]
    [InlineData("139.66", "22.35", 6, 6, 6, "139.66", "22.35", "139.66", "22.35")]
    [InlineData("0.01", "0.01", 3, 1, 3, "0.01", "0.01", "0", "0.01")]
    [InlineData("0.01", "0.01", 3, 1, 2, "0.01", "0", "0.01", "0")]
    [InlineData("100", "0", 3, 1, 1, "33.34", "0", "33.34", "0")]
    public void ProrrateaElTotalSinInventarCentavosNiSuperarElSaldo(
        string subtotal, string itbis, int original, int cantidad, int disponible,
        string saldoBase, string saldoImpuesto, string baseEsperada, string itbisEsperado)
    {
        var linea = new FacturaDetalle { Cantidad = original, Subtotal = M(subtotal), Itbis = M(itbis) };
        var reparto = DistribucionCredito.Repartir(linea, cantidad, disponible, M(saldoBase), M(saldoImpuesto));
        Assert.Equal(M(baseEsperada), reparto.Subtotal);
        Assert.Equal(M(itbisEsperado), reparto.Itbis);
        Assert.InRange(reparto.Subtotal, 0, M(saldoBase));
        Assert.InRange(reparto.Itbis, 0, M(saldoImpuesto));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    public void UnaCantidadNoDisponibleNoPuedeProrratearse(int cantidad, int disponible)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DistribucionCredito.Repartir(
            new FacturaDetalle { Cantidad = 2, Subtotal = 46.55m, Itbis = 7.45m },
            cantidad, disponible, 46.55m, 7.45m));
    }

    private static decimal M(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);
}
