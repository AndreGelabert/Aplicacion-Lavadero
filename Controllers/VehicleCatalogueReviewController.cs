using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Firebase.Controllers;

[Authorize]
public sealed class VehicleCatalogueReviewController : Controller
{
    private readonly VehicleCatalogueReviewService _reviews;

    public VehicleCatalogueReviewController(VehicleCatalogueReviewService reviews) => _reviews = reviews;

    [HttpGet]
    [Permiso("Vehiculos.Modificar")]
    public async Task<IActionResult> Index() => View(await _reviews.GetPendingAsync());

    [HttpGet]
    [Permiso("Vehiculos.Modificar")]
    public async Task<IActionResult> ModalDatos()
    {
        var pendientes = await _reviews.GetPendingAsync();
        return Json(pendientes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Vehiculos.Modificar")]
    public async Task<IActionResult> Resolve(string id, string decision, string? brand, string? model)
    {
        try
        {
            await _reviews.ResolveAsync(id, decision, brand, model,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.FindFirstValue(ClaimTypes.Email) ?? "");
            TempData["ReviewMessage"] = "La propuesta fue revisada.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["ReviewError"] = exception.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Permiso("Vehiculos.Modificar")]
    public async Task<IActionResult> ResolveAjax([FromForm] string id, [FromForm] string decision, [FromForm] string? brand, [FromForm] string? model)
    {
        try
        {
            await _reviews.ResolveAsync(id, decision, brand, model,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", User.FindFirstValue(ClaimTypes.Email) ?? "");
            return Json(new { exito = true, mensaje = "La propuesta fue revisada correctamente." });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { exito = false, mensaje = exception.Message });
        }
    }
}
