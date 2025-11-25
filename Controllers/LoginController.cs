using FirebaseAdmin;
using Firebase.Models;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using static Firebase.Models.AuthModels;
using Firebase.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Controlador para la gestión de autenticación y registro de usuarios.
/// Maneja el inicio de sesión con email/contraseña, registro de nuevos usuarios y autenticación con Google.
/// </summary>
public class LoginController : Controller
{
    private readonly Firebase.Services.AuthenticationService _authService;
    private readonly ConfiguracionService _configuracionService;
    private readonly PersonalService _personalService;
    private readonly ILogger<LoginController> _logger;

    /// <summary>
    /// Constructor del controlador de login.
    /// </summary>
    /// <param name="authService">Servicio de autenticación</param>
    /// <param name="configuracionService">Servicio de configuración</param>
    /// <param name="personalService">Servicio de personal</param>
    /// <param name="logger">Logger para diagnóstico</param>
    public LoginController(
    Firebase.Services.AuthenticationService authService,
        ConfiguracionService configuracionService,
        PersonalService personalService,
      ILogger<LoginController> logger)
    {
        _authService = authService;
        _configuracionService = configuracionService;
        _personalService = personalService;
        _logger = logger;
    }

    /// <summary>
    /// Muestra la página principal de login/registro.
    /// </summary>
    /// <param name="expired">Indica si la sesión expiró por inactividad</param>
    /// <returns>Vista de login</returns>
    public IActionResult Index(bool expired = false)
    {
        if (expired)
        {
            ViewBag.Warning = "Su sesión ha expirado por inactividad. Por favor, inicie sesión nuevamente.";
        }
        
        return View();
    }

