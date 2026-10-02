using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class InventarioController : Controller
{
    public IActionResult Index() => View();
}
