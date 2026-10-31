using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador,Facturador")]
public class ProductosController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public ProductosController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? buscar)
    {
        var consulta = _context.Productos
            .AsNoTracking()
            .AsQueryable();

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

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public IActionResult Crear()
    {
        return View(new ProductoCrearViewModel());
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(
        ProductoCrearViewModel formulario)
    {
        Normalizar(formulario);

        if (await _context.Productos.AnyAsync(
                producto =>
                    producto.Codigo == formulario.Codigo))
        {
            ModelState.AddModelError(
                nameof(formulario.Codigo),
                "Ya existe un producto o servicio con este código.");
        }

        if (!ModelState.IsValid)
        {
            return View(formulario);
        }

        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var esBien =
            formulario.IndicadorBienOServicio ==
            TipoItemDgii.Bien;

        var existenciaInicial = esBien
            ? formulario.ExistenciaInicial
            : 0m;

        await using var transaccion =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var producto = new Producto
            {
                Codigo = formulario.Codigo,
                Nombre = formulario.Nombre,
                Descripcion = formulario.Descripcion,
                Precio = formulario.Precio,
                IndicadorFacturacion =
                    formulario.IndicadorFacturacion,
                TasaItbis =
                    CatalogosDgii.ObtenerTasaItbis(
                        formulario.IndicadorFacturacion),
                IndicadorBienOServicio =
                    formulario.IndicadorBienOServicio,
                UnidadMedidaCodigo =
                    formulario.UnidadMedidaCodigo,
                Existencia = existenciaInicial,
                Activo = true
            };

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            if (esBien && existenciaInicial > 0)
            {
                _context.MovimientosInventario.Add(
                    new MovimientoInventario
                    {
                        ProductoId = producto.Id,
                        Tipo = TipoMovimiento.Entrada,
                        Motivo =
                            MotivoMovimientoInventario
                                .InventarioInicial,
                        Cantidad = existenciaInicial,
                        ExistenciaAnterior = 0m,
                        ExistenciaResultante =
                            existenciaInicial,
                        Fecha = DateTime.UtcNow,
                        Observacion =
                            "Inventario inicial registrado " +
                            "al crear el producto.",
                        UsuarioId = usuario.Id,
                        UsuarioNombre =
                            ObtenerNombreUsuario(usuario)
                    });

                await _context.SaveChangesAsync();
            }

            await transaccion.CommitAsync();

            TempData["Exito"] =
                existenciaInicial > 0
                    ? "El producto y su inventario inicial " +
                      "fueron registrados correctamente."
                    : "El producto o servicio fue registrado " +
                      "correctamente.";

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            await transaccion.RollbackAsync();

            ModelState.AddModelError(
                string.Empty,
                "No se pudo registrar el producto. " +
                "Verifica que el código no esté duplicado.");

            return View(formulario);
        }
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var producto = await _context.Productos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (producto is null)
        {
            return NotFound();
        }

        var formulario = new ProductoEditarViewModel
        {
            Id = producto.Id,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            IndicadorFacturacion =
                producto.IndicadorFacturacion,
            IndicadorBienOServicio =
                producto.IndicadorBienOServicio,
            UnidadMedidaCodigo =
                producto.UnidadMedidaCodigo,
            ExistenciaActual = producto.Existencia
        };

        return View(formulario);
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        ProductoEditarViewModel formulario)
    {
        if (id != formulario.Id)
        {
            return BadRequest();
        }

        Normalizar(formulario);

        var producto = await _context.Productos
            .FirstOrDefaultAsync(x => x.Id == id);

        if (producto is null)
        {
            return NotFound();
        }

        if (await _context.Productos.AnyAsync(x =>
                x.Id != id &&
                x.Codigo == formulario.Codigo))
        {
            ModelState.AddModelError(
                nameof(formulario.Codigo),
                "Ya existe otro producto o servicio " +
                "con este código.");
        }

        if (producto.Existencia != 0 &&
            formulario.IndicadorBienOServicio ==
            TipoItemDgii.Servicio)
        {
            ModelState.AddModelError(
                nameof(formulario.IndicadorBienOServicio),
                "No puede convertir este producto en servicio " +
                "mientras tenga existencia. Registre primero " +
                "la salida o el ajuste correspondiente.");
        }

        if (!ModelState.IsValid)
        {
            formulario.ExistenciaActual =
                producto.Existencia;

            return View(formulario);
        }

        producto.Codigo = formulario.Codigo;
        producto.Nombre = formulario.Nombre;
        producto.Descripcion = formulario.Descripcion;
        producto.Precio = formulario.Precio;
        producto.IndicadorFacturacion =
            formulario.IndicadorFacturacion;
        producto.TasaItbis =
            CatalogosDgii.ObtenerTasaItbis(
                formulario.IndicadorFacturacion);
        producto.IndicadorBienOServicio =
            formulario.IndicadorBienOServicio;
        producto.UnidadMedidaCodigo =
            formulario.UnidadMedidaCodigo;

        // La existencia no se modifica desde Productos.
        await _context.SaveChangesAsync();

        TempData["Exito"] =
            "Los datos del producto o servicio " +
            "fueron actualizados.";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id)
    {
        var producto = await _context.Productos
            .FirstOrDefaultAsync(x => x.Id == id);

        if (producto is null)
        {
            return NotFound();
        }

        if (producto.Activo &&
            producto.ControlaInventario &&
            producto.Existencia > 0)
        {
            TempData["Error"] =
                "No puede desactivar un producto que todavía " +
                "tiene existencia. Registre primero la salida " +
                "o el ajuste correspondiente.";

            return RedirectToAction(nameof(Index));
        }

        producto.Activo = !producto.Activo;

        await _context.SaveChangesAsync();

        TempData["Exito"] = producto.Activo
            ? "El producto o servicio fue activado."
            : "El producto o servicio fue desactivado.";

        return RedirectToAction(nameof(Index));
    }

    private static void Normalizar(
        ProductoFormularioViewModel formulario)
    {
        formulario.Codigo =
            formulario.Codigo?.Trim().ToUpperInvariant()
            ?? string.Empty;

        formulario.Nombre =
            formulario.Nombre?.Trim()
            ?? string.Empty;

        formulario.Descripcion =
            string.IsNullOrWhiteSpace(formulario.Descripcion)
                ? null
                : formulario.Descripcion.Trim();
    }

    private static string ObtenerNombreUsuario(
        IdentityUser usuario)
    {
        return usuario.Email ??
               usuario.UserName ??
               usuario.Id;
    }
}
