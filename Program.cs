using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// CAMBIO: Configuración mejorada de autenticación con tiempos de sesión
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.LogoutPath = "/Lavados/Logout";
        options.AccessDeniedPath = "/Login/Index";
        
        // NUEVO: Configuración de tiempos de sesión
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15); // Tiempo de inactividad
        options.SlidingExpiration = true; // Renovar sesión con cada actividad
        
        // NUEVO: Eventos para manejar expiración
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                // Verificar si la sesión ha expirado
                if (context.Properties.ExpiresUtc.HasValue && 
                    context.Properties.ExpiresUtc.Value < DateTimeOffset.UtcNow)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });
// Configurar Session para tracking adicional
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15); // Mismo tiempo que la cookie
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Solo HTTPS
});
// Asegurar codificación UTF-8
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("es-MX");
    options.SupportedCultures = new[] { new CultureInfo("es-MX"), new CultureInfo("es-ES") };
    options.SupportedUICultures = new[] { new CultureInfo("es-MX"), new CultureInfo("es-ES") };
});
// Registrar FirestoreDb como un servicio singleton
builder.Services.AddSingleton(provider =>
{
    string path = AppDomain.CurrentDomain.BaseDirectory + @"Utils\loginmvc.json";
    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
    return FirestoreDb.Create("aplicacion-lavadero");
});
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<PersonalService>();
builder.Services.AddScoped<ServicioService>();
builder.Services.AddScoped<TipoServicioService>();
builder.Services.AddScoped<TipoVehiculoService>();
builder.Services.AddHttpClient<Firebase.Services.AuthenticationService>();
builder.Services.AddScoped<Firebase.Services.AuthenticationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// NUEVO: Agregar Session antes de Authentication
app.UseSession();

// Usar localización
app.UseRequestLocalization();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Lavados}/{action=Index}/{id?}");

FirebaseApp.Create(new AppOptions()
{
    Credential = GoogleCredential.FromFile("Utils/loginmvc.json")
});

app.Run();
