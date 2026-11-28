using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Controllers;

[AllowAnonymous]
public class CotizacionProveedorPageController : Controller
{
    [HttpGet("/cotizacion-proveedor/responder")]
    public IActionResult Responder()
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Frame-Options"] = "DENY";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; script-src 'nonce-{nonce}' 'strict-dynamic'; style-src 'nonce-{nonce}'; connect-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'none'; require-trusted-types-for 'script'";
        ViewData["Nonce"] = nonce;
        return View("~/Views/CotizacionProveedor/Responder.cshtml");
    }
}
