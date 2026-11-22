using System.Globalization;
using Microsoft.Extensions.Options;
using NexoFELRD.Web.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NexoFELRD.Web.Services.Documentos;

/// <summary>
/// Proyección de lectura: no consulta catálogos vivos, recalcula impuestos,
/// escribe archivos públicos, modifica entidades ni realiza solicitudes HTTP.
/// </summary>
public sealed class ServicioComprobantePdf(IOptions<DocumentoPdfOpciones> opciones)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-DO");
    private const string Azul = "#14567C";
    private const string Tinta = "#173349";
    private const string Gris = "#607284";
    private const string Fondo = "#F0F5F8";
    private const string Borde = "#D9E4EC";

    public byte[] Generar(Factura factura)
    {
        ArgumentNullException.ThrowIfNull(factura);
        var emisor = opciones.Value;
        ValidarEmisor(emisor);
        var url = ConsultaInternaComprobante.CrearUrl(emisor.UrlBaseConsulta, factura.Id);
        var qr = url is null ? null : ConsultaInternaComprobante.CrearImagen(url);

        return Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(34);
            pagina.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(9).FontColor(Tinta));

            pagina.Header().Element(c => Encabezado(c, factura, emisor));
            pagina.Content().PaddingTop(16).Column(columna =>
            {
                columna.Spacing(14);
                columna.Item().Element(c => DatosOperacion(c, factura));
                columna.Item().ShowEntire().Element(c => Comprador(c, factura));

                if (factura.FacturaAfectadaId.HasValue || factura.CodigoModificacion.HasValue)
                    columna.Item().ShowEntire().Element(c => Referencia(c, factura));

                columna.Item().Element(c => Lineas(c, factura));
                columna.Item().ShowEntire().Element(c => Totales(c, factura));
                columna.Item().ShowEntire().Element(c => Consulta(c, url, qr));
            });

            pagina.Footer().PaddingTop(14).BorderTop(1).BorderColor(Borde).PaddingTop(7)
                .Row(fila =>
                {
                    fila.RelativeItem().Text("Representación interna. No acredita firma ni aceptación de DGII.")
                        .FontSize(7).FontColor(Gris);
                    fila.ConstantItem(80).AlignRight().Text(texto =>
                    {
                        texto.DefaultTextStyle(x => x.FontSize(7).FontColor(Gris));
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
        })).WithMetadata(new DocumentMetadata
        {
            Title = $"{Titulo(factura.TipoComprobante)} {factura.NumeroInterno}",
            Author = emisor.NombreEmisor,
            Subject = "Consulta interna de un comprobante registrado en NexoFEL RD"
        }).GeneratePdf();
    }

    public static string NombreArchivo(Factura factura)
    {
        var referencia = !string.IsNullOrWhiteSpace(factura.ENCF)
            ? factura.ENCF : factura.NumeroInterno;
        var seguro = new string((referencia ?? string.Empty)
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').Take(60).ToArray());
        if (seguro.Length == 0)
            seguro = factura.Id.ToString(CultureInfo.InvariantCulture);
        var prefijo = factura.TipoComprobante switch
        {
            TipoComprobante.NotaCredito => "nota-credito",
            TipoComprobante.NotaDebito => "nota-debito",
            TipoComprobante.Gubernamental => "gubernamental",
            _ => "factura"
        };
        return $"{prefijo}-{seguro}.pdf";
    }

    private static void Encabezado(IContainer contenedor, Factura f, DocumentoPdfOpciones emisor)
    {
        contenedor.BorderBottom(2).BorderColor(Azul).PaddingBottom(14).Row(fila =>
        {
            fila.RelativeItem(1.25f).PaddingRight(18).Column(columna =>
            {
                columna.Item().Text("NEXOFEL RD").FontSize(10).Bold().FontColor(Azul).LetterSpacing(0.06f);
                columna.Item().PaddingTop(5).Text(emisor.NombreEmisor.Trim()).FontSize(16).Bold();
                columna.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(emisor.RncEmisor)
                    ? "Emisor de demostración: RNC no configurado"
                    : "RNC: " + emisor.RncEmisor.Trim()).FontSize(8).FontColor(Gris);
                DatoOpcional(columna, emisor.DireccionEmisor);
                DatoOpcional(columna, emisor.TelefonoEmisor);
                DatoOpcional(columna, emisor.CorreoEmisor);
            });
            fila.RelativeItem().AlignRight().Column(columna =>
            {
                columna.Item().AlignRight().Text(Titulo(f.TipoComprobante))
                    .FontSize(f.TipoComprobante == TipoComprobante.Gubernamental ? 16 : 19)
                    .Bold().FontColor(Azul);
                columna.Item().AlignRight().PaddingTop(5)
                    .Text($"E{(int)f.TipoComprobante} | {Estado(f.Estado)}").FontSize(9).SemiBold();
                columna.Item().AlignRight().PaddingTop(8).Text(f.ENCF ?? "Sin e-NCF asignado")
                    .FontSize(14).Bold();
                columna.Item().AlignRight().PaddingTop(3).Text(f.NumeroInterno).FontSize(8).FontColor(Gris);
                if (f.Estado == EstadoFactura.Borrador)
                    columna.Item().AlignRight().PaddingTop(5).Text("BORRADOR - SIN VALIDEZ FISCAL")
                        .FontColor(Colors.Red.Darken2).Bold().FontSize(8);
            });
        });
    }

    private static void DatosOperacion(IContainer contenedor, Factura f)
    {
        contenedor.Background(Fondo).Padding(10).Row(fila =>
        {
            fila.RelativeItem().Column(c =>
            {
                Campo(c, "Fecha de emisión", Fecha(f.FechaEmision));
                if (f.FechaVencimientoSecuencia.HasValue)
                    Campo(c, "Vencimiento de secuencia", Fecha(f.FechaVencimientoSecuencia.Value));
            });
            fila.RelativeItem().Column(c =>
            {
                Campo(c, "Condición de pago", f.TipoPago switch
                {
                    TipoPagoDgii.Contado => "Contado", TipoPagoDgii.Credito => "Crédito",
                    TipoPagoDgii.Gratuito => "Gratuito", _ => "No especificada"
                });
                if (f.FechaLimitePago.HasValue)
                    Campo(c, "Fecha límite de pago", Fecha(f.FechaLimitePago.Value));
                if (!string.IsNullOrWhiteSpace(f.TerminoPago))
                    Campo(c, "Término de pago", f.TerminoPago);
            });
            fila.RelativeItem().Column(c =>
            {
                Campo(c, "Tratamiento del precio", f.IndicadorMontoGravado == 1
                    ? "ITBIS incluido" : "ITBIS no incluido");
                Campo(c, "Moneda", "Peso dominicano (DOP)");
            });
        });
    }

    private static void Comprador(IContainer contenedor, Factura f)
    {
        contenedor.Border(1).BorderColor(Borde).Padding(11).Column(c =>
        {
            Subtitulo(c, "DATOS DEL COMPRADOR");
            c.Item().PaddingTop(5).Text(!string.IsNullOrWhiteSpace(f.RazonSocialComprador)
                ? f.RazonSocialComprador : f.ClienteId.HasValue
                    ? "Comprador sin nombre histórico registrado" : "Consumidor final").Bold().FontSize(11);
            c.Item().PaddingTop(3).Text("RNC / cédula: " + Texto(f.RncComprador, "No identificado"));
            if (!string.IsNullOrWhiteSpace(f.IdentificadorExtranjero))
                c.Item().Text("Identificador extranjero: " + f.IdentificadorExtranjero);
            DatoOpcional(c, f.DireccionComprador);
            DatoOpcional(c, f.CorreoComprador);
            DatoOpcional(c, f.PaisComprador);
        });
    }

    private static void Referencia(IContainer contenedor, Factura f)
    {
        contenedor.Background(Fondo).Padding(11).Column(c =>
        {
            Subtitulo(c, "COMPROBANTE MODIFICADO");
            c.Item().PaddingTop(5).Text("e-NCF: " + Texto(f.NCFModificado, "No registrado")).Bold();
            if (f.FacturaAfectada is not null)
                c.Item().Text("Número interno: " + f.FacturaAfectada.NumeroInterno);
            if (f.FechaNCFModificado.HasValue)
                c.Item().Text("Fecha original: " + Fecha(f.FechaNCFModificado.Value));
            c.Item().PaddingTop(3).Text("Modificación: " + (f.CodigoModificacion switch
            {
                CodigoModificacionDgii.AnulacionTotal => "1 - Anulación total",
                CodigoModificacionDgii.CorreccionTexto => "2 - Corrección de texto",
                CodigoModificacionDgii.CorreccionMontos => "3 - Corrección de montos",
                CodigoModificacionDgii.ReemplazoContingencia => "4 - Reemplazo de contingencia",
                CodigoModificacionDgii.ReferenciaFacturaConsumo => "5 - Referencia a factura de consumo",
                _ => "No registrada"
            }));
            DatoOpcional(c, f.RazonModificacion);
            if (f.TipoComprobante == TipoComprobante.NotaCredito)
                c.Item().PaddingTop(4).Text(f.IndicadorNotaCreditoMayor30Dias switch
                {
                    1 => "Tratamiento del ITBIS: más de 30 días.",
                    0 => "Tratamiento del ITBIS: 30 días o menos.",
                    _ => "Tratamiento del ITBIS: no registrado."
                }).FontSize(8).FontColor(Gris);
        });
    }

    private static void Lineas(IContainer contenedor, Factura f)
    {
        contenedor.Column(c =>
        {
            Subtitulo(c, "DETALLE DE BIENES Y SERVICIOS");
            c.Item().PaddingTop(7).Table(tabla =>
            {
                tabla.ColumnsDefinition(columnas =>
                {
                    columnas.ConstantColumn(21);
                    columnas.RelativeColumn(2.7f);
                    columnas.RelativeColumn(0.8f);
                    columnas.RelativeColumn(1.25f);
                    columnas.RelativeColumn(1.15f);
                    columnas.RelativeColumn(1.1f);
                    columnas.RelativeColumn(1.25f);
                });
                tabla.Header(h =>
                {
                    foreach (var texto in new[] { "#", "Producto / servicio", "Cant.", "Precio", "Base", "ITBIS", "Total línea" })
                        h.Cell().Background(Azul).Padding(6).Text(texto).FontColor(Colors.White).FontSize(8).SemiBold();
                });

                if (f.Detalles.Count == 0)
                    tabla.Cell().ColumnSpan(7).Padding(10).Text("Sin líneas registradas.").FontColor(Gris);

                foreach (var d in f.Detalles.OrderBy(x => x.NumeroLinea).ThenBy(x => x.Id))
                {
                    var color = d.NumeroLinea % 2 == 0 ? Fondo : "#FFFFFF";
                    // Las cantidades o importes extremos no se parten en columnas estrechas.
                    // Se mantiene el número completo con un bloque de ancho completo.
                    if (d.Cantidad.ToString("N2", Cultura).Length > 10 ||
                        d.PrecioUnitario.ToString("N4", Cultura).Length > 13 ||
                        d.Subtotal.ToString("N2", Cultura).Length > 13 ||
                        d.Itbis.ToString("N2", Cultura).Length > 13 ||
                        (d.Subtotal + d.Itbis).ToString("N2", Cultura).Length > 13)
                    {
                        tabla.Cell().ColumnSpan(7).ShowEntire().Background(color)
                            .BorderBottom(0.5f).BorderColor(Borde).Padding(8).Column(extensa =>
                            {
                                extensa.Spacing(3);
                                extensa.Item().Text($"Línea {d.NumeroLinea} | {d.CodigoProducto} - {d.NombreItem}").Bold();
                                extensa.Item().Text(
                                    (d.IndicadorBienOServicio == TipoItemDgii.Bien ? "Bien" : "Servicio") + " | " +
                                    Unidad(d.UnidadMedidaCodigo)).FontSize(8).FontColor(Gris);
                                var indicador = CatalogosDgii.IndicadoresFacturacion.TryGetValue(d.IndicadorFacturacion, out var clasificacion)
                                    ? clasificacion : "Clasificación no registrada";
                                extensa.Item().Text(indicador).FontSize(8).FontColor(Gris);
                                extensa.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Cantidad: " + d.Cantidad.ToString("N2", Cultura)).FontSize(8);
                                    r.RelativeItem().Text("Precio: " + d.PrecioUnitario.ToString("N4", Cultura)).FontSize(8);
                                });
                                extensa.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Base: " + Dinero(d.Subtotal)).FontSize(8);
                                    r.RelativeItem().Text("ITBIS: " + Dinero(d.Itbis)).FontSize(8);
                                });
                                if (d.Descuento != 0 || d.Recargo != 0)
                                    extensa.Item().Text($"Descuento: {Dinero(d.Descuento)} | Recargo: {Dinero(d.Recargo)}")
                                        .FontSize(8).FontColor(Gris);
                                extensa.Item().AlignRight().Text("Total línea: " + Dinero(d.Subtotal + d.Itbis)).SemiBold();
                            });
                        continue;
                    }
                    IContainer Celda() => tabla.Cell().ShowEntire().Background(color).BorderBottom(0.5f)
                        .BorderColor(Borde).PaddingVertical(7).PaddingHorizontal(4);

                    Celda().Text(d.NumeroLinea.ToString(Cultura)).FontSize(8);
                    Celda().Column(detalle =>
                    {
                        detalle.Item().Text(d.CodigoProducto).Bold();
                        detalle.Item().Text(d.NombreItem);
                        detalle.Item().PaddingTop(2).Text(
                            (d.IndicadorBienOServicio == TipoItemDgii.Bien ? "Bien" : "Servicio") + " | " + Unidad(d.UnidadMedidaCodigo))
                            .FontSize(7).FontColor(Gris);
                        var indicador = CatalogosDgii.IndicadoresFacturacion.TryGetValue(d.IndicadorFacturacion, out var nombre)
                            ? nombre : "Clasificación no registrada";
                        detalle.Item().Text(indicador).FontSize(7).FontColor(Gris);
                        if (d.Descuento != 0 || d.Recargo != 0)
                            detalle.Item().Text($"Descuento: {Dinero(d.Descuento)} | Recargo: {Dinero(d.Recargo)}")
                                .FontSize(7).FontColor(Gris);
                    });
                    Celda().AlignRight().Text(d.Cantidad.ToString("N2", Cultura)).FontSize(8);
                    Celda().AlignRight().Text(d.PrecioUnitario.ToString("N4", Cultura)).FontSize(8);
                    Celda().AlignRight().Text(d.Subtotal.ToString("N2", Cultura)).FontSize(8);
                    Celda().AlignRight().Text(d.Itbis.ToString("N2", Cultura)).FontSize(8);
                    Celda().AlignRight().Text((d.Subtotal + d.Itbis).ToString("N2", Cultura)).FontSize(8).SemiBold();
                }
            });
        });
    }

    private static void Totales(IContainer contenedor, Factura f)
    {
        contenedor.Row(fila =>
        {
            fila.RelativeItem().PaddingRight(20).Column(c =>
            {
                if (f.TipoComprobante == TipoComprobante.NotaCredito)
                {
                    Subtitulo(c, "EFECTO DEL DOCUMENTO");
                    c.Item().PaddingTop(5).Text(f.CodigoModificacion == CodigoModificacionDgii.CorreccionTexto
                        ? "Corrección de texto: no modifica el importe ni las existencias."
                        : "Nota de crédito: reduce el importe del comprobante referido. " +
                          "El reintegro de inventario se consulta en su historial de movimientos.");
                }
                else if (f.TipoComprobante == TipoComprobante.NotaDebito)
                {
                    Subtitulo(c, "EFECTO DEL DOCUMENTO");
                    c.Item().PaddingTop(5).Text("Nota de débito: aumenta el importe del comprobante referido.");
                }

                if (f.FormasPago.Count > 0)
                {
                    c.Item().PaddingTop(8).Text("Formas de pago registradas").SemiBold();
                    foreach (var pago in f.FormasPago.OrderBy(x => x.FormaPago))
                        c.Item().PaddingTop(2).Text($"{FormaPago(pago.FormaPago)}: {Dinero(pago.Monto)}").FontSize(8);
                }
            });
            fila.ConstantItem(235).Border(1).BorderColor(Borde).Padding(12).Column(c =>
            {
                c.Spacing(6);
                Total(c, "Subtotal facturable", f.Subtotal);
                Total(c, "ITBIS", f.Itbis);
                Total(c, "Monto no facturable", f.MontoNoFacturable);
                c.Item().BorderTop(1).BorderColor(Borde).PaddingTop(8).Column(total =>
                {
                    total.Item().Text("Monto total").Bold().FontSize(10);
                    total.Item().AlignRight().PaddingTop(3).Text(Dinero(f.Total)).Bold().FontSize(13).FontColor(Azul);
                });
                if (f.MontoNoFacturable != 0)
                    Total(c, "Monto período", f.Total + f.MontoNoFacturable);
            });
        });
    }

    private static void Consulta(IContainer contenedor, Uri? url, byte[]? qr)
    {
        contenedor.BorderTop(1).BorderColor(Borde).PaddingTop(12).Row(fila =>
        {
            if (qr is not null)
                fila.ConstantItem(82).Height(82).Image(qr).FitArea();
            fila.RelativeItem().PaddingLeft(qr is null ? 0 : 12).Column(c =>
            {
                Subtitulo(c, "CONSULTA INTERNA DEL COMPROBANTE");
                c.Item().PaddingTop(4).Text("Requiere iniciar sesión como Administrador o Facturador. " +
                    "Este QR no verifica ni certifica el documento ante DGII.").FontSize(8).FontColor(Gris);
                if (url is not null)
                {
                    c.Item().PaddingTop(5).Hyperlink(url.AbsoluteUri).Text(url.AbsoluteUri).FontSize(8).FontColor(Azul);
                    if (url.IsLoopback)
                        c.Item().PaddingTop(3).Text("Dirección local: disponible solamente en el equipo que ejecuta la aplicación.")
                            .FontSize(7).FontColor(Gris);
                }
                else
                    c.Item().PaddingTop(4).Text("QR desactivado: no hay una URL base de consulta configurada.")
                        .FontSize(8).FontColor(Gris);
            });
        });
    }

    private static void Campo(ColumnDescriptor c, string etiqueta, string valor)
    {
        c.Item().PaddingBottom(2).Text(etiqueta).FontSize(7).FontColor(Gris);
        c.Item().PaddingBottom(5).Text(valor).SemiBold();
    }

    private static void Subtitulo(ColumnDescriptor c, string texto) =>
        c.Item().Text(texto).FontSize(8).Bold().FontColor(Azul);

    private static void DatoOpcional(ColumnDescriptor c, string? texto)
    {
        if (!string.IsNullOrWhiteSpace(texto))
            c.Item().PaddingTop(2).Text(texto.Trim()).FontSize(8).FontColor(Gris);
    }

    private static void Total(ColumnDescriptor c, string etiqueta, decimal monto) =>
        c.Item().Row(r =>
        {
            r.ConstantItem(84).Text(etiqueta).FontSize(8);
            r.RelativeItem().AlignRight().Text(Dinero(monto)).SemiBold()
                .FontSize(Dinero(monto).Length > 19 ? 8 : 9);
        });

    private static string Dinero(decimal monto) => "RD$ " + monto.ToString("N2", Cultura);
    private static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Cultura);
    private static string Texto(string? texto, string alternativa) => string.IsNullOrWhiteSpace(texto) ? alternativa : texto;
    private static string Unidad(byte? codigo) => codigo.HasValue && CatalogosDgii.UnidadesMedida.TryGetValue(codigo.Value, out var texto)
        ? $"{codigo} - {texto}" : "Unidad no especificada";

    private static string Titulo(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.CreditoFiscal => "Factura de crédito fiscal",
        TipoComprobante.Consumo => "Factura de consumo",
        TipoComprobante.NotaCredito => "Nota de crédito",
        TipoComprobante.NotaDebito => "Nota de débito",
        TipoComprobante.Compras => "Comprobante de compras",
        TipoComprobante.GastosMenores => "Gastos menores",
        TipoComprobante.RegimenesEspeciales => "Regímenes especiales",
        TipoComprobante.Gubernamental => "Comprobante gubernamental",
        TipoComprobante.Exportaciones => "Exportaciones",
        TipoComprobante.PagosExterior => "Pagos al exterior",
        _ => "Comprobante"
    };

    private static string Estado(EstadoFactura estado) => estado switch
    {
        EstadoFactura.Borrador => "Borrador", EstadoFactura.Confirmada => "Confirmada",
        EstadoFactura.Generada => "Generada", EstadoFactura.Anulada => "Anulada", _ => "Desconocido"
    };

    private static string FormaPago(FormaPagoDgii forma) => forma switch
    {
        FormaPagoDgii.Efectivo => "Efectivo",
        FormaPagoDgii.ChequeTransferenciaDeposito => "Cheque / transferencia / depósito",
        FormaPagoDgii.TarjetaDebitoCredito => "Tarjeta de débito / crédito",
        FormaPagoDgii.VentaCredito => "Venta a crédito",
        FormaPagoDgii.BonoOCertificadoRegalo => "Bono / certificado de regalo",
        FormaPagoDgii.Permuta => "Permuta",
        FormaPagoDgii.NotaCredito => "Nota de crédito", _ => "Otra forma de pago"
    };

    private static void ValidarEmisor(DocumentoPdfOpciones opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones.NombreEmisor) || opciones.NombreEmisor.Length > 150 ||
            opciones.RncEmisor?.Length > 20 || opciones.DireccionEmisor?.Length > 250 ||
            opciones.TelefonoEmisor?.Length > 40 || opciones.CorreoEmisor?.Length > 120)
            throw new ConfiguracionDocumentoPdfException(
                "Revise los datos de DocumentosPdf: nombre obligatorio (150 caracteres), " +
                "RNC (20), dirección (250), teléfono (40) y correo (120).");
    }
}
