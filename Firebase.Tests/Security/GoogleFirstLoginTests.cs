using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Firebase.Middleware;
using Firebase.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Firebase.Tests.Security;

public class GoogleFirstLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SyntheticSignInPersistsTrackingForFirstAndExistingSession(bool existingSession)
    {
        using var host = await StartHost();
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true }) { BaseAddress = new Uri(address) };
        if (existingSession) Assert.Equal("ready", await client.GetStringAsync("/Login/prepare"));

        using var response = await client.PostAsync("/Login/synthetic-google", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/protected", await response.Content.ReadAsStringAsync());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Cookies=") && !c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        using var landing = await client.GetAsync("/protected");
        Assert.True(landing.StatusCode == HttpStatusCode.OK, $"Landing returned {landing.StatusCode} with Location {landing.Headers.Location}");
        Assert.Equal("tracked", await landing.Content.ReadAsStringAsync());
        Assert.DoesNotContain(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Session=;", StringComparison.Ordinal));
    }

    private static async Task<IHost> StartHost()
    {
        var host = new HostBuilder().ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0")
            .ConfigureServices(services =>
            {
                services.AddLogging();
        services.AddRouting();
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddDistributedMemoryCache();
                services.AddMemoryCache();
                services.AddSingleton<Firebase.Security.AuthenticationSessionRegistry>();
                services.AddSession();
                services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
                services.AddSingleton((AuditService)RuntimeHelpers.GetUninitializedObject(typeof(AuditService)));
                services.AddSingleton((ConfiguracionService)RuntimeHelpers.GetUninitializedObject(typeof(ConfiguracionService)));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseSession();
                app.UseAuthentication();
                app.UseSessionActivity();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/Login/prepare", async context => { context.Session.SetString("marker", "old"); await context.Response.WriteAsync("ready"); });
                    endpoints.MapPost("/Login/synthetic-google", async context =>
                    {
                        var auth = (AuthenticationService)RuntimeHelpers.GetUninitializedObject(typeof(AuthenticationService));
                        var roles = (RolService)RuntimeHelpers.GetUninitializedObject(typeof(RolService));
                        var controller = new LoginController(auth, context.RequestServices.GetRequiredService<ConfiguracionService>(), null!, roles, context.RequestServices.GetRequiredService<Firebase.Security.AuthenticationSessionRegistry>(), context.RequestServices.GetRequiredService<ILogger<LoginController>>()) { ControllerContext = new ControllerContext { HttpContext = context } };
                        var user = new UserInfo { Uid = "synthetic", Email = "synthetic@example.invalid", Name = "Synthetic", Role = "" };
                        var method = typeof(LoginController).GetMethod("SignInUserAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                        await (Task)method.Invoke(controller, new object[] { user })!;
                        await context.Response.WriteAsync("/protected");
                    });
                    endpoints.MapGet("/protected", async context => await context.Response.WriteAsync(context.Session.GetString("LoginTime") != null && context.Session.GetString("LastActivity") != null && context.Session.GetString("MaxDuration") != null && context.Session.GetString("marker") == null ? "tracked" : "missing"));
                });
            })).Build();
        await host.StartAsync();
        return host;
    }
}
