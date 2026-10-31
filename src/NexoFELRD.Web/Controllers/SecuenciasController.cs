using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class SecuenciasController : Controller
{
    private readonly ApplicationDbContext _context;

    private readonly UserManager<IdentityUser>
        _userManager;

    private readonly ILogger<SecuenciasController>
        _logger;

    public SecuenciasController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<SecuenciasController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rangos = await _context
            .RangosComprobantesElectronicos
            .AsNoTracking()
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.TipoComprobante)
            .ThenByDescending(x => x.FechaCreacion)
            .ToListAsync();

        return View(rangos);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        return View(
            new RangoComprobanteCrearViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(
        RangoComprobanteCrearViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var tipoComprobante =
            modelo.TipoComprobante!.Value;

        await using var transaccion =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var existeSolapamiento =
                await _context
                    .RangosComprobantesElectronicos
                    .AnyAsync(x =>
                        x.TipoComprobante ==
                            tipoComprobante &&
                        modelo.SecuenciaInicial <=
                            x.SecuenciaFinal &&
                        modelo.SecuenciaFinal >=
                            x.SecuenciaInicial);

            if (existeSolapamiento)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "El rango indicado se solapa con otro " +
                    "rango registrado para este tipo de comprobante.");
            }

            if (modelo.ActivarInmediatamente)
            {
                var existeRangoActivo =
                    await _context
                        .RangosComprobantesElectronicos
                        .AnyAsync(x =>
                            x.TipoComprobante ==
                                tipoComprobante &&
                            x.Activo);

                if (existeRangoActivo)
                {
                    ModelState.AddModelError(
                        nameof(
                            modelo.ActivarInmediatamente),
                        "Ya existe un rango activo para este " +
                        "tipo de comprobante.");
                }
            }

            if (!ModelState.IsValid)
            {
                await transaccion.RollbackAsync();
                return View(modelo);
            }

            var usuarioNombre =
                usuario.UserName ??
                usuario.Email ??
                "Usuario";

            var rango =
                new RangoComprobanteElectronico
                {
                    TipoComprobante =
                        tipoComprobante,

                    SecuenciaInicial =
                        modelo.SecuenciaInicial,

                    SecuenciaFinal =
                        modelo.SecuenciaFinal,

                    ProximaSecuencia =
                        modelo.SecuenciaInicial,

                    FechaAutorizacion =
                        modelo.FechaAutorizacion.Date,

                    FechaVencimiento =
                        modelo.FechaVencimiento.Date,

                    Activo =
                        modelo.ActivarInmediatamente,

                    FechaCreacion =
                        DateTime.UtcNow,

                    UsuarioId =
                        usuario.Id,

                    UsuarioNombre =
                        usuarioNombre
                };

            _context
                .RangosComprobantesElectronicos
                .Add(rango);

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Exito"] =
                $"El rango {rango.Prefijo}" +
                $"{rango.SecuenciaInicial:D10} - " +
                $"{rango.Prefijo}" +
                $"{rango.SecuenciaFinal:D10} " +
                "fue registrado correctamente.";

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException excepcion)
        {
            await transaccion.RollbackAsync();

            _logger.LogError(
                excepcion,
                "Error registrando un rango de e-NCF.");

            ModelState.AddModelError(
                string.Empty,
                "No fue posible registrar el rango. " +
                "Verifica que no exista otro rango activo " +
                "o una secuencia duplicada.");

            return View(modelo);
        }
        catch (Exception excepcion)
        {
            await transaccion.RollbackAsync();

            _logger.LogError(
                excepcion,
                "Error inesperado registrando un rango de e-NCF.");

            ModelState.AddModelError(
                string.Empty,
                "Ocurrió un error al registrar el rango.");

            return View(modelo);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(
        int id)
    {
        var rango = await _context
            .RangosComprobantesElectronicos
            .FirstOrDefaultAsync(x => x.Id == id);

        if (rango is null)
        {
            return NotFound();
        }

        if (rango.Activo)
        {
            rango.Activo = false;

            await _context.SaveChangesAsync();

            TempData["Exito"] =
                "El rango fue desactivado.";

            return RedirectToAction(nameof(Index));
        }

        if (rango.Agotado)
        {
            TempData["Error"] =
                "No se puede activar un rango agotado.";

            return RedirectToAction(nameof(Index));
        }

        if (rango.Vencido)
        {
            TempData["Error"] =
                "No se puede activar un rango vencido.";

            return RedirectToAction(nameof(Index));
        }

        var existeOtroActivo =
            await _context
                .RangosComprobantesElectronicos
                .AnyAsync(x =>
                    x.Id != rango.Id &&
                    x.TipoComprobante ==
                        rango.TipoComprobante &&
                    x.Activo);

        if (existeOtroActivo)
        {
            TempData["Error"] =
                "Ya existe otro rango activo para este " +
                "tipo de comprobante.";

            return RedirectToAction(nameof(Index));
        }

        rango.Activo = true;

        try
        {
            await _context.SaveChangesAsync();

            TempData["Exito"] =
                "El rango fue activado.";
        }
        catch (DbUpdateConcurrencyException excepcion)
        {
            _logger.LogWarning(
                excepcion,
                "Conflicto de concurrencia cambiando " +
                "el estado del rango {RangoId}.",
                id);

            TempData["Error"] =
                "El rango fue modificado por otro usuario. " +
                "Actualiza la página e inténtalo nuevamente.";
        }
        catch (DbUpdateException excepcion)
        {
            _logger.LogError(
                excepcion,
                "Error activando el rango {RangoId}.",
                id);

            TempData["Error"] =
                "No fue posible activar el rango.";
        }

        return RedirectToAction(nameof(Index));
    }
}
