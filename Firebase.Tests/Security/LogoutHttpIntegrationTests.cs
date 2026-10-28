using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Firebase.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Firebase.Tests.Security;

public class LogoutHttpIntegrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LogoutRequiresAntiforgeryAndClearsAuthenticationAndSession(bool ajax)
    {
        using var host = await StartHost();
        using var client = Client(host);
        using var signIn = await client.GetAsync("/test/signin");
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var issuedCookies = signIn.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(issuedCookies, value => value.StartsWith(".AspNetCore.Cookies=") && !value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issuedCookies, value => value.StartsWith(".AspNetCore.Session="));
        var token = await client.GetStringAsync("/Login/testtoken");
        Assert.Equal("authenticated:present", await client.GetStringAsync("/test/state"));

        foreach (var badToken in new string?[] { null, "invalid" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Login/Logout");
            if (badToken != null) request.Headers.Add("RequestVerificationToken", badToken);
            using var rejected = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            Assert.Equal("authenticated:present", await client.GetStringAsync("/test/state"));
        }

        using var valid = new HttpRequestMessage(HttpMethod.Post, "/Login/Logout");
        valid.Headers.Add("RequestVerificationToken", token);
        if (ajax) valid.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(valid);
        Assert.Equal(ajax ? HttpStatusCode.NoContent : HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
        if (!ajax) Assert.Equal("/Login", response.Headers.Location?.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Cookies=;") && value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("anonymous:absent", await client.GetStringAsync("/test/state"));
    }

    [Fact]
    public async Task ExpiredDurationSignsOutEvenWhenAuditFails()
    {
        using var host = await StartHost();
        using var client = Client(host);
        await client.GetStringAsync("/test/signin?expired=true");
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Login/Index?expired=duration", response.Headers.Location?.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Cookies=;"));
        Assert.Equal("anonymous:absent", await client.GetStringAsync("/test/state"));
    }

    [Fact]
    public async Task LogoutInvalidatesCopiedAuthenticationAndSessionCookies()
    {
        using var host = await StartHost();
        using var client = Client(host);
        using var signIn = await client.GetAsync("/test/signin");
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var copiedCookies = signIn.Headers.GetValues("Set-Cookie")
            .Select(value => value[..value.IndexOf(';')])
            .Where(value => value.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal) ||
                            value.StartsWith(".AspNetCore.Session=", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(copiedCookies, value => value.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));
        Assert.Contains(copiedCookies, value => value.StartsWith(".AspNetCore.Session=", StringComparison.Ordinal));

        using var copiedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            BaseAddress = client.BaseAddress
        };

        var authenticationCookie = copiedCookies.Single(value => value.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal));
        using var authOnlyRequest = new HttpRequestMessage(HttpMethod.Get, "/protected");
        authOnlyRequest.Headers.TryAddWithoutValidation("Cookie", authenticationCookie);
        using var authOnlyResponse = await copiedClient.SendAsync(authOnlyRequest);
        Assert.Equal(HttpStatusCode.Redirect, authOnlyResponse.StatusCode);
        Assert.Equal("/Login/Index?expired=true", authOnlyResponse.Headers.Location?.OriginalString);

        using var copiedRequest = new HttpRequestMessage(HttpMethod.Get, "/protected");
        copiedRequest.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", copiedCookies));
        using var beforeLogout = await copiedClient.SendAsync(copiedRequest);
        Assert.Equal(HttpStatusCode.OK, beforeLogout.StatusCode);

        var token = await client.GetStringAsync("/Login/testtoken");
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/Login/Logout");
        logout.Headers.Add("RequestVerificationToken", token);
        logout.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Get, "/protected");
        replayRequest.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", copiedCookies));
        using var afterLogout = await copiedClient.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Equal("/Login/Index?expired=true", afterLogout.Headers.Location?.OriginalString);
        Assert.Contains(afterLogout.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(".AspNetCore.Cookies=;", StringComparison.Ordinal) &&
            value.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        using var reauthentication = await client.GetAsync("/test/signin?uid=other-synthetic-id");
        Assert.Equal(HttpStatusCode.OK, reauthentication.StatusCode);

        using var replayAfterReauthentication = new HttpRequestMessage(HttpMethod.Get, "/protected");
        replayAfterReauthentication.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", copiedCookies));
        using var afterReauthentication = await copiedClient.SendAsync(replayAfterReauthentication);
        Assert.Equal(HttpStatusCode.Redirect, afterReauthentication.StatusCode);
        Assert.Equal("/Login/Index?expired=true", afterReauthentication.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task InFlightSessionWriteAfterLogoutCannotRestoreCopiedCookies()
    {
        using var host = await StartHost();
        using var client = Client(host);
        using var copiedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            BaseAddress = client.BaseAddress
        };
        using var signIn = await client.GetAsync("/test/signin");
        var copiedCookies = signIn.Headers.GetValues("Set-Cookie")
            .Select(value => value[..value.IndexOf(';')])
            .Where(value => value.StartsWith(".AspNetCore.Cookies=", StringComparison.Ordinal) ||
                            value.StartsWith(".AspNetCore.Session=", StringComparison.Ordinal))
            .ToArray();

        using var heldRequest = new HttpRequestMessage(HttpMethod.Get, "/test/hold-session-write");
        heldRequest.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", copiedCookies));
        var inFlightResponseTask = copiedClient.SendAsync(heldRequest);
        var gate = host.Services.GetRequiredService<InFlightRequestGate>();
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var token = await client.GetStringAsync("/Login/testtoken");
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/Login/Logout");
        logout.Headers.Add("RequestVerificationToken", token);
        logout.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        gate.Release.TrySetResult();
        using var inFlightResponse = await inFlightResponseTask;
        Assert.Equal(HttpStatusCode.OK, inFlightResponse.StatusCode);

        using var replay = new HttpRequestMessage(HttpMethod.Get, "/protected");
        replay.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", copiedCookies));
        using var afterLogout = await copiedClient.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Equal("/Login/Index?expired=true", afterLogout.Headers.Location?.OriginalString);
    }

    private static HttpClient Client(IHost host)
    {
        var address = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true }) { BaseAddress = new Uri(address) };
    }

    private static async Task<IHost> StartHost()
    {
        var host = new HostBuilder().ConfigureWebHost(web => web.UseKestrel().UseUrls("http://127.0.0.1:0")
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddDistributedMemoryCache();
                services.AddMemoryCache();
                services.AddSingleton<Firebase.Security.AuthenticationSessionRegistry>();
                services.AddSession();
                services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
                services.AddAuthorization();
                services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
                services.AddControllersWithViews().AddApplicationPart(typeof(LoginController).Assembly).AddControllersAsServices();
                services.AddTransient(_ => new LoginController(null!, null!, null!, null!, _.GetRequiredService<Firebase.Security.AuthenticationSessionRegistry>(), _.GetRequiredService<ILogger<LoginController>>()));
                // No Firestore instance or network access: these instances deliberately fail on use.
                services.AddSingleton((AuditService)RuntimeHelpers.GetUninitializedObject(typeof(AuditService)));
                services.AddSingleton((ConfiguracionService)RuntimeHelpers.GetUninitializedObject(typeof(ConfiguracionService)));
                services.AddSingleton<InFlightRequestGate>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseSession();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseSessionActivity();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/test/signin", async context =>
                    {
                        var uid = context.Request.Query["uid"].FirstOrDefault() ?? "synthetic-id";
                        var authenticationSessionId = Guid.NewGuid().ToString("N");
                        context.RequestServices.GetRequiredService<Firebase.Security.AuthenticationSessionRegistry>()
                            .Register(authenticationSessionId, TimeSpan.FromMinutes(1));
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, uid),
                            new Claim(ClaimTypes.Email, $"{uid}@example.invalid"),
                            new Claim(Firebase.Security.SessionTracking.AuthenticationSessionClaimType, authenticationSessionId)
                        };
                        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
                        var expired = context.Request.Query.ContainsKey("expired");
                        context.Session.SetString("LoginTime", DateTimeOffset.UtcNow.AddMinutes(expired ? -10 : 0).ToString("O"));
                        context.Session.SetString("LastActivity", DateTimeOffset.UtcNow.ToString("O"));
                        context.Session.SetString("MaxDuration", "1");
                        context.Session.SetString(Firebase.Security.SessionTracking.AuthenticationSessionKey, authenticationSessionId);
                        context.Session.SetString("marker", "present");
                        await context.Response.WriteAsync("signed in");
                    });
                    endpoints.MapGet("/Login/testtoken", async context => await context.Response.WriteAsync(context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context).RequestToken!));
                    endpoints.MapGet("/test/state", async context => await context.Response.WriteAsync($"{(context.User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous")}:{context.Session.GetString("marker") ?? "absent"}"));
                    endpoints.MapGet("/test/hold-session-write", async context =>
                    {
                        context.Session.SetString("late-write", "stale");
                        var gate = context.RequestServices.GetRequiredService<InFlightRequestGate>();
                        gate.Entered.TrySetResult();
                        await gate.Release.Task;
                        await context.Response.WriteAsync("completed");
                    });
                    endpoints.MapGet("/protected", async context => await context.Response.WriteAsync("protected"));
                    endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
                });
            })).Build();
        await host.StartAsync();
        return host;
    }

    private sealed class InFlightRequestGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
