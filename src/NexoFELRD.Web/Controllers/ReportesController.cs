using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;
using NexoFELRD.Web.Services;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ReportesController : Controller
{
    private const int LimiteDocumentosTabla = 200;

    private readonly ApplicationDbContext _context;

    public ReportesController(
        ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? desde,
        DateTime? hasta,
        TipoComprobante? tipoSeleccionado,
        bool incluirAnuladas = false)
    {
        var zonaDominicana =
            TimeZoneInfo.FindSystemTimeZoneById(
                "America/Santo_Domingo");

        var hoyDominicana =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                zonaDominicana
            ).Date;

        var desdeSeleccionada =
            desde?.Date ??
            new DateTime(
                hoyDominicana.Year,
                hoyDominicana.Month,
                1);

        var hastaSeleccionada =
            hasta?.Date ??
            hoyDominicana;

        if (hastaSeleccionada < desdeSeleccionada)
        {
            (desdeSeleccionada, hastaSeleccionada) =
                (hastaSeleccionada, desdeSeleccionada);

            ModelState.AddModelError(
                string.Empty,
                "Las fechas estaban invertidas y fueron ordenadas automáticamente.");
        }

        var finExclusivo =
            hastaSeleccionada.AddDays(1);

        var consultaDocumentos =
            _context.Facturas
                .AsNoTracking()
                .Where(factura =>
                    factura.FechaEmision >=
                        desdeSeleccionada &&
                    factura.FechaEmision <
                        finExclusivo);

        if (tipoSeleccionado.HasValue)
        {
            consultaDocumentos =
                consultaDocumentos.Where(factura =>
                    factura.TipoComprobante ==
                        tipoSeleccionado.Value);
        }

        var documentos =
            await consultaDocumentos
                .OrderByDescending(factura =>
                    factura.FechaEmision)
                .ThenByDescending(factura =>
                    factura.Id)
                .Select(factura =>
                    new ReporteDocumentoViewModel
                    {
                        Id = factura.Id,

                        NumeroInterno =
                            factura.NumeroInterno,

                        ENCF =
                            factura.ENCF,

                        FechaEmision =
                            factura.FechaEmision,

                        Comprador =
                            factura.RazonSocialComprador ??
                            (
                                factura.Cliente != null
                                    ? factura.Cliente.Nombre
                                    : null
                            ) ??
                            "Consumidor final",

                        TipoComprobante =
                            factura.TipoComprobante,

                        TipoComprobanteAfectado =
                            factura.FacturaAfectada != null
                                ? factura.FacturaAfectada
                                    .TipoComprobante
                                : null,

                        FacturaAfectadaId =
                            factura.FacturaAfectadaId,

                        ENCFOriginal =
                            factura.FacturaAfectada != null
                                ? factura.FacturaAfectada.ENCF
                                : null,

                        Estado =
                            factura.Estado,

                        Subtotal =
                            factura.Subtotal,

                        Itbis =
                            factura.Itbis,

                        MontoNoFacturable =
                            factura.MontoNoFacturable,

                        Total =
                            factura.Total
                    })
                .ToListAsync();

        foreach (var documento in documentos)
        {
            documento.MontoFirmado =
                documento.Total *
                ClasificadorComprobanteFiscal.ObtenerSigno(
                    documento.TipoComprobante);
        }

        var documentosVigentes =
            documentos
                .Where(documento =>
                    EsEstadoFiscalValido(
                        documento.Estado))
                .ToList();

        var documentosVenta =
            documentosVigentes
                .Where(documento =>
                    ClasificadorComprobanteFiscal
                        .ObtenerCategoria(
                            documento.TipoComprobante,
                            documento.TipoComprobanteAfectado) ==
                        CategoriaReporteFiscal.Venta)
                .ToList();

        var facturacionBruta =
            documentosVenta
                .Where(documento =>
                    ClasificadorComprobanteFiscal
                        .EsDocumentoBaseVenta(
                            documento.TipoComprobante))
                .Sum(documento =>
                    documento.Total);

        var notasDebito =
            documentosVenta
                .Where(documento =>
                    documento.TipoComprobante ==
                        TipoComprobante.NotaDebito)
                .Sum(documento =>
                    documento.Total);

        var notasCredito =
            documentosVenta
                .Where(documento =>
                    documento.TipoComprobante ==
                        TipoComprobante.NotaCredito)
                .Sum(documento =>
                    documento.Total);

        var ventasNetas =
            documentosVenta.Sum(documento =>
                documento.MontoFirmado);

        var itbisNeto =
            documentosVenta.Sum(documento =>
                documento.Itbis *
                ClasificadorComprobanteFiscal.ObtenerSigno(
                    documento.TipoComprobante));

        var montoNoFacturableNeto =
            documentosVenta.Sum(documento =>
                documento.MontoNoFacturable *
                ClasificadorComprobanteFiscal.ObtenerSigno(
                    documento.TipoComprobante));

        var comprasYGastosNetos =
            documentosVigentes
                .Where(documento =>
                {
                    var categoria =
                        ClasificadorComprobanteFiscal
                            .ObtenerCategoria(
                                documento.TipoComprobante,
                                documento
                                    .TipoComprobanteAfectado);

                    return categoria ==
                               CategoriaReporteFiscal.Compra ||
                           categoria ==
                               CategoriaReporteFiscal.Gasto ||
                           categoria ==
                               CategoriaReporteFiscal
                                   .PagoExterior;
                })
                .Sum(documento =>
                    documento.MontoFirmado);

        var resumenDiario =
            documentosVenta
                .GroupBy(documento =>
                    documento.FechaEmision.Date)
                .Select(grupo =>
                    new ReporteDiarioViewModel
                    {
                        Fecha = grupo.Key,

                        FacturacionBruta =
                            grupo
                                .Where(documento =>
                                    ClasificadorComprobanteFiscal
                                        .EsDocumentoBaseVenta(
                                            documento
                                                .TipoComprobante))
                                .Sum(documento =>
                                    documento.Total),

                        NotasDebito =
                            grupo
                                .Where(documento =>
                                    documento.TipoComprobante ==
                                        TipoComprobante
                                            .NotaDebito)
                                .Sum(documento =>
                                    documento.Total),

                        NotasCredito =
                            grupo
                                .Where(documento =>
                                    documento.TipoComprobante ==
                                        TipoComprobante
                                            .NotaCredito)
                                .Sum(documento =>
                                    documento.Total),

                        VentasNetas =
                            grupo.Sum(documento =>
                                documento.MontoFirmado)
                    })
                .OrderBy(resumen =>
                    resumen.Fecha)
                .ToList();

        var resumenPorTipo =
            documentosVigentes
                .GroupBy(documento => new
                {
                    documento.TipoComprobante,

                    Categoria =
                        ClasificadorComprobanteFiscal
                            .ObtenerCategoria(
                                documento.TipoComprobante,
                                documento
                                    .TipoComprobanteAfectado)
                })
                .Select(grupo =>
                    new ReporteTipoComprobanteViewModel
                    {
                        TipoComprobante =
                            grupo.Key.TipoComprobante,

                        Codigo =
                            ClasificadorComprobanteFiscal
                                .ObtenerCodigo(
                                    grupo.Key
                                        .TipoComprobante),

                        Nombre =
                            ClasificadorComprobanteFiscal
                                .ObtenerNombre(
                                    grupo.Key
                                        .TipoComprobante),

                        Categoria =
                            ClasificadorComprobanteFiscal
                                .ObtenerNombreCategoria(
                                    grupo.Key.Categoria),

                        Cantidad =
                            grupo.Count(),

                        Monto =
                            grupo.Sum(documento =>
                                documento.MontoFirmado)
                    })
                .OrderBy(resumen =>
                    (int)resumen.TipoComprobante)
                .ToList();

        var consultaDetalles =
            _context.FacturaDetalles
                .AsNoTracking()
                .Where(detalle =>
                    detalle.Factura.FechaEmision >=
                        desdeSeleccionada &&
                    detalle.Factura.FechaEmision <
                        finExclusivo &&
                    (
                        detalle.Factura.Estado ==
                            EstadoFactura.Confirmada ||
                        detalle.Factura.Estado ==
                            EstadoFactura.Generada
                    ));

        if (tipoSeleccionado.HasValue)
        {
            consultaDetalles =
                consultaDetalles.Where(detalle =>
                    detalle.Factura.TipoComprobante ==
                        tipoSeleccionado.Value);
        }

        var detalles =
            await consultaDetalles
                .Select(detalle => new
                {
                    detalle.ProductoId,
                    detalle.CodigoProducto,
                    detalle.NombreItem,
                    detalle.Cantidad,

                    TipoComprobante =
                        detalle.Factura.TipoComprobante,

                    TipoComprobanteAfectado =
                        detalle.Factura
                                .FacturaAfectada != null
                            ? detalle.Factura
                                .FacturaAfectada
                                .TipoComprobante
                            : (TipoComprobante?)null
                })
                .ToListAsync();

        var productos =
            detalles
                .Where(detalle =>
                    detalle.TipoComprobante !=
                        TipoComprobante.NotaDebito &&
                    ClasificadorComprobanteFiscal
                        .ObtenerCategoria(
                            detalle.TipoComprobante,
                            detalle
                                .TipoComprobanteAfectado) ==
                        CategoriaReporteFiscal.Venta)
                .GroupBy(detalle =>
                    detalle.ProductoId)
                .Select(grupo =>
                {
                    var primero =
                        grupo.First();

                    var cantidadFacturada =
                        grupo
                            .Where(detalle =>
                                detalle.TipoComprobante !=
                                    TipoComprobante
                                        .NotaCredito)
                            .Sum(detalle =>
                                detalle.Cantidad);

                    var cantidadDevuelta =
                        grupo
                            .Where(detalle =>
                                detalle.TipoComprobante ==
                                    TipoComprobante
                                        .NotaCredito)
                            .Sum(detalle =>
                                detalle.Cantidad);

                    return new ReporteProductoViewModel
                    {
                        ProductoId =
                            grupo.Key,

                        Codigo =
                            primero.CodigoProducto,

                        Nombre =
                            primero.NombreItem,

                        CantidadFacturada =
                            cantidadFacturada,

                        CantidadDevuelta =
                            cantidadDevuelta,

                        CantidadNeta =
                            cantidadFacturada -
                            cantidadDevuelta
                    };
                })
                .OrderByDescending(producto =>
                    producto.CantidadNeta)
                .ThenByDescending(producto =>
                    producto.CantidadFacturada)
                .Take(5)
                .ToList();

        var documentosParaTabla =
            incluirAnuladas
                ? documentos
                : documentos
                    .Where(documento =>
                        documento.Estado !=
                            EstadoFactura.Anulada)
                    .ToList();

        var totalResultados =
            documentosParaTabla.Count;

        var modelo = new ReporteIndexViewModel
        {
            Desde =
                desdeSeleccionada,

            Hasta =
                hastaSeleccionada,

            TipoSeleccionado =
                tipoSeleccionado,

            IncluirAnuladas =
                incluirAnuladas,

            FacturacionBruta =
                facturacionBruta,

            NotasDebito =
                notasDebito,

            NotasCredito =
                notasCredito,

            VentasNetas =
                ventasNetas,

            ItbisNeto =
                itbisNeto,

            MontoNoFacturableNeto =
                montoNoFacturableNeto,

            ComprasYGastosNetos =
                comprasYGastosNetos,

            CantidadDocumentos =
                documentosVigentes.Count,

            CantidadAnulados =
                documentos.Count(documento =>
                    documento.Estado ==
                        EstadoFactura.Anulada),

            CantidadBorradores =
                documentos.Count(documento =>
                    documento.Estado ==
                        EstadoFactura.Borrador),

            TotalResultados =
                totalResultados,

            ResultadosLimitados =
                totalResultados >
                    LimiteDocumentosTabla,

            Documentos =
                documentosParaTabla
                    .Take(LimiteDocumentosTabla)
                    .ToList(),

            ResumenPorTipo =
                resumenPorTipo,

            ResumenDiario =
                resumenDiario,

            Productos =
                productos
        };

        return View(modelo);
    }

    private static bool EsEstadoFiscalValido(
        EstadoFactura estado)
    {
        return estado == EstadoFactura.Confirmada ||
               estado == EstadoFactura.Generada;
    }
}
