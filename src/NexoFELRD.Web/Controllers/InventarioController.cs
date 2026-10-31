using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class InventarioController : Controller
{
    private const decimal LimiteExistenciaBaja = 5m;

    private static readonly TimeZoneInfo ZonaHoraria =
        ObtenerZonaHoraria();

    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<InventarioController> _logger;

    public InventarioController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<InventarioController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? buscar,
        int? productoId,
        TipoMovimiento? tipo,
        DateTime? desde,
        DateTime? hasta)
    {
        buscar = buscar?.Trim();

        bool rangoFechasValido =
            !desde.HasValue ||
            !hasta.HasValue ||
            desde.Value.Date <= hasta.Value.Date;

        if (!rangoFechasValido)
        {
            ModelState.AddModelError(
                string.Empty,
                "La fecha inicial no puede ser mayor que la fecha final.");
        }

        var todosLosProductos = await _context.Productos
            .AsNoTracking()
            .Where(x =>
                x.IndicadorBienOServicio == TipoItemDgii.Bien)
            .OrderByDescending(x => x.Activo)
            .ThenBy(x => x.Nombre)
            .ToListAsync();

        IEnumerable<Producto> productosFiltrados =
            todosLosProductos;

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            productosFiltrados = productosFiltrados.Where(x =>
                x.Codigo.Contains(
                    buscar,
                    StringComparison.OrdinalIgnoreCase) ||
                x.Nombre.Contains(
                    buscar,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (productoId.HasValue && productoId.Value > 0)
        {
            productosFiltrados = productosFiltrados.Where(
                x => x.Id == productoId.Value);
        }

        var consultaMovimientos = _context.MovimientosInventario
            .AsNoTracking()
            .Include(x => x.Producto)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            consultaMovimientos =
                consultaMovimientos.Where(x =>
                    x.Producto.Codigo.Contains(buscar) ||
                    x.Producto.Nombre.Contains(buscar) ||
                    (x.Observacion != null &&
                     x.Observacion.Contains(buscar)) ||
                    x.UsuarioNombre.Contains(buscar));
        }

        if (productoId.HasValue && productoId.Value > 0)
        {
            consultaMovimientos =
                consultaMovimientos.Where(
                    x => x.ProductoId == productoId.Value);
        }

        if (tipo.HasValue &&
            Enum.IsDefined(typeof(TipoMovimiento), tipo.Value))
        {
            consultaMovimientos =
                consultaMovimientos.Where(x => x.Tipo == tipo.Value);
        }

        if (rangoFechasValido && desde.HasValue)
        {
            DateTime desdeUtc =
                ConvertirFechaLocalAUtc(desde.Value.Date);

            consultaMovimientos =
                consultaMovimientos.Where(x => x.Fecha >= desdeUtc);
        }

        if (rangoFechasValido && hasta.HasValue)
        {
            DateTime hastaExclusivaUtc =
                ConvertirFechaLocalAUtc(
                    hasta.Value.Date.AddDays(1));

            consultaMovimientos =
                consultaMovimientos.Where(
                    x => x.Fecha < hastaExclusivaUtc);
        }

        var movimientos = await consultaMovimientos
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .Take(300)
            .ToListAsync();

        var modelo = new InventarioIndexViewModel
        {
            Buscar = buscar,
            ProductoId = productoId,
            Tipo = tipo,
            Desde = desde,
            Hasta = hasta,

            Productos = productosFiltrados
                .Select(x => new InventarioProductoViewModel
                {
                    Id = x.Id,
                    Codigo = x.Codigo,
                    Nombre = x.Nombre,
                    UnidadMedida =
                        ObtenerUnidadMedida(
                            x.UnidadMedidaCodigo),
                    Existencia = x.Existencia,
                    Activo = x.Activo,
                    ExistenciaBaja =
                        x.Activo &&
                        x.Existencia <= LimiteExistenciaBaja
                })
                .ToList(),

            Movimientos = movimientos
                .Select(x =>
                    new MovimientoInventarioListadoViewModel
                    {
                        Id = x.Id,
                        ProductoId = x.ProductoId,
                        CodigoProducto = x.Producto.Codigo,
                        NombreProducto = x.Producto.Nombre,
                        UnidadMedida =
                            ObtenerUnidadMedida(
                                x.Producto.UnidadMedidaCodigo),
                        Tipo = x.Tipo,
                        Motivo = x.Motivo,
                        Cantidad = x.Cantidad,
                        ExistenciaAnterior =
                            x.ExistenciaAnterior,
                        ExistenciaResultante =
                            x.ExistenciaResultante,
                        Fecha = ConvertirFechaUtcALocal(
                            x.Fecha),
                        UsuarioNombre = x.UsuarioNombre,
                        Observacion = x.Observacion,
                        FacturaId = x.FacturaId
                    })
                .ToList(),

            ProductosFiltro = todosLosProductos
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = $"{x.Codigo} - {x.Nombre}",
                    Selected = productoId == x.Id
                })
                .ToList(),

            TiposFiltro = Enum
                .GetValues<TipoMovimiento>()
                .Select(x => new SelectListItem
                {
                    Value = ((int)x).ToString(),
                    Text = ObtenerNombreEnum(x),
                    Selected = tipo == x
                })
                .ToList()
        };

        return View(modelo);
    }

    [HttpGet]
    public async Task<IActionResult> Registrar()
    {
        var modelo = new MovimientoInventarioCrearViewModel();

        await PrepararFormularioAsync(modelo);

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(
        MovimientoInventarioCrearViewModel modelo)
    {
        ValidarFormulario(modelo);

        if (!ModelState.IsValid)
        {
            await PrepararFormularioAsync(modelo);
            return View(modelo);
        }

        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        await using var transaccion =
            await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        try
        {
            var producto = await _context.Productos
                .SingleOrDefaultAsync(
                    x => x.Id == modelo.ProductoId);

            if (producto is null)
            {
                ModelState.AddModelError(
                    nameof(modelo.ProductoId),
                    "El producto seleccionado no existe.");
            }
            else if (!producto.Activo)
            {
                ModelState.AddModelError(
                    nameof(modelo.ProductoId),
                    "No se pueden registrar movimientos " +
                    "para un producto inactivo.");
            }
            else if (
                producto.IndicadorBienOServicio !=
                TipoItemDgii.Bien)
            {
                ModelState.AddModelError(
                    nameof(modelo.ProductoId),
                    "Los servicios no controlan existencia.");
            }
            else if (
                !CatalogosDgii.EsUnidadMedidaValida(
                    producto.UnidadMedidaCodigo) ||
                producto.UnidadMedidaCodigo is null)
            {
                ModelState.AddModelError(
                    nameof(modelo.ProductoId),
                    "El producto no tiene una unidad de " +
                    "medida válida.");
            }

            if (!ModelState.IsValid || producto is null)
            {
                await transaccion.RollbackAsync();
                await PrepararFormularioAsync(modelo);
                return View(modelo);
            }

            TipoMovimiento tipo =
                modelo.Tipo!.Value;

            MotivoMovimientoInventario motivo =
                modelo.Motivo!.Value;

            decimal cantidad =
                decimal.Round(
                    modelo.Cantidad,
                    2,
                    MidpointRounding.AwayFromZero);

            decimal existenciaAnterior =
                producto.Existencia;

            bool aumentaExistencia =
                tipo == TipoMovimiento.Entrada ||
                tipo == TipoMovimiento.AjustePositivo;

            decimal existenciaResultante =
                aumentaExistencia
                    ? existenciaAnterior + cantidad
                    : existenciaAnterior - cantidad;

            if (existenciaResultante < 0)
            {
                ModelState.AddModelError(
                    nameof(modelo.Cantidad),
                    $"Existencia insuficiente. Disponible: " +
                    $"{existenciaAnterior:N2}.");

                await transaccion.RollbackAsync();
                await PrepararFormularioAsync(modelo);
                return View(modelo);
            }

            producto.Existencia = existenciaResultante;

            var movimiento = new MovimientoInventario
            {
                ProductoId = producto.Id,
                FacturaId = null,
                Tipo = tipo,
                Motivo = motivo,
                Cantidad = cantidad,
                ExistenciaAnterior = existenciaAnterior,
                ExistenciaResultante = existenciaResultante,
                Fecha = DateTime.UtcNow,
                Observacion =
                    string.IsNullOrWhiteSpace(modelo.Observacion)
                        ? null
                        : modelo.Observacion.Trim(),
                UsuarioId = usuario.Id,
                UsuarioNombre =
                    usuario.UserName ??
                    usuario.Email ??
                    "Usuario"
            };

            _context.MovimientosInventario.Add(movimiento);

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Exito"] =
                "El movimiento de inventario fue registrado " +
                "correctamente.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaccion.RollbackAsync();

            _logger.LogError(
                ex,
                "Error registrando un movimiento de inventario.");

            ModelState.AddModelError(
                string.Empty,
                "No fue posible registrar el movimiento. " +
                "Intenta nuevamente.");

            await PrepararFormularioAsync(modelo);

            return View(modelo);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerProducto(int id)
    {
        var producto = await _context.Productos
            .AsNoTracking()
            .Where(x =>
                x.Id == id &&
                x.Activo &&
                x.IndicadorBienOServicio ==
                    TipoItemDgii.Bien)
            .Select(x => new
            {
                x.Id,
                x.Codigo,
                x.Nombre,
                x.Existencia,
                x.UnidadMedidaCodigo
            })
            .SingleOrDefaultAsync();

        if (producto is null)
        {
            return NotFound();
        }

        return Json(new
        {
            producto.Id,
            producto.Codigo,
            producto.Nombre,
            producto.Existencia,
            unidadMedida =
                ObtenerUnidadMedida(
                    producto.UnidadMedidaCodigo)
        });
    }

    private void ValidarFormulario(
        MovimientoInventarioCrearViewModel modelo)
    {
        if (modelo.Cantidad > 0 &&
            decimal.Round(modelo.Cantidad, 2) !=
            modelo.Cantidad)
        {
            ModelState.AddModelError(
                nameof(modelo.Cantidad),
                "La cantidad admite un máximo de dos decimales.");
        }

        if (modelo.Tipo.HasValue &&
            !Enum.IsDefined(
                typeof(TipoMovimiento),
                modelo.Tipo.Value))
        {
            ModelState.AddModelError(
                nameof(modelo.Tipo),
                "El tipo de movimiento no es válido.");
        }

        if (modelo.Motivo.HasValue &&
            !Enum.IsDefined(
                typeof(MotivoMovimientoInventario),
                modelo.Motivo.Value))
        {
            ModelState.AddModelError(
                nameof(modelo.Motivo),
                "El motivo seleccionado no es válido.");
        }

        if (modelo.Tipo.HasValue &&
            modelo.Motivo.HasValue &&
            !EsCombinacionManualValida(
                modelo.Tipo.Value,
                modelo.Motivo.Value))
        {
            ModelState.AddModelError(
                nameof(modelo.Motivo),
                "El motivo no corresponde con el tipo " +
                "de movimiento seleccionado.");
        }
    }

    private async Task PrepararFormularioAsync(
        MovimientoInventarioCrearViewModel modelo)
    {
        var productos = await _context.Productos
            .AsNoTracking()
            .Where(x =>
                x.Activo &&
                x.IndicadorBienOServicio ==
                    TipoItemDgii.Bien)
            .OrderBy(x => x.Nombre)
            .ToListAsync();

        modelo.Productos = productos
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text =
                    $"{x.Codigo} - {x.Nombre} | " +
                    $"{x.Existencia:N2} " +
                    $"{ObtenerUnidadMedida(x.UnidadMedidaCodigo)}",
                Selected = modelo.ProductoId == x.Id
            })
            .ToList();

        modelo.Tipos = Enum
            .GetValues<TipoMovimiento>()
            .Select(x => new SelectListItem
            {
                Value = ((int)x).ToString(),
                Text = ObtenerNombreEnum(x),
                Selected = modelo.Tipo == x
            })
            .ToList();

        MotivoMovimientoInventario[] motivosManuales =
        {
            MotivoMovimientoInventario.Compra,
            MotivoMovimientoInventario.DevolucionCliente,
            MotivoMovimientoInventario.Merma,
            MotivoMovimientoInventario.Deterioro,
            MotivoMovimientoInventario.DevolucionProveedor,
            MotivoMovimientoInventario.ConteoFisico,
            MotivoMovimientoInventario.Correccion,
            MotivoMovimientoInventario.Otro
        };

        modelo.Motivos = motivosManuales
            .Select(x => new SelectListItem
            {
                Value = ((int)x).ToString(),
                Text = ObtenerNombreEnum(x),
                Selected = modelo.Motivo == x
            })
            .ToList();
    }

    private static bool EsCombinacionManualValida(
        TipoMovimiento tipo,
        MotivoMovimientoInventario motivo)
    {
        return tipo switch
        {
            TipoMovimiento.Entrada =>
                motivo is
                    MotivoMovimientoInventario.Compra or
                    MotivoMovimientoInventario
                        .DevolucionCliente or
                    MotivoMovimientoInventario.Otro,

            TipoMovimiento.Salida =>
                motivo is
                    MotivoMovimientoInventario.Merma or
                    MotivoMovimientoInventario.Deterioro or
                    MotivoMovimientoInventario
                        .DevolucionProveedor or
                    MotivoMovimientoInventario.Otro,

            TipoMovimiento.AjustePositivo or
            TipoMovimiento.AjusteNegativo =>
                motivo is
                    MotivoMovimientoInventario.ConteoFisico or
                    MotivoMovimientoInventario.Correccion or
                    MotivoMovimientoInventario.Otro,

            _ => false
        };
    }

    private static string ObtenerUnidadMedida(byte? codigo)
    {
        if (codigo.HasValue &&
            CatalogosDgii.UnidadesMedida.TryGetValue(
                codigo.Value,
                out string? nombre))
        {
            return $"{codigo.Value} - {nombre}";
        }

        return "Sin unidad";
    }

    private static string ObtenerNombreEnum<TEnum>(
        TEnum valor)
        where TEnum : struct, Enum
    {
        MemberInfo? miembro = typeof(TEnum)
            .GetMember(valor.ToString())
            .FirstOrDefault();

        return miembro?
            .GetCustomAttribute<DisplayAttribute>()?
            .GetName() ??
            valor.ToString();
    }

    private static TimeZoneInfo ObtenerZonaHoraria()
    {
        string[] identificadores =
        {
            "America/Santo_Domingo",
            "SA Western Standard Time"
        };

        foreach (string identificador in identificadores)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    identificador);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static DateTime ConvertirFechaLocalAUtc(
        DateTime fecha)
    {
        DateTime fechaSinZona =
            DateTime.SpecifyKind(
                fecha,
                DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(
            fechaSinZona,
            ZonaHoraria);
    }

    private static DateTime ConvertirFechaUtcALocal(
        DateTime fecha)
    {
        DateTime fechaUtc =
            DateTime.SpecifyKind(
                fecha,
                DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(
            fechaUtc,
            ZonaHoraria);
    }
}
