using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NexoFELRD.IntegrationTests.Infraestructura;
using NexoFELRD.Web.Models;

namespace NexoFELRD.IntegrationTests;

// No se omiten estas pruebas cuando detectan duplicados. Un resultado rojo es un hallazgo.
[Trait("Categoria", "Idempotencia")]
public class IdempotenciaTests : PruebaConSqlServer
{
    [Fact]
    [Trait("Categoria", "Idempotencia")]
    public async Task ReenviarElMismoFormularioDeVenta_NoDebeDuplicarLaOperacion()
    {
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        using var primera = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        using var repetida = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, repetida.StatusCode);
        await ConDbAsync(async db =>
        {
            Assert.True(await db.Facturas.CountAsync() == 1,
                "Se registró dos veces el mismo formulario. Falta una protección de idempotencia en el servidor.");
            Assert.Equal(19m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(1, await db.MovimientosInventario.CountAsync());
            Assert.Equal(2L, (await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.Consumo)).ProximaSecuencia);
        });
    }

    [Fact]
    [Trait("Categoria", "Idempotencia")]
    public async Task ReenviarElMismoFormularioDeCredito_NoDebeAcreditarDosVeces()
    {
        var original = await VentaConfirmadaAsync();
        var datos = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{original.Id}", DatosCredito(original, 1m, true));
        using var primera = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        using var repetida = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos));
        Assert.Equal(primera.Headers.Location, repetida.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, repetida.StatusCode);
        await ConDbAsync(async db =>
        {
            Assert.True(await db.Facturas.CountAsync(x => x.TipoComprobante == TipoComprobante.NotaCredito) == 1,
                "La repetición del formulario emitió otra nota. El saldo disponible no sustituye una clave de idempotencia.");
            Assert.Equal(15m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(1, await db.MovimientosInventario.CountAsync(x => x.Tipo == TipoMovimiento.Entrada));
        });
    }

    [Fact]
    [Trait("Categoria", "Idempotencia")]
    public async Task DosEnviosSimultaneosDelMismoFormulario_NoDebenCrearDosVentas()
    {
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        var respuestas = await ConcurrenciaTests.EnviarJuntosAsync(
            () => Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos)),
            () => Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos)));
        try
        {
            Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            await ConDbAsync(async db =>
            {
                Assert.Equal(1, await db.Facturas.CountAsync());
                Assert.Equal(19m, (await db.Productos.SingleAsync()).Existencia);
                Assert.Equal(1, await db.MovimientosInventario.CountAsync());
            });
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("token-inventado")]
    public async Task SinTokenEmitidoPorElServidor_NoSeGuardaNada(string token)
    {
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        datos["TokenOperacion"] = token;
        using var respuesta = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await ComprobarVentasAsync(0);
    }

    [Fact]
    public async Task TokenDeOtroUsuario_NoSePuedeReutilizarAunqueAmbosPuedanFacturar()
    {
        var ajeno = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        var propio = await PrepararFormularioAsync(Facturador, "/Facturas/Crear", DatosVenta(1m));
        propio["TokenOperacion"] = ajeno["TokenOperacion"];
        using var respuesta = await Facturador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(propio));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await ComprobarVentasAsync(0);
    }

    [Fact]
    public async Task TokenDeVenta_NoSirveParaEmitirUnaNota()
    {
        var original = await VentaConfirmadaAsync();
        var venta = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        var nota = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{original.Id}", DatosCredito(original, 1m, true));
        nota["TokenOperacion"] = venta["TokenOperacion"];
        using var respuesta = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(nota));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, await LeerDbAsync(db => db.Facturas.CountAsync(x => x.FacturaAfectadaId != null)));
    }

    [Fact]
    public async Task TokenDeNota_QuedaVinculadoALaFacturaOriginal()
    {
        var uno = await VentaConfirmadaAsync();
        var dos = await VentaConfirmadaAsync();
        var formularioUno = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{uno.Id}", DatosCredito(uno, 1m, true));
        var formularioDos = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{dos.Id}", DatosCredito(dos, 1m, true));
        formularioDos["TokenOperacion"] = formularioUno["TokenOperacion"];
        using var respuesta = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(formularioDos));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(2, await LeerDbAsync(db => db.Facturas.CountAsync()));
    }

    [Fact]
    public async Task FormularioYaProcesadoConCantidadDistinta_DevuelveConflicto()
    {
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        using var primera = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        datos["Detalles[0].Cantidad"] = "2";
        using var distinta = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Conflict, distinta.StatusCode);
        await ComprobarVentasAsync(1);
    }

    [Fact]
    public async Task CreditoYaProcesadoConOtroReintegro_NoModificaInventario()
    {
        var original = await VentaConfirmadaAsync();
        var datos = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{original.Id}", DatosCredito(original, 1m, false));
        using var primera = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        datos["Detalles[0].ReintegrarInventario"] = "true";
        using var distinta = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Conflict, distinta.StatusCode);
        Assert.Equal(14m, await LeerDbAsync(db => db.Productos.Select(x => x.Existencia).SingleAsync()));
        Assert.Equal(0, await LeerDbAsync(db => db.MovimientosInventario.CountAsync(x => x.Tipo == TipoMovimiento.Entrada)));
    }

    [Fact]
    public async Task DosFormulariosNuevosConLosMismosDatos_SonDosVentasLegitimas()
    {
        using var primera = await VenderAsync(1m);
        using var segunda = await VenderAsync(1m);
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, segunda.StatusCode);
        await ComprobarVentasAsync(2);
        Assert.Equal(2, await LeerDbAsync(db => db.Facturas.Select(x => x.ClaveOperacion).Distinct().CountAsync()));
    }

    [Fact]
    public async Task DosCreditosNuevosDeUnaUnidad_SePermitenMientrasHayaSaldo()
    {
        var original = await VentaConfirmadaAsync();
        using var primera = await AcreditarAsync(original, 1m, true);
        using var segunda = await AcreditarAsync(original, 1m, true);
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, segunda.StatusCode);
        Assert.Equal(2, await LeerDbAsync(db => db.Facturas.CountAsync(x => x.TipoComprobante == TipoComprobante.NotaCredito)));
        Assert.Equal(16m, await LeerDbAsync(db => db.Productos.Select(x => x.Existencia).SingleAsync()));
    }

    [Fact]
    public async Task ErrorDeValidacion_PermiteCorregirElMismoFormulario()
    {
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(21m));
        using var invalida = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.OK, invalida.StatusCode);
        await ComprobarVentasAsync(0);
        datos["Detalles[0].Cantidad"] = "1";
        using var corregida = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, corregida.StatusCode);
        await ComprobarVentasAsync(1);
    }

    [Fact]
    public async Task ErrorDePersistencia_RetrocedeTodoYPermiteReintentarConLaMismaClave()
    {
        // Restricción temporal, exclusivamente en la base aislada de esta prueba.
        await ConDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE [Facturas] ADD CONSTRAINT [CK_PruebaFalloPersistencia] CHECK ([ClaveOperacion] IS NULL)"));
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        using var fallida = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.OK, fallida.StatusCode);
        await ComprobarVentasAsync(0);
        await ConDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE [Facturas] DROP CONSTRAINT [CK_PruebaFalloPersistencia]"));
        using var reintento = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, reintento.StatusCode);
        await ComprobarVentasAsync(1);
    }

    [Theory]
    [InlineData(CodigoModificacionDgii.AnulacionTotal, "4", true)]
    [InlineData(CodigoModificacionDgii.CorreccionTexto, "0", false)]
    public async Task ReenvioDeAnulacionOTexto_DevuelveLaNotaExistente(
        CodigoModificacionDgii codigo, string cantidad, bool reintegrar)
    {
        var original = await VentaConfirmadaAsync(4m);
        var datos = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{original.Id}",
            DatosCredito(original, decimal.Parse(cantidad), reintegrar, codigo));
        using var primera = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos));
        using var repetida = await Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, repetida.StatusCode);
        Assert.Equal(primera.Headers.Location, repetida.Headers.Location);
        Assert.Equal(1, await LeerDbAsync(db => db.Facturas.CountAsync(x => x.TipoComprobante == TipoComprobante.NotaCredito)));
        Assert.Equal(reintegrar ? 20m : 16m, await LeerDbAsync(db => db.Productos.Select(x => x.Existencia).SingleAsync()));
    }

    [Fact]
    public async Task DosEnviosSimultaneosDelMismoCredito_DevuelvenLaMismaNota()
    {
        var original = await VentaConfirmadaAsync();
        var datos = await PrepararFormularioAsync(Administrador,
            $"/Facturas/CrearNotaCredito/{original.Id}", DatosCredito(original, 1m, true));
        var respuestas = await ConcurrenciaTests.EnviarJuntosAsync(
            () => Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos)),
            () => Administrador.PostAsync("/Facturas/CrearNotaCredito", new FormUrlEncodedContent(datos)));
        try
        {
            Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            Assert.Equal(respuestas[0].Headers.Location, respuestas[1].Headers.Location);
            Assert.Equal(1, await LeerDbAsync(db => db.Facturas.CountAsync(x => x.TipoComprobante == TipoComprobante.NotaCredito)));
            Assert.Equal(15m, await LeerDbAsync(db => db.Productos.Select(x => x.Existencia).SingleAsync()));
            Assert.Equal(1, await LeerDbAsync(db => db.MovimientosInventario.CountAsync(x => x.Tipo == TipoMovimiento.Entrada)));
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Fact]
    public async Task ReenvioDeDebito_NoDuplicaElCargoNiConsumeOtraSecuencia()
    {
        var original = await VentaConfirmadaAsync();
        var datos = await PrepararFormularioAsync(Administrador, $"/Facturas/CrearNotaDebito/{original.Id}",
            new Dictionary<string, string>
            {
                ["FacturaAfectadaId"] = original.Id.ToString(), ["FechaEmision"] = Hoy.ToString("yyyy-MM-dd"),
                ["RazonModificacion"] = "Cargo para comprobar reenvíos.",
                ["Detalles[0].FacturaDetalleAfectadoId"] = original.Detalles.Single().Id.ToString(),
                ["Detalles[0].Concepto"] = "Cargo simulado", ["Detalles[0].MontoBase"] = "100"
            });
        using var primera = await Administrador.PostAsync("/Facturas/CrearNotaDebito", new FormUrlEncodedContent(datos));
        using var repetida = await Administrador.PostAsync("/Facturas/CrearNotaDebito", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, repetida.StatusCode);
        Assert.Equal(primera.Headers.Location, repetida.Headers.Location);
        await ConDbAsync(async db =>
        {
            Assert.Equal(116m, (await db.Facturas.SingleAsync(x => x.TipoComprobante == TipoComprobante.NotaDebito)).Total);
            Assert.Equal(2L, (await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.NotaDebito)).ProximaSecuencia);
            Assert.Equal(14m, (await db.Productos.SingleAsync()).Existencia);
        });
    }

    [Fact]
    public async Task IndiceUnico_RechazaDuplicadosInclusoSiSeOmiteElFiltro()
    {
        var original = await VentaConfirmadaAsync(1m);
        await ConDbAsync(async db =>
        {
            db.Facturas.Add(new Factura
            {
                NumeroInterno = "QA-DUPLICADO", ClaveOperacion = original.ClaveOperacion,
                HuellaSolicitud = original.HuellaSolicitud, UsuarioId = original.UsuarioId,
                TipoComprobante = TipoComprobante.Consumo, FechaEmision = Hoy
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        await ComprobarVentasAsync(1);
    }

    [Fact]
    public async Task DosHostsIndependientes_CompartenLaProteccionEnSqlServer()
    {
        var conexion = await LeerDbAsync(db => Task.FromResult(db.Database.GetConnectionString()!));
        await using var otraApp = new AplicacionDePruebas(conexion);
        var otroCliente = await CrearClienteAutenticadoAsync("Administrador", otraApp);
        var uno = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", DatosVenta(1m));
        var dos = await PrepararFormularioAsync(otroCliente, "/Facturas/Crear", DatosVenta(1m));
        // Cada sesión conserva su propio antiforgery; la operación es la misma.
        dos["TokenOperacion"] = uno["TokenOperacion"];
        var respuestas = await ConcurrenciaTests.EnviarJuntosAsync(
            () => Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(uno)),
            () => otroCliente.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(dos)));
        try
        {
            Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
            await ComprobarVentasAsync(1);
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Fact]
    public async Task Migracion_ConservaFacturasAnterioresYPermiteNuevasEmisiones()
    {
        var original = await VentaConfirmadaAsync(1m);
        // Únicamente en esta base temporal se simula el esquema anterior.
        // Este retroceso NO es una instrucción para la base del usuario.
        await ConDbAsync(async db =>
        {
            var migrador = db.GetService<IMigrator>();
            await migrador.MigrateAsync("20261031044958_RelacionarDetallesNotasFiscales");
            await migrador.MigrateAsync();
        });
        await ConDbAsync(async db =>
        {
            var conservada = await db.Facturas.SingleAsync();
            Assert.Equal(original.Id, conservada.Id);
            Assert.Equal(original.ENCF, conservada.ENCF);
            Assert.Equal(original.Total, conservada.Total);
            Assert.Equal(original.Subtotal, conservada.Subtotal);
            Assert.Equal(original.Itbis, conservada.Itbis);
            Assert.Null(conservada.ClaveOperacion);
            Assert.Null(conservada.HuellaSolicitud);
            Assert.Equal(19m, (await db.Productos.SingleAsync()).Existencia);
        });
        using var nueva = await VenderAsync(1m);
        Assert.Equal(HttpStatusCode.Redirect, nueva.StatusCode);
        await ConDbAsync(async db =>
        {
            Assert.Equal(2, await db.Facturas.CountAsync());
            Assert.Equal(1, await db.Facturas.CountAsync(x => x.ClaveOperacion != null));
            Assert.Equal(18m, (await db.Productos.SingleAsync()).Existencia);
            Assert.Equal(3L, (await db.RangosComprobantesElectronicos.SingleAsync(
                x => x.TipoComprobante == TipoComprobante.Consumo)).ProximaSecuencia);
        });
    }

    private Task ComprobarVentasAsync(int cantidad) => ConDbAsync(async db =>
    {
        Assert.Equal(cantidad, await db.Facturas.CountAsync());
        Assert.Equal(20m - cantidad, (await db.Productos.SingleAsync()).Existencia);
        Assert.Equal(cantidad, await db.MovimientosInventario.CountAsync());
        Assert.Equal(1L + cantidad, (await db.RangosComprobantesElectronicos.SingleAsync(
            x => x.TipoComprobante == TipoComprobante.Consumo)).ProximaSecuencia);
        Assert.Equal(cantidad, await db.Facturas.CountAsync(x =>
            x.ClaveOperacion != null && x.HuellaSolicitud != null));
    });
}
