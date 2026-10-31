using Firebase.Middleware;
using Firebase.Models;
using Firebase.Security;
using FirebaseAdmin;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

// Las solicitudes AJAX reciben 401/403 en lugar de una redirección HTML.
static bool EsSolicitudAjax(HttpRequest request) =>
    string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
    || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

var seedLocalEmulators = args.Contains("--seed-local-emulators", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--seed-local-emulators").ToArray());
var localEmulators = LocalEmulatorSettings.Validate(
    builder.Environment.IsDevelopment(),
    Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"),
    Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST"));
builder.Configuration["LocalEmulators:Enabled"] = localEmulators ? "true" : "false";
if (seedLocalEmulators && !localEmulators)
    throw new InvalidOperationException("The local seed command requires both local emulators in Development.");

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Firebase.Security.AuthenticationSessionRegistry>();

// Políticas dinámicas para los permisos "Modulo.Accion".
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();

// Configuración de autenticación
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.LogoutPath = "/Lavados/Logout";
        options.AccessDeniedPath = "/Home/AccessDenied";
        // La duración se configurará dinámicamente en el LoginController
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Valor por defecto
        options.SlidingExpiration = false; // No renovar automáticamente
        // Cookie NO persistente por defecto (se borra al cerrar navegador)
        options.Cookie.MaxAge = null; // null = cookie de sesión, se borra al cerrar navegador

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                if (context.Properties.ExpiresUtc.HasValue &&
                    context.Properties.ExpiresUtc.Value < DateTimeOffset.UtcNow)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            },
            OnRedirectToLogin = context =>
            {
                if (EsSolicitudAjax(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (EsSolicitudAjax(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSession(options =>
{
    // Sesión expira después de la duración configurada de inactividad
    options.IdleTimeout = TimeSpan.FromMinutes(20); // Timeout razonable para la sesión del servidor
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Cookie de sesión NO persistente (se borra al cerrar navegador)
    options.Cookie.MaxAge = null; // null = cookie de sesión
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("es-AR");
    options.SupportedCultures = new[] { new CultureInfo("es-AR"), new CultureInfo("es-MX"), new CultureInfo("es-ES") };
    options.SupportedUICultures = new[] { new CultureInfo("es-AR"), new CultureInfo("es-MX"), new CultureInfo("es-ES") };
    // Do not let the browser's Accept-Language header override the application's regional default.
    // Explicit region choices remain available through the query string or culture cookie.
    options.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider()
    };
});

// NUEVO: Configuración mejorada para FirestoreDb que funciona en local y producción
builder.Services.AddSingleton(provider =>
{
    if (localEmulators)
        return new FirestoreDbBuilder
        {
            ProjectId = LocalEmulatorSettings.ProjectId,
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();

    GoogleCredential credential;

    // Verificar si existe variable de entorno con credenciales JSON
    var firebaseCredentialsJson = builder.Configuration["FIREBASE_CREDENTIALS"];

    if (!string.IsNullOrEmpty(firebaseCredentialsJson))
    {
        // Producción: usar credenciales desde variable de entorno
        credential = GoogleCredential.FromJson(firebaseCredentialsJson);
    }
    else if (builder.Environment.IsDevelopment())
    {
        // Desarrollo local: usar archivo JSON
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Utils", "loginmvc.json");
        if (File.Exists(path))
        {
            credential = GoogleCredential.FromFile(path);
        }
        else
        {
            // Fallback a Application Default Credentials
            credential = GoogleCredential.GetApplicationDefault();
        }
    }
    else
    {
        // Firebase App Hosting: usar Application Default Credentials
        // Google Cloud automaticamente proporciona las credenciales
        credential = GoogleCredential.GetApplicationDefault();
    }

    return new FirestoreDbBuilder
    {
        ProjectId = "aplicacion-lavadero",
        Credential = credential
    }.Build();
});

// NUEVO: Servicio NHTSA API con HttpClient
builder.Services.AddHttpClient<Firebase.Services.CarQueryService>(client =>
{
    client.BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/vehicles/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<Firebase.Services.IVehicleCatalogueSnapshotStore, Firebase.Services.FirestoreVehicleCatalogueSnapshotStore>();
builder.Services.AddScoped<Firebase.Services.IApprovedVehicleCatalogue, Firebase.Services.FirestoreApprovedVehicleCatalogue>();
builder.Services.AddScoped<Firebase.Services.VehicleCatalogueReviewService>();
builder.Services.AddScoped<Firebase.Services.ICarQueryService>(provider => new Firebase.Services.HybridVehicleCatalogue(
    provider.GetRequiredService<Firebase.Services.CarQueryService>(),
    provider.GetRequiredService<Firebase.Services.IVehicleCatalogueSnapshotStore>(),
    provider.GetRequiredService<ILogger<Firebase.Services.HybridVehicleCatalogue>>(),
    provider.GetRequiredService<Firebase.Services.IApprovedVehicleCatalogue>()));

builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<RolService>();
builder.Services.AddScoped<PersonalService>();
builder.Services.AddScoped<ServicioService>();
builder.Services.AddScoped<TipoServicioService>();
builder.Services.AddScoped<TipoVehiculoService>();
builder.Services.AddScoped<PaqueteServicioService>();
builder.Services.AddScoped<ConfiguracionService>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<VehiculoService>();
builder.Services.AddScoped<TipoDocumentoService>();
builder.Services.AddScoped<Firebase.Services.LavadoService>();
builder.Services.AddScoped<Firebase.Services.LavaderoInfoService>();
builder.Services.AddHttpClient<Firebase.Services.AuthenticationService>();
builder.Services.AddScoped<Firebase.Services.AuthenticationService>();

// Configuración WhatsApp Business API
builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsApp"));

// HttpClient para Meta WhatsApp API
builder.Services.AddHttpClient<Firebase.Services.MetaWhatsAppService>(client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Servicios WhatsApp
builder.Services.AddScoped<Firebase.Services.WhatsAppSessionService>();
builder.Services.AddScoped<Firebase.Services.WhatsAppFlowService>();

// Background Service para limpieza automática de sesiones
if (!localEmulators)
    builder.Services.AddHostedService<Firebase.BackgroundServices.WhatsAppSessionCleanupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseRequestLocalization(); // Puede ir aquí
app.UseSessionActivity(); // Middleware de inactividad

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// NUEVO: Configuracion mejorada de FirebaseApp
try
{
    if (localEmulators)
    {
        FirebaseApp.Create(new AppOptions
        {
            ProjectId = LocalEmulatorSettings.ProjectId,
            Credential = GoogleCredential.FromAccessToken("owner")
        });
    }
    else
    {
    GoogleCredential credential;
    var firebaseCredentialsJson = builder.Configuration["FIREBASE_CREDENTIALS"];

    if (!string.IsNullOrEmpty(firebaseCredentialsJson))
    {
        credential = GoogleCredential.FromJson(firebaseCredentialsJson);
    }
    else if (builder.Environment.IsDevelopment())
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Utils", "loginmvc.json");
        credential = File.Exists(path)
            ? GoogleCredential.FromFile(path)
            : GoogleCredential.GetApplicationDefault();
    }
    else
    {
        credential = GoogleCredential.GetApplicationDefault();
    }

    FirebaseApp.Create(new AppOptions()
    {
        Credential = credential
    });
    }
}
catch (Exception ex)
{
    // Log del error (considera agregar un logger aqui)
    Console.WriteLine($"Error al inicializar FirebaseApp: {ex.Message}");
    throw;
}

if (seedLocalEmulators)
{
    await LocalEmulatorSeed.RunAsync(app.Services.GetRequiredService<FirestoreDb>(), FirebaseApp.DefaultInstance, app.Environment.IsDevelopment());
    return;
}

app.Run();
