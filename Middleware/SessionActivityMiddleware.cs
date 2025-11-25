using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Firebase.Middleware
{
    /// <summary>
    /// Middleware para gestionar el cierre de sesión por duración máxima e inactividad.
    /// La sesión se cierra automáticamente al cerrar el navegador (cookie NO persistente).
    /// </summary>
    public class SessionActivityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SessionActivityMiddleware> _logger;

        public SessionActivityMiddleware(RequestDelegate next, ILogger<SessionActivityMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ConfiguracionService configuracionService, AuditService auditService)
        {
            // Saltar verificación en rutas de autenticación
            var path = context.Request.Path.Value?.ToLower() ?? "";
            if (path.StartsWith("/login") || path.StartsWith("/account"))
            {
                await _next(context);
                return;
            }

            // Solo procesar si el usuario está autenticado
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    await context.Session.LoadAsync();

                    var lastActivity = context.Session.GetString("LastActivity");
                    var loginTime = context.Session.GetString("LoginTime");
                    var maxDuration = context.Session.GetString("MaxDuration");
                    var now = DateTimeOffset.UtcNow;

                    // Obtener datos del usuario para auditoría
                    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value;

                    // VALIDACIÓN 1: Verificar duración máxima de la sesión (usando datos de sesión)
                    if (!string.IsNullOrEmpty(loginTime) && !string.IsNullOrEmpty(maxDuration))
                    {
                        var loginDateTime = DateTimeOffset.Parse(loginTime);
                        var duracionMaximaMinutos = int.Parse(maxDuration);
                        var sessionDuration = (now - loginDateTime).TotalMinutes;

                        if (sessionDuration > duracionMaximaMinutos)
                        {
                            _logger.LogInformation(
                                $"Sesión expirada por duración máxima para usuario: {context.User.Identity.Name} " +
                                $"(Duración: {sessionDuration:F2} minutos, Límite: {duracionMaximaMinutos} minutos)"
                            );

                            // Registrar evento de auditoría ANTES de cerrar la sesión
                            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userEmail))
                            {
                                try
                                {
                                    await auditService.LogEvent(
                                        userId,
                                        userEmail,
                                        "Cerrar Sesión por Duración Máxima",
                                        userId,
                                        "Empleado"
                                    );
                                    _logger.LogInformation($"Auditoría registrada - Sesión cerrada por duración máxima: {userEmail}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Error al registrar auditoría de cierre por duración: {ex.Message}");
                                }
                            }

                            if (!context.Response.HasStarted)
                            {
                                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                context.Session.Clear();
                                context.Response.Redirect("/Login/Index?expired=duration");
                                return;
                            }
                        }
                    }

                    // VALIDACIÓN 2: Verificar inactividad
                    if (!string.IsNullOrEmpty(lastActivity) && !string.IsNullOrEmpty(loginTime))
                    {
                        var lastActivityTime = DateTimeOffset.Parse(lastActivity);
                        var inactivityTime = now - lastActivityTime;

                        int tiempoInactividad;
                        try
                        {
                            tiempoInactividad = await configuracionService.ObtenerSesionInactividadMinutos();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Error al obtener configuración de inactividad: {ex.Message}");
                            tiempoInactividad = 15;
                        }

                        if (inactivityTime.TotalMinutes > tiempoInactividad)
                        {
                            _logger.LogInformation(
                                $"Sesión expirada por inactividad para usuario: {context.User.Identity.Name} " +
                                $"(Inactivo por {inactivityTime.TotalMinutes:F2} minutos, límite: {tiempoInactividad} min)"
                            );

                            // Registrar evento de auditoría ANTES de cerrar la sesión
                            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userEmail))
                            {
                                try
                                {
                                    await auditService.LogEvent(
                                        userId,
                                        userEmail,
                                        "Cerrar Sesión por Inactividad",
                                        userId,
                                        "Empleado"
                                    );
                                    _logger.LogInformation($"Auditoría registrada - Sesión cerrada por inactividad: {userEmail}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Error al registrar auditoría de cierre por inactividad: {ex.Message}");
                                }
                            }

                            if (!context.Response.HasStarted)
                            {
                                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                context.Session.Clear();
                                context.Response.Redirect("/Login/Index?expired=inactivity");
                                return;
                            }
                        }

                        // Actualizar tracking
                        context.Session.SetString("LastActivity", DateTimeOffset.UtcNow.ToString("O"));
                    }
                    else
                    {
                        // Inicializar tracking si no existe
                        _logger.LogInformation($"Inicializando tracking para usuario: {context.User.Identity.Name}");

                        // Obtener duración desde configuración
                        int duracionSesionMinutos;
                        try
                        {
                            duracionSesionMinutos = await configuracionService.ObtenerSesionDuracionMinutos();
                        }
                        catch
                        {
                            duracionSesionMinutos = 480; // 8 horas por defecto
                        }

                        context.Session.SetString("LoginTime", DateTimeOffset.UtcNow.ToString("O"));
                        context.Session.SetString("LastActivity", DateTimeOffset.UtcNow.ToString("O"));
                        context.Session.SetString("MaxDuration", duracionSesionMinutos.ToString());
                    }

                    await context.Session.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error en SessionActivityMiddleware: {ex.Message}");
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extensión para agregar el middleware fácilmente.
    /// </summary>
    public static class SessionActivityMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionActivity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionActivityMiddleware>();
        }
    }
}