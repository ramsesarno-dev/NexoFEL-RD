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

    public ReportesController(ApplicationDbContext context)
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
            TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");

        var hoy = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, zonaDominicana).Date;

        var inicio = desde?.Date ?? new DateTime(hoy.Year, hoy.Month, 1);
        var fin = hasta?.Date ?? hoy;

        if (fin < inicio)
        {
            (inicio, fin) = (fin, inicio);

            ModelState.AddModelError(
                string.Empty,
                "Las fechas estaban invertidas y fueron ordenadas automáticamente.");
        }

        if (tipoSeleccionado.HasValue &&
            !Enum.IsDefined(typeof(TipoComprobante), tipoSeleccionado.Value))
        {
            ModelState.AddModelError(
                nameof(tipoSeleccionado),
                "El tipo de comprobante seleccionado no es válido.");

            tipoSeleccionado = null;
        }

        var finExclusivo = fin == DateTime.MaxValue.Date
            ? DateTime.MaxValue
            : fin.AddDays(1);

        var consulta = _context.Facturas
            .AsNoTracking()
            .Where(factura =>
                factura.FechaEmision >= inicio &&
                factura.FechaEmision < finExclusivo);

        if (tipoSeleccionado.HasValue)
        {
            consulta = consulta.Where(factura =>
                factura.TipoComprobante == tipoSeleccionado.Value);
        }

        // La nota de anulación se busca en todo el historial.
        // La factura conserva su fecha comercial original.
        var idsConEfectoFiscal = consulta
            .ConEfectoFiscal()
            .Select(factura => factura.Id);

        var documentos = await consulta
            .OrderByDescending(factura => factura.FechaEmision)
            .ThenByDescending(factura => factura.Id)
            .Select(factura => new ReporteDocumentoViewModel
            {
                Id = factura.Id,
                NumeroInterno = factura.NumeroInterno,
                ENCF = factura.ENCF,
                FechaEmision = factura.FechaEmision,

                Comprador =
                    factura.RazonSocialComprador ??
                    (factura.Cliente != null
                        ? factura.Cliente.Nombre
                        : null) ??
                    "Consumidor final",

                TipoComprobante = factura.TipoComprobante,

                TipoComprobanteAfectado =
                    factura.FacturaAfectada != null
                        ? factura.FacturaAfectada.TipoComprobante
                        : (TipoComprobante?)null,

                FacturaAfectadaId = factura.FacturaAfectadaId,

                ENCFOriginal =
                    factura.FacturaAfectada != null
                        ? factura.FacturaAfectada.ENCF
                        : null,

                Estado = factura.Estado,

                TieneEfectoFiscal =
                    idsConEfectoFiscal.Contains(factura.Id),

                Subtotal = factura.Subtotal,
                Itbis = factura.Itbis,
                MontoNoFacturable = factura.MontoNoFacturable,
                Total = factura.Total
            })
            .ToListAsync();

        foreach (var documento in documentos)
        {
            documento.MontoFirmado =
                documento.Total *
                ClasificadorComprobanteFiscal.ObtenerSigno(
                    documento.TipoComprobante);
        }

        var documentosDelCalculo = documentos
            .Where(documento => documento.TieneEfectoFiscal)
            .ToList();

        var ventas = documentosDelCalculo
            .Where(documento =>
                ClasificadorComprobanteFiscal.ObtenerCategoria(
                    documento.TipoComprobante,
                    documento.TipoComprobanteAfectado) ==
                CategoriaReporteFiscal.Venta)
            .ToList();

        var resumenDiario = ventas
            .GroupBy(documento => documento.FechaEmision.Date)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new ReporteDiarioViewModel
            {
                Fecha = grupo.Key,

                FacturacionBruta = grupo
                    .Where(documento =>
                        ClasificadorComprobanteFiscal.EsDocumentoBaseVenta(
                            documento.TipoComprobante))
                    .Sum(documento => documento.Total),

                NotasDebito = grupo
                    .Where(documento =>
                        documento.TipoComprobante ==
                        TipoComprobante.NotaDebito)
                    .Sum(documento => documento.Total),

                NotasCredito = grupo
                    .Where(documento =>
                        documento.TipoComprobante ==
                        TipoComprobante.NotaCredito)
                    .Sum(documento => documento.Total),

                VentasNetas = grupo.Sum(documento =>
                    documento.MontoFirmado)
            })
            .ToList();

        var resumenPorTipo = documentosDelCalculo
            .GroupBy(documento => new
            {
                documento.TipoComprobante,

                Categoria =
                    ClasificadorComprobanteFiscal.ObtenerCategoria(
                        documento.TipoComprobante,
                        documento.TipoComprobanteAfectado)
            })
            .OrderBy(grupo => (int)grupo.Key.TipoComprobante)
            .Select(grupo => new ReporteTipoComprobanteViewModel
            {
                TipoComprobante = grupo.Key.TipoComprobante,

                Codigo =
                    ClasificadorComprobanteFiscal.ObtenerCodigo(
                        grupo.Key.TipoComprobante),

                Nombre =
                    ClasificadorComprobanteFiscal.ObtenerNombre(
                        grupo.Key.TipoComprobante),

                Categoria =
                    ClasificadorComprobanteFiscal.ObtenerNombreCategoria(
                        grupo.Key.Categoria),

                Cantidad = grupo.Count(),

                Monto = grupo.Sum(documento =>
                    documento.MontoFirmado)
            })
            .ToList();

        var tiposBaseVenta = Enum.GetValues<TipoComprobante>()
            .Where(ClasificadorComprobanteFiscal.EsDocumentoBaseVenta)
            .ToArray();

        var cantidadesFacturadas = await _context.FacturaDetalles
            .AsNoTracking()
            .Where(detalle =>
                idsConEfectoFiscal.Contains(detalle.FacturaId) &&
                tiposBaseVenta.Contains(
                    detalle.Factura.TipoComprobante))
            .Select(detalle => new ReporteProductoViewModel
            {
                ProductoId = detalle.ProductoId,
                Codigo = detalle.CodigoProducto,
                Nombre = detalle.NombreItem,
                CantidadFacturada = detalle.Cantidad
            })
            .ToListAsync();

        // Solo cuentan reintegros registrados en inventario.
        // Las correcciones de texto no generan cantidades devueltas.
        var cantidadesReintegradas = await _context.MovimientosInventario
            .AsNoTracking()
            .ReintegrosDeNotasCredito()
            .Where(movimiento =>
                movimiento.FacturaId.HasValue &&
                idsConEfectoFiscal.Contains(
                    movimiento.FacturaId.Value) &&
                (
                    movimiento.Factura!.FacturaAfectada == null ||
                    tiposBaseVenta.Contains(
                        movimiento.Factura.FacturaAfectada.TipoComprobante)
                ))
            .Select(movimiento => new ReporteProductoViewModel
            {
                ProductoId = movimiento.ProductoId,
                Codigo = movimiento.Producto.Codigo,
                Nombre = movimiento.Producto.Nombre,
                CantidadDevuelta = movimiento.Cantidad
            })
            .ToListAsync();

        var productos = cantidadesFacturadas
            .Concat(cantidadesReintegradas)
            .GroupBy(producto => producto.ProductoId)
            .Select(grupo => new ReporteProductoViewModel
            {
                ProductoId = grupo.Key,
                Codigo = grupo.First().Codigo,
                Nombre = grupo.First().Nombre,

                CantidadFacturada = grupo.Sum(producto =>
                    producto.CantidadFacturada),

                CantidadDevuelta = grupo.Sum(producto =>
                    producto.CantidadDevuelta),

                CantidadNeta = grupo.Sum(producto =>
                    producto.CantidadFacturada -
                    producto.CantidadDevuelta)
            })
            .OrderByDescending(producto =>
                producto.CantidadFacturada +
                producto.CantidadDevuelta)
            .ThenBy(producto => producto.Codigo)
            .Take(5)
            .ToList();

        var documentosParaTabla = documentos
            .Where(documento =>
                incluirAnuladas ||
                documento.Estado != EstadoFactura.Anulada ||
                documento.RevertidaPorNotaCredito)
            .ToList();

        var modelo = new ReporteIndexViewModel
        {
            Desde = inicio,
            Hasta = fin,
            TipoSeleccionado = tipoSeleccionado,
            IncluirAnuladas = incluirAnuladas,

            FacturacionBruta = ventas
                .Where(documento =>
                    ClasificadorComprobanteFiscal.EsDocumentoBaseVenta(
                        documento.TipoComprobante))
                .Sum(documento => documento.Total),

            NotasDebito = ventas
                .Where(documento =>
                    documento.TipoComprobante ==
                    TipoComprobante.NotaDebito)
                .Sum(documento => documento.Total),

            NotasCredito = ventas
                .Where(documento =>
                    documento.TipoComprobante ==
                    TipoComprobante.NotaCredito)
                .Sum(documento => documento.Total),

            VentasNetas = ventas.Sum(documento =>
                documento.MontoFirmado),

            ItbisNeto = ventas.Sum(documento =>
                documento.Itbis *
                ClasificadorComprobanteFiscal.ObtenerSigno(
                    documento.TipoComprobante)),

            MontoNoFacturableNeto = ventas.Sum(documento =>
                documento.MontoNoFacturable *
                ClasificadorComprobanteFiscal.ObtenerSigno(
                    documento.TipoComprobante)),

            ComprasYGastosNetos = documentosDelCalculo
                .Where(documento =>
                    ClasificadorComprobanteFiscal.ObtenerCategoria(
                        documento.TipoComprobante,
                        documento.TipoComprobanteAfectado) is
                    CategoriaReporteFiscal.Compra or
                    CategoriaReporteFiscal.Gasto or
                    CategoriaReporteFiscal.PagoExterior)
                .Sum(documento => documento.MontoFirmado),

            CantidadDocumentos = documentosDelCalculo.Count,

            CantidadAnulados = documentos.Count(documento =>
                documento.Estado == EstadoFactura.Anulada),

            CantidadRevertidos = documentos.Count(documento =>
                documento.RevertidaPorNotaCredito),

            CantidadBorradores = documentos.Count(documento =>
                documento.Estado == EstadoFactura.Borrador),

            TotalResultados = documentosParaTabla.Count,

            ResultadosLimitados =
                documentosParaTabla.Count > LimiteDocumentosTabla,

            Documentos = documentosParaTabla
                .Take(LimiteDocumentosTabla)
                .ToList(),

            ResumenPorTipo = resumenPorTipo,
            ResumenDiario = resumenDiario,
            Productos = productos
        };

        return View(modelo);
    }
}
