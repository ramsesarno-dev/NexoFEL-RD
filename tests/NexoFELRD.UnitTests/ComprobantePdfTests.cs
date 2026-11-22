using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Services.Documentos;
using UglyToad.PdfPig;

namespace NexoFELRD.UnitTests;

public class ComprobantePdfTests
{
    static ComprobantePdfTests()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
    }

    [Theory]
    [InlineData(TipoComprobante.CreditoFiscal, "Factura de crédito fiscal")]
    [InlineData(TipoComprobante.Consumo, "Factura de consumo")]
    [InlineData(TipoComprobante.NotaCredito, "Nota de crédito")]
    [InlineData(TipoComprobante.NotaDebito, "Nota de débito")]
    public void GeneraUnPdfRealConElTipoYLosDatosGuardados(TipoComprobante tipo, string titulo)
    {
        var factura = Ejemplo(tipo);
        var bytes = Servicio().Generar(factura);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        using var pdf = PdfDocument.Open(bytes);
        var texto = Texto(pdf);
        Assert.Equal(1, pdf.NumberOfPages);
        foreach (var esperado in new[] { titulo, factura.ENCF!, "Comprador histórico de prueba",
            "QA-PROD-01", "Plátanos de prueba", "139.66", "22.35", "162.01", "27.0000",
            "https://localhost:7184/Facturas/Detalle/7", "no verifica ni certifica" })
            Assert.Contains(esperado, texto);
        Assert.True(pdf.GetPages().Sum(p => p.NumberOfImages) > 0);
        GuardarEjemplo("ejemplo-" + (int)tipo + ".pdf", bytes);
    }

    [Fact]
    public void NoRecalculaElCentavoHistoricoNiUsaElCatalogoActual()
    {
        var factura = Ejemplo();
        factura.Cliente = new Cliente { Nombre = "CLIENTE ACTUAL QUE NO DEBE APARECER" };
        factura.Detalles.Single().Producto = new Producto
        {
            Nombre = "PRODUCTO ACTUAL QUE NO DEBE APARECER", Precio = 999m, TasaItbis = 18m
        };
        using var pdf = PdfDocument.Open(Servicio().Generar(factura));
        var texto = Texto(pdf);
        Assert.Contains("162.01", texto); // No sustituir por 6 * 27 = 162.00.
        Assert.Contains("22.35", texto);
        Assert.Contains("Plátanos de prueba", texto);
        Assert.DoesNotContain("QUE NO DEBE APARECER", texto);
        Assert.Equal(162.01m, factura.Total);
        Assert.Equal(22.35m, factura.Itbis);
    }

    [Fact]
    public void MuestraLaReferenciaYLaRazonDeUnaNotaParcial()
    {
        var nota = Ejemplo(TipoComprobante.NotaCredito);
        nota.Subtotal = 23.28m;
        nota.Itbis = 3.72m;
        nota.Total = 27m;
        nota.RazonModificacion = "Devolución de una unidad para comprobación.";
        var linea = nota.Detalles.Single();
        linea.Cantidad = 1;
        linea.Subtotal = 23.28m;
        linea.Itbis = 3.72m;
        var bytes = Servicio().Generar(nota);
        using var pdf = PdfDocument.Open(bytes);
        var texto = Texto(pdf);
        foreach (var esperado in new[] { "E320000000004", "FAC-PRUEBA-ORIGINAL", "Corrección de montos",
            "Devolución de una unidad", "23.28", "3.72", "27.00", "reduce el importe" })
            Assert.Contains(esperado, texto);
        GuardarEjemplo("ejemplo-credito-parcial.pdf", bytes);
    }

    [Fact]
    public void NotaDeTextoMantieneMontosCero()
    {
        var nota = Ejemplo(TipoComprobante.NotaCredito);
        nota.CodigoModificacion = CodigoModificacionDgii.CorreccionTexto;
        nota.Subtotal = nota.Itbis = nota.Total = 0;
        nota.Detalles.Clear();
        using var pdf = PdfDocument.Open(Servicio().Generar(nota));
        var texto = Texto(pdf);
        Assert.Contains("Corrección de texto", texto);
        Assert.Contains("Sin líneas registradas", texto);
        Assert.Contains("0.00", texto);
    }

    [Fact]
    public void CreditoMayorDeTreintaDiasUsaElItbisRegistradoSinRestituirlo()
    {
        var nota = Ejemplo(TipoComprobante.NotaCredito);
        nota.IndicadorNotaCreditoMayor30Dias = 1;
        nota.Subtotal = nota.Total = 27;
        nota.Itbis = 0;
        nota.Detalles.Single().Itbis = 0;
        using var pdf = PdfDocument.Open(Servicio().Generar(nota));
        Assert.Contains("más de 30 días", Texto(pdf));
        Assert.Contains("0.00", Texto(pdf));
        Assert.Equal(0m, nota.Itbis);
    }

    [Fact]
    public void AnuladaYBorradorNoSePresentanComoConfirmados()
    {
        var factura = Ejemplo();
        factura.Estado = EstadoFactura.Anulada;
        using var anulada = PdfDocument.Open(Servicio().Generar(factura));
        Assert.Contains("Anulada", Texto(anulada));
        factura.Estado = EstadoFactura.Borrador;
        factura.ENCF = null;
        using var borrador = PdfDocument.Open(Servicio().Generar(factura));
        Assert.Contains("BORRADOR - SIN VALIDEZ FISCAL", Texto(borrador));
        Assert.Contains("Sin e-NCF asignado", Texto(borrador));
    }

    [Fact]
    public void SeparaMontoNoFacturableDelMontoTotalYDelMontoPeriodo()
    {
        var factura = Ejemplo();
        factura.MontoNoFacturable = 50m;
        using var pdf = PdfDocument.Open(Servicio().Generar(factura));
        var texto = Texto(pdf);
        Assert.Contains("Monto no facturable", texto);
        Assert.Contains("50.00", texto);
        Assert.Contains("Monto período", texto);
        Assert.Contains("212.01", texto);
        Assert.Contains("162.01", texto);
    }

    [Fact]
    public void DocumentoLargoRepiteEncabezadosYNoPierdeLineasNiTotales()
    {
        var factura = Ejemplo();
        factura.Detalles.Clear();
        for (var i = 1; i <= 110; i++)
            factura.Detalles.Add(new FacturaDetalle
            {
                NumeroLinea = i, CodigoProducto = $"ITEM-{i:000}",
                NombreItem = $"Artículo con acentos, descripción y unidad histórica. FIN-{i:000}",
                Cantidad = 1, PrecioUnitario = 27, Subtotal = 23.28m, Itbis = 3.72m,
                IndicadorFacturacion = 2, UnidadMedidaCodigo = 43
            });
        factura.Subtotal = 23.28m * 110;
        factura.Itbis = 3.72m * 110;
        factura.Total = 27m * 110;
        factura.FormasPago.Single().Monto = factura.Total;
        var bytes = Servicio().Generar(factura);
        using var pdf = PdfDocument.Open(bytes);
        Assert.InRange(pdf.NumberOfPages, 4, 30);
        var texto = Texto(pdf);
        for (var i = 1; i <= 110; i++)
            Assert.Contains($"ITEM-{i:000}", texto);
        Assert.Contains("2,970.00", texto);
        foreach (var pagina in pdf.GetPages().Where(p => p.Text.Contains("ITEM-")))
        {
            Assert.Contains("Producto / servicio", pagina.Text);
            Assert.Contains("Página", pagina.Text);
        }
        foreach (var pagina in pdf.GetPages())
            for (var i = 1; i <= 110; i++)
                Assert.Equal(pagina.Text.Contains($"ITEM-{i:000}"), pagina.Text.Contains($"FIN-{i:000}"));
        GuardarEjemplo("ejemplo-multipagina.pdf", bytes);
    }

    [Fact]
    public void AdmiteLongitudesMaximasDeEmisorYCompradorSinRomperElDocumento()
    {
        var emisor = new DocumentoPdfOpciones
        {
            NombreEmisor = string.Join(" ", Enumerable.Repeat("Empresa", 18)),
            DireccionEmisor = string.Join(" ", Enumerable.Repeat("Dirección", 24)),
            CorreoEmisor = new string('a', 105) + "@ejemplo.test",
            RncEmisor = "000000000", TelefonoEmisor = new string('0', 40)
        };
        var factura = Ejemplo();
        factura.RazonSocialComprador = new string('A', 150);
        factura.DireccionComprador = new string('D', 100);
        var bytes = Servicio(emisor).Generar(factura);
        using var pdf = PdfDocument.Open(bytes);
        Assert.Contains("162.01", Texto(pdf));
        GuardarEjemplo("ejemplo-textos-largos.pdf", bytes);
    }

    [Fact]
    public void ImportesGrandesNoSeRecortanNiDesbordanElDocumento()
    {
        var factura = Ejemplo();
        factura.Subtotal = 99999999999999.99m;
        factura.Itbis = 0;
        factura.Total = factura.Subtotal;
        var linea = factura.Detalles.Single();
        linea.Cantidad = 99999999999999.99m;
        linea.PrecioUnitario = 99999999999999.9999m;
        linea.Subtotal = factura.Subtotal;
        linea.Itbis = 0;
        factura.FormasPago.Single().Monto = factura.Total;
        var bytes = Servicio().Generar(factura);
        using var pdf = PdfDocument.Open(bytes);
        Assert.Contains("99,999,999,999,999.99", Texto(pdf));
        GuardarEjemplo("ejemplo-importes-grandes.pdf", bytes);
    }

    [Fact]
    public void NoDependeDeLaCulturaDelServidor()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using var pdf = PdfDocument.Open(Servicio().Generar(Ejemplo()));
            Assert.Contains("139.66", Texto(pdf));
            Assert.DoesNotContain("139,66", Texto(pdf));
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    [Fact]
    public void PuedeDesactivarElQrSinFingirUnaConsultaValida()
    {
        using var pdf = PdfDocument.Open(Servicio(new DocumentoPdfOpciones { UrlBaseConsulta = "" }).Generar(Ejemplo()));
        Assert.Equal(0, pdf.GetPages().Sum(x => x.NumberOfImages));
        Assert.Contains("QR desactivado", Texto(pdf));
    }

    [Theory]
    [InlineData("https://ejemplo.test", "https://ejemplo.test/Facturas/Detalle/7")]
    [InlineData("https://ejemplo.test/nexofel", "https://ejemplo.test/nexofel/Facturas/Detalle/7")]
    [InlineData("https://ejemplo.test/nexofel/", "https://ejemplo.test/nexofel/Facturas/Detalle/7")]
    [InlineData("http://localhost:5041/", "http://localhost:5041/Facturas/Detalle/7")]
    public void ElQrUsaLaBaseConfiguradaYConservaElDirectorioVirtual(string origen, string esperado)
    {
        Assert.Equal(esperado, ConsultaInternaComprobante.CrearUrl(origen, 7)!.AbsoluteUri);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///datos/factura.pdf")]
    [InlineData("//ejemplo.test")]
    [InlineData("https://usuario:clave@ejemplo.test/")]
    [InlineData("https://ejemplo.test/?token=secreto")]
    [InlineData("https://ejemplo.test/#fragmento")]
    [InlineData("https://ejemplo.test/\\ruta")]
    [InlineData("https://ejemplo.test/ru\nta")]
    public void RechazaBasesInsegurasOAmbiguas(string origen)
    {
        Assert.Throws<ConfiguracionDocumentoPdfException>(() => ConsultaInternaComprobante.CrearUrl(origen, 7));
    }

    [Fact]
    public void ElQrNoContieneIdentificacionesImportesNiTokens()
    {
        var url = ConsultaInternaComprobante.CrearUrl("https://ejemplo.test/", 7)!;
        Assert.Equal("/Facturas/Detalle/7", url.AbsolutePath);
        Assert.Equal(string.Empty, url.Query);
        Assert.Equal(string.Empty, url.UserInfo);
        var png = ConsultaInternaComprobante.CrearImagen(url);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
    }

    [Fact]
    public void NombreDeDescargaNoPermiteRutasNiCabecerasInyectadas()
    {
        var factura = Ejemplo(TipoComprobante.NotaCredito);
        factura.ENCF = "../../secreto\r\nX-Header: sí";
        var nombre = ServicioComprobantePdf.NombreArchivo(factura);
        Assert.Matches("^nota-credito-[A-Za-z0-9_-]+\\.pdf$", nombre);
    }

    [Fact]
    public void DatosDeEmisorInvalidosProducenUnErrorControlable()
    {
        Assert.Throws<ConfiguracionDocumentoPdfException>(() =>
            Servicio(new DocumentoPdfOpciones { NombreEmisor = "" }).Generar(Ejemplo()));
    }

    private static ServicioComprobantePdf Servicio(DocumentoPdfOpciones? opciones = null) =>
        new(Options.Create(opciones ?? new DocumentoPdfOpciones()));

    private static string Texto(PdfDocument documento) => string.Join("\n", documento.GetPages().Select(p => p.Text));

    private static Factura Ejemplo(TipoComprobante tipo = TipoComprobante.Consumo)
    {
        var nota = tipo is TipoComprobante.NotaCredito or TipoComprobante.NotaDebito;
        return new Factura
        {
            Id = 7, NumeroInterno = tipo == TipoComprobante.NotaCredito ? "NC-PRUEBA-000001"
                : tipo == TipoComprobante.NotaDebito ? "ND-PRUEBA-000001" : "FAC-PRUEBA-00001",
            ENCF = $"E{(int)tipo}0000000004", TipoComprobante = tipo,
            FechaEmision = new DateTime(2026, 11, 20), Estado = EstadoFactura.Confirmada,
            IndicadorMontoGravado = 1, Subtotal = 139.66m, Itbis = 22.35m, Total = 162.01m,
            RazonSocialComprador = "Comprador histórico de prueba", RncComprador = "000000000",
            DireccionComprador = "Dirección ficticia de demostración", CorreoComprador = "prueba@ejemplo.test",
            FacturaAfectadaId = nota ? 4 : null,
            FacturaAfectada = nota ? new Factura { NumeroInterno = "FAC-PRUEBA-ORIGINAL" } : null,
            NCFModificado = nota ? "E320000000004" : null,
            FechaNCFModificado = nota ? new DateTime(2026, 11, 20) : null,
            CodigoModificacion = nota ? CodigoModificacionDgii.CorreccionMontos : null,
            IndicadorNotaCreditoMayor30Dias = tipo == TipoComprobante.NotaCredito ? (byte)0 : null,
            RazonModificacion = nota ? "Ajuste de importes para comprobación." : null,
            Detalles = new List<FacturaDetalle>
            {
                new() { NumeroLinea = 1, CodigoProducto = "QA-PROD-01", NombreItem = "Plátanos de prueba",
                    Cantidad = 6, PrecioUnitario = 27, Subtotal = 139.66m, Itbis = 22.35m,
                    IndicadorFacturacion = 2, TasaItbis = 16, UnidadMedidaCodigo = 43 }
            },
            FormasPago = nota ? new List<FacturaFormaPago>()
                : new List<FacturaFormaPago> { new() { FormaPago = FormaPagoDgii.Efectivo, Monto = 162.01m } }
        };
    }

    // Optativo: genera ejemplos sintéticos para inspección visual. No se ejecuta en CI.
    private static void GuardarEjemplo(string nombre, byte[] contenido)
    {
        var carpeta = Environment.GetEnvironmentVariable("NEXOFEL_PDF_EJEMPLOS_DIR");
        if (string.IsNullOrWhiteSpace(carpeta)) return;
        Directory.CreateDirectory(carpeta);
        File.WriteAllBytes(Path.Combine(carpeta, nombre), contenido);
    }
}
