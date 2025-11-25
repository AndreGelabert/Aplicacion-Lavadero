using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Firebase.Controllers
{
    /// <summary>
    /// API para gestionar la información de sesión del usuario
    /// </summary>
    [ApiController]
    [Route("api")]
    [Authorize]
    public class SessionApiController : ControllerBase
    {
        private readonly ConfiguracionService _configuracionService;
        private readonly ILogger<SessionApiController> _logger;

        public SessionApiController(
            ConfiguracionService configuracionService,
            ILogger<SessionApiController> logger)
        {
            _configuracionService = configuracionService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la configuración de sesión actual del usuario
        /// </summary>
        [HttpGet("session-config")]
        public async Task<IActionResult> GetSessionConfig()
        {
            try
            {
                var loginTimeStr = HttpContext.Session.GetString("LoginTime");
                var maxDurationStr = HttpContext.Session.GetString("MaxDuration");

                if (string.IsNullOrEmpty(loginTimeStr) || string.IsNullOrEmpty(maxDurationStr))
                {
                    return BadRequest(new { error = "Sesión no inicializada correctamente" });
                }

                int maxDurationMinutes = int.Parse(maxDurationStr);
                int inactivityMinutes;

                try
                {
                    inactivityMinutes = await _configuracionService.ObtenerSesionInactividadMinutos();
                }
                catch
                {
                    inactivityMinutes = 15; // Valor por defecto
                }

                var config = new
                {
                    maxDurationMinutes = maxDurationMinutes,
                    inactivityMinutes = inactivityMinutes,
                    loginTime = loginTimeStr,
                    currentTime = DateTimeOffset.UtcNow.ToString("O")
                };

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al obtener configuración de sesión: {ex.Message}");
                return StatusCode(500, new { error = "Error al obtener configuración de sesión" });
            }
        }

        /// <summary>
        /// Endpoint para mantener la sesión activa (ping)
        /// </summary>
        [HttpPost("ping-session")]
        public IActionResult PingSession()
        {
            try
            {
                // Actualizar el tiempo de última actividad
                HttpContext.Session.SetString("LastActivity", DateTimeOffset.UtcNow.ToString("O"));

                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                _logger.LogInformation($"Sesión extendida para usuario: {userEmail}");

                return Ok(new { message = "Sesión actualizada", timestamp = DateTimeOffset.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al hacer ping de sesión: {ex.Message}");
                return StatusCode(500, new { error = "Error al actualizar sesión" });
            }
        }
    }
}
