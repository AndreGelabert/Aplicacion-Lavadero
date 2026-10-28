using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Firebase.Security;

namespace Firebase.Middleware
{
    /// <summary>
    /// Middleware para gestionar el cierre de sesi�n por duraci�n m�xima e inactividad.
    /// La sesi�n se cierra autom�ticamente al cerrar el navegador (cookie NO persistente).
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

        public async Task InvokeAsync(HttpContext context, ConfiguracionService configuracionService, AuditService auditService)
        {
            // Saltar verificaci�n en rutas de autenticaci�n
            var path = context.Request.Path.Value?.ToLower() ?? "";
            if (path.StartsWith("/login") || path.StartsWith("/account"))
            {
                await _next(context);
                return;
            }

            // Solo procesar si el usuario est� autenticado
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

                    // Obtener datos del usuario para auditor�a
                    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value;

                    // VALIDACI�N 1: Verificar duraci�n m�xima de la sesi�n (usando datos de sesi�n)
                    if (!string.IsNullOrEmpty(loginTime) && !string.IsNullOrEmpty(maxDuration))
                    {
                        var loginDateTime = DateTimeOffset.Parse(loginTime);
                        var duracionMaximaMinutos = int.Parse(maxDuration);
                        var sessionDuration = (now - loginDateTime).TotalMinutes;

                        if (sessionDuration > duracionMaximaMinutos)
                        {
                            _logger.LogInformation(
                                $"Sesi�n expirada por duraci�n m�xima para usuario: {context.User.Identity.Name} " +
                                $"(Duraci�n: {sessionDuration:F2} minutos, L�mite: {duracionMaximaMinutos} minutos)"
                            );

                            // Registrar evento de auditor�a ANTES de cerrar la sesi�n
                            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userEmail))
                            {
                                try
                                {
                                    await auditService.LogEvent(
                                        userId,
                                        userEmail,
                                        "Cerrar Sesi�n por Duraci�n M�xima",
                                        userId,
                                        "Empleado"
                                    );
                                    _logger.LogInformation($"Auditor�a registrada - Sesi�n cerrada por duraci�n m�xima: {userEmail}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("Error al registrar auditor�a de cierre por duraci�n.");
                                }
                            }

                            _sessionRegistry.Revoke(authenticationSessionClaim);
                            context.Session.Clear();
                            await context.Session.CommitAsync();
                            if (context.Response.HasStarted)
                                throw new InvalidOperationException("Cannot complete session expiration after the response has started.");

                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Response.Redirect("/Login/Index?expired=duration");
                            return;
                        }
                    }

                    // VALIDACI�N 2: Verificar inactividad
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
                            _logger.LogWarning("Error al obtener configuraci�n de inactividad.");
                            tiempoInactividad = 15;
                        }

                        if (inactivityTime.TotalMinutes > tiempoInactividad)
                        {
                            _logger.LogInformation(
                                $"Sesi�n expirada por inactividad para usuario: {context.User.Identity.Name} " +
                                $"(Inactivo por {inactivityTime.TotalMinutes:F2} minutos, l�mite: {tiempoInactividad} min)"
                            );

                            // Registrar evento de auditor�a ANTES de cerrar la sesi�n
                            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userEmail))
                            {
                                try
                                {
                                    await auditService.LogEvent(
                                        userId,
                                        userEmail,
                                        "Cerrar Sesi�n por Inactividad",
                                        userId,
                                        "Empleado"
                                    );
                                    _logger.LogInformation($"Auditor�a registrada - Sesi�n cerrada por inactividad: {userEmail}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("Error al registrar auditor�a de cierre por inactividad.");
                                }
                            }

                            _sessionRegistry.Revoke(authenticationSessionClaim);
                            context.Session.Clear();
                            await context.Session.CommitAsync();
                            if (context.Response.HasStarted)
                                throw new InvalidOperationException("Cannot complete session expiration after the response has started.");

                            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            context.Response.Redirect("/Login/Index?expired=inactivity");
                            return;
                        }

                        // Actualizar tracking
                        context.Session.SetString("LastActivity", DateTimeOffset.UtcNow.ToString("O"));
                    }
                    else
                    {
                        // Inicializar tracking si no existe
                        _logger.LogInformation($"Inicializando tracking para usuario: {context.User.Identity.Name}");

                        // Obtener duraci�n desde configuraci�n
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
    }

    /// <summary>
    /// Extensi�n para agregar el middleware f�cilmente.
    /// </summary>
    public static class SessionActivityMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionActivity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionActivityMiddleware>();
        }
    }
}
