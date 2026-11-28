using System.Reflection;
using Firebase.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Tests.Controllers;

public sealed class CotizacionProveedorPageTests
{
    [Fact]
    public void Responder_EsAllowAnonymous_YConfiguraHeadersEstrictosYNonceUnico()
    {
        Assert.NotNull(typeof(CotizacionProveedorPageController).GetCustomAttribute<AllowAnonymousAttribute>());

        var controller1 = new CotizacionProveedorPageController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result1 = Assert.IsType<ViewResult>(controller1.Responder());
        Assert.Equal("~/Views/CotizacionProveedor/Responder.cshtml", result1.ViewName);

        var headers = controller1.Response.Headers;
        Assert.Equal("no-store", headers.CacheControl.ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Equal("DENY", headers["X-Frame-Options"].ToString());
        Assert.Equal("nosniff", headers["X-Content-Type-Options"].ToString());

        var nonce1 = Assert.IsType<string>(result1.ViewData["Nonce"]);
        var csp = headers.ContentSecurityPolicy.ToString();
        Assert.Contains($"script-src 'nonce-{nonce1}' 'strict-dynamic'", csp);
        Assert.Contains($"style-src 'nonce-{nonce1}'", csp);
        Assert.Contains("connect-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("form-action 'none'", csp);
        Assert.Contains("require-trusted-types-for 'script'", csp);

        var controller2 = new CotizacionProveedorPageController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result2 = Assert.IsType<ViewResult>(controller2.Responder());
        var nonce2 = Assert.IsType<string>(result2.ViewData["Nonce"]);
        Assert.NotEqual(nonce1, nonce2);
    }
}
