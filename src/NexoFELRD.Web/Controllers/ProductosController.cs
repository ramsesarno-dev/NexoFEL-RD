using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador,Facturador")]
public class ProductosController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductosController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? buscar)
    {
        var consulta = _context.Productos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            buscar = buscar.Trim();

            consulta = consulta.Where(producto =>
                producto.Codigo.Contains(buscar) ||
                producto.Nombre.Contains(buscar));
        }

        ViewData["Buscar"] = buscar;

        var productos = await consulta
            .OrderByDescending(producto => producto.Activo)
            .ThenBy(producto => producto.Nombre)
            .ToListAsync();

        return View(productos);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View(new Producto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(Producto producto)
    {
        Normalizar(producto);

        if (await _context.Productos.AnyAsync(
            x => x.Codigo == producto.Codigo))
        {
            ModelState.AddModelError(
                nameof(Producto.Codigo),
                "Ya existe un producto con este código.");
        }

        if (!ModelState.IsValid)
        {
            return View(producto);
        }

        producto.Id = 0;
        producto.Activo = true;

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        TempData["Exito"] =
            "El producto fue registrado correctamente.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var producto = await _context.Productos.FindAsync(id);

        if (producto is null)
        {
            return NotFound();
        }

        return View(producto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        Producto formulario)
    {
        if (id != formulario.Id)
        {
            return BadRequest();
        }

        Normalizar(formulario);

        if (await _context.Productos.AnyAsync(x =>
            x.Id != id &&
            x.Codigo == formulario.Codigo))
        {
            ModelState.AddModelError(
                nameof(Producto.Codigo),
                "Ya existe otro producto con este código.");
        }

        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var producto = await _context.Productos.FindAsync(id);

        if (producto is null)
        {
            return NotFound();
        }

        producto.Codigo = formulario.Codigo;
        producto.Nombre = formulario.Nombre;
        producto.Precio = formulario.Precio;
        producto.TasaItbis = formulario.TasaItbis;
        producto.IndicadorFacturacion = formulario.IndicadorFacturacion;
        producto.Existencia = formulario.Existencia;

        await _context.SaveChangesAsync();

        TempData["Exito"] =
            "Los datos del producto fueron actualizados.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var producto = await _context.Productos.FindAsync(id);

        if (producto is null)
        {
            return NotFound();
        }

        producto.Activo = !producto.Activo;

        await _context.SaveChangesAsync();

        TempData["Exito"] = producto.Activo
            ? "El producto fue activado."
            : "El producto fue desactivado.";

        return RedirectToAction(nameof(Index));
    }

    private static void Normalizar(Producto producto)
    {
        producto.Codigo =
            producto.Codigo?.Trim().ToUpperInvariant()
            ?? string.Empty;

        producto.Nombre =
            producto.Nombre?.Trim()
            ?? string.Empty;

        producto.TasaItbis =
            ObtenerTasaItbis(producto.IndicadorFacturacion);
    }
    private static decimal ObtenerTasaItbis(
    byte indicadorFacturacion)
    {
        return indicadorFacturacion switch
        {
            1 => 18m,
            2 => 16m,
            3 => 0m,
            4 => 0m,
            0 => 0m,
            _ => 0m
        };
    }
}
