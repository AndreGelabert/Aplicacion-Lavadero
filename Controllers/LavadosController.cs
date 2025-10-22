using FirebaseLoginCustom.Models;
using FirebaseLoginCustom.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace FirebaseLoginCustom.Controllers
{
    [AutorizacionRequerida]
    public class LavadosController : Controller
    {
        private readonly ILogger<LavadosController> _logger;
        private readonly AuditService _auditService;

        public LavadosController(ILogger<LavadosController> logger, AuditService auditService)
        {
            _logger = logger;
            _auditService = auditService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

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
    }
}