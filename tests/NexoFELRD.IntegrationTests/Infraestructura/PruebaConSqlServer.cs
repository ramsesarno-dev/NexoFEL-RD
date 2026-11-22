using System.Globalization;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexoFELRD.Web.Data;
using NexoFELRD.Web.Models;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace NexoFELRD.IntegrationTests.Infraestructura;

public abstract class PruebaConSqlServer : IAsyncLifetime
{
    static PruebaConSqlServer()
    {
        // Los numerosos hosts aislados no necesitan notificaciones de edición.
        // El sondeo evita agotar las instancias inotify de Linux; no cambia
        // permisos del sistema ni la configuración de la aplicación normal.
        Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
    }

    protected AplicacionDePruebas App { get; private set; } = null!;
    protected HttpClient Administrador { get; private set; } = null!;
    protected HttpClient Facturador { get; private set; } = null!;
    protected HttpClient Anonimo { get; private set; } = null!;
    protected int ProductoId { get; private set; }
    protected static DateTime Hoy => DateTime.Today;
    private ConexionPruebas? _conexion;
    private bool _baseCreada;
    private readonly List<HttpClient> _clientes = new();
    private const string ClavePruebas = "SoloPruebas!2026";

    public async Task InitializeAsync()
    {
        _conexion = ConexionPruebas.Crear(Environment.GetEnvironmentVariable(ConexionPruebas.Variable));
        await _conexion.EsperarServidorAsync();
        App = new AplicacionDePruebas(_conexion.Cadena);
        await ConDbAsync(async db =>
        {
            // Cada caso crea una base nueva; se prueban las migraciones publicadas.
            _baseCreada = true;
            await db.Database.MigrateAsync();
        });

        using (var scope = App.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            foreach (var rol in new[] { "Administrador", "Facturador" })
            {
                var correo = rol.ToLowerInvariant() + "@nexofel.test";
                var usuario = new IdentityUser { UserName = correo, Email = correo, EmailConfirmed = true };
                var crear = await users.CreateAsync(usuario, ClavePruebas);
                Assert.True(crear.Succeeded, string.Join("; ", crear.Errors.Select(x => x.Description)));
                var asignar = await users.AddToRoleAsync(usuario, rol);
                Assert.True(asignar.Succeeded, string.Join("; ", asignar.Errors.Select(x => x.Description)));
            }
        }

        await ConDbAsync(async db =>
        {
            var usuario = await db.Users.SingleAsync(x => x.Email == "administrador@nexofel.test");
            var producto = new Producto
            {
                Codigo = "QA-PROD-01", Nombre = "Producto simulado", Precio = 27m,
                Existencia = 20m, IndicadorFacturacion = 2, TasaItbis = 16m,
                IndicadorBienOServicio = TipoItemDgii.Bien, UnidadMedidaCodigo = 43, Activo = true
            };
            db.Productos.Add(producto);
            foreach (var tipo in new[] { TipoComprobante.CreditoFiscal, TipoComprobante.Consumo,
                TipoComprobante.NotaCredito, TipoComprobante.NotaDebito })
                db.RangosComprobantesElectronicos.Add(new RangoComprobanteElectronico
                {
                    TipoComprobante = tipo, SecuenciaInicial = 1, SecuenciaFinal = 1000,
                    ProximaSecuencia = 1, FechaAutorizacion = Hoy.AddYears(-1),
                    FechaVencimiento = Hoy.AddYears(1), Activo = true,
                    UsuarioId = usuario.Id, UsuarioNombre = usuario.Email!
                });
            await db.SaveChangesAsync();
            ProductoId = producto.Id;
        });

        Administrador = await CrearClienteAutenticadoAsync("Administrador");
        Facturador = await CrearClienteAutenticadoAsync("Facturador");
        Anonimo = RegistrarCliente(App.CrearCliente());
    }

    protected async Task<HttpClient> CrearClienteAutenticadoAsync(string rol, AplicacionDePruebas? app = null)
    {
        var cliente = RegistrarCliente((app ?? App).CrearCliente());
        var datos = new Dictionary<string, string>
        {
            ["Input.Email"] = rol.ToLowerInvariant() + "@nexofel.test",
            ["Input.Password"] = ClavePruebas,
            ["Input.RememberMe"] = "false"
        };
        using var respuesta = await EnviarFormularioAsync(
            cliente, "/Identity/Account/Login", "/Identity/Account/Login", datos);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.DoesNotContain("Login", respuesta.Headers.Location?.OriginalString ?? string.Empty);
        return cliente;
    }

    private HttpClient RegistrarCliente(HttpClient cliente)
    {
        _clientes.Add(cliente);
        return cliente;
    }

