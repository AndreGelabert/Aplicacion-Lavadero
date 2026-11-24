using FirebaseLoginCustom.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Tests.Controllers;

public class TurnoOfertaEmailPageTests
{
    [Fact]
    public void AnonymousPageHasSecurityHeadersAndStandaloneView()
    {
        var controller = new TurnoOfertaEmailPageController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var result = Assert.IsType<ViewResult>(controller.Responder());
        Assert.Equal("~/Views/TurnoOfertaEmail/Responder.cshtml", result.ViewName);
        var headers = controller.Response.Headers;
        Assert.Equal("no-store", headers.CacheControl.ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Contains("frame-ancestors 'none'", headers.ContentSecurityPolicy.ToString());
        Assert.Contains("script-src 'nonce-", headers.ContentSecurityPolicy.ToString());
    }
}
