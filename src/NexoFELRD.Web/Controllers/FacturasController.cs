using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;
using NexoFELRD.Web.Models.ViewModels;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador,Facturador")]
public class FacturasController : Controller
{
    private const decimal LimiteIdentificacionConsumo = 250000m;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<FacturasController> _logger;

    public FacturasController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<FacturasController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
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
                (factura.ENCF != null &&
                 factura.ENCF.Contains(buscar)) ||
                (factura.Cliente != null &&
                 (factura.Cliente.Nombre.Contains(buscar) ||
                  (factura.Cliente.IdentificacionFiscal != null &&
                   factura.Cliente.IdentificacionFiscal.Contains(buscar)))));
        }

        ViewData["Buscar"] = buscar;

        var facturas = await consulta
            .OrderByDescending(factura => factura.FechaEmision)
            .ThenByDescending(factura => factura.Id)
            .ToListAsync();

        return View(facturas);
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var factura = await _context.Facturas
            .AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.FacturaAfectada)
            .Include(x => x.DocumentosRelacionados)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.Producto)
            .Include(x => x.FormasPago)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (factura is null)
        {
            return NotFound();
        }

        return View(factura);
    }

    [HttpGet]
    public async Task<IActionResult> CrearNotaCredito(int id)
    {
        var modelo = new NotaCreditoCrearViewModel
        {
            FacturaAfectadaId = id,
            FechaEmision = DateTime.Today,
            CodigoModificacion =
                CodigoModificacionDgii.CorreccionMontos
        };

        var facturaValida =
            await CargarFacturaAfectadaNotaCreditoAsync(
                modelo);

        if (!facturaValida)
        {
            return NotFound(
                "La factura no existe o no puede ser " +
                "modificada mediante una nota de crédito.");
        }

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearNotaCredito(
    NotaCreditoCrearViewModel modelo)
    {
        var detallesRecibidos = modelo.Detalles?
            .ToList()
            ?? new List<NotaCreditoDetalleViewModel>();

        modelo.Detalles = detallesRecibidos;

        var codigoValido =
            modelo.CodigoModificacion ==
                CodigoModificacionDgii.AnulacionTotal ||
            modelo.CodigoModificacion ==
                CodigoModificacionDgii.CorreccionTexto ||
            modelo.CodigoModificacion ==
                CodigoModificacionDgii.CorreccionMontos;

        if (!codigoValido)
        {
            ModelState.AddModelError(
                nameof(modelo.CodigoModificacion),
                "El código de modificación debe ser 1, 2 o 3.");
        }

        modelo.RazonModificacion =
            modelo.RazonModificacion?.Trim()
            ?? string.Empty;

        if (modelo.RazonModificacion.Length < 5 ||
            modelo.RazonModificacion.Length > 90)
        {
            ModelState.AddModelError(
                nameof(modelo.RazonModificacion),
                "La razón debe contener entre 5 y 90 caracteres.");
        }

        var facturaValida =
            await CargarFacturaAfectadaNotaCreditoAsync(
                modelo);

        if (!facturaValida)
        {
            return NotFound(
                "La factura no existe o no puede ser modificada " +
                "mediante una nota de crédito.");
        }

        if (modelo.FechaEmision == default)
        {
            ModelState.AddModelError(
                nameof(modelo.FechaEmision),
                "La fecha de emisión es obligatoria.");
        }
        else
        {
            if (modelo.FechaEmision.Date >
                DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(modelo.FechaEmision),
                    "La fecha de emisión no puede ser futura.");
            }

            if (modelo.FacturaAfectada is not null &&
                modelo.FechaEmision.Date <
                modelo.FacturaAfectada.FechaEmision.Date)
            {
                ModelState.AddModelError(
                    nameof(modelo.FechaEmision),
                    "La nota de crédito no puede tener una fecha " +
                    "anterior al comprobante afectado.");
            }
        }

        if (modelo.CodigoModificacion ==
                CodigoModificacionDgii.CorreccionTexto &&
            detallesRecibidos.Any(x =>
                x.Cantidad != 0 ||
                x.ReintegrarInventario))
        {
            ModelState.AddModelError(
                string.Empty,
                "La corrección de texto no puede modificar montos " +
                "ni existencias.");
        }

        if (modelo.CodigoModificacion ==
                CodigoModificacionDgii.CorreccionMontos &&
            !detallesRecibidos.Any(x =>
                x.Cantidad > 0))
        {
            ModelState.AddModelError(
                string.Empty,
                "Selecciona al menos una cantidad para acreditar.");
        }

        if (!ModelState.IsValid)
        {
            return View("CrearNotaCredito", modelo);
        }

        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var usuarioNombre =
            usuario.UserName ??
            usuario.Email ??
            "Usuario";

        await using var transaccion =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            /*
             * Se bloquea la factura original para impedir que dos
             * usuarios acrediten simultáneamente el mismo saldo.
             */
            var facturaAfectada =
                await _context.Facturas
                    .FromSqlInterpolated($@"
                    SELECT *
                    FROM [Facturas]
                        WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] =
                        {modelo.FacturaAfectadaId}")
                    .SingleOrDefaultAsync();

            var estadoValido =
                facturaAfectada is not null &&
                (facturaAfectada.Estado ==
                    EstadoFactura.Confirmada ||
                 facturaAfectada.Estado ==
                    EstadoFactura.Generada);

            var tipoValido =
                facturaAfectada is not null &&
                facturaAfectada.TipoComprobante !=
                    TipoComprobante.NotaCredito &&
                facturaAfectada.TipoComprobante !=
                    TipoComprobante.NotaDebito;

            if (facturaAfectada is null ||
                !estadoValido ||
                !tipoValido ||
                string.IsNullOrWhiteSpace(
                    facturaAfectada.ENCF) ||
                facturaAfectada.ENCF.Length != 13)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "La factura fue modificada por otro usuario o " +
                    "ya no puede recibir una nota de crédito.");

                return View(
                    "CrearNotaCredito",
                    modelo);
            }

            await _context.Entry(facturaAfectada)
                .Collection(x => x.Detalles)
                .Query()
                .Include(x => x.Producto)
                .LoadAsync();

            if (facturaAfectada.ClienteId.HasValue)
            {
                await _context.Entry(facturaAfectada)
                    .Reference(x => x.Cliente)
                    .LoadAsync();
            }

            if (modelo.FechaEmision.Date <
                    facturaAfectada.FechaEmision.Date ||
                modelo.FechaEmision.Date >
                    DateTime.Today)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    nameof(modelo.FechaEmision),
                    "La fecha de la nota debe estar comprendida " +
                    "entre la fecha del comprobante y el día actual.");

                return View(
                    "CrearNotaCredito",
                    modelo);
            }

            var anulacionAnterior =
                await _context.Facturas.AnyAsync(x =>
                    x.FacturaAfectadaId ==
                        facturaAfectada.Id &&
                    x.TipoComprobante ==
                        TipoComprobante.NotaCredito &&
                    x.Estado != EstadoFactura.Anulada &&
                    x.CodigoModificacion ==
                        CodigoModificacionDgii.AnulacionTotal);

            if (anulacionAnterior)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "El comprobante ya posee una nota de crédito " +
                    "de anulación total.");

                return View(
                    "CrearNotaCredito",
                    modelo);
            }

            var detallesOriginales =
                facturaAfectada.Detalles
                    .OrderBy(x => x.NumeroLinea)
                    .ToList();

            if (detallesOriginales.Count == 0)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "La factura afectada no contiene líneas de detalle.");

                return View(
                    "CrearNotaCredito",
                    modelo);
            }

            var idsOriginales =
                detallesOriginales
                    .Select(x => x.Id)
                    .ToHashSet();

            var idsDuplicados =
                detallesRecibidos
                    .Where(x =>
                        x.FacturaDetalleAfectadoId > 0)
                    .GroupBy(x =>
                        x.FacturaDetalleAfectadoId)
                    .Where(grupo =>
                        grupo.Count() > 1)
                    .Select(grupo =>
                        grupo.Key)
                    .ToList();

            if (idsDuplicados.Count > 0)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Se recibieron líneas de detalle duplicadas.");
            }

            var contieneDetallesDesconocidos =
                detallesRecibidos.Any(x =>
                    x.FacturaDetalleAfectadoId <= 0 ||
                    !idsOriginales.Contains(
                        x.FacturaDetalleAfectadoId));

            if (contieneDetallesDesconocidos)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Una de las líneas enviadas no pertenece a la " +
                    "factura afectada.");
            }

            if (detallesRecibidos.Any(x =>
                x.Cantidad < 0))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Las cantidades no pueden ser negativas.");
            }

            if (!ModelState.IsValid)
            {
                await transaccion.RollbackAsync();

                return View(
                    "CrearNotaCredito",
                    modelo);
            }

            var entradasPorDetalle =
                detallesRecibidos
                    .GroupBy(x =>
                        x.FacturaDetalleAfectadoId)
                    .ToDictionary(
                        grupo => grupo.Key,
                        grupo => grupo.Last());

            /*
             * Se consultan únicamente notas que realmente afectan
             * montos: código 1 o código 3.
             */
            var acreditaciones =
                await _context.FacturaDetalles
                    .Where(x =>
                        x.FacturaDetalleAfectadoId.HasValue &&
                        x.Factura.FacturaAfectadaId ==
                            facturaAfectada.Id &&
                        x.Factura.TipoComprobante ==
                            TipoComprobante.NotaCredito &&
                        x.Factura.Estado !=
                            EstadoFactura.Anulada &&
                        (x.Factura.CodigoModificacion ==
                            CodigoModificacionDgii.AnulacionTotal ||
                         x.Factura.CodigoModificacion ==
                            CodigoModificacionDgii.CorreccionMontos))
                    .GroupBy(x =>
                        x.FacturaDetalleAfectadoId!.Value)
                    .Select(grupo => new
                    {
                        DetalleId = grupo.Key,

                        Cantidad =
                            grupo.Sum(x => x.Cantidad),

                        Subtotal =
                            grupo.Sum(x => x.Subtotal),

                        Itbis =
                            grupo.Sum(x => x.Itbis)
                    })
                    .ToDictionaryAsync(
                        x => x.DetalleId);

            var codigo =
                modelo.CodigoModificacion!.Value;

            var mayorDeTreintaDias =
                (modelo.FechaEmision.Date -
                 facturaAfectada.FechaEmision.Date)
                .TotalDays > 30;

            var detallesNota =
                new List<FacturaDetalle>();

            var reintegros =
                new List<(
                    Producto Producto,
                    decimal Cantidad)>();

            decimal montoGravado18 = 0m;
            decimal montoGravado16 = 0m;
            decimal montoGravado0 = 0m;
            decimal montoExento = 0m;
            decimal montoNoFacturable = 0m;
            decimal totalItbis = 0m;

            var numeroLinea = 1;

            /*
             * Código 2: corrección de texto.
             * El comprobante debe quedar en cero, pero se conservan
             * líneas de referencia con precio, subtotal e ITBIS en cero.
             */
            if (codigo ==
                CodigoModificacionDgii.CorreccionTexto)
            {
                foreach (var detalleOriginal in
                         detallesOriginales)
                {
                    detallesNota.Add(
                        new FacturaDetalle
                        {
                            ProductoId =
                                detalleOriginal.ProductoId,

                            FacturaDetalleAfectadoId =
                                detalleOriginal.Id,

                            NumeroLinea =
                                numeroLinea++,

                            CodigoProducto =
                                detalleOriginal.CodigoProducto,

                            NombreItem =
                                detalleOriginal.NombreItem,

                            IndicadorBienOServicio =
                                detalleOriginal
                                    .IndicadorBienOServicio,

                            IndicadorFacturacion =
                                detalleOriginal
                                    .IndicadorFacturacion,

                            UnidadMedidaCodigo =
                                detalleOriginal
                                    .UnidadMedidaCodigo,

                            Cantidad =
                                detalleOriginal.Cantidad,

                            PrecioUnitario = 0m,

                            TasaItbis =
                                detalleOriginal.TasaItbis,

                            Descuento = 0m,
                            Recargo = 0m,
                            Itbis = 0m,
                            Subtotal = 0m
                        });
                }
            }
            else
            {
                foreach (var detalleOriginal in
                         detallesOriginales)
                {
                    if (detalleOriginal.Cantidad <= 0)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            $"La línea {detalleOriginal.NumeroLinea} " +
                            "tiene una cantidad original inválida.");

                        continue;
                    }

                    acreditaciones.TryGetValue(
                        detalleOriginal.Id,
                        out var acreditacion);

                    var cantidadAcreditada =
                        acreditacion?.Cantidad ?? 0m;

                    var cantidadDisponible =
                        Math.Max(
                            0m,
                            detalleOriginal.Cantidad -
                            cantidadAcreditada);

                    entradasPorDetalle.TryGetValue(
                        detalleOriginal.Id,
                        out var entrada);

                    var cantidadSeleccionada =
                        codigo ==
                            CodigoModificacionDgii.AnulacionTotal
                            ? cantidadDisponible
                            : entrada?.Cantidad ?? 0m;

                    if (cantidadSeleccionada <= 0)
                    {
                        if (entrada?.ReintegrarInventario ==
                            true)
                        {
                            ModelState.AddModelError(
                                string.Empty,
                                $"Debes indicar una cantidad para " +
                                $"reintegrar {detalleOriginal.NombreItem}.");
                        }

                        continue;
                    }

                    if (cantidadSeleccionada >
                        cantidadDisponible)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            $"La cantidad indicada para " +
                            $"{detalleOriginal.NombreItem} supera " +
                            $"la disponible. Disponible: " +
                            $"{cantidadDisponible:N2}.");

                        continue;
                    }

                    if (entrada?.ReintegrarInventario ==
                            true &&
                        detalleOriginal.IndicadorBienOServicio !=
                            TipoItemDgii.Bien)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            $"{detalleOriginal.NombreItem} es un " +
                            "servicio y no puede reintegrarse al inventario.");

                        continue;
                    }

                    var subtotalAcreditado =
                        acreditacion?.Subtotal ?? 0m;

                    var itbisAcreditado =
                        acreditacion?.Itbis ?? 0m;

                    var subtotalDisponible =
                        Math.Max(
                            0m,
                            detalleOriginal.Subtotal -
                            subtotalAcreditado);

                    var itbisDisponible =
                        Math.Max(
                            0m,
                            detalleOriginal.Itbis -
                            itbisAcreditado);

                    var acreditaSaldoCompleto =
                        cantidadSeleccionada ==
                        cantidadDisponible;

                    var subtotalLinea =
                        acreditaSaldoCompleto
                            ? Redondear(
                                subtotalDisponible)
                            : Redondear(
                                detalleOriginal.Subtotal *
                                cantidadSeleccionada /
                                detalleOriginal.Cantidad);

                    subtotalLinea =
                        Math.Min(
                            subtotalLinea,
                            subtotalDisponible);

                    var itbisCalculado =
                        acreditaSaldoCompleto
                            ? Redondear(
                                itbisDisponible)
                            : Redondear(
                                detalleOriginal.Itbis *
                                cantidadSeleccionada /
                                detalleOriginal.Cantidad);

                    itbisCalculado =
                        Math.Min(
                            itbisCalculado,
                            itbisDisponible);

                    /*
                     * Después de 30 días el ITBIS no disminuye.
                     */
                    var itbisLinea =
                        mayorDeTreintaDias
                            ? 0m
                            : itbisCalculado;

                    switch (
                        detalleOriginal.IndicadorFacturacion)
                    {
                        case 0:
                            montoNoFacturable +=
                                subtotalLinea;
                            break;

                        case 1:
                            montoGravado18 +=
                                subtotalLinea;

                            totalItbis +=
                                itbisLinea;
                            break;

                        case 2:
                            montoGravado16 +=
                                subtotalLinea;

                            totalItbis +=
                                itbisLinea;
                            break;

                        case 3:
                            montoGravado0 +=
                                subtotalLinea;
                            break;

                        case 4:
                            montoExento +=
                                subtotalLinea;
                            break;

                        default:
                            ModelState.AddModelError(
                                string.Empty,
                                $"La línea de " +
                                $"{detalleOriginal.NombreItem} tiene " +
                                "un indicador fiscal inválido.");

                            continue;
                    }

                    detallesNota.Add(
                        new FacturaDetalle
                        {
                            ProductoId =
                                detalleOriginal.ProductoId,

                            FacturaDetalleAfectadoId =
                                detalleOriginal.Id,

                            NumeroLinea =
                                numeroLinea++,

                            CodigoProducto =
                                detalleOriginal.CodigoProducto,

                            NombreItem =
                                detalleOriginal.NombreItem,

                            IndicadorBienOServicio =
                                detalleOriginal
                                    .IndicadorBienOServicio,

                            IndicadorFacturacion =
                                detalleOriginal
                                    .IndicadorFacturacion,

                            UnidadMedidaCodigo =
                                detalleOriginal
                                    .UnidadMedidaCodigo,

                            Cantidad =
                                cantidadSeleccionada,

                            PrecioUnitario =
                                detalleOriginal
                                    .PrecioUnitario,

                            TasaItbis =
                                detalleOriginal.TasaItbis,

                            Descuento = 0m,
                            Recargo = 0m,
                            Itbis = itbisLinea,
                            Subtotal = subtotalLinea
                        });

                    if (entrada?.ReintegrarInventario ==
                            true &&
                        detalleOriginal.IndicadorBienOServicio ==
                            TipoItemDgii.Bien)
                    {
                        if (detalleOriginal.Producto is null)
                        {
                            ModelState.AddModelError(
                                string.Empty,
                                $"No fue posible localizar el producto " +
                                $"{detalleOriginal.NombreItem}.");

                            continue;
                        }

                        reintegros.Add((
                            detalleOriginal.Producto,
                            cantidadSeleccionada));
                    }
                }

                if (detallesNota.Count == 0)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "No existen cantidades disponibles para " +
                        "registrar esta nota de crédito.");
                }
            }

            if (!ModelState.IsValid)
            {
                await transaccion.RollbackAsync();

                await CargarFacturaAfectadaNotaCreditoAsync(
                    modelo);

                return View(
                    "CrearNotaCredito",
                    modelo);
            }

            montoGravado18 =
                Redondear(montoGravado18);

            montoGravado16 =
                Redondear(montoGravado16);

            montoGravado0 =
                Redondear(montoGravado0);

            montoExento =
                Redondear(montoExento);

            montoNoFacturable =
                Redondear(montoNoFacturable);

            totalItbis =
                Redondear(totalItbis);

            var subtotalFiscal =
                Redondear(
                    montoGravado18 +
                    montoGravado16 +
                    montoGravado0 +
                    montoExento);

            var totalNota =
                Redondear(
                    subtotalFiscal +
                    totalItbis);

            var rango =
                await ObtenerRangoDisponibleAsync(
                    TipoComprobante.NotaCredito,
                    modelo.FechaEmision.Date);

            if (rango is null)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "No existe un rango E34 activo, vigente y con " +
                    "secuencias disponibles.");

                return View(
                    "CrearNotaCredito",
                    modelo);
            }

            var secuenciaAsignada =
                rango.ProximaSecuencia;

            var encf =
                $"E{(int)TipoComprobante.NotaCredito:00}" +
                $"{secuenciaAsignada:D10}";

            if (encf.Length != 13)
            {
                throw new InvalidOperationException(
                    "El e-NCF E34 generado no tiene 13 caracteres.");
            }

            var notaCredito =
                new Factura
                {
                    NumeroInterno =
                        $"NC-{DateTime.UtcNow:yyMMddHHmmssfff}",

                    ENCF = encf,

                    RangoComprobanteElectronicoId =
                        rango.Id,

                    FechaVencimientoSecuencia =
                        rango.FechaVencimiento.Date,

                    ClienteId =
                        facturaAfectada.ClienteId,

                    RncComprador =
                        facturaAfectada.RncComprador,

                    IdentificadorExtranjero =
                        facturaAfectada
                            .IdentificadorExtranjero,

                    RazonSocialComprador =
                        facturaAfectada
                            .RazonSocialComprador,

                    CorreoComprador =
                        facturaAfectada.CorreoComprador,

                    DireccionComprador =
                        facturaAfectada.DireccionComprador,

                    PaisComprador =
                        facturaAfectada.PaisComprador,

                    TipoComprobante =
                        TipoComprobante.NotaCredito,

                    Estado =
                        EstadoFactura.Confirmada,

                    FechaEmision =
                        modelo.FechaEmision.Date,

                    TipoPago =
                        facturaAfectada.TipoPago,

                    FechaLimitePago =
                        facturaAfectada.FechaLimitePago,

                    TerminoPago =
                        facturaAfectada.TerminoPago,

                    TipoIngreso =
                        facturaAfectada.TipoIngreso,

                    IndicadorMontoGravado =
                        facturaAfectada
                            .IndicadorMontoGravado,

                    FacturaAfectadaId =
                        facturaAfectada.Id,

                    NCFModificado =
                        facturaAfectada.ENCF,

                    FechaNCFModificado =
                        facturaAfectada.FechaEmision.Date,

                    CodigoModificacion =
                        codigo,

                    RazonModificacion =
                        modelo.RazonModificacion,

                    IndicadorNotaCreditoMayor30Dias =
                        mayorDeTreintaDias
                            ? (byte)1
                            : (byte)0,

                    UsuarioId =
                        usuario.Id,

                    MontoGravado18 =
                        montoGravado18,

                    MontoGravado16 =
                        montoGravado16,

                    MontoGravado0 =
                        montoGravado0,

                    MontoExento =
                        montoExento,

                    MontoNoFacturable =
                        montoNoFacturable,

                    Subtotal =
                        subtotalFiscal,

                    Itbis =
                        totalItbis,

                    Total =
                        totalNota
                };

            foreach (var detalle in detallesNota)
            {
                notaCredito.Detalles.Add(detalle);
            }

            rango.ProximaSecuencia =
                secuenciaAsignada + 1;

            if (rango.ProximaSecuencia >
                rango.SecuenciaFinal)
            {
                rango.Activo = false;
            }

            foreach (var reintegro in reintegros)
            {
                var existenciaAnterior =
                    reintegro.Producto.Existencia;

                var existenciaResultante =
                    existenciaAnterior +
                    reintegro.Cantidad;

                reintegro.Producto.Existencia =
                    existenciaResultante;

                _context.MovimientosInventario.Add(
                    new MovimientoInventario
                    {
                        ProductoId =
                            reintegro.Producto.Id,

                        Factura =
                            notaCredito,

                        Tipo =
                            TipoMovimiento.Entrada,

                        Motivo =
                            codigo ==
                                CodigoModificacionDgii
                                    .AnulacionTotal
                                ? MotivoMovimientoInventario
                                    .AnulacionFactura
                                : MotivoMovimientoInventario
                                    .DevolucionCliente,

                        Cantidad =
                            reintegro.Cantidad,

                        ExistenciaAnterior =
                            existenciaAnterior,

                        ExistenciaResultante =
                            existenciaResultante,

                        Fecha =
                            DateTime.UtcNow,

                        Observacion =
                            $"Reintegro automático por la nota " +
                            $"de crédito {encf}, aplicada al " +
                            $"{facturaAfectada.ENCF}.",

                        UsuarioId =
                            usuario.Id,

                        UsuarioNombre =
                            usuarioNombre
                    });
            }

            if (codigo ==
                CodigoModificacionDgii.AnulacionTotal)
            {
                facturaAfectada.Estado =
                    EstadoFactura.Anulada;
            }

            _context.Facturas.Add(
                notaCredito);

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Exito"] =
                $"La nota de crédito {encf} fue registrada " +
                $"correctamente para el comprobante " +
                $"{facturaAfectada.ENCF}.";

            return RedirectToAction(
                nameof(Detalle),
                new
                {
                    id = notaCredito.Id
                });
        }
        catch (Exception excepcion)
        {
            await transaccion.RollbackAsync();

            _logger.LogError(
                excepcion,
                "Error registrando la nota de crédito para " +
                "la factura {FacturaId}.",
                modelo.FacturaAfectadaId);

            ModelState.AddModelError(
                string.Empty,
                "No fue posible registrar la nota de crédito. " +
                "Inténtalo nuevamente.");

            await CargarFacturaAfectadaNotaCreditoAsync(
                modelo);

            return View(
                "CrearNotaCredito",
                modelo);
        }
    }

    [HttpGet]
    public async Task<IActionResult> CrearNotaDebito(int id)
    {
        var modelo = new NotaDebitoCrearViewModel
        {
            FacturaAfectadaId = id,
            FechaEmision = DateTime.Today
        };

        var facturaValida =
            await CargarFacturaAfectadaNotaDebitoAsync(modelo);

        if (!facturaValida)
        {
            return NotFound(
                "La factura indicada no existe o no puede recibir una nota de débito.");
        }

        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearNotaDebito(
    NotaDebitoCrearViewModel modelo)
    {
        var detallesRecibidos =
            modelo.Detalles?.ToList()
            ?? new List<NotaDebitoDetalleInputViewModel>();

        modelo.Detalles = detallesRecibidos;

        modelo.RazonModificacion =
            modelo.RazonModificacion?.Trim()
            ?? string.Empty;

        foreach (var detalle in detallesRecibidos)
        {
            detalle.Concepto =
                detalle.Concepto?.Trim()
                ?? string.Empty;
        }

        var facturaValida =
            await CargarFacturaAfectadaNotaDebitoAsync(
                modelo);

        if (!facturaValida)
        {
            return NotFound(
                "La factura indicada no existe o no puede " +
                "recibir una nota de débito.");
        }

        if (modelo.FechaEmision == default)
        {
            ModelState.AddModelError(
                nameof(modelo.FechaEmision),
                "La fecha de emisión es obligatoria.");
        }
        else
        {
            if (modelo.FechaEmision.Date >
                DateTime.Today)
            {
                ModelState.AddModelError(
                    nameof(modelo.FechaEmision),
                    "La fecha de emisión no puede ser futura.");
            }

            if (modelo.FacturaAfectada is not null &&
                modelo.FechaEmision.Date <
                modelo.FacturaAfectada.FechaEmision.Date)
            {
                ModelState.AddModelError(
                    nameof(modelo.FechaEmision),
                    "La nota de débito no puede tener una " +
                    "fecha anterior al comprobante afectado.");
            }
        }

        if (modelo.RazonModificacion.Length < 5 ||
            modelo.RazonModificacion.Length > 90)
        {
            ModelState.AddModelError(
                nameof(modelo.RazonModificacion),
                "La razón debe contener entre 5 y 90 caracteres.");
        }

        if (detallesRecibidos.Count == 0)
        {
            ModelState.AddModelError(
                nameof(modelo.Detalles),
                "Agrega al menos un cargo a la nota de débito.");
        }

        foreach (var detalle in detallesRecibidos)
        {
            if (detalle.FacturaDetalleAfectadoId <= 0)
            {
                ModelState.AddModelError(
                    nameof(modelo.Detalles),
                    "Una de las líneas relacionadas no es válida.");
            }

            if (detalle.Concepto.Length < 3 ||
                detalle.Concepto.Length > 80)
            {
                ModelState.AddModelError(
                    nameof(modelo.Detalles),
                    "Cada concepto debe contener entre 3 y 80 caracteres.");
            }

            if (detalle.MontoBase <= 0)
            {
                ModelState.AddModelError(
                    nameof(modelo.Detalles),
                    "Todos los cargos deben tener un monto mayor que cero.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(
                "CrearNotaDebito",
                modelo);
        }

        var usuario =
            await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        await using var transaccion =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            /*
             * Bloquea la factura original para evitar que cambie
             * mientras se registra la nota de débito.
             */
            var facturaAfectada =
                await _context.Facturas
                    .FromSqlInterpolated($@"
                SELECT *
                FROM [Facturas]
                    WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] =
                    {modelo.FacturaAfectadaId}")
                    .SingleOrDefaultAsync();

            var estadoValido =
                facturaAfectada is not null &&
                (facturaAfectada.Estado ==
                    EstadoFactura.Confirmada ||
                 facturaAfectada.Estado ==
                    EstadoFactura.Generada);

            var tipoValido =
                facturaAfectada is not null &&
                (facturaAfectada.TipoComprobante ==
                    TipoComprobante.CreditoFiscal ||
                 facturaAfectada.TipoComprobante ==
                    TipoComprobante.Consumo);

            if (facturaAfectada is null ||
                !estadoValido ||
                !tipoValido ||
                string.IsNullOrWhiteSpace(
                    facturaAfectada.ENCF) ||
                facturaAfectada.ENCF.Length != 13)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "La factura fue modificada por otro usuario " +
                    "o ya no puede recibir una nota de débito.");

                return View(
                    "CrearNotaDebito",
                    modelo);
            }

            await _context.Entry(facturaAfectada)
                .Collection(x => x.Detalles)
                .LoadAsync();

            if (modelo.FechaEmision.Date <
                    facturaAfectada.FechaEmision.Date ||
                modelo.FechaEmision.Date >
                    DateTime.Today)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    nameof(modelo.FechaEmision),
                    "La fecha de la nota debe estar comprendida " +
                    "entre la fecha del comprobante y el día actual.");

                return View(
                    "CrearNotaDebito",
                    modelo);
            }

            var detallesOriginales =
                facturaAfectada.Detalles
                    .OrderBy(x => x.NumeroLinea)
                    .ThenBy(x => x.Id)
                    .ToDictionary(x => x.Id);

            if (detallesOriginales.Count == 0)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "La factura afectada no contiene líneas de detalle.");

                return View(
                    "CrearNotaDebito",
                    modelo);
            }

            var contieneLineasDesconocidas =
                detallesRecibidos.Any(detalle =>
                    !detallesOriginales.ContainsKey(
                        detalle.FacturaDetalleAfectadoId));

            if (contieneLineasDesconocidas)
            {
                ModelState.AddModelError(
                    nameof(modelo.Detalles),
                    "Una de las líneas seleccionadas no pertenece " +
                    "a la factura afectada.");
            }

            if (!ModelState.IsValid)
            {
                await transaccion.RollbackAsync();

                await CargarFacturaAfectadaNotaDebitoAsync(
                    modelo);

                return View(
                    "CrearNotaDebito",
                    modelo);
            }

            decimal montoGravado18 = 0m;
            decimal montoGravado16 = 0m;
            decimal montoGravado0 = 0m;
            decimal montoExento = 0m;
            decimal montoNoFacturable = 0m;
            decimal totalItbis = 0m;

            var detallesNota =
                new List<FacturaDetalle>();

            var numeroLinea = 1;

            foreach (var entrada in detallesRecibidos)
            {
                var detalleOriginal =
                    detallesOriginales[
                        entrada.FacturaDetalleAfectadoId];

                var montoBase =
                    Redondear(entrada.MontoBase);

                if (montoBase <= 0)
                {
                    ModelState.AddModelError(
                        nameof(modelo.Detalles),
                        $"El cargo relacionado con " +
                        $"{detalleOriginal.NombreItem} debe ser " +
                        $"mayor que cero.");

                    continue;
                }

                decimal tasaItbis;
                decimal itbisLinea;

                switch (detalleOriginal.IndicadorFacturacion)
                {
                    case 0:
                        tasaItbis = 0m;
                        itbisLinea = 0m;

                        montoNoFacturable +=
                            montoBase;
                        break;

                    case 1:
                        tasaItbis = 18m;

                        itbisLinea =
                            Redondear(
                                montoBase *
                                tasaItbis /
                                100m);

                        montoGravado18 +=
                            montoBase;

                        totalItbis +=
                            itbisLinea;
                        break;

                    case 2:
                        tasaItbis = 16m;

                        itbisLinea =
                            Redondear(
                                montoBase *
                                tasaItbis /
                                100m);

                        montoGravado16 +=
                            montoBase;

                        totalItbis +=
                            itbisLinea;
                        break;

                    case 3:
                        tasaItbis = 0m;
                        itbisLinea = 0m;

                        montoGravado0 +=
                            montoBase;
                        break;

                    case 4:
                        tasaItbis = 0m;
                        itbisLinea = 0m;

                        montoExento +=
                            montoBase;
                        break;

                    default:
                        ModelState.AddModelError(
                            nameof(modelo.Detalles),
                            $"La línea de " +
                            $"{detalleOriginal.NombreItem} tiene " +
                            $"un indicador fiscal inválido.");

                        continue;
                }

                detallesNota.Add(
                    new FacturaDetalle
                    {
                        ProductoId =
                            detalleOriginal.ProductoId,

                        FacturaDetalleAfectadoId =
                            detalleOriginal.Id,

                        NumeroLinea =
                            numeroLinea++,

                        CodigoProducto =
                            detalleOriginal.CodigoProducto,

                        NombreItem =
                            entrada.Concepto,

                        IndicadorBienOServicio =
                            detalleOriginal
                                .IndicadorBienOServicio,

                        IndicadorFacturacion =
                            detalleOriginal
                                .IndicadorFacturacion,

                        UnidadMedidaCodigo =
                            detalleOriginal
                                .UnidadMedidaCodigo,

                        Cantidad = 1m,

                        PrecioUnitario =
                            montoBase,

                        TasaItbis =
                            tasaItbis,

                        Descuento = 0m,
                        Recargo = 0m,

                        Itbis =
                            itbisLinea,

                        Subtotal =
                            montoBase
                    });
            }

            if (detallesNota.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(modelo.Detalles),
                    "No existen cargos válidos para registrar " +
                    "la nota de débito.");
            }

            if (!ModelState.IsValid)
            {
                await transaccion.RollbackAsync();

                await CargarFacturaAfectadaNotaDebitoAsync(
                    modelo);

                return View(
                    "CrearNotaDebito",
                    modelo);
            }

            montoGravado18 =
                Redondear(montoGravado18);

            montoGravado16 =
                Redondear(montoGravado16);

            montoGravado0 =
                Redondear(montoGravado0);

            montoExento =
                Redondear(montoExento);

            montoNoFacturable =
                Redondear(montoNoFacturable);

            totalItbis =
                Redondear(totalItbis);

            var subtotalFiscal =
                Redondear(
                    montoGravado18 +
                    montoGravado16 +
                    montoGravado0 +
                    montoExento);

            var totalNota =
                Redondear(
                    subtotalFiscal +
                    totalItbis);

            /*
             * Obtiene y bloquea la próxima secuencia E33.
             */
            var rango =
                await ObtenerRangoDisponibleAsync(
                    TipoComprobante.NotaDebito,
                    modelo.FechaEmision.Date);

            if (rango is null)
            {
                await transaccion.RollbackAsync();

                ModelState.AddModelError(
                    string.Empty,
                    "No existe un rango E33 activo, vigente y " +
                    "con secuencias disponibles.");

                return View(
                    "CrearNotaDebito",
                    modelo);
            }

            var secuenciaAsignada =
                rango.ProximaSecuencia;

            var encf =
                $"E{(int)TipoComprobante.NotaDebito:00}" +
                $"{secuenciaAsignada:D10}";

            if (encf.Length != 13)
            {
                throw new InvalidOperationException(
                    "El e-NCF E33 generado no tiene 13 caracteres.");
            }

            var notaDebito =
                new Factura
                {
                    NumeroInterno =
                        $"ND-{DateTime.UtcNow:yyMMddHHmmssfff}",

                    ENCF =
                        encf,

                    RangoComprobanteElectronicoId =
                        rango.Id,

                    FechaVencimientoSecuencia =
                        rango.FechaVencimiento.Date,

                    ClienteId =
                        facturaAfectada.ClienteId,

                    RncComprador =
                        facturaAfectada.RncComprador,

                    IdentificadorExtranjero =
                        facturaAfectada
                            .IdentificadorExtranjero,

                    RazonSocialComprador =
                        facturaAfectada
                            .RazonSocialComprador,

                    CorreoComprador =
                        facturaAfectada.CorreoComprador,

                    DireccionComprador =
                        facturaAfectada.DireccionComprador,

                    PaisComprador =
                        facturaAfectada.PaisComprador,

                    TipoComprobante =
                        TipoComprobante.NotaDebito,

                    Estado =
                        EstadoFactura.Confirmada,

                    FechaEmision =
                        modelo.FechaEmision.Date,

                    TipoPago =
                        facturaAfectada.TipoPago,

                    FechaLimitePago =
                        facturaAfectada.FechaLimitePago,

                    TerminoPago =
                        facturaAfectada.TerminoPago,

                    TipoIngreso =
                        facturaAfectada.TipoIngreso,

                    /*
                     * En la pantalla el usuario introduce el monto
                     * adicional antes del ITBIS.
                     */
                    IndicadorMontoGravado = 0,

                    FacturaAfectadaId =
                        facturaAfectada.Id,

                    NCFModificado =
                        facturaAfectada.ENCF,

                    FechaNCFModificado =
                        facturaAfectada.FechaEmision.Date,

                    CodigoModificacion =
                        CodigoModificacionDgii
                            .CorreccionMontos,

                    RazonModificacion =
                        modelo.RazonModificacion,

                    IndicadorNotaCreditoMayor30Dias =
                        null,

                    UsuarioId =
                        usuario.Id,

                    MontoGravado18 =
                        montoGravado18,

                    MontoGravado16 =
                        montoGravado16,

                    MontoGravado0 =
                        montoGravado0,

                    MontoExento =
                        montoExento,

                    MontoNoFacturable =
                        montoNoFacturable,

                    Subtotal =
                        subtotalFiscal,

                    Itbis =
                        totalItbis,

                    Total =
                        totalNota
                };

            foreach (var detalle in detallesNota)
            {
                notaDebito.Detalles.Add(
                    detalle);
            }

            rango.ProximaSecuencia =
                secuenciaAsignada + 1;

            if (rango.ProximaSecuencia >
                rango.SecuenciaFinal)
            {
                rango.Activo = false;
            }

            /*
             * Una nota de débito aumenta el valor fiscal, pero no
             * representa una salida adicional de mercancía.
             */
            _context.Facturas.Add(
                notaDebito);

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Exito"] =
                $"La nota de débito {encf} fue registrada " +
                $"correctamente para el comprobante " +
                $"{facturaAfectada.ENCF}.";

            return RedirectToAction(
                nameof(Detalle),
                new
                {
                    id = notaDebito.Id
                });
        }
        catch (Exception excepcion)
        {
            await transaccion.RollbackAsync();

            _logger.LogError(
                excepcion,
                "Error registrando la nota de débito para " +
                "la factura {FacturaId}.",
                modelo.FacturaAfectadaId);

            ModelState.AddModelError(
                string.Empty,
                "No fue posible registrar la nota de débito. " +
                "Inténtalo nuevamente.");

            await CargarFacturaAfectadaNotaDebitoAsync(
                modelo);

            return View(
                "CrearNotaDebito",
                modelo);
        }
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
    public async Task<IActionResult> Crear(
        FacturaCrearViewModel modelo)
    {
        modelo.Detalles ??= new();

        ValidarEncabezado(modelo);

        var detallesSolicitados = modelo.Detalles
            .Where(detalle =>
                detalle.ProductoId > 0 &&
                detalle.Cantidad > 0)
            .GroupBy(detalle => detalle.ProductoId)
            .Select(grupo => new
            {
                ProductoId = grupo.Key,
                Cantidad = grupo.Sum(x => x.Cantidad)
            })
            .ToList();

        if (detallesSolicitados.Count == 0)
        {
            ModelState.AddModelError(
                string.Empty,
                "Debes agregar al menos un producto o servicio.");
        }

        Cliente? cliente = null;

        if (modelo.ClienteId.HasValue &&
            modelo.ClienteId.Value > 0)
        {
            cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == modelo.ClienteId.Value &&
                    x.Activo);

            if (cliente is null)
            {
                ModelState.AddModelError(
                    nameof(modelo.ClienteId),
                    "El cliente seleccionado no existe o está inactivo.");
            }
        }

        ValidarCliente(modelo, cliente);

        if (!ModelState.IsValid)
        {
            await CargarCatalogosAsync(modelo);
            return View(modelo);
        }

        var usuario = await _userManager.GetUserAsync(User);

        if (usuario is null)
        {
            return Challenge();
        }

        var usuarioNombre =
            usuario.UserName ??
            usuario.Email ??
            "Usuario";

        await using var transaccion =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            var productosIds = detallesSolicitados
                .Select(x => x.ProductoId)
                .ToList();

            var productos = await _context.Productos
                .Where(producto =>
                    productosIds.Contains(producto.Id) &&
                    producto.Activo)
                .ToDictionaryAsync(producto => producto.Id);

            var lineasPreparadas =
                new List<(
                    Producto Producto,
                    decimal Cantidad,
                    FacturaDetalle Detalle)>();

            decimal montoGravado18 = 0m;
            decimal montoGravado16 = 0m;
            decimal montoGravado0 = 0m;
            decimal montoExento = 0m;
            decimal montoNoFacturable = 0m;
            decimal totalItbis = 0m;

            var numeroLinea = 1;

            foreach (var solicitud in detallesSolicitados)
            {
                if (!productos.TryGetValue(
                    solicitud.ProductoId,
                    out var producto))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"El producto con identificador " +
                        $"{solicitud.ProductoId} no existe o está inactivo.");

                    continue;
                }

                if (!Enum.IsDefined(
                    typeof(TipoItemDgii),
                    producto.IndicadorBienOServicio))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"El producto {producto.Nombre} tiene una " +
                        "clasificación de bien o servicio inválida.");

                    continue;
                }

                if (!CatalogosDgii.EsIndicadorFacturacionValido(
                    producto.IndicadorFacturacion))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"El producto {producto.Nombre} tiene un " +
                        "indicador de facturación inválido.");

                    continue;
                }

                if (!CatalogosDgii.EsUnidadMedidaValida(
                    producto.UnidadMedidaCodigo))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"El producto {producto.Nombre} tiene una " +
                        "unidad de medida inválida.");

                    continue;
                }

                if (producto.IndicadorBienOServicio ==
                        TipoItemDgii.Bien &&
                    producto.UnidadMedidaCodigo is null)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"El bien {producto.Nombre} debe tener una " +
                        "unidad de medida.");

                    continue;
                }

                if (producto.Precio < 0)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"El producto {producto.Nombre} tiene un " +
                        "precio inválido.");

                    continue;
                }

                var tasaEsperada =
                    CatalogosDgii.ObtenerTasaItbis(
                        producto.IndicadorFacturacion);

                if (producto.TasaItbis != tasaEsperada)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"La tasa de ITBIS de {producto.Nombre} no " +
                        "coincide con su indicador de facturación.");

                    continue;
                }

                if (producto.IndicadorBienOServicio ==
                        TipoItemDgii.Bien &&
                    solicitud.Cantidad > producto.Existencia)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        $"No hay existencia suficiente de " +
                        $"{producto.Nombre}. Disponible: " +
                        $"{producto.Existencia:N2}.");

                    continue;
                }

                var montoItem = Redondear(
                    producto.Precio * solicitud.Cantidad);

                var baseImponible = montoItem;

                if (modelo.IndicadorMontoGravado == 1 &&
                    tasaEsperada > 0)
                {
                    baseImponible = Redondear(
                        montoItem /
                        (1m + tasaEsperada / 100m));
                }

                var itbisLinea =
                    producto.IndicadorFacturacion is 1 or 2
                        ? Redondear(
                            baseImponible *
                            tasaEsperada /
                            100m)
                        : 0m;

                switch (producto.IndicadorFacturacion)
                {
                    case 0:
                        montoNoFacturable += montoItem;
                        break;

                    case 1:
                        montoGravado18 += baseImponible;
                        totalItbis += itbisLinea;
                        break;

                    case 2:
                        montoGravado16 += baseImponible;
                        totalItbis += itbisLinea;
                        break;

                    case 3:
                        montoGravado0 += baseImponible;
                        break;

                    case 4:
                        montoExento += montoItem;
                        break;
                }

                var detalle = new FacturaDetalle
                {
                    ProductoId = producto.Id,
                    NumeroLinea = numeroLinea++,
                    CodigoProducto = producto.Codigo,
                    NombreItem = producto.Nombre,
                    IndicadorBienOServicio =
                        producto.IndicadorBienOServicio,
                    IndicadorFacturacion =
                        producto.IndicadorFacturacion,
                    UnidadMedidaCodigo =
                        producto.UnidadMedidaCodigo,
                    Cantidad = solicitud.Cantidad,
                    PrecioUnitario = producto.Precio,
                    TasaItbis = tasaEsperada,
                    Descuento = 0m,
                    Recargo = 0m,
                    Itbis = itbisLinea,
                    Subtotal = baseImponible
                };

                lineasPreparadas.Add((
                    producto,
                    solicitud.Cantidad,
                    detalle));
            }

            if (!ModelState.IsValid)
            {
                await transaccion.RollbackAsync();
                await CargarCatalogosAsync(modelo);

                return View(modelo);
            }

            montoGravado18 = Redondear(montoGravado18);
            montoGravado16 = Redondear(montoGravado16);
            montoGravado0 = Redondear(montoGravado0);
            montoExento = Redondear(montoExento);
            montoNoFacturable = Redondear(montoNoFacturable);
            totalItbis = Redondear(totalItbis);

            var subtotalFiscal = Redondear(
                montoGravado18 +
                montoGravado16 +
                montoGravado0 +
                montoExento);

            var totalFactura = Redondear(
                subtotalFiscal + totalItbis);

            ValidarCompradorPorMonto(
                modelo,
                cliente,
                totalFactura);

            if (!ModelState.IsValid)
            {
                await transaccion.RollbackAsync();
                await CargarCatalogosAsync(modelo);

                return View(modelo);
            }

            var rango = await ObtenerRangoDisponibleAsync(
            modelo.TipoComprobante,
            modelo.FechaEmision.Date);

            if (rango is null)
            {
                var prefijo =
                    $"E{(int)modelo.TipoComprobante:00}";

                ModelState.AddModelError(
                    nameof(modelo.TipoComprobante),
                    $"No existe un rango {prefijo} activo, vigente " +
                    "y con secuencias disponibles.");

                await transaccion.RollbackAsync();
                await CargarCatalogosAsync(modelo);

                return View(modelo);
            }

            var secuenciaAsignada = rango.ProximaSecuencia;

            var encf =
                $"E{(int)modelo.TipoComprobante:00}" +
                $"{secuenciaAsignada:D10}";

            if (encf.Length != 13)
            {
                throw new InvalidOperationException(
                    "El e-NCF generado no tiene la longitud esperada.");
            }

            var factura = new Factura
            {
                NumeroInterno =
                    $"FAC-{DateTime.UtcNow:yyMMddHHmmssfff}",

                ENCF = encf,

                FechaVencimientoSecuencia =
                rango.FechaVencimiento.Date,
                
                            RangoComprobanteElectronicoId =
                rango.Id,
                
                ClienteId = cliente?.Id,
                RncComprador =
                NormalizarIdentificacionFiscal(
                    cliente?.IdentificacionFiscal),
                
                            RazonSocialComprador =
                NormalizarTextoOpcional(cliente?.Nombre),
                
                            CorreoComprador =
                NormalizarTextoOpcional(cliente?.Correo),
                
                            DireccionComprador =
                NormalizarTextoOpcional(cliente?.Direccion),
                TipoComprobante = modelo.TipoComprobante,
                Estado = EstadoFactura.Confirmada,
                FechaEmision = modelo.FechaEmision.Date,
                TipoPago = modelo.TipoPago,

                FechaLimitePago =
                    modelo.TipoPago == TipoPagoDgii.Credito
                        ? modelo.FechaLimitePago?.Date
                        : null,

                TipoIngreso = modelo.TipoIngreso,
                IndicadorMontoGravado =
                    modelo.IndicadorMontoGravado,

                UsuarioId = usuario.Id,
                MontoGravado18 = montoGravado18,
                MontoGravado16 = montoGravado16,
                MontoGravado0 = montoGravado0,
                MontoExento = montoExento,
                MontoNoFacturable = montoNoFacturable,
                Subtotal = subtotalFiscal,
                Itbis = totalItbis,
                Total = totalFactura
            };

            rango.ProximaSecuencia = secuenciaAsignada + 1;

            if (rango.ProximaSecuencia >
                rango.SecuenciaFinal)
            {
                rango.Activo = false;
            }

            foreach (var linea in lineasPreparadas)
            {
                factura.Detalles.Add(linea.Detalle);

                if (linea.Producto.IndicadorBienOServicio !=
                    TipoItemDgii.Bien)
                {
                    continue;
                }

                var existenciaAnterior =
                    linea.Producto.Existencia;

                var existenciaResultante =
                    existenciaAnterior - linea.Cantidad;

                linea.Producto.Existencia =
                    existenciaResultante;

                _context.MovimientosInventario.Add(
                    new MovimientoInventario
                    {
                        ProductoId = linea.Producto.Id,
                        Factura = factura,
                        Tipo = TipoMovimiento.Salida,
                        Motivo = MotivoMovimientoInventario.Venta,
                        Cantidad = linea.Cantidad,
                        ExistenciaAnterior =
                            existenciaAnterior,
                        ExistenciaResultante =
                            existenciaResultante,
                        Fecha = DateTime.UtcNow,
                        Observacion =
                            $"Salida automática por la factura " +
                            $"{factura.NumeroInterno}.",
                        UsuarioId = usuario.Id,
                        UsuarioNombre = usuarioNombre
                    });
            }

            _context.Facturas.Add(factura);

            await _context.SaveChangesAsync();
            await transaccion.CommitAsync();

            TempData["Exito"] =
                $"La factura {factura.NumeroInterno} fue registrada " +
                $"correctamente con el e-NCF {factura.ENCF}.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception excepcion)
        {
            await transaccion.RollbackAsync();

            _logger.LogError(
                excepcion,
                "Error registrando una factura.");

            ModelState.AddModelError(
                string.Empty,
                "No fue posible registrar la factura. " +
                "Inténtalo nuevamente.");

            await CargarCatalogosAsync(modelo);

            return View(modelo);
        }
    }

    private void ValidarEncabezado(
        FacturaCrearViewModel modelo)
    {
        if (!Enum.IsDefined(
            typeof(TipoComprobante),
            modelo.TipoComprobante))
        {
            ModelState.AddModelError(
                nameof(modelo.TipoComprobante),
                "El tipo de comprobante no es válido.");
        }

        if (!Enum.IsDefined(
            typeof(TipoPagoDgii),
            modelo.TipoPago))
        {
            ModelState.AddModelError(
                nameof(modelo.TipoPago),
                "El tipo de pago no es válido.");
        }

        if (!Enum.IsDefined(
            typeof(TipoIngresoDgii),
            modelo.TipoIngreso))
        {
            ModelState.AddModelError(
                nameof(modelo.TipoIngreso),
                "El tipo de ingreso no es válido.");
        }

        if (modelo.TipoComprobante !=
        TipoComprobante.CreditoFiscal &&
    modelo.TipoComprobante !=
        TipoComprobante.Consumo)
        {
            ModelState.AddModelError(
                nameof(modelo.TipoComprobante),
                "Este tipo de comprobante requiere su flujo especializado.");
        }

        if (modelo.IndicadorMontoGravado is not 0 and not 1)
        {
            ModelState.AddModelError(
                nameof(modelo.IndicadorMontoGravado),
                "El indicador de monto gravado debe ser 0 o 1.");
        }

        if (modelo.FechaEmision == default)
        {
            ModelState.AddModelError(
                nameof(modelo.FechaEmision),
                "La fecha de emisión es obligatoria.");
        }
        else if (modelo.FechaEmision.Date > DateTime.Today)
        {
            ModelState.AddModelError(
                nameof(modelo.FechaEmision),
                "La fecha de emisión no puede ser futura.");
        }

        if (modelo.TipoPago == TipoPagoDgii.Credito)
        {
            if (!modelo.FechaLimitePago.HasValue)
            {
                ModelState.AddModelError(
                    nameof(modelo.FechaLimitePago),
                    "Indica la fecha límite para la factura a crédito.");
            }
            else if (
                modelo.FechaLimitePago.Value.Date <
                modelo.FechaEmision.Date)
            {
                ModelState.AddModelError(
                    nameof(modelo.FechaLimitePago),
                    "La fecha límite de pago debe ser igual o " +
                    "posterior a la fecha de emisión.");
            }
        }
        else
        {
            modelo.FechaLimitePago = null;
        }

        if (modelo.TipoComprobante ==
                TipoComprobante.CreditoFiscal &&
            modelo.TipoPago == TipoPagoDgii.Gratuito)
        {
            ModelState.AddModelError(
                nameof(modelo.TipoPago),
                "Una entrega gratuita no puede emitirse como " +
                "comprobante de crédito fiscal.");
        }
    }

    private void ValidarCliente(
        FacturaCrearViewModel modelo,
        Cliente? cliente)
    {
        if (modelo.TipoComprobante ==
                TipoComprobante.CreditoFiscal &&
            cliente is null)
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "El comprobante de crédito fiscal requiere " +
                "un cliente.");
        }

        if (cliente is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(cliente.Nombre))
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "El cliente debe tener nombre o razón social.");
        }

        if (!string.IsNullOrWhiteSpace(
                cliente.IdentificacionFiscal) &&
            !EsIdentificacionFiscalValida(
                cliente.IdentificacionFiscal))
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "El RNC o cédula del cliente no tiene una " +
                "estructura válida.");
        }

        if (modelo.TipoComprobante ==
                TipoComprobante.CreditoFiscal &&
            string.IsNullOrWhiteSpace(
                cliente.IdentificacionFiscal))
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "El comprobante de crédito fiscal requiere " +
                "el RNC o cédula del comprador.");
        }

        var correo =
    NormalizarTextoOpcional(cliente.Correo);

        if (correo is { Length: > 80 })
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "El correo del comprador no puede exceder " +
                "80 caracteres para el comprobante electrónico.");
        }

        var direccion =
            NormalizarTextoOpcional(cliente.Direccion);

        if (direccion is { Length: > 100 })
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "La dirección del comprador no puede exceder " +
                "100 caracteres para el comprobante electrónico.");
        }
    }

    private void ValidarCompradorPorMonto(
        FacturaCrearViewModel modelo,
        Cliente? cliente,
        decimal total)
    {
        if (modelo.TipoComprobante !=
                TipoComprobante.Consumo ||
            total < LimiteIdentificacionConsumo)
        {
            return;
        }

        if (cliente is null)
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "Las facturas de consumo con total igual o " +
                "superior a RD$250,000 requieren identificar " +
                "al comprador.");
            return;
        }

        if (string.IsNullOrWhiteSpace(
                cliente.IdentificacionFiscal) ||
            !EsIdentificacionFiscalValida(
                cliente.IdentificacionFiscal))
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "Para una factura de consumo igual o superior " +
                "a RD$250,000, el comprador debe tener un RNC " +
                "o cédula válido.");
        }

        if (string.IsNullOrWhiteSpace(cliente.Nombre))
        {
            ModelState.AddModelError(
                nameof(modelo.ClienteId),
                "Para una factura de consumo igual o superior " +
                "a RD$250,000 se requiere el nombre o razón " +
                "social del comprador.");
        }
    }

    private async Task<RangoComprobanteElectronico?>
    ObtenerRangoDisponibleAsync(
        TipoComprobante tipoComprobante,
        DateTime fechaEmision)
    {
        var fechaActual = DateTime.Today;

        var rangos = await _context
            .RangosComprobantesElectronicos
            .FromSqlInterpolated($@"
            SELECT *
            FROM [RangosComprobantesElectronicos]
                WITH (UPDLOCK, HOLDLOCK)
            WHERE [TipoComprobante] =
                {(int)tipoComprobante}
              AND [Activo] = 1
              AND [FechaAutorizacion] <=
                {fechaEmision.Date}
              AND [FechaVencimiento] >=
                {fechaActual}
              AND [ProximaSecuencia] >=
                [SecuenciaInicial]
              AND [ProximaSecuencia] <=
                [SecuenciaFinal]")
            .ToListAsync();

        return rangos.SingleOrDefault();
    }

    private async Task<bool> CargarFacturaAfectadaNotaDebitoAsync(
    NotaDebitoCrearViewModel modelo)
    {
        var factura = await _context.Facturas
            .AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.Producto)
            .FirstOrDefaultAsync(x =>
                x.Id == modelo.FacturaAfectadaId);

        if (factura is null ||
            string.IsNullOrWhiteSpace(factura.ENCF) ||
            factura.Estado is not (
                EstadoFactura.Confirmada or
                EstadoFactura.Generada) ||
            factura.TipoComprobante is not (
                TipoComprobante.CreditoFiscal or
                TipoComprobante.Consumo))
        {
            return false;
        }

        var nombreComprador =
            factura.RazonSocialComprador
            ?? factura.Cliente?.Nombre
            ?? "Consumidor final";

        var identificacionComprador =
            factura.RncComprador
            ?? factura.IdentificadorExtranjero
            ?? factura.Cliente?.IdentificacionFiscal
            ?? "No identificado";

        modelo.FacturaAfectada =
            new FacturaAfectadaNotaViewModel
            {
                Id = factura.Id,
                NumeroInterno = factura.NumeroInterno,
                ENCF = factura.ENCF,
                FechaEmision = factura.FechaEmision,
                TipoComprobante = factura.TipoComprobante,
                Comprador = nombreComprador,
                IdentificacionFiscal =
                    identificacionComprador,
                TotalOriginal = factura.Total,
                MontoNoFacturableOriginal =
                    factura.MontoNoFacturable,
                MontoAcreditado = 0m,
                MayorDeTreintaDias = false
            };

        modelo.LineasDisponibles = factura.Detalles
            .OrderBy(x => x.NumeroLinea)
            .ThenBy(x => x.Id)
            .Select(x =>
                new LineaDisponibleNotaDebitoViewModel
                {
                    FacturaDetalleId = x.Id,
                    NumeroLinea = x.NumeroLinea,
                    ProductoId = x.ProductoId,
                    CodigoProducto = x.CodigoProducto,
                    NombreItem = x.NombreItem,
                    IndicadorBienOServicio =
                        x.IndicadorBienOServicio,
                    IndicadorFacturacion =
                        x.IndicadorFacturacion,
                    TasaItbis = x.TasaItbis,
                    UnidadMedidaCodigo =
                        x.UnidadMedidaCodigo,
                    UnidadMedida =
                        ObtenerUnidadMedidaTexto(
                            x.UnidadMedidaCodigo)
                })
            .ToList();

        return modelo.LineasDisponibles.Count > 0;
    }

    private async Task<bool>
    CargarFacturaAfectadaNotaCreditoAsync(
        NotaCreditoCrearViewModel modelo)
    {
        var factura = await _context.Facturas
            .AsNoTracking()
            .Include(x => x.Cliente)
            .Include(x => x.Detalles)
            .FirstOrDefaultAsync(x =>
                x.Id == modelo.FacturaAfectadaId);

        if (factura is null)
        {
            return false;
        }

        var estadoValido =
            factura.Estado == EstadoFactura.Confirmada ||
            factura.Estado == EstadoFactura.Generada;

        var tipoValido =
            factura.TipoComprobante !=
                TipoComprobante.NotaCredito &&
            factura.TipoComprobante !=
                TipoComprobante.NotaDebito;

        if (!estadoValido ||
            !tipoValido ||
            string.IsNullOrWhiteSpace(factura.ENCF) ||
            factura.ENCF.Length != 13)
        {
            return false;
        }

        var cantidadesAcreditadas =
            await _context.FacturaDetalles
                .AsNoTracking()
                .Where(x =>
                    x.FacturaDetalleAfectadoId.HasValue &&
                    x.Factura.FacturaAfectadaId ==
                        factura.Id &&
                    x.Factura.TipoComprobante ==
                        TipoComprobante.NotaCredito &&
                    x.Factura.Estado !=
                        EstadoFactura.Anulada &&
                    (x.Factura.CodigoModificacion ==
                        CodigoModificacionDgii.AnulacionTotal ||
                     x.Factura.CodigoModificacion ==
                        CodigoModificacionDgii.CorreccionMontos))
                .GroupBy(x =>
                    x.FacturaDetalleAfectadoId!.Value)
                .Select(grupo => new
                {
                    DetalleId = grupo.Key,
                    Cantidad = grupo.Sum(x => x.Cantidad)
                })
                .ToDictionaryAsync(
                    x => x.DetalleId,
                    x => x.Cantidad);

        var montoAcreditado =
            await _context.Facturas
                .AsNoTracking()
                .Where(x =>
                    x.FacturaAfectadaId == factura.Id &&
                    x.TipoComprobante ==
                        TipoComprobante.NotaCredito &&
                    x.Estado != EstadoFactura.Anulada &&
                    (x.CodigoModificacion ==
                        CodigoModificacionDgii.AnulacionTotal ||
                     x.CodigoModificacion ==
                        CodigoModificacionDgii.CorreccionMontos))
                .Select(x =>
                    (decimal?)(
                        x.Total +
                        x.MontoNoFacturable))
                .SumAsync() ?? 0m;

        var entradasAnteriores = modelo.Detalles
            .Where(x =>
                x.FacturaDetalleAfectadoId > 0)
            .GroupBy(x =>
                x.FacturaDetalleAfectadoId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Last());

        var fechaNota =
            modelo.FechaEmision == default
                ? DateTime.Today
                : modelo.FechaEmision.Date;

        modelo.FacturaAfectada =
            new FacturaAfectadaNotaViewModel
            {
                Id = factura.Id,
                NumeroInterno = factura.NumeroInterno,
                ENCF = factura.ENCF,
                FechaEmision = factura.FechaEmision,
                TipoComprobante =
                    factura.TipoComprobante,

                Comprador =
                    factura.RazonSocialComprador ??
                    factura.Cliente?.Nombre ??
                    "Consumidor final",

                IdentificacionFiscal =
                    factura.RncComprador ??
                    factura.Cliente?
                        .IdentificacionFiscal ??
                    "No identificado",

                TotalOriginal = factura.Total,

                MontoNoFacturableOriginal =
                    factura.MontoNoFacturable,

                MontoAcreditado = montoAcreditado,

                MayorDeTreintaDias =
                    (fechaNota -
                     factura.FechaEmision.Date)
                    .TotalDays > 30
            };

        modelo.Detalles = factura.Detalles
            .OrderBy(x => x.NumeroLinea)
            .Select(detalle =>
            {
                cantidadesAcreditadas.TryGetValue(
                    detalle.Id,
                    out var cantidadAcreditada);

                entradasAnteriores.TryGetValue(
                    detalle.Id,
                    out var entradaAnterior);

                var cantidadDisponible = Math.Max(
                    0m,
                    detalle.Cantidad -
                    cantidadAcreditada);

                return new NotaCreditoDetalleViewModel
                {
                    FacturaDetalleAfectadoId =
                        detalle.Id,

                    ProductoId = detalle.ProductoId,
                    NumeroLinea = detalle.NumeroLinea,
                    CodigoProducto =
                        detalle.CodigoProducto,
                    NombreItem = detalle.NombreItem,

                    IndicadorBienOServicio =
                        detalle.IndicadorBienOServicio,

                    UnidadMedidaCodigo =
                        detalle.UnidadMedidaCodigo,

                    UnidadMedida =
                        ObtenerUnidadMedidaTexto(
                            detalle.UnidadMedidaCodigo),

                    IndicadorFacturacion =
                        detalle.IndicadorFacturacion,

                    PrecioUnitario =
                        detalle.PrecioUnitario,

                    TasaItbis = detalle.TasaItbis,

                    CantidadOriginal =
                        detalle.Cantidad,

                    CantidadAcreditada =
                        cantidadAcreditada,

                    CantidadDisponible =
                        cantidadDisponible,

                    Cantidad =
                        entradaAnterior?.Cantidad ?? 0m,

                    ReintegrarInventario =
                        entradaAnterior?
                            .ReintegrarInventario ??
                        false
                };
            })
            .ToList();

        return true;
    }

    private static string ObtenerUnidadMedidaTexto(
        byte? codigo)
    {
        if (!codigo.HasValue)
        {
            return "No especificada";
        }

        return CatalogosDgii.UnidadesMedida.TryGetValue(
            codigo.Value,
            out var descripcion)
                ? $"{codigo.Value} - {descripcion}"
                : codigo.Value.ToString();
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
                        : $"{cliente.Nombre} - " +
                          $"{cliente.IdentificacionFiscal}"
            })
            .ToList();

        modelo.Productos = await _context.Productos
            .AsNoTracking()
            .Where(producto =>
                producto.Activo &&
                (producto.IndicadorBienOServicio ==
                    TipoItemDgii.Servicio ||
                 producto.Existencia > 0))
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
                Existencia = producto.Existencia,
                IndicadorBienOServicio =
                    producto.IndicadorBienOServicio,
                UnidadMedidaCodigo =
                    producto.UnidadMedidaCodigo
            })
            .ToListAsync();
    }

    private static decimal Redondear(decimal valor)
    {
        return decimal.Round(
            valor,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static bool EsIdentificacionFiscalValida(
        string? identificacion)
    {
        var digitos = ObtenerSoloDigitos(identificacion);

        return digitos.Length switch
        {
            9 => EsRncValido(digitos),
            11 => EsCedulaValida(digitos),
            _ => false
        };
    }

    private static string? NormalizarIdentificacionFiscal(
    string? identificacion)
    {
        var digitos =
            ObtenerSoloDigitos(identificacion);

        return string.IsNullOrEmpty(digitos)
            ? null
            : digitos;
    }

    private static string? NormalizarTextoOpcional(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }

    private static string ObtenerSoloDigitos(
        string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        return new string(
            valor.Where(char.IsDigit).ToArray());
    }

    private static bool EsRncValido(string rnc)
    {
        int[] pesos = { 7, 9, 8, 6, 5, 4, 3, 2 };

        var suma = 0;

        for (var indice = 0; indice < 8; indice++)
        {
            suma +=
                (rnc[indice] - '0') *
                pesos[indice];
        }

        var verificador = 11 - suma % 11;

        verificador = verificador switch
        {
            10 => 1,
            11 => 2,
            _ => verificador
        };

        return verificador == rnc[8] - '0';
    }

    private static bool EsCedulaValida(string cedula)
    {
        var suma = 0;

        for (var indice = 0; indice < 10; indice++)
        {
            var multiplicador =
                indice % 2 == 0 ? 1 : 2;

            var producto =
                (cedula[indice] - '0') *
                multiplicador;

            if (producto > 9)
            {
                producto -= 9;
            }

            suma += producto;
        }

        var verificador =
            (10 - suma % 10) % 10;

        return verificador ==
               cedula[10] - '0';
    }
}
