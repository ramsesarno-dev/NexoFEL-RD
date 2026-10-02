using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexoFELRD.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class ReportesController : Controller
{
    public IActionResult Index() => View();
}
