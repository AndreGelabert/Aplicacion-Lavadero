using FirebaseAdmin;
using Firebase.Models;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using static Firebase.Models.AuthModels;
using Newtonsoft.Json;
using System.Text;

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
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Por favor, complete todos los campos correctamente.";
            return View("Index");
        }

        try
        {
            var firebaseApiKey = "AIzaSyBubyUIDmvFmRIvQ--pvnw9wnQcAulJJy8"; // Reemplaza con tu API Key de Firebase
            var client = new HttpClient();
            var uri = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={firebaseApiKey}";

            var loginInfo = new
            {
                email = request.Email,
                password = request.Password,
                returnSecureToken = true
            };

            var content = new StringContent(JsonConvert.SerializeObject(loginInfo), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                var firebaseError = JsonConvert.DeserializeObject<FirebaseErrorResponse>(errorResponse);
                ViewBag.Error = $"Error: {firebaseError.error.message}";
                return View("Index");
            }

            var responseData = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonConvert.DeserializeObject<FirebaseLoginResponse>(responseData);

            // Obtener el UID del usuario desde la respuesta
            var uid = loginResponse.localId;

            // Obtener datos adicionales desde Firestore
            var employeeDoc = await _firestore.Collection("empleados").Document(uid).GetSnapshotAsync();

            if (!employeeDoc.Exists || employeeDoc.GetValue<string>("Estado") != "Activo")
            {
                ViewBag.Error = "Cuenta inactiva o no existe.";
                return View("Index");
            }

            // Crear claims y autenticar
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, uid),
            new Claim(ClaimTypes.Name, employeeDoc.GetValue<string>("Nombre")),
            new Claim(ClaimTypes.Email, request.Email),
            new Claim(ClaimTypes.Role, employeeDoc.GetValue<string>("Rol"))
        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Index", "Lavados");
        }
        catch (Exception)
        {
            ViewBag.Error = "Error al iniciar sesión. Por favor, intente de nuevo.";
            return View("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> RegisterUser(RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Por favor, complete todos los campos del registro correctamente.";
            return View("Index");
        }

        try
        {
            // Crear usuario en Firebase Authentication
            var userRecordArgs = new UserRecordArgs
            {
                Email = request.Email,
                Password = request.Password,
                DisplayName = request.NombreCompleto
            };

            var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userRecordArgs);

            // Guardar datos adicionales en Firestore sin la contraseña
            var employeeData = new Dictionary<string, object>
        {
            { "Uid", userRecord.Uid },
            { "Nombre", request.NombreCompleto },
            { "Email", request.Email },
            { "Rol", "Empleado" },
            { "Estado", "Activo" }
        };

            await _firestore.Collection("empleados").Document(userRecord.Uid).SetAsync(employeeData);

            // *** Autenticar al usuario con Firebase Authentication ***

            var firebaseApiKey = "AIzaSyBubyUIDmvFmRIvQ--pvnw9wnQcAulJJy8"; // Reemplaza con tu API Key de Firebase
            var client = new HttpClient();
            var uri = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={firebaseApiKey}";

            var loginInfo = new
            {
                email = request.Email,
                password = request.Password,
                returnSecureToken = true
            };

            var content = new StringContent(JsonConvert.SerializeObject(loginInfo), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                var firebaseError = JsonConvert.DeserializeObject<FirebaseErrorResponse>(errorResponse);
                ViewBag.Error = $"Error al autenticar el usuario: {firebaseError.error.message}";
                return View("Index");
            }

            var responseData = await response.Content.ReadAsStringAsync();
            var loginResponse = JsonConvert.DeserializeObject<FirebaseLoginResponse>(responseData);

            // Autenticar al usuario en tu aplicación
            await SignInUser(userRecord.Uid, request.Email, "Empleado", request.NombreCompleto);

            return RedirectToAction("Index", "Lavados");
        }
        catch (FirebaseAuthException ex)
        {
            ViewBag.Error = $"Error al registrar el usuario: {ex.Message}";
            return View("Index");
        }
        catch (Exception)
        {
            ViewBag.Error = "Error al registrar el usuario. Por favor, intente de nuevo.";
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
            string estado;
            if (snapshot.Count == 0)
            {
                role = "Empleado";
                estado = "Activo";
                var newEmployee = new
                {
                    Nombre = displayName,
                    Email = email,
                    Rol = role,
                    Estado = estado
                };
                await employeesCollection.AddAsync(newEmployee);
            }
            else
            {
                role = snapshot.Documents[0].GetValue<string>("Rol");
                estado = snapshot.Documents[0].GetValue<string>("Estado");

                if (estado != "Activo")
                {
                    return BadRequest(new { error = "Su cuenta está inactiva. Por favor, contacte al administrador." });
                }
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

            return Json(new { redirectUrl = Url.Action("Index", "Lavados") });
        }
        catch (Firebase.Auth.FirebaseAuthException ex)
        {
            return BadRequest(new { error = "Error de autenticación: " + ex.Message });
        }
    }
    private async Task SignInUser(string uid, string email, string role, string nombre)
    {
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, uid),
        new Claim(ClaimTypes.Name, nombre),
        new Claim(ClaimTypes.Email, email),
        new Claim(ClaimTypes.Role, role)
    };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }
}

public class GoogleLoginRequest
{
    public string IdToken { get; set; }
}

