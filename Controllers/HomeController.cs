using Firebase.Models;
using Firebase.Security;
using FirebaseLoginCustom.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

/// <summary>
/// Controlador de la página inicial y de las pantallas de error.
/// Resuelve la primera sección accesible según los permisos del rol activo.
/// </summary>
[Authorize]
public class HomeController : Controller
{
    /// <summary>
    /// Redirige al primer módulo que el usuario puede abrir o a la página de acceso denegado.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        var ruta = NavegacionSistema.RutaLanding(User);

        if (string.Equals(ruta, NavegacionSistema.RutaAccesoDenegado, StringComparison.Ordinal))
        {
            return RedirectToAction(nameof(AccessDenied));
        }

        return Redirect(ruta);
    }

    /// <summary>
    /// Muestra la página de acceso denegado con estado HTTP 403.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult AccessDenied()
    {
        var tieneModulo = NavegacionSistema.PrimerModuloAccesible(NavegacionSistema.PermisosDe(User)) != null;

        ViewBag.TieneModulo = tieneModulo;
        ViewBag.Mensaje = tieneModulo
            ? "No tiene permiso para acceder a esta sección. Contacte al administrador."
            : NavegacionSistema.MensajeSinAcceso;

        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    /// <summary>
    /// Muestra la página genérica de error.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
