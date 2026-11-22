using System.Data;
using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models.ViewModels;
using NexoFELRD.Web.Services;

namespace NexoFELRD.Web.Filters;

// Se ejecuta después de autorización/antiforgery y antes de validar el estado
// comercial: incluso el reenvío de una anulación total encuentra su nota anterior.
public sealed class EmisionIdempotenteFilter(
    ApplicationDbContext db, ProteccionEmision proteccion,
    ILogger<EmisionIdempotenteFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var usuarioId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            context.Result = new ChallengeResult();
            return;
        }

        var solicitud = context.ActionArguments.Values.FirstOrDefault(x =>
            x is FacturaCrearViewModel or NotaCreditoCrearViewModel or NotaDebitoCrearViewModel);

        var datos = solicitud switch
        {
            FacturaCrearViewModel venta => (venta.TokenOperacion, ProteccionEmision.Venta, 0,
                ProteccionEmision.Huella(venta)),
            NotaCreditoCrearViewModel credito => (credito.TokenOperacion, ProteccionEmision.Credito,
                credito.FacturaAfectadaId, ProteccionEmision.Huella(credito)),
            NotaDebitoCrearViewModel debito => (debito.TokenOperacion, ProteccionEmision.Debito,
                debito.FacturaAfectadaId, ProteccionEmision.Huella(debito)),
            _ => throw new InvalidOperationException("El filtro requiere un formulario de emisión.")
        };

        if (!proteccion.ValidarToken(datos.Item1, usuarioId, datos.Item2, datos.Item3, out var clave))
        {
            context.Result = new BadRequestObjectResult(
                "El identificador del formulario no es válido. Abre un formulario nuevo e inténtalo de nuevo.");
            return;
        }

        var cancelacion = context.HttpContext.RequestAborted;
        var recurso = "NexoFELRD:Emision:" + clave.ToString("N");
        await db.Database.OpenConnectionAsync(cancelacion);
        var conexion = db.Database.GetDbConnection();
        // Si la espera se cancela después de conceder el bloqueo, la liberación
        // puede resultar ambigua: en ese caso se descarta la conexión física.
        var descartarConexion = true;
        var bloqueada = false;
        try
        {
            var resultado = await BloquearAsync(conexion, recurso, cancelacion);
            bloqueada = resultado >= 0;
            descartarConexion = false;
            if (!bloqueada)
            {
                context.Result = new ConflictObjectResult(
                    "La solicitud está siendo procesada. Espera unos segundos y reenvía el mismo formulario.");
                return;
            }

            var anterior = await db.Facturas.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ClaveOperacion == clave, cancelacion);
            if (anterior is not null)
            {
                if (anterior.UsuarioId != usuarioId || anterior.HuellaSolicitud != datos.Item4)
                {
                    context.Result = new ConflictObjectResult(
                        "Este formulario ya se utilizó con otros datos. Abre uno nuevo para otra operación.");
                    return;
                }

                if (context.Controller is Controller controller)
                    controller.TempData["Exito"] =
                        "La solicitud ya estaba registrada. Se muestra el comprobante existente sin duplicarlo.";
                context.Result = new RedirectToActionResult("Detalle", "Facturas", new { id = anterior.Id });
                return;
            }

            proteccion.Preparar(clave, datos.Item4);
            await next();
        }
        finally
        {
            try
            {
                if (bloqueada) await LiberarAsync(conexion, recurso);
            }
            catch (Exception ex)
            {
                descartarConexion = true;
                logger.LogError(ex, "No fue posible liberar el bloqueo de emisión; se descarta la conexión.");
            }
            finally
            {
                if (descartarConexion && conexion is SqlConnection sql) SqlConnection.ClearPool(sql);
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    private static async Task<int> BloquearAsync(DbConnection conexion, string recurso, CancellationToken ct)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandTimeout = 15;
        comando.CommandText = """
            DECLARE @resultado int;
            EXEC @resultado = sys.sp_getapplock @Resource = @recurso,
                @LockMode = 'Exclusive', @LockOwner = 'Session',
                @LockTimeout = 10000, @DbPrincipal = 'public';
            SELECT @resultado;
            """;
        AgregarRecurso(comando, recurso);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(ct));
    }

    private static async Task LiberarAsync(DbConnection conexion, string recurso)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandTimeout = 5;
        comando.CommandText = """
            DECLARE @resultado int;
            EXEC @resultado = sys.sp_releaseapplock @Resource = @recurso,
                @LockOwner = 'Session', @DbPrincipal = 'public';
            SELECT @resultado;
            """;
        AgregarRecurso(comando, recurso);
        if (Convert.ToInt32(await comando.ExecuteScalarAsync(CancellationToken.None)) < 0)
            throw new InvalidOperationException("SQL Server no liberó el bloqueo de emisión.");
    }

    private static void AgregarRecurso(DbCommand comando, string recurso)
    {
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "@recurso";
        parametro.DbType = DbType.String;
        parametro.Size = 255;
        parametro.Value = recurso;
        comando.Parameters.Add(parametro);
    }
}
