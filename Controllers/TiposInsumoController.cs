using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Controllers;

public sealed class TipoInsumoModalInput
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "El nombre del tipo de insumo es obligatorio"), StringLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La categoría de magnitud es obligatoria")]
    public string Categoria { get; set; } = string.Empty;

    [Required(ErrorMessage = "La unidad resumen es obligatoria")]
    public string UnidadResumen { get; set; } = string.Empty;
}

[Authorize]
public sealed class TiposInsumoController(TipoInsumoService tipos) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> Guardar(TipoInsumo tipo, bool crear)
    {
        if (!User.TienePermiso(crear ? "Insumos.Crear" : "Insumos.Modificar")) return Forbid();
        var resultado = await tipos.Guardar(tipo, crear, Actor());
        Mensaje(resultado);
        return RedirectToAction("Index", "Insumos");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear")]
    public async Task<IActionResult> InicializarHerramienta()
    {
        var tipo = await tipos.AsegurarTipoHerramienta(Actor());
        var valido = tipo.Estado == "Activo" &&
            string.Equals(tipo.Categoria, TipoInsumo.Categorias.Unidades, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(tipo.UnidadResumen, TipoInsumo.UnidadHerramienta, StringComparison.OrdinalIgnoreCase);
        TempData[valido ? "Success" : "Error"] = valido
            ? "El tipo Herramienta está disponible."
            : "El tipo Herramienta existente tiene una configuración incompatible o está inactivo; no se modificó.";
        return RedirectToAction("Index", "Insumos");
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> CambiarEstado(string id, string estado)
    {
        if (estado != "Activo" && estado != "Inactivo") return BadRequest();
        Mensaje(await tipos.CambiarEstado(id, estado, Actor()));
        return RedirectToAction("Index", "Insumos");
    }



    [HttpGet]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> ModalDatos() => Json(await tipos.ObtenerTodos());

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> ModalGuardar(TipoInsumoModalInput input, bool crear)
    {
        if (!User.TienePermiso(crear ? "Insumos.Crear" : "Insumos.Modificar")) return Forbid();
        if (!ModelState.IsValid) return BadRequest(new { mensaje = "Datos del tipo de insumo inválidos." });
        if (!crear && string.IsNullOrWhiteSpace(input.Id)) return BadRequest(new { mensaje = "El tipo de insumo no existe." });
        var tipo = new TipoInsumo
        {
            Id = crear ? string.Empty : input.Id!,
            Nombre = input.Nombre,
            Categoria = input.Categoria,
            UnidadResumen = input.UnidadResumen
        };
        var resultado = await tipos.GuardarConAuditoria(tipo, crear, Actor());
        if (!resultado.Resultado.EsExitoso) return BadRequest(new { mensaje = resultado.Resultado.MensajeError });
        return Json(new { mensaje = resultado.Resultado.MensajeExito, id = resultado.TipoInsumoId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> ModalEstado(string id, string estado)
    {
        if (estado != "Activo" && estado != "Inactivo") return BadRequest(new { mensaje = "Estado inválido." });
        var resultado = await tipos.CambiarEstado(id, estado, Actor());
        if (!resultado.EsExitoso) return BadRequest(new { mensaje = resultado.MensajeError });
        return Json(new { mensaje = resultado.MensajeExito });
    }



    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear")]
    public async Task<IActionResult> ModalInicializarHerramienta()
    {
        var tipo = await tipos.AsegurarTipoHerramienta(Actor());
        var valido = tipo.Estado == "Activo" &&
            string.Equals(tipo.Categoria, TipoInsumo.Categorias.Unidades, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(tipo.UnidadResumen, TipoInsumo.UnidadHerramienta, StringComparison.OrdinalIgnoreCase);
        if (!valido) return BadRequest(new { mensaje = "El tipo Herramienta existente tiene una configuración incompatible o está inactivo; no se modificó." });
        return Json(new { mensaje = "El tipo Herramienta está disponible.", id = tipo.Id });
    }

    private ActorAuditoria Actor() => new(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);

    private void Mensaje(ResultadoOperacion resultado) =>
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
}
