using Firebase.Models;
using Firebase.Security;
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
        private readonly IAuditWriter _auditService;

        /// <summary>
        /// Constructor que inyecta las dependencias necesarias.
        /// </summary>
        /// <param name="tipoVehiculoService">Servicio para operaciones con tipos de vehículo.</param>
        /// <param name="auditService">Servicio para registro de auditoría.</param>
        public TipoVehiculoController(TipoVehiculoService tipoVehiculoService, IAuditWriter auditService)
        {
            _tipoVehiculoService = tipoVehiculoService;
            _auditService = auditService;
        }

        public sealed class TipoVehiculoModalInput
        {
            public string? Id { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string FormatoPatente { get; set; } = string.Empty;
            public int CantidadEmpleadosRequeridos { get; set; } = 1;
        }

        [HttpGet]
        [Permiso("Vehiculos.Crear", "Vehiculos.Modificar", "Servicios.Crear", "Servicios.Modificar")]
        public async Task<IActionResult> ModalDatos() =>
            Json(await _tipoVehiculoService.ObtenerTodos(incluirInactivos: true));

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Vehiculos.Crear", "Vehiculos.Modificar", "Servicios.Crear", "Servicios.Modificar")]
        public async Task<IActionResult> ModalGuardar(TipoVehiculoModalInput input, bool crear)
        {
            var permiso = crear
                ? (User.TienePermiso("Vehiculos.Crear") || User.TienePermiso("Servicios.Crear"))
                : (User.TienePermiso("Vehiculos.Modificar") || User.TienePermiso("Servicios.Modificar"));
            if (!permiso) return Forbid();
            if (!crear && string.IsNullOrWhiteSpace(input.Id))
                return BadRequest(new { mensaje = "El tipo de vehículo no existe." });

            var resultado = await _tipoVehiculoService.GuardarConAuditoria(
                new TipoVehiculo
                {
                    Id = crear ? string.Empty : input.Id!,
                    Nombre = input.Nombre,
                    FormatoPatente = input.FormatoPatente,
                    CantidadEmpleadosRequeridos = input.CantidadEmpleadosRequeridos
                },
                crear,
                Actor());

            if (!resultado.Resultado.EsExitoso)
                return BadRequest(new { mensaje = resultado.Resultado.MensajeError });

            var tiposActivos = await _tipoVehiculoService.ObtenerTiposVehiculosCompletos();
            return Json(new
            {
                mensaje = resultado.Resultado.MensajeExito,
                id = resultado.TipoVehiculoId,
                tipos = tiposActivos.Select(t => t.Nombre).ToList(),
                tiposCompletos = tiposActivos.Select(t => new
                {
                    nombre = t.Nombre,
                    formatoPatente = t.FormatoPatente,
                    cantidadEmpleadosRequeridos = t.CantidadEmpleadosRequeridos,
                    regex = !string.IsNullOrWhiteSpace(t.FormatoPatente) ? t.ObtenerRegexPattern() : null
                })
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Permiso("Vehiculos.Modificar", "Vehiculos.Eliminar", "Servicios.Modificar", "Servicios.Eliminar")]
        public async Task<IActionResult> ModalEstado(string id, string estado)
        {
            if (estado != "Activo" && estado != "Inactivo")
                return BadRequest(new { mensaje = "Estado inválido." });

            var resultado = await _tipoVehiculoService.CambiarEstadoConAuditoria(id, estado, Actor());
            if (!resultado.Resultado.EsExitoso)
                return BadRequest(new { mensaje = resultado.Resultado.MensajeError });

            var tiposActivos = await _tipoVehiculoService.ObtenerTiposVehiculosCompletos();
            return Json(new
            {
                mensaje = resultado.Resultado.MensajeExito,
                tipos = tiposActivos.Select(t => t.Nombre).ToList(),
                tiposCompletos = tiposActivos.Select(t => new
                {
                    nombre = t.Nombre,
                    formatoPatente = t.FormatoPatente,
                    cantidadEmpleadosRequeridos = t.CantidadEmpleadosRequeridos,
                    regex = !string.IsNullOrWhiteSpace(t.FormatoPatente) ? t.ObtenerRegexPattern() : null
                })
            });
        }

        private ActorAuditoria Actor() => new(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);

        /// <summary>
        /// Obtiene todos los tipos de vehículo con sus formatos.
        /// </summary>
        [HttpGet]
        [Permiso("Vehiculos.Consultar", "Servicios.Consultar", "Clientes.Consultar")]
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
        [Permiso("Vehiculos.Consultar", "Servicios.Consultar", "Clientes.Consultar")]
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
