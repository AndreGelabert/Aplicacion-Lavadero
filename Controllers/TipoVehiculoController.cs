using Firebase.Models;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Firebase.Controllers
{
    /// <summary>
    /// Controlador para la gestión de tipos de vehículo.
    /// Maneja operaciones CRUD y validaciones relacionadas con tipos de vehículo.
    /// </summary>
    [Authorize]
    public class TipoVehiculoController : Controller
    {
        private readonly TipoVehiculoService _tipoVehiculoService;
        private readonly AuditService _auditService;

        /// <summary>
        /// Constructor que inyecta las dependencias necesarias.
        /// </summary>
        /// <param name="tipoVehiculoService">Servicio para operaciones con tipos de vehículo.</param>
        /// <param name="auditService">Servicio para registro de auditoría.</param>
        public TipoVehiculoController(TipoVehiculoService tipoVehiculoService, AuditService auditService)
        {
            _tipoVehiculoService = tipoVehiculoService;
            _auditService = auditService;
        }

        /// <summary>
        /// Obtiene todos los tipos de vehículo con sus formatos.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTiposConFormatos()
        {
            var tipos = await _tipoVehiculoService.ObtenerTiposVehiculosCompletos();
            return Json(tipos.Select(t => new
            {
                nombre = t.Nombre,
                formato = t.FormatoPatente,
                regex = t.ObtenerRegexPattern()
            }));
        }

        /// <summary>
        /// Obtiene el formato de un tipo de vehículo específico.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerFormato(string nombreTipo)
        {
            if (string.IsNullOrWhiteSpace(nombreTipo))
            {
                return Json(new { success = false, formato = (string?)null, regex = (string?)null });
            }

            var tipo = await _tipoVehiculoService.ObtenerTipoVehiculoPorNombre(nombreTipo);
            if (tipo == null)
            {
                return Json(new { success = false, formato = (string?)null, regex = (string?)null });
            }

            return Json(new
            {
                success = true,
                formato = tipo.FormatoPatente,
                regex = tipo.ObtenerRegexPattern()
            });
        }

        private async Task RegistrarEvento(string accion, string targetId, string entidad)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            await _auditService.LogEvent(userId, userEmail, accion, targetId, entidad);
        }
    }
}