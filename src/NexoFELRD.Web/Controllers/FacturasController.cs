using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;
using System.Security.Claims;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador,Facturador")]
public class FacturasController : Controller
{
    private readonly ApplicationDbContext _context;

    public FacturasController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar)
    {
        var consulta = _context.Facturas
            .AsNoTracking()
            .Include(factura => factura.Cliente)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            buscar = buscar.Trim();

            consulta = consulta.Where(factura =>
                factura.NumeroInterno.Contains(buscar) ||
                factura.Cliente.Nombre.Contains(buscar) ||
                (factura.Cliente.IdentificacionFiscal != null &&
                 factura.Cliente.IdentificacionFiscal.Contains(buscar)));
        }

        ViewData["Buscar"] = buscar;

        var facturas = await consulta
            .OrderByDescending(factura => factura.FechaEmision)
            .ThenByDescending(factura => factura.Id)
            .ToListAsync();

        return View(facturas);
    }
    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var modelo = new FacturaCrearViewModel();

        await CargarCatalogosAsync(modelo);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(FacturaCrearViewModel modelo)
    {
        var detallesSolicitados = modelo.Detalles?
            .Where(detalle =>
                detalle.ProductoId > 0 &&
                detalle.Cantidad > 0)
            .GroupBy(detalle => detalle.ProductoId)
            .Select(grupo => new
            {
                ProductoId = grupo.Key,
                Cantidad = grupo.Sum(detalle => detalle.Cantidad)
            })
            .ToList();

        if (detallesSolicitados == null ||
            detallesSolicitados.Count == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Debes agregar al menos un producto a la factura.");
        }

        var cliente = await _context.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(cliente =>
                cliente.Id == modelo.ClienteId &&
                cliente.Activo);

        if (cliente == null)
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "El cliente seleccionado no existe o está inactivo.");
        }
        else if (
            modelo.TipoComprobante == TipoComprobante.CreditoFiscal &&
            string.IsNullOrWhiteSpace(cliente.IdentificacionFiscal))
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "Para emitir un comprobante de crédito fiscal, el cliente debe tener RNC o cédula.");
        }

        var productos = new Dictionary<int, Producto>();

        if (detallesSolicitados != null &&
            detallesSolicitados.Count > 0)
        {
            var productosIds = detallesSolicitados
                .Select(detalle => detalle.ProductoId)
                .ToList();

            productos = await _context.Productos
                .Where(producto =>
                    productosIds.Contains(producto.Id) &&
                    producto.Activo)
                .ToDictionaryAsync(producto => producto.Id);

            foreach (var detalle in detallesSolicitados)
            {
                if (!productos.TryGetValue(
                    detalle.ProductoId,
                    out var producto))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"El producto con identificador {detalle.ProductoId} no existe o está inactivo.");

                    continue;
                }

                if (detalle.Cantidad > producto.Existencia)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"No hay suficiente existencia de {producto.Nombre}. " +
                        $"Disponible: {producto.Existencia}.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync(modelo);
            return View(modelo);
        }

        var factura = new Factura
        {
            NumeroInterno =
            $"FAC-{DateTime.Now:yyMMddHHmmssfff}",

            ClienteId = modelo.ClienteId,
            TipoComprobante = modelo.TipoComprobante,
            Estado = EstadoFactura.Confirmada,
            FechaEmision = modelo.FechaEmision,
            UsuarioId =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.Identity?.Name
                ?? "Sistema"
        };

        await using var transaccion =
            await _context.Database.BeginTransactionAsync();

        try
        {
            foreach (var detalleSolicitado in detallesSolicitados!)
            {
                var producto =
                    productos[detalleSolicitado.ProductoId];

                var subtotalLinea = decimal.Round(
                    producto.Precio * detalleSolicitado.Cantidad,
                    2,
                    MidpointRounding.AwayFromZero);

                var itbisLinea = decimal.Round(
                    subtotalLinea * producto.TasaItbis / 100m,
                    2,
                    MidpointRounding.AwayFromZero);

                factura.Detalles.Add(new FacturaDetalle
                {
                    ProductoId = producto.Id,
                    Cantidad = detalleSolicitado.Cantidad,
                    PrecioUnitario = producto.Precio,
                    Subtotal = subtotalLinea,
                    Itbis = itbisLinea
                });

                factura.Subtotal += subtotalLinea;
                factura.Itbis += itbisLinea;

                producto.Existencia -= detalleSolicitado.Cantidad;

                _context.MovimientosInventario.Add(
                    new MovimientoInventario
                    {
                        ProductoId = producto.Id,
                        Factura = factura,
                        Tipo = TipoMovimiento.Salida,
                        Cantidad = detalleSolicitado.Cantidad,
                        Fecha = DateTime.UtcNow,
                        Observacion =
                            $"Salida por factura {factura.NumeroInterno}"
                    });
            }

            factura.Total =
                factura.Subtotal + factura.Itbis;

            _context.Facturas.Add(factura);

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Exito"] =
                $"La factura {factura.NumeroInterno} fue registrada correctamente.";

            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await transaccion.RollbackAsync();

            ModelState.AddModelError(
                string.Empty,
                "No fue posible registrar la factura. Inténtalo nuevamente.");

            await CargarCatalogosAsync(modelo);

            return View(modelo);
        }
    }

    private async Task CargarCatalogosAsync(
        FacturaCrearViewModel modelo)
    {
        var clientes = await _context.Clientes
            .AsNoTracking()
            .Where(cliente => cliente.Activo)
            .OrderBy(cliente => cliente.Nombre)
            .Select(cliente => new
            {
                cliente.Id,
                cliente.Nombre,
                cliente.IdentificacionFiscal
            })
            .ToListAsync();

        modelo.Clientes = clientes.Select(cliente =>
            new SelectListItem
            {
                Value = cliente.Id.ToString(),
                Text = string.IsNullOrWhiteSpace(
                    cliente.IdentificacionFiscal)
                        ? cliente.Nombre
                        : $"{cliente.Nombre} - {cliente.IdentificacionFiscal}"
            })
            .ToList();

        modelo.Productos = await _context.Productos
            .AsNoTracking()
            .Where(producto =>
                producto.Activo &&
                producto.Existencia > 0)
            .OrderBy(producto => producto.Nombre)
            .Select(producto => new ProductoFacturaViewModel
            {
                Id = producto.Id,
                Codigo = producto.Codigo,
                Nombre = producto.Nombre,
                Precio = producto.Precio,
                TasaItbis = producto.TasaItbis,
                IndicadorFacturacion =
                    producto.IndicadorFacturacion,
                Existencia = producto.Existencia
            })
            .ToListAsync();
    }
}
