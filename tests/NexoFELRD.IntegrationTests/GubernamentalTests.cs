using System.Globalization;
using System.Net;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.IntegrationTests.Infraestructura;
using NexoFELRD.Web.Models;
using UglyToad.PdfPig;

namespace NexoFELRD.IntegrationTests;

public class GubernamentalTests : PruebaConSqlServer
{
    // Datos sintéticos: dígito verificador calculado, sin afirmar inscripción en DGII.
    private const string RncSimulado = "400000005";
    private int _compradorId;

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Facturador")]
    public async Task RolAutorizado_EmiteE45ConCompradorHistoricoYSecuenciaPropia(string rol)
    {
        await PrepararAsync();
        using var respuesta = await EmitirAsync(cliente: rol == "Administrador" ? Administrador : Facturador);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var factura = await FacturaAsync();
        Assert.Equal("E450000000001", factura.ENCF);
        Assert.Equal(_compradorId, factura.ClienteId);
        Assert.Equal(RncSimulado, factura.RncComprador);
        Assert.Equal("Institución pública simulada", factura.RazonSocialComprador);
        Assert.Equal(EstadoFactura.Confirmada, factura.Estado);
        Assert.NotNull(factura.ClaveOperacion);
        Assert.Equal(64, factura.HuellaSolicitud?.Length);
        Assert.Equal(19m, await LeerDbAsync(db => db.Productos.Select(p => p.Existencia).SingleAsync()));
        Assert.Equal(2L, await SiguienteE45Async());
        Assert.All(await LeerDbAsync(db => db.RangosComprobantesElectronicos
            .Where(r => r.TipoComprobante != TipoComprobante.Gubernamental).ToListAsync()),
            r => Assert.Equal(1L, r.ProximaSecuencia));
    }

