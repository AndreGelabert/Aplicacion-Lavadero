using Firebase.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Firebase.Security;

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
        private readonly AuthenticationSessionRegistry _sessionRegistry;

        public SessionActivityMiddleware(RequestDelegate next, ILogger<SessionActivityMiddleware> logger, AuthenticationSessionRegistry sessionRegistry)
        {
            _next = next;
            _logger = logger;
            _sessionRegistry = sessionRegistry;
        }

        public async Task InvokeAsync(HttpContext context, ConfiguracionService configuracionService, IAuditWriter auditService)
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
                    var authenticationSessionClaim = context.User.FindFirst(SessionTracking.AuthenticationSessionClaimType)?.Value;
                    var authenticationSessionId = context.Session.GetString(SessionTracking.AuthenticationSessionKey);
                    var now = DateTimeOffset.UtcNow;

                    // Reject stale cookies without clearing tracking that may belong to a newer login.
                    if (!string.Equals(authenticationSessionClaim, authenticationSessionId, StringComparison.Ordinal))
                    {
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Response.Redirect("/Login/Index?expired=true");
                        return;
                    }

                    // Never restart the authentication clock when server tracking is lost.
                    if (!DateTimeOffset.TryParse(loginTime, out _) ||
                        !DateTimeOffset.TryParse(lastActivity, out _) ||
                        !int.TryParse(maxDuration, out var duration) || duration <= 0)
                    {
                        _sessionRegistry.Revoke(authenticationSessionClaim);
                        context.Session.Clear();
                        await context.Session.CommitAsync();
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Response.Redirect("/Login/Index?expired=true");
                        return;
                    }

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

                            await ExpireSessionAsync(context, auditService, authenticationSessionClaim, userId, userEmail,
                                "Cerrar Sesión por Duración Máxima",
                                $"Auditoría registrada - Sesión cerrada por duración máxima: {userEmail}",
                                "Error al registrar auditoría de cierre por duración.",
                                "/Login/Index?expired=duration");
                            return;
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
                            _logger.LogWarning("Error al obtener configuración de inactividad.");
                            tiempoInactividad = 15;
                        }

                        if (inactivityTime.TotalMinutes > tiempoInactividad)
                        {
                            _logger.LogInformation(
                                $"Sesión expirada por inactividad para usuario: {context.User.Identity.Name} " +
                                $"(Inactivo por {inactivityTime.TotalMinutes:F2} minutos, límite: {tiempoInactividad} min)"
                            );

                            await ExpireSessionAsync(context, auditService, authenticationSessionClaim, userId, userEmail,
                                "Cerrar Sesión por Inactividad",
                                $"Auditoría registrada - Sesión cerrada por inactividad: {userEmail}",
                                "Error al registrar auditoría de cierre por inactividad.",
                                "/Login/Index?expired=inactivity");
                            return;
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

                    if (string.IsNullOrEmpty(authenticationSessionClaim) || !_sessionRegistry.IsActive(authenticationSessionClaim))
                    {
                        context.Session.Clear();
                        await context.Session.CommitAsync();
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Response.Redirect("/Login/Index?expired=true");
                        return;
                    }

                    await context.Session.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error en SessionActivityMiddleware.");
                    if (context.Response.HasStarted)
                        throw;

                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    return;
                }
            }

            await _next(context);
        }

        private async Task ExpireSessionAsync(HttpContext context, IAuditWriter auditService, string? authenticationSessionClaim,
            string? userId, string? userEmail, string auditAction, string successLog, string errorLog, string redirectLocation)
        {
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userEmail))
            {
                try
                {
                    await auditService.LogSessionEvent(userId, userEmail, auditAction, userId, "Empleado");
                    _logger.LogInformation(successLog);
                }
                catch (Exception ex)
                {
                    _logger.LogError(errorLog);
                }
            }

            _sessionRegistry.Revoke(authenticationSessionClaim);
            context.Session.Clear();
            await context.Session.CommitAsync();
            if (context.Response.HasStarted)
                throw new InvalidOperationException("Cannot complete session expiration after the response has started.");

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Response.Redirect(redirectLocation);
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
