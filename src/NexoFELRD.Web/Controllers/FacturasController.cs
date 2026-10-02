using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador,Facturador")]
public class FacturasController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Crear() => View();
}