    /// <summary>
    /// Procesa el inicio de sesión con email y contraseña.
    /// </summary>
    /// <param name="request">Datos de login del usuario</param>
    /// <returns>Resultado de la autenticación</returns>
    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Por favor, complete todos los campos correctamente.";
            return View("Index");
        }

        try
        {
            var result = await _authService.AuthenticateWithEmailAsync(request.Email, request.Password);

            if (!result.IsSuccess)
            {
                ViewBag.Error = result.ErrorMessage;
                return View("Index");
            }

            // Iniciar sesión NO persistente (se cierra al cerrar navegador)
            await SignInUserAsync(result.UserInfo!);

            // Registrar evento de inicio de sesión en Google Analytics
            TempData["LoginEvent"] = true;

            return RedirectToAction("Index", "Lavados");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error catastrófico durante el login.");
            ViewBag.Error = "Error al iniciar sesión. Por favor, intente de nuevo.";
            return View("Index");
        }
    }

    /// <summary>
    /// Procesa el registro de un nuevo usuario.
    /// </summary>
    /// <param name="request">Datos de registro del usuario</param>
    /// <returns>Resultado del registro</returns>
    [HttpPost]
    public async Task<IActionResult> RegisterUser(RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Por favor, complete todos los campos del registro correctamente.";
            return View("Index");
        }

        try
        {
            var result = await _authService.RegisterUserAsync(request);

            if (!result.IsSuccess)
            {
                ViewBag.Error = result.ErrorMessage;
                return View("Index");
            }

            // NUEVO: En lugar de autenticar, mostrar modal de verificación
            ViewBag.ShowVerificationModal = true;
            ViewBag.RegistrationEmail = request.Email;

            return View("Index");
        }
        catch (Exception)
        {
            ViewBag.Error = "Error al registrar el usuario. Por favor, intente de nuevo.";
            return View("Index");
        }
    }

    /// <summary>
    /// Procesa la autenticación con Google.
    /// </summary>
    /// <param name="request">Token de ID de Google</param>
    /// <returns>Resultado de la autenticación</returns>
    [HttpPost]
    public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var result = await _authService.AuthenticateWithGoogleAsync(request.IdToken);
            
            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            // Iniciar sesión NO persistente
            await SignInUserAsync(result.UserInfo!);

            return Json(new { redirectUrl = Url.Action("Index", "Lavados") });
        }
        catch (Exception)
        {
            return BadRequest(new { error = "Error al autenticar con Google. Por favor, intente de nuevo." });
        }
    }

    /// <summary>
    /// Procesa la solicitud de recuperación de contraseña.
    /// </summary>
    /// <param name="email">Email del usuario que solicita recuperar la contraseña</param>
    /// <returns>Resultado de la operación</returns>
    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { error = "Por favor, ingrese un correo electrónico válido." });
            }

            var result = await _authService.SendPasswordResetEmailAsync(request.Email);

            if (!result.IsSuccess)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(new { message = "Correo de recuperación enviado exitosamente." });
        }
        catch (Exception)
        {
            return BadRequest(new { error = "Error al enviar el correo de recuperación. Por favor, intente de nuevo." });
        }
    }

    /// <summary>
    /// Autentica al usuario en la aplicación creando las claims correspondientes.
    /// La sesión NO es persistente y se cierra automáticamente al cerrar el navegador.
    /// </summary>
    /// <param name="userInfo">Información del usuario</param>
    /// <returns>Task</returns>
    private async Task SignInUserAsync(UserInfo userInfo)
    {
        // Regenerar sesión
        var oldSessionId = HttpContext.Session.Id;

        HttpContext.Session.Clear();
        await HttpContext.Session.CommitAsync();

        Response.Cookies.Delete(".AspNetCore.Session");
        Response.Cookies.Delete(".AspNetCore.Cookies");

        await HttpContext.Session.LoadAsync();

        var claims = _authService.CreateUserClaims(userInfo);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Obtener duración máxima de sesión desde configuración
        int duracionSesionMinutos;

        try
        {
            duracionSesionMinutos = await _configuracionService.ObtenerSesionDuracionMinutos();
            _logger.LogInformation($"Configuración obtenida - Duración máxima de sesión: {duracionSesionMinutos} min");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error al obtener configuración de sesión, usando valor por defecto: {ex.Message}");
            duracionSesionMinutos = 480; // 8 horas por defecto
        }

        // Cookie NO PERSISTENTE: se elimina al cerrar el navegador
        // IMPORTANTE: NO establecer ExpiresUtc para que sea una cookie de sesión real
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = false, // CRÍTICO: false = cookie de sesión
            AllowRefresh = false, // No renovar la cookie
            IssuedUtc = DateTimeOffset.UtcNow
            // NO establecer ExpiresUtc aquí - eso la haría persistente
            // La validación de duración se hace en el middleware usando datos de sesión
        };

        _logger.LogInformation($"Usuario {userInfo.Email} - Sesión NO persistente (cookie de sesión)");
        _logger.LogInformation($"Sesión regenerada: OLD={oldSessionId}, NEW={HttpContext.Session.Id}");

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        // Inicializar tracking de actividad y guardar tiempo máximo de duración en sesión
        HttpContext.Session.SetString("LastActivity", DateTimeOffset.UtcNow.ToString("O"));
        HttpContext.Session.SetString("LoginTime", DateTimeOffset.UtcNow.ToString("O"));
        HttpContext.Session.SetString("MaxDuration", duracionSesionMinutos.ToString());
        await HttpContext.Session.CommitAsync();

        _logger.LogInformation($"Sesión iniciada para {userInfo.Email}, duración máxima: {duracionSesionMinutos} min");
    }
}
/// <summary>
/// Modelo para las solicitudes de login con Google.
/// </summary>
public class GoogleLoginRequest
{
    /// <summary>
    /// Token de ID proporcionado por Google.
    /// </summary>
    [Required]
    public required string IdToken { get; set; }
}


/// <summary>
/// Modelo para las solicitudes de recuperación de contraseña.
/// </summary>
public class ForgotPasswordRequest
{
    /// <summary>
    /// Email del usuario que solicita recuperar la contraseña.
    /// </summary>
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
}

