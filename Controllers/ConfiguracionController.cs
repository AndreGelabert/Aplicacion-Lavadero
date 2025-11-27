using Firebase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// Controlador para la gestión de configuración del sistema.
/// Solo accesible para administradores.
/// </summary>
[Authorize(Roles = "Administrador")]
public class ConfiguracionController : Controller
{
    #region Dependencias
    private readonly ConfiguracionService _configuracionService;
    private readonly AuditService _auditService;

    /// <summary>
    /// Crea una nueva instancia del controlador de configuración.
    /// </summary>
    /// <param name="configuracionService">Servicio de configuración.</param>
    /// <param name="auditService">Servicio de auditoría.</param>
    public ConfiguracionController(
        ConfiguracionService configuracionService,
        AuditService auditService)
    {
        _configuracionService = configuracionService;
        _auditService = auditService;
    }
    #endregion

    #region Vistas Principales

    /// <summary>
    /// Página principal de configuración del sistema.
    /// </summary>
    /// <returns>Vista con la configuración actual.</returns>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var configuracion = await _configuracionService.ObtenerConfiguracion();
            return View(configuracion);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al cargar la configuración: {ex.Message}";
            return View(new Configuracion());
        }
    }

    #endregion

    #region Operaciones de Actualización

    /// <summary>
    /// Actualiza la configuración del sistema.
    /// </summary>
    /// <param name="configuracion">Modelo de configuración a actualizar.</param>
    /// <param name="HorariosOperacion">Horarios de operación opcionales.</param>
    /// <returns>Redirección a Index tras actualizar.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Actualizar(Configuracion configuracion, Dictionary<string, string>? HorariosOperacion)
    {
        try
        {
            // Si se enviaron horarios personalizados, usarlos
            if (HorariosOperacion != null && HorariosOperacion.Count > 0)
            {
                configuracion.HorariosOperacion = HorariosOperacion;
            }

            // Validar modelo
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Por favor, corrija los errores en el formulario.";
                return View("Index", configuracion);
            }

            // Validar que PaquetesDescuentoStep sea >= 5
            if (configuracion.PaquetesDescuentoStep < 5)
            {
                ModelState.AddModelError("PaquetesDescuentoStep", "El paso de descuento debe ser al menos 5%");
                TempData["ErrorMessage"] = "El paso de descuento debe ser al menos 5%";
                return View("Index", configuracion);
            }

            var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "Desconocido";
            await _configuracionService.ActualizarConfiguracion(configuracion, userEmail);

            // Registrar evento de auditoría
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _auditService.LogEvent(userId, userEmail, "Actualización de configuración del sistema", 
                configuracion.Id, "Configuracion");

            TempData["SuccessMessage"] = "Configuración actualizada correctamente.";
            return RedirectToAction("Index");
        }
        catch (ArgumentException ex)
        {
            // Errores de validación específicos
            TempData["ErrorMessage"] = $"Error de validación: {ex.Message}";
            return View("Index", configuracion);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al actualizar la configuración: {ex.Message}";
            return View("Index", configuracion);
        }
    }

    #endregion
}
