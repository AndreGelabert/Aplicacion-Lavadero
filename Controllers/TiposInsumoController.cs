using System.Security.Claims;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Controllers;

[Authorize]
public sealed class TiposInsumoController(TipoInsumoService tipos) : Controller
{
    [HttpGet]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> Index() => View(await tipos.ObtenerTodos());

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Crear", "Insumos.Modificar")]
    public async Task<IActionResult> Guardar(TipoInsumo tipo, bool crear)
    {
        if (!User.TienePermiso(crear ? "Insumos.Crear" : "Insumos.Modificar")) return Forbid();
        var resultado = await tipos.Guardar(tipo, crear, Actor());
        Mensaje(resultado);
        return RedirectToAction(nameof(Index));
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
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> CambiarEstado(string id, string estado)
    {
        if (estado != "Activo" && estado != "Inactivo") return BadRequest();
        Mensaje(await tipos.CambiarEstado(id, estado, Actor()));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Permiso("Insumos.Modificar")]
    public async Task<IActionResult> Eliminar(string id)
    {
        Mensaje(await tipos.Eliminar(id, Actor()));
        return RedirectToAction(nameof(Index));
    }

    private ActorAuditoria Actor() => new(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        User.FindFirstValue(ClaimTypes.Email) ?? string.Empty);

    private void Mensaje(ResultadoOperacion resultado) =>
        TempData[resultado.EsExitoso ? "Success" : "Error"] = resultado.EsExitoso ? resultado.MensajeExito : resultado.MensajeError;
}
