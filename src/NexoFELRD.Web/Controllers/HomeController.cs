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
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var zonaDominicana =
            TimeZoneInfo.FindSystemTimeZoneById(
                "America/Santo_Domingo");

        var hoyDominicana =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                zonaDominicana
            ).Date;

        var inicioHoyUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                hoyDominicana,
                zonaDominicana);

        var inicioMananaUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                hoyDominicana.AddDays(1),
                zonaDominicana);

        var modelo = new DashboardViewModel
        {
            CantidadClientes = await _context.Clientes
                .CountAsync(cliente => cliente.Activo),

            CantidadProductos = await _context.Productos
                .CountAsync(producto => producto.Activo),

            CantidadFacturas = await _context.Facturas
                .CountAsync(),

            ProductosBajoInventario =
                await _context.Productos.CountAsync(
                    producto =>
                        producto.Activo &&
                        producto.Existencia <= 5),

            VentasHoy = await _context.Facturas
                .Where(factura =>
                    factura.FechaEmision >= inicioHoyUtc &&
                    factura.FechaEmision < inicioMananaUtc &&
                    factura.Estado != EstadoFactura.Anulada)
                .SumAsync(factura =>
                    (decimal?)factura.Total) ?? 0m
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
