using Firebase.Middleware;
using Firebase.Models;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Configuración de autenticación
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.LogoutPath = "/Lavados/Logout";
        options.AccessDeniedPath = "/Login/Index";
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
    options.DefaultRequestCulture = new RequestCulture("es-MX");
    options.SupportedCultures = new[] { new CultureInfo("es-MX"), new CultureInfo("es-ES") };
    options.SupportedUICultures = new[] { new CultureInfo("es-MX"), new CultureInfo("es-ES") };
});

// NUEVO: Configuraci�n mejorada para FirestoreDb que funciona en local y producci�n
builder.Services.AddSingleton(provider =>
{
    GoogleCredential credential;

    // Verificar si existe variable de entorno con credenciales JSON
    var firebaseCredentialsJson = builder.Configuration["FIREBASE_CREDENTIALS"];

    if (!string.IsNullOrEmpty(firebaseCredentialsJson))
    {
        // Producci�n: usar credenciales desde variable de entorno
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
        // Google Cloud autom�ticamente proporciona las credenciales
        credential = GoogleCredential.GetApplicationDefault();
    }

    return new FirestoreDbBuilder
    {
        ProjectId = "aplicacion-lavadero",
        Credential = credential
    }.Build();
});

// NUEVO: Servicio CarQuery API con HttpClient
builder.Services.AddHttpClient<Firebase.Services.ICarQueryService, Firebase.Services.CarQueryService>(client =>
{
    client.BaseAddress = new Uri("https://www.carqueryapi.com/api/0.3/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<AuditService>();
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
    pattern: "{controller=Lavados}/{action=Index}/{id?}");

// NUEVO: Configuraci�n mejorada de FirebaseApp
try
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
catch (Exception ex)
{
    // Log del error (considera agregar un logger aqu�)
    Console.WriteLine($"Error al inicializar FirebaseApp: {ex.Message}");
    throw;
}

app.Run();