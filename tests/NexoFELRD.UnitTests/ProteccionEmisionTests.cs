using Microsoft.AspNetCore.DataProtection;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;
using NexoFELRD.Web.Services;

namespace NexoFELRD.UnitTests;

public class ProteccionEmisionTests
{
    private readonly ProteccionEmision _servicio = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void CadaFormularioNuevoTieneUnaClaveDistinta()
    {
        var uno = _servicio.CrearToken("usuario", ProteccionEmision.Venta);
        var dos = _servicio.CrearToken("usuario", ProteccionEmision.Venta);
        Assert.True(_servicio.ValidarToken(uno, "usuario", ProteccionEmision.Venta, 0, out var primera));
        Assert.True(_servicio.ValidarToken(dos, "usuario", ProteccionEmision.Venta, 0, out var segunda));
        Assert.NotEqual(primera, segunda);
    }

    [Theory]
    [InlineData("otro", ProteccionEmision.Credito, 10)]
    [InlineData("usuario", ProteccionEmision.Debito, 10)]
    [InlineData("usuario", ProteccionEmision.Credito, 11)]
    public void ElTokenEstaVinculadoAUsuarioFlujoYFactura(string usuario, string flujo, int factura)
    {
        var token = _servicio.CrearToken("usuario", ProteccionEmision.Credito, 10);
        Assert.False(_servicio.ValidarToken(token, usuario, flujo, factura, out _));
    }

    [Fact]
    public void VincularSinValidarUnaSolicitud_EsUnError()
    {
        Assert.Throws<InvalidOperationException>(() => _servicio.Vincular(new Factura()));
    }

    [Fact]
    public void HuellaDeVenta_IgnoraElOrdenYLaEscalaDecimalPeroDetectaCambioDeCantidad()
    {
        var modelo = new FacturaCrearViewModel
        {
            Detalles = [new() { ProductoId = 1, Cantidad = 1.00m }, new() { ProductoId = 2, Cantidad = 2m }]
        };
        var huella = ProteccionEmision.Huella(modelo);
        modelo.Detalles.Reverse();
        modelo.Detalles.Single(x => x.ProductoId == 1).Cantidad = 1m;
        Assert.Equal(huella, ProteccionEmision.Huella(modelo));
        modelo.Detalles[0].Cantidad = 3m;
        Assert.NotEqual(huella, ProteccionEmision.Huella(modelo));
    }

    [Fact]
    public void HuellaDeCredito_DetectaElCambioDeReintegro()
    {
        var modelo = new NotaCreditoCrearViewModel
        {
            Detalles = [new() { FacturaDetalleAfectadoId = 1, Cantidad = 1m }]
        };
        var huella = ProteccionEmision.Huella(modelo);
        modelo.Detalles[0].ReintegrarInventario = true;
        Assert.NotEqual(huella, ProteccionEmision.Huella(modelo));
    }

    [Fact]
    public void HuellaDeDebito_DetectaElCambioDeMonto()
    {
        var modelo = new NotaDebitoCrearViewModel
        {
            Detalles = [new() { FacturaDetalleAfectadoId = 1, Concepto = "Cargo simulado", MontoBase = 100m }]
        };
        var huella = ProteccionEmision.Huella(modelo);
        modelo.Detalles[0].MontoBase = 101m;
        Assert.NotEqual(huella, ProteccionEmision.Huella(modelo));
    }
}
