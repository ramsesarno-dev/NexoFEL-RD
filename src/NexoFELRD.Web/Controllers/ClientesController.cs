using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador,Facturador")]
public class ClientesController : Controller
{
    private readonly ApplicationDbContext _context;

    public ClientesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? buscar)
    {
        var consulta = _context.Clientes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            buscar = buscar.Trim();
            consulta = consulta.Where(cliente =>
                cliente.Nombre.Contains(buscar) ||
                (cliente.IdentificacionFiscal != null &&
                 cliente.IdentificacionFiscal.Contains(buscar)));
        }

        ViewData["Buscar"] = buscar;

        var clientes = await consulta
            .OrderByDescending(cliente => cliente.Activo)
            .ThenBy(cliente => cliente.Nombre)
            .ToListAsync();

        return View(clientes);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View(new Cliente());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(Cliente cliente)
    {
        Normalizar(cliente);

        if (!string.IsNullOrWhiteSpace(cliente.IdentificacionFiscal) &&
            await _context.Clientes.AnyAsync(x =>
                x.IdentificacionFiscal == cliente.IdentificacionFiscal))
        {
            ModelState.AddModelError(
                nameof(Cliente.IdentificacionFiscal),
                "Ya existe un cliente con esta identificación fiscal.");
        }

        if (!ModelState.IsValid)
        {
            return View(cliente);
        }

        cliente.Id = 0;
        cliente.Activo = true;
        cliente.FechaCreacion = DateTime.UtcNow;

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        TempData["Exito"] = "El cliente fue registrado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);

        if (cliente is null)
        {
            return NotFound();
        }

        return View(cliente);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(int id, Cliente formulario)
    {
        if (id != formulario.Id)
        {
            return BadRequest();
        }

        Normalizar(formulario);

        if (!string.IsNullOrWhiteSpace(formulario.IdentificacionFiscal) &&
            await _context.Clientes.AnyAsync(x =>
                x.Id != id &&
                x.IdentificacionFiscal == formulario.IdentificacionFiscal))
        {
            ModelState.AddModelError(
                nameof(Cliente.IdentificacionFiscal),
                "Ya existe otro cliente con esta identificación fiscal.");
        }

        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var cliente = await _context.Clientes.FindAsync(id);

        if (cliente is null)
        {
            return NotFound();
        }

        cliente.Nombre = formulario.Nombre;
        cliente.IdentificacionFiscal = formulario.IdentificacionFiscal;
        cliente.Telefono = formulario.Telefono;
        cliente.Correo = formulario.Correo;
        cliente.Direccion = formulario.Direccion;

        await _context.SaveChangesAsync();

        TempData["Exito"] = "Los datos del cliente fueron actualizados.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);

        if (cliente is null)
        {
            return NotFound();
        }

        cliente.Activo = !cliente.Activo;
        await _context.SaveChangesAsync();

        TempData["Exito"] = cliente.Activo
            ? "El cliente fue activado."
            : "El cliente fue desactivado.";

        return RedirectToAction(nameof(Index));
    }

    private static void Normalizar(Cliente cliente)
    {
        cliente.Nombre = cliente.Nombre?.Trim() ?? string.Empty;
        cliente.IdentificacionFiscal = LimpiarOpcional(cliente.IdentificacionFiscal);
        cliente.Telefono = FormatearTelefono(cliente.Telefono);
        cliente.Correo = LimpiarOpcional(cliente.Correo);
        cliente.Direccion = LimpiarOpcional(cliente.Direccion);
    }

    private static string? LimpiarOpcional(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
    private static string? FormatearTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return null;
        }

        var digitos = new string(
            telefono.Where(char.IsDigit).ToArray()
        );

        return digitos.Length == 10
            ? $"{digitos[..3]}-{digitos[3..6]}-{digitos[6..]}"
            : telefono.Trim();
    }
}
