using Firebase.Security;
using Firebase.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace FirebaseLoginCustom.Controllers;

public class TurnoCuponesController(FirestoreDb db) : Controller
{
    [HttpGet]
    [Permiso("Turnos.Crear")]
    public async Task<IActionResult> Consultar(string? clienteId)
    {
        if (!TurnoCuponConsultaService.EsIdValido(clienteId)) return BadRequest();
        try
        {
            var coupons = await new TurnoCuponConsultaService(db).Consultar(clienteId!);
            return coupons == null ? NotFound() : Ok(coupons);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