    protected async Task ConDbAsync(Func<ApplicationDbContext, Task> accion)
    {
        using var scope = App.Services.CreateScope();
        await accion(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    protected async Task<T> LeerDbAsync<T>(Func<ApplicationDbContext, Task<T>> consulta)
    {
        using var scope = App.Services.CreateScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    protected static async Task<Dictionary<string, string>> PrepararFormularioAsync(
        HttpClient cliente, string ruta, IDictionary<string, string> valores)
    {
        using var pagina = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var documento = await new HtmlParser().ParseDocumentAsync(await pagina.Content.ReadAsStringAsync());
        var token = documento.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        Assert.False(string.IsNullOrWhiteSpace(token), "El formulario debe conservar su protección antifalsificación.");
        var resultado = new Dictionary<string, string>(valores) { ["__RequestVerificationToken"] = token! };
        // Se obtiene del formulario real; nunca se inventa una clave desde la prueba.
        var operacion = documento.QuerySelector("input[name='TokenOperacion']")?.GetAttribute("value");
        if (operacion is not null && !resultado.ContainsKey("TokenOperacion"))
            resultado["TokenOperacion"] = operacion;
        return resultado;
    }

    protected static async Task<HttpResponseMessage> EnviarFormularioAsync(
        HttpClient cliente, string rutaGet, string rutaPost, IDictionary<string, string> valores)
    {
        var datos = await PrepararFormularioAsync(cliente, rutaGet, valores);
        return await cliente.PostAsync(rutaPost, new FormUrlEncodedContent(datos));
    }

    protected Dictionary<string, string> DatosVenta(decimal cantidad, DateTime? fecha = null,
        bool precioIncluyeItbis = true) => new()
    {
        ["TipoComprobante"] = "32", ["FechaEmision"] = (fecha ?? Hoy).ToString("yyyy-MM-dd"),
        ["TipoPago"] = "1", ["TipoIngreso"] = "1",
        ["IndicadorMontoGravado"] = precioIncluyeItbis ? "1" : "0",
        ["Detalles[0].ProductoId"] = ProductoId.ToString(CultureInfo.InvariantCulture),
        ["Detalles[0].Cantidad"] = cantidad.ToString(CultureInfo.InvariantCulture)
    };

    protected Task<HttpResponseMessage> VenderAsync(decimal cantidad, DateTime? fecha = null,
        bool precioIncluyeItbis = true) => EnviarFormularioAsync(
            Administrador, "/Facturas/Crear", "/Facturas/Crear", DatosVenta(cantidad, fecha, precioIncluyeItbis));

    protected async Task<Factura> VentaConfirmadaAsync(decimal cantidad = 6m, DateTime? fecha = null)
    {
        using var respuesta = await VenderAsync(cantidad, fecha);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return await LeerDbAsync(db => db.Facturas.AsNoTracking().Include(x => x.Detalles)
            .Where(x => x.TipoComprobante == TipoComprobante.Consumo)
            .OrderByDescending(x => x.Id).FirstAsync());
    }

    protected static Dictionary<string, string> DatosCredito(Factura original, decimal cantidad,
        bool reintegrar, CodigoModificacionDgii codigo = CodigoModificacionDgii.CorreccionMontos,
        DateTime? fecha = null) => new()
    {
        ["FacturaAfectadaId"] = original.Id.ToString(CultureInfo.InvariantCulture),
        ["FechaEmision"] = (fecha ?? Hoy).ToString("yyyy-MM-dd"),
        ["CodigoModificacion"] = ((int)codigo).ToString(CultureInfo.InvariantCulture),
        ["RazonModificacion"] = "Ajuste con datos simulados para prueba automatizada.",
        ["Detalles[0].FacturaDetalleAfectadoId"] = original.Detalles.Single().Id.ToString(CultureInfo.InvariantCulture),
        ["Detalles[0].Cantidad"] = cantidad.ToString(CultureInfo.InvariantCulture),
        ["Detalles[0].ReintegrarInventario"] = reintegrar ? "true" : "false"
    };

    protected Task<HttpResponseMessage> AcreditarAsync(Factura original, decimal cantidad, bool reintegrar,
        CodigoModificacionDgii codigo = CodigoModificacionDgii.CorreccionMontos, DateTime? fecha = null) =>
        EnviarFormularioAsync(Administrador, $"/Facturas/CrearNotaCredito/{original.Id}",
            "/Facturas/CrearNotaCredito", DatosCredito(original, cantidad, reintegrar, codigo, fecha));

    protected Task<Factura> UltimaNotaAsync() => LeerDbAsync(db => db.Facturas.AsNoTracking()
        .Include(x => x.Detalles).Where(x => x.TipoComprobante == TipoComprobante.NotaCredito)
        .OrderByDescending(x => x.Id).FirstAsync());

    protected async Task<IDocument> ReporteAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        using var respuesta = await Administrador.GetAsync(
            $"/Reportes?desde={(desde ?? Hoy):yyyy-MM-dd}&hasta={(hasta ?? Hoy):yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await new HtmlParser().ParseDocumentAsync(await respuesta.Content.ReadAsStringAsync());
    }

    protected static decimal Dinero(IElement? elemento)
    {
        Assert.NotNull(elemento);
        var texto = elemento.TextContent.Replace("RD$", "", StringComparison.Ordinal)
            .Replace("+", "", StringComparison.Ordinal).Trim();
        return decimal.Parse(texto, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    protected static void AccesoDenegado(HttpResponseMessage respuesta)
    {
        Assert.True(respuesta.StatusCode == HttpStatusCode.Forbidden ||
            (respuesta.StatusCode == HttpStatusCode.Redirect &&
             (respuesta.Headers.Location?.OriginalString.Contains("/Account/AccessDenied", StringComparison.Ordinal) ?? false)),
            $"Se esperaba denegación, se recibió {(int)respuesta.StatusCode}.");
    }

    public async Task DisposeAsync()
    {
        foreach (var cliente in _clientes) cliente.Dispose();
        if (App is null) return;
        try
        {
            if (_baseCreada && _conexion is not null)
            {
                await ConDbAsync(async db =>
                {
                    _conexion.ValidarLimpieza(db.Database.GetConnectionString()!);
                    using var pool = new SqlConnection(_conexion.Cadena);
                    SqlConnection.ClearPool(pool);
                    await db.Database.EnsureDeletedAsync();
                });
            }
        }
        finally { await App.DisposeAsync(); }
    }
}
