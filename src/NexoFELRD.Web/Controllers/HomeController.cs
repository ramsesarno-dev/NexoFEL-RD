using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;

namespace NexoFELRD.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private const decimal LimiteBajoInventario = 5m;

    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var zonaDominicana =
            TimeZoneInfo.FindSystemTimeZoneById(
                "America/Santo_Domingo");

        /*
         * Fecha comercial dominicana.
         *
         * FechaEmision se almacena como la fecha seleccionada
         * por el usuario, normalmente a las 00:00:00.
         * Por eso se compara directamente con el día local
         * dominicano y no con un intervalo convertido a UTC.
         */
        var hoyDominicana =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                zonaDominicana
            ).Date;

        var inicioManana =
            hoyDominicana.AddDays(1);

        var cantidadClientes =
            await _context.Clientes
                .AsNoTracking()
                .CountAsync(cliente => cliente.Activo);

        var cantidadProductos =
            await _context.Productos
                .AsNoTracking()
                .CountAsync(producto => producto.Activo);

        var cantidadFacturas =
            await _context.Facturas
                .AsNoTracking()
                .CountAsync();

        var productosBajoInventario =
            await _context.Productos
                .AsNoTracking()
                .CountAsync(producto =>
                    producto.Activo &&
                    producto.IndicadorBienOServicio ==
                        TipoItemDgii.Bien &&
                    producto.Existencia <=
                        LimiteBajoInventario);

        var resumenHoy =
            await _context.Facturas
                .AsNoTracking()
                .Where(factura =>
                    factura.FechaEmision >= hoyDominicana &&
                    factura.FechaEmision < inicioManana &&
                    (
                        factura.Estado ==
                            EstadoFactura.Confirmada ||
                        factura.Estado ==
                            EstadoFactura.Generada
                    ) &&
                    (
                        factura.TipoComprobante ==
                            TipoComprobante.CreditoFiscal ||
                        factura.TipoComprobante ==
                            TipoComprobante.Consumo ||
                        factura.TipoComprobante ==
                            TipoComprobante.NotaDebito ||
                        factura.TipoComprobante ==
                            TipoComprobante.NotaCredito
                    ))
                .GroupBy(factura => 1)
                .Select(grupo => new
                {
                    /*
                     * E31 y E32 aumentan las ventas.
                     * E33 aumenta el comprobante original.
                     * E34 disminuye el comprobante original.
                     */
                    VentasNetas = grupo.Sum(factura =>
                        factura.TipoComprobante ==
                            TipoComprobante.NotaCredito
                            ? -factura.Total
                            : factura.Total),

                    CantidadFacturas = grupo.Count(factura =>
                        factura.TipoComprobante ==
                            TipoComprobante.CreditoFiscal ||
                        factura.TipoComprobante ==
                            TipoComprobante.Consumo),

                    CantidadNotasDebito = grupo.Count(factura =>
                        factura.TipoComprobante ==
                            TipoComprobante.NotaDebito),

                    CantidadNotasCredito = grupo.Count(factura =>
                        factura.TipoComprobante ==
                            TipoComprobante.NotaCredito)
                })
                .FirstOrDefaultAsync();

        var documentosRecientes =
            await _context.Facturas
                .AsNoTracking()
                .OrderByDescending(factura =>
                    factura.FechaEmision)
                .ThenByDescending(factura =>
                    factura.Id)
                .Take(6)
                .Select(factura =>
                    new DocumentoRecienteDashboardViewModel
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

                        Estado =
                            factura.Estado,

                        Total =
                            factura.Total
                    })
                .ToListAsync();

        var productosConBajoInventario =
            await _context.Productos
                .AsNoTracking()
                .Where(producto =>
                    producto.Activo &&
                    producto.IndicadorBienOServicio ==
                        TipoItemDgii.Bien &&
                    producto.Existencia <=
                        LimiteBajoInventario)
                .OrderBy(producto =>
                    producto.Existencia)
                .ThenBy(producto =>
                    producto.Nombre)
                .Take(5)
                .Select(producto =>
                    new ProductoBajoInventarioDashboardViewModel
                    {
                        Id =
                            producto.Id,

                        Codigo =
                            producto.Codigo,

                        Nombre =
                            producto.Nombre,

                        Existencia =
                            producto.Existencia
                    })
                .ToListAsync();

        var modelo = new DashboardViewModel
        {
            CantidadClientes =
                cantidadClientes,

            CantidadProductos =
                cantidadProductos,

            CantidadFacturas =
                cantidadFacturas,

            ProductosBajoInventario =
                productosBajoInventario,

            VentasHoy =
                resumenHoy?.VentasNetas ?? 0m,

            CantidadFacturasHoy =
                resumenHoy?.CantidadFacturas ?? 0,

            CantidadNotasDebitoHoy =
                resumenHoy?.CantidadNotasDebito ?? 0,

            CantidadNotasCreditoHoy =
                resumenHoy?.CantidadNotasCredito ?? 0,

            DocumentosRecientes =
                documentosRecientes,

            ProductosConBajoInventario =
                productosConBajoInventario
        };

        return View(modelo);
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId =
                Activity.Current?.Id ??
                HttpContext.TraceIdentifier
        });
    }
}
