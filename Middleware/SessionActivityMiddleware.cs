using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Firebase.Middleware
{
    /// <summary>
    /// Middleware para rastrear la actividad del usuario y gestionar el cierre de sesión por inactividad.
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

        public async Task InvokeAsync(HttpContext context, ConfiguracionService configuracionService)
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
     // IMPORTANTE: Forzar carga de la sesión antes de acceder a ella
  await context.Session.LoadAsync();
    
        _logger.LogWarning($"[DEBUG] SessionID={context.Session.Id}, Path={context.Request.Path}");
  var lastActivity = context.Session.GetString("LastActivity");
    var now = DateTimeOffset.UtcNow;
   _logger.LogWarning($"[DEBUG] LastActivity={lastActivity ?? "NULL"}");

     if (!string.IsNullOrEmpty(lastActivity))
       {
        var lastActivityTime = DateTimeOffset.Parse(lastActivity);
  var inactivityTime = now - lastActivityTime;

      // VALIDACIÓN CRÍTICA: Si el LastActivity es mayor al doble del tiempo máximo de inactividad,
  // es de una sesión anterior corrupta. Reiniciar en lugar de cerrar.
     if (inactivityTime.TotalMinutes > 120) // 2 horas = cualquier valor mayor es imposible
   {
    _logger.LogWarning($"[CORRECCIÓN] LastActivity corrupto detectado ({inactivityTime.TotalMinutes:F2} min). Reiniciando para usuario: {context.User.Identity.Name}");
  context.Session.SetString("LastActivity", now.ToString("O"));
  await context.Session.CommitAsync();
   await _next(context);
       return;
 }

        // Obtener tiempo de inactividad desde configuración
      int tiempoInactividad;
   try
    {
     tiempoInactividad = await configuracionService.ObtenerSesionInactividadMinutos();
    }
catch (Exception ex)
        {
    _logger.LogWarning($"Error al obtener configuración de inactividad, usando valor por defecto: {ex.Message}");
    tiempoInactividad = 15; // Valor por defecto
     }

      // Si la inactividad supera el límite, cerrar sesión
          if (inactivityTime.TotalMinutes > tiempoInactividad)
     {
   _logger.LogInformation($"Sesión expirada por inactividad para usuario: {context.User.Identity.Name} (Inactivo por {inactivityTime.TotalMinutes:F2} minutos)");
    
  // Verificar que no se haya comenzado a enviar la respuesta
        if (!context.Response.HasStarted)
{
       // Limpiar la sesión y autenticación
await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
  context.Session.Clear();
  
// Redirigir al login con mensaje de sesión expirada
   context.Response.Redirect("/Login/Index?expired=true");
    return;
     }
   else
      {
 _logger.LogWarning("No se pudo redirigir porque la respuesta ya comenzó a enviarse");
         }
 }
   }
     else
     {
// Primera vez que se detecta actividad, inicializar
   _logger.LogInformation($"Inicializando tracking de actividad para usuario: {context.User.Identity.Name}");
    }

  // Actualizar última actividad y guardar cambios
   context.Session.SetString("LastActivity", DateTimeOffset.UtcNow.ToString("O"));
         await context.Session.CommitAsync();
          }
    catch (Exception ex)
         {
          _logger.LogError($"Error en SessionActivityMiddleware: {ex.Message}");
        // No interrumpir el flujo normal de la aplicación
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