    [Theory]
    [InlineData("sin-cliente")]
    [InlineData("inexistente")]
    [InlineData("inactivo")]
    [InlineData("sin-nombre")]
    [InlineData("sin-rnc")]
    [InlineData("rnc-invalido")]
    [InlineData("extranjero")]
    [InlineData("letras-con-rnc-valido")]
    public async Task CompradorInvalido_NoGuardaNadaNiConsumeSecuencias(string caso)
    {
        await PrepararAsync();
        var datos = Datos();
        if (caso == "sin-cliente") datos.Remove("ClienteId");
        else if (caso == "inexistente") datos["ClienteId"] = int.MaxValue.ToString();
        else await ConDbAsync(async db =>
        {
            var comprador = await db.Clientes.FindAsync(_compradorId);
            switch (caso)
            {
                case "inactivo": comprador!.Activo = false; break;
                case "sin-nombre": comprador!.Nombre = " "; break;
                case "sin-rnc": comprador!.IdentificacionFiscal = null; break;
                case "rnc-invalido": comprador!.IdentificacionFiscal = "400000006"; break;
                case "extranjero": comprador!.IdentificacionFiscal = "PASAPORTE-TEST"; break;
                case "letras-con-rnc-valido": comprador!.IdentificacionFiscal = "ABC" + RncSimulado; break;
            }
            await db.SaveChangesAsync();
        });
        var antes = await EstadoAsync();
        using var respuesta = await EnviarAsync(datos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
        Assert.Contains("field-validation-error", await respuesta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SinDeclaracionGubernamental_NoSeEmiteAunqueSeOmitaJavascript(bool omitirCampo)
    {
        await PrepararAsync();
        var datos = Datos();
        if (omitirCampo) datos.Remove("ConfirmarDestinatarioGubernamental");
        else datos["ConfirmarDestinatarioGubernamental"] = "false";
        var antes = await EstadoAsync();
        using var respuesta = await EnviarAsync(datos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
        var pagina = await new HtmlParser().ParseDocumentAsync(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Confirma que el comprador", pagina.Body!.TextContent);
        Assert.Equal("45", pagina.QuerySelector("#TipoComprobante option[selected]")?.GetAttribute("value"));
    }

    [Theory]
    [InlineData(1, 18, false, "200.00", "36.00", "236.00")]
    [InlineData(2, 16, false, "200.00", "32.00", "232.00")]
    [InlineData(1, 18, true, "169.49", "30.51", "200.00")]
    [InlineData(2, 16, true, "172.41", "27.59", "200.00")]
    [InlineData(3, 0, false, "200.00", "0.00", "200.00")]
    [InlineData(4, 0, false, "200.00", "0.00", "200.00")]
    public async Task E45_NoFuerzaExencionYConservaElTratamientoDelPrecio(
        byte indicador, int tasa, bool incluido, string baseEsperada, string impuestoEsperado, string totalEsperado)
    {
        await PrepararAsync();
        await ConDbAsync(async db =>
        {
            var producto = await db.Productos.FindAsync(ProductoId);
            producto!.Precio = 100;
            producto.IndicadorFacturacion = indicador;
            producto.TasaItbis = tasa;
            await db.SaveChangesAsync();
        });
        using var respuesta = await EmitirAsync(2, incluido);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var factura = await FacturaAsync();
        Assert.Equal(Importe(baseEsperada), factura.Subtotal);
        Assert.Equal(Importe(impuestoEsperado), factura.Itbis);
        Assert.Equal(Importe(totalEsperado), factura.Total);
        Assert.Equal(factura.Total, factura.Subtotal + factura.Itbis);
        Assert.Equal(indicador, factura.Detalles.Single().IndicadorFacturacion);
        Assert.Equal(tasa, factura.Detalles.Single().TasaItbis);
        var reporte = await ReporteAsync();
        Assert.Equal(factura.Total, Dinero(reporte.QuerySelector(".report-net-card h2")));
    }

    [Fact]
    public async Task Servicio_NoDescuentaExistenciaNiCreaSalidaDeBienes()
    {
        await PrepararAsync();
        await ConDbAsync(async db =>
        {
            var producto = await db.Productos.FindAsync(ProductoId);
            producto!.IndicadorBienOServicio = TipoItemDgii.Servicio;
            producto.UnidadMedidaCodigo = null;
            producto.Existencia = 0;
            await db.SaveChangesAsync();
        });
        using var respuesta = await EmitirAsync(3);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(0, await LeerDbAsync(db => db.MovimientosInventario.CountAsync()));
        Assert.Equal(0m, await LeerDbAsync(db => db.Productos.Select(p => p.Existencia).SingleAsync()));
        Assert.Equal(81m, (await FacturaAsync()).Total);
    }

    [Theory]
    [InlineData("sin-rango")]
    [InlineData("inactivo")]
    [InlineData("agotado")]
    [InlineData("vencido")]
    [InlineData("autorizacion-futura")]
    public async Task SinRangoE45Disponible_NoTomaElRangoDeOtroTipo(string caso)
    {
        await PrepararAsync(caso != "sin-rango");
        if (caso != "sin-rango") await ConDbAsync(async db =>
        {
            var rango = await db.RangosComprobantesElectronicos.SingleAsync(r => r.TipoComprobante == TipoComprobante.Gubernamental);
            switch (caso)
            {
                case "inactivo": rango.Activo = false; break;
                case "agotado": rango.ProximaSecuencia = 1001; break;
                case "vencido": rango.FechaVencimiento = Hoy.AddDays(-1); break;
                case "autorizacion-futura": rango.FechaAutorizacion = Hoy.AddDays(1); break;
            }
            await db.SaveChangesAsync();
        });
        var antes = await EstadoAsync();
        using var respuesta = await EmitirAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("No existe un rango E45 activo", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(antes, await EstadoAsync());
    }

    [Fact]
    public async Task UltimaSecuenciaE45_SeAgotaYNoPuedeReutilizarse()
    {
        await PrepararAsync();
        await ConDbAsync(async db =>
        {
            var rango = await db.RangosComprobantesElectronicos.SingleAsync(r => r.TipoComprobante == TipoComprobante.Gubernamental);
            rango.SecuenciaFinal = 1;
            await db.SaveChangesAsync();
        });
        using var primera = await EmitirAsync();
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        var antes = await EstadoAsync();
        using var segunda = await EmitirAsync();
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
        Assert.False(await LeerDbAsync(db => db.RangosComprobantesElectronicos
            .Where(r => r.TipoComprobante == TipoComprobante.Gubernamental).Select(r => r.Activo).SingleAsync()));
    }

    [Theory]
    [InlineData("sin-fecha")]
    [InlineData("fecha-anterior")]
    public async Task CreditoSinVencimientoValido_NoEmite(string caso)
    {
        await PrepararAsync();
        var datos = Datos();
        datos["TipoPago"] = "2";
        if (caso == "fecha-anterior") datos["FechaLimitePago"] = Hoy.AddDays(-1).ToString("yyyy-MM-dd");
        var antes = await EstadoAsync();
        using var respuesta = await EnviarAsync(datos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
    }

    [Fact]
    public async Task CreditoValido_GuardaVencimientoYNoUsaTotalesDelNavegador()
    {
        await PrepararAsync();
        var datos = Datos();
        datos["TipoPago"] = "2";
        datos["FechaLimitePago"] = Hoy.AddDays(30).ToString("yyyy-MM-dd");
        datos["Total"] = "0.01";
        datos["Itbis"] = "0";
        datos["RncComprador"] = "000000000";
        datos["Detalles[0].PrecioUnitario"] = "0.01";
        using var respuesta = await EnviarAsync(datos);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var factura = await FacturaAsync();
        Assert.Equal(Hoy.AddDays(30), factura.FechaLimitePago);
        Assert.Equal(27m, factura.Total);
        Assert.Equal(3.72m, factura.Itbis);
        Assert.Equal(RncSimulado, factura.RncComprador);
        Assert.Equal(27m, factura.Detalles.Single().PrecioUnitario);
    }

    [Theory]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(41)]
    [InlineData(43)]
    [InlineData(44)]
    [InlineData(46)]
    [InlineData(47)]
    [InlineData(999)]
    public async Task HabilitarE45_NoPermiteOtrosTiposPorUnPostManipulado(int tipo)
    {
        await PrepararAsync();
        var datos = Datos();
        datos["TipoComprobante"] = tipo.ToString();
        var antes = await EstadoAsync();
        using var respuesta = await EnviarAsync(datos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
    }

    [Fact]
    public async Task Formulario_OfreceSoloE31E32E45YConservaAntifalsificacion()
    {
        using var respuesta = await Administrador.GetAsync("/Facturas/Crear");
        var pagina = await new HtmlParser().ParseDocumentAsync(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "32", "31", "45" }, pagina.QuerySelectorAll("#TipoComprobante option")
            .Select(o => o.GetAttribute("value")));
        Assert.NotNull(pagina.QuerySelector("input[name='__RequestVerificationToken']"));
        Assert.NotNull(pagina.QuerySelector("input[name='TokenOperacion']"));
        Assert.NotNull(pagina.QuerySelector("input[name='ConfirmarDestinatarioGubernamental'][type='checkbox']"));
    }

    [Fact]
    public async Task RepetirFormularioE45_NoDuplicaVentaNiInventarioNiSecuencia()
    {
        await PrepararAsync();
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", Datos());
        using var primera = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        var antes = await EstadoAsync();
        using var repetida = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, repetida.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
        Assert.Equal(1, await LeerDbAsync(db => db.Facturas.CountAsync()));
    }

    [Fact]
    public async Task ReenviosE45Simultaneos_CreanSoloUnaOperacion()
    {
        await PrepararAsync();
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", Datos());
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos))));
        try { Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode)); }
        finally { foreach (var r in respuestas) r.Dispose(); }
        Assert.Equal(1, await LeerDbAsync(db => db.Facturas.CountAsync()));
        Assert.Equal(1, await LeerDbAsync(db => db.MovimientosInventario.CountAsync()));
        Assert.Equal(2L, await SiguienteE45Async());
    }

