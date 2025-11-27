using FirebaseLoginCustom.Models;
using FirebaseLoginCustom.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace FirebaseLoginCustom.Controllers
{
    /// <summary>
    /// Controlador para las vistas generales (Index, Privacy) y gestión de sesión.
    /// </summary>
    [AutorizacionRequerida]
    public class LavadosController : Controller
    {
        #region Dependencias
        private readonly ILogger<LavadosController> _logger;
        private readonly AuditService _auditService;

        /// <summary>
        /// Crea una nueva instancia de <see cref="LavadosController"/>.
        /// </summary>
        public LavadosController(ILogger<LavadosController> logger, AuditService auditService)
        {
            _logger = logger;
            _auditService = auditService;
        }
        #endregion

        #region Vistas
        /// <summary>
        /// Página principal.
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Página de privacidad.
        /// </summary>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Vista de error.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        #endregion

        #region Operaciones de Sesión
        /// <summary>
        /// Cierra la sesión del usuario actual y registra auditoría.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                await _auditService.LogEvent(
                    userId: userId,
                    userEmail: email,
                    action: "Cierre de sesión",
                    targetId: userId,
                    targetType: "Empleado");
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Login");
        }
        #endregion
    }
}