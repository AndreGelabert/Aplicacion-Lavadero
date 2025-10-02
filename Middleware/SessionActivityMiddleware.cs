using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Firebase.Middleware
{
    /// <summary>
    /// Middleware para rastrear la actividad del usuario y gestionar el cierre de sesi�n por inactividad.
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

        public async Task InvokeAsync(HttpContext context)
        {
            // Solo procesar si el usuario est� autenticado
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var lastActivity = context.Session.GetString("LastActivity");
                var now = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(lastActivity))
                {
                    var lastActivityTime = DateTime.Parse(lastActivity);
                    var inactivityTime = now - lastActivityTime;

                    // Si la inactividad supera el l�mite, cerrar sesi�n
                    if (inactivityTime.TotalMinutes > 15) // 15 minutos de inactividad
                    {
                        _logger.LogInformation($"Sesi�n expirada por inactividad para usuario: {context.User.Identity.Name}");
                        
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Session.Clear();
                        
                        context.Response.Redirect("/Login/Index?expired=true");
                        return;
                    }
                }

                // Actualizar �ltima actividad
                context.Session.SetString("LastActivity", now.ToString("O"));
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