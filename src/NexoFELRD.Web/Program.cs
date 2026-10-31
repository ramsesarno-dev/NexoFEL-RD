using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexoFELRD.Web.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena DefaultConnection.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await InicializarDesarrolloAsync(
        app.Services,
        app.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

static async Task InicializarDesarrolloAsync(
    IServiceProvider serviceProvider,
    IConfiguration configuration)
{
    using var scope = serviceProvider.CreateScope();

    var context = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await context.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<IdentityUser>>();

    string[] roles =
    {
        "Administrador",
        "Facturador"
    };

    foreach (var nombreRol in roles)
    {
        if (!await roleManager.RoleExistsAsync(nombreRol))
        {
            var resultadoRol = await roleManager.CreateAsync(
                new IdentityRole(nombreRol));

            ValidarResultado(
                resultadoRol,
                $"No se pudo crear el rol {nombreRol}");
        }
    }

    var correoAdministrador = configuration["SeedAdmin:Email"];
    var claveAdministrador = configuration["SeedAdmin:Password"];

    if (string.IsNullOrWhiteSpace(correoAdministrador) ||
        string.IsNullOrWhiteSpace(claveAdministrador))
    {
        throw new InvalidOperationException(
            "Configure SeedAdmin:Email y SeedAdmin:Password " +
            "mediante User Secrets.");
    }

    var administrador = await userManager.FindByEmailAsync(
        correoAdministrador);

    if (administrador is null)
    {
        administrador = new IdentityUser
        {
            UserName = correoAdministrador,
            Email = correoAdministrador,
            EmailConfirmed = true
        };

        var resultadoUsuario = await userManager.CreateAsync(
            administrador,
            claveAdministrador);

        ValidarResultado(
            resultadoUsuario,
            "No se pudo crear el usuario administrador");
    }

    if (!await userManager.IsInRoleAsync(
            administrador,
            "Administrador"))
    {
        var resultadoAsignacion = await userManager.AddToRoleAsync(
            administrador,
            "Administrador");

        ValidarResultado(
            resultadoAsignacion,
            "No se pudo asignar el rol Administrador");
    }
}

static void ValidarResultado(
    IdentityResult resultado,
    string mensaje)
{
    if (resultado.Succeeded)
    {
        return;
    }

    var errores = string.Join(
        " | ",
        resultado.Errors.Select(error => error.Description));

    throw new InvalidOperationException(
        $"{mensaje}: {errores}");
}