    [Fact]
    public async Task ReenvioConDeclaracionAlterada_Devuelve409SinEscrituras()
    {
        await PrepararAsync();
        var datos = await PrepararFormularioAsync(Administrador, "/Facturas/Crear", Datos());
        using var primera = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        var antes = await EstadoAsync();
        datos["ConfirmarDestinatarioGubernamental"] = "false";
        using var alterada = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.Conflict, alterada.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
    }

    [Fact]
    public async Task E45SinAntifalsificacion_NoSeEmite()
    {
        await PrepararAsync();
        var antes = await EstadoAsync();
        using var respuesta = await Administrador.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(Datos()));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(antes, await EstadoAsync());
    }

    [Fact]
    public async Task E45ConRncFormateado_GuardaIdentificacionNormalizada()
    {
        await PrepararAsync();
        await ConDbAsync(async db =>
        {
            (await db.Clientes.FindAsync(_compradorId))!.IdentificacionFiscal = "4-00-00000-5";
            await db.SaveChangesAsync();
        });
        using var respuesta = await EmitirAsync();
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(RncSimulado, (await FacturaAsync()).RncComprador);
    }

    [Fact]
    public async Task E45TieneNotasPdfYReportesSinReescribirSusDatosHistoricos()
    {
        await PrepararAsync();
        using var venta = await EmitirAsync(6);
        Assert.Equal(HttpStatusCode.Redirect, venta.StatusCode);
        var original = await FacturaAsync();
        using var detalle = await Administrador.GetAsync($"/Facturas/Detalle/{original.Id}");
        var pagina = await new HtmlParser().ParseDocumentAsync(await detalle.Content.ReadAsStringAsync());
        Assert.NotNull(pagina.QuerySelector($"a[href='/Facturas/CrearNotaDebito/{original.Id}']"));
        Assert.NotNull(pagina.QuerySelector($"a[href='/Facturas/CrearNotaCredito/{original.Id}']"));

        using var credito = await AcreditarAsync(original, 1, true);
        Assert.Equal(HttpStatusCode.Redirect, credito.StatusCode);
        var nota = await UltimaNotaAsync();
        Assert.Equal(original.ENCF, nota.NCFModificado);
        Assert.Equal(RncSimulado, nota.RncComprador);
        Assert.Equal(27m, nota.Total);
        Assert.Equal(15m, await LeerDbAsync(db => db.Productos.Select(p => p.Existencia).SingleAsync()));
        Assert.Equal(135m, Dinero((await ReporteAsync()).QuerySelector(".report-net-card h2")));

        await ConDbAsync(async db =>
        {
            (await db.Clientes.FindAsync(_compradorId))!.Nombre = "NOMBRE ACTUAL QUE NO DEBE CAMBIAR EL PDF";
            (await db.Productos.FindAsync(ProductoId))!.Precio = 999;
            await db.SaveChangesAsync();
        });
        var antes = await EstadoAsync();
        using var descarga = await Administrador.GetAsync($"/Facturas/DescargarPdf/{original.Id}");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal("application/pdf", descarga.Content.Headers.ContentType?.MediaType);
        Assert.Contains("gubernamental-E45", descarga.Content.Headers.ContentDisposition?.FileNameStar
            ?? descarga.Content.Headers.ContentDisposition?.FileName ?? "");
        var bytes = await descarga.Content.ReadAsByteArrayAsync();
        using var pdf = PdfDocument.Open(bytes);
        var texto = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        foreach (var esperado in new[] { "Comprobante gubernamental", "Institución pública simulada", RncSimulado,
            original.ENCF!, "162.00", "139.66", "22.34", "no verifica ni certifica" })
            Assert.Contains(esperado, texto);
        Assert.DoesNotContain("NOMBRE ACTUAL", texto);
        Assert.Equal(antes, await EstadoAsync());
        var destino = Environment.GetEnvironmentVariable("NEXOFEL_E45_EVIDENCIAS");
        if (!string.IsNullOrWhiteSpace(destino))
        {
            Directory.CreateDirectory(destino);
            await File.WriteAllBytesAsync(Path.Combine(destino, "E45_ejemplo_sintetico.pdf"), bytes);
        }
        using var filtro = await Administrador.GetAsync($"/Reportes?desde={Hoy:yyyy-MM-dd}&hasta={Hoy:yyyy-MM-dd}&tipoSeleccionado=45");
        var filtrado = await new HtmlParser().ParseDocumentAsync(await filtro.Content.ReadAsStringAsync());
        Assert.Equal(162m, Dinero(filtrado.QuerySelector(".report-net-card h2")));
        using var inicio = await Administrador.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, inicio.StatusCode);
        Assert.Contains(original.ENCF!, await inicio.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NotaDebitoE45_AumentaLaVentaSinDescontarInventarioOtraVez()
    {
        await PrepararAsync();
        using var venta = await EmitirAsync(2);
        Assert.Equal(HttpStatusCode.Redirect, venta.StatusCode);
        var original = await FacturaAsync();
        using var respuesta = await EnviarFormularioAsync(Administrador,
            $"/Facturas/CrearNotaDebito/{original.Id}", "/Facturas/CrearNotaDebito", new Dictionary<string, string>
            {
                ["FacturaAfectadaId"] = original.Id.ToString(), ["FechaEmision"] = Hoy.ToString("yyyy-MM-dd"),
                ["RazonModificacion"] = "Cargo adicional simulado de comprobante gubernamental.",
                ["Detalles[0].FacturaDetalleAfectadoId"] = original.Detalles.Single().Id.ToString(),
                ["Detalles[0].Concepto"] = "Cargo simulado", ["Detalles[0].MontoBase"] = "100"
            });
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        var nota = await LeerDbAsync(db => db.Facturas.SingleAsync(f => f.TipoComprobante == TipoComprobante.NotaDebito));
        Assert.Equal(original.ENCF, nota.NCFModificado);
        Assert.Equal(116m, nota.Total);
        Assert.Equal(18m, await LeerDbAsync(db => db.Productos.Select(p => p.Existencia).SingleAsync()));
        Assert.Equal(1, await LeerDbAsync(db => db.MovimientosInventario.CountAsync()));
        Assert.Equal(170m, Dinero((await ReporteAsync()).QuerySelector(".report-net-card h2")));
    }

    [Fact]
    public async Task AnulacionTotalE45_CompensaLaVentaYReintegraSoloLoVendido()
    {
        await PrepararAsync();
        using var venta = await EmitirAsync(2);
        Assert.Equal(HttpStatusCode.Redirect, venta.StatusCode);
        var original = await FacturaAsync();
        using var respuesta = await AcreditarAsync(original, 2, true, CodigoModificacionDgii.AnulacionTotal);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(EstadoFactura.Anulada,
            await LeerDbAsync(db => db.Facturas.Where(f => f.Id == original.Id).Select(f => f.Estado).SingleAsync()));
        Assert.Equal(20m, await LeerDbAsync(db => db.Productos.Select(p => p.Existencia).SingleAsync()));
        Assert.Equal(0m, Dinero((await ReporteAsync()).QuerySelector(".report-net-card h2")));
    }

    [Fact]
    public async Task DashboardIncluyeE45YSuCreditoEnLaFechaComercialDominicana()
    {
        await PrepararAsync();
        var fecha = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo")).Date;
        var datos = Datos(2);
        datos["FechaEmision"] = fecha.ToString("yyyy-MM-dd");
        using var venta = await EnviarAsync(datos);
        Assert.Equal(HttpStatusCode.Redirect, venta.StatusCode);
        using var inicio = await Administrador.GetAsync("/");
        var pagina = await new HtmlParser().ParseDocumentAsync(await inicio.Content.ReadAsStringAsync());
        Assert.Equal(54m, Dinero(pagina.QuerySelector(".dashboard-balance-card h2")));
        Assert.Equal("1", pagina.QuerySelector(".dashboard-balance-summary strong")!.TextContent.Trim());
        var original = await FacturaAsync();
        using var credito = await AcreditarAsync(original, 1, true, fecha: fecha);
        Assert.Equal(HttpStatusCode.Redirect, credito.StatusCode);
        using var actualizado = await Administrador.GetAsync("/");
        pagina = await new HtmlParser().ParseDocumentAsync(await actualizado.Content.ReadAsStringAsync());
        Assert.Equal(27m, Dinero(pagina.QuerySelector(".dashboard-balance-card h2")));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(45)]
    public async Task DosDevolucionesParciales_ConservanElTotalYElImpuestoOriginal(int tipo)
    {
        await PrepararAsync();
        var datos = Datos(2);
        datos["TipoComprobante"] = tipo.ToString();
        using var venta = await EnviarAsync(datos);
        Assert.Equal(HttpStatusCode.Redirect, venta.StatusCode);
        var original = await LeerDbAsync(db => db.Facturas.AsNoTracking().Include(f => f.Detalles).SingleAsync());
        Assert.Equal(54m, original.Total);
        using var primera = await AcreditarAsync(original, 1, true);
        Assert.Equal(HttpStatusCode.Redirect, primera.StatusCode);
        Assert.Equal(27m, (await UltimaNotaAsync()).Total);
        using var ultima = await AcreditarAsync(original, 1, true);
        Assert.Equal(HttpStatusCode.Redirect, ultima.StatusCode);
        Assert.Equal(27m, (await UltimaNotaAsync()).Total);
        var notas = await LeerDbAsync(db => db.Facturas.Where(f => f.TipoComprobante == TipoComprobante.NotaCredito).ToListAsync());
        Assert.Equal(original.Total, notas.Sum(n => n.Total));
        Assert.Equal(original.Subtotal, notas.Sum(n => n.Subtotal));
        Assert.Equal(original.Itbis, notas.Sum(n => n.Itbis));
        Assert.Equal(20m, await LeerDbAsync(db => db.Productos.Select(p => p.Existencia).SingleAsync()));
        Assert.Equal(0m, Dinero((await ReporteAsync()).QuerySelector(".report-net-card h2")));
    }

    private async Task PrepararAsync(bool crearRango = true)
    {
        await ConDbAsync(async db =>
        {
            var comprador = new Cliente
            {
                Nombre = "Institución pública simulada", IdentificacionFiscal = RncSimulado,
                Direccion = "Dirección sintética para pruebas", Correo = "comprador@ejemplo.test", Activo = true
            };
            db.Clientes.Add(comprador);
            if (crearRango)
            {
                var usuario = await db.Users.SingleAsync(u => u.Email == "administrador@nexofel.test");
                db.RangosComprobantesElectronicos.Add(new RangoComprobanteElectronico
                {
                    TipoComprobante = TipoComprobante.Gubernamental, SecuenciaInicial = 1,
                    SecuenciaFinal = 1000, ProximaSecuencia = 1, Activo = true,
                    FechaAutorizacion = Hoy.AddYears(-1), FechaVencimiento = Hoy.AddYears(1),
                    UsuarioId = usuario.Id, UsuarioNombre = usuario.Email!
                });
            }
            await db.SaveChangesAsync();
            _compradorId = comprador.Id;
        });
    }

    private Dictionary<string, string> Datos(decimal cantidad = 1, bool incluido = true)
    {
        var datos = DatosVenta(cantidad, precioIncluyeItbis: incluido);
        datos["TipoComprobante"] = "45";
        datos["ClienteId"] = _compradorId.ToString(CultureInfo.InvariantCulture);
        datos["ConfirmarDestinatarioGubernamental"] = "true";
        return datos;
    }

    private Task<HttpResponseMessage> EmitirAsync(decimal cantidad = 1, bool incluido = true, HttpClient? cliente = null) =>
        EnviarFormularioAsync(cliente ?? Administrador, "/Facturas/Crear", "/Facturas/Crear", Datos(cantidad, incluido));
    private Task<HttpResponseMessage> EnviarAsync(Dictionary<string, string> datos) =>
        EnviarFormularioAsync(Administrador, "/Facturas/Crear", "/Facturas/Crear", datos);
    private Task<Factura> FacturaAsync() => LeerDbAsync(db => db.Facturas.AsNoTracking().Include(f => f.Detalles)
        .SingleAsync(f => f.TipoComprobante == TipoComprobante.Gubernamental));
    private Task<long> SiguienteE45Async() => LeerDbAsync(db => db.RangosComprobantesElectronicos
        .Where(r => r.TipoComprobante == TipoComprobante.Gubernamental).Select(r => r.ProximaSecuencia).SingleAsync());
    private static decimal Importe(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);
    private Task<string> EstadoAsync() => LeerDbAsync(async db => JsonSerializer.Serialize(new
    {
        Facturas = await db.Facturas.CountAsync(), Detalles = await db.FacturaDetalles.CountAsync(),
        Movimientos = await db.MovimientosInventario.CountAsync(),
        Existencias = await db.Productos.OrderBy(p => p.Id).Select(p => new { p.Id, p.Existencia }).ToListAsync(),
        Rangos = await db.RangosComprobantesElectronicos.OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.ProximaSecuencia, r.Activo }).ToListAsync()
    }));
}
