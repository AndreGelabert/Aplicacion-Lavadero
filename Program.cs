using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.LogoutPath = "/Lavados/Logout";
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
