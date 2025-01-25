using Firebase.Auth;
using Firebase.Models;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;

public class LoginController : Controller
{
    private readonly FirestoreDb _firestore;

    public LoginController()
    {
        string path = AppDomain.CurrentDomain.BaseDirectory + @"Utils\loginmvc.json";
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
        _firestore = FirestoreDb.Create("aplicacion-lavadero");
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var employeesCollection = _firestore.Collection("empleados");
        var query = employeesCollection.WhereEqualTo("Email", request.Email);
        var snapshot = await query.GetSnapshotAsync();

        if (snapshot.Count == 0)
        {
            ViewBag.Error = "Correo electrónico o contraseña incorrectos.";
            return View("Index");
        }

        var employee = snapshot.Documents[0];
        var storedPassword = employee.GetValue<string>("Password");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, storedPassword))
        {
            ViewBag.Error = "Correo electrónico o contraseña incorrectos.";
            return View("Index");
        }

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, employee.GetValue<string>("Nombre")),
        new Claim(ClaimTypes.Email, request.Email),
        new Claim(ClaimTypes.Role, employee.GetValue<string>("Rol"))
    };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> RegisterUser([FromForm] AuthModels.RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            // Devuelve la vista con el estado del modelo para mostrar los errores en el modal
            return View("Index");
        }

        var employeesCollection = _firestore.Collection("empleados");
        var query = employeesCollection.WhereEqualTo("Email", request.Email);
        var snapshot = await query.GetSnapshotAsync();

        if (snapshot.Count == 0)
        {
            var newEmployee = new
            {
                Nombre = request.NombreCompleto,
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Rol = "Empleado"
            };
            await employeesCollection.AddAsync(newEmployee);

            // Iniciar sesión automáticamente después del registro
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, request.NombreCompleto),
            new Claim(ClaimTypes.Email, request.Email),
            new Claim(ClaimTypes.Role, "Empleado")
        };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            return RedirectToAction("Index", "Home");
        }
        else
        {
            ModelState.AddModelError("email", "El correo electrónico ya está registrado.");
            return View("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var decodedToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
            var email = decodedToken.Claims["email"].ToString();
            var displayName = decodedToken.Claims["name"].ToString();

            var employeesCollection = _firestore.Collection("empleados");
            var query = employeesCollection.WhereEqualTo("Email", email);
            var snapshot = await query.GetSnapshotAsync();

            string role;
            if (snapshot.Count == 0)
            {
                role = "Empleado";
                var newEmployee = new
                {
                    Nombre = displayName,
                    Email = email,
                    Rol = role
                };
                await employeesCollection.AddAsync(newEmployee);
            }
            else
            {
                role = snapshot.Documents[0].GetValue<string>("Rol");
            }

            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, displayName),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            return Json(new { redirectUrl = Url.Action("Index", "Home") });
        }
        catch (Firebase.Auth.FirebaseAuthException ex)
        {
            return BadRequest(new { error = "Error de autenticación: " + ex.Message });
        }
    }

}

public class GoogleLoginRequest
{
    public string IdToken { get; set; }
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }
}

