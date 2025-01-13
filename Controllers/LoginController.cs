using Firebase.Auth;
using Firebase.Models;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
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

            if (snapshot.Count == 0)
            {
                var newEmployee = new
                {
                    Nombre = displayName,
                    Email = email,
                    Rol = "empleado"
                };
                await employeesCollection.AddAsync(newEmployee);
            }

            TempData["UserEmail"] = email;
            TempData["UserName"] = displayName;

            return Ok();
        }
        catch (Firebase.Auth.FirebaseAuthException ex)
        {
            return BadRequest(new { error = "Error de autenticación: " + ex.Message });
        }
    }

    public IActionResult Dashboard()
    {
        if (TempData["UserEmail"] == null)
        {
            return RedirectToAction("Index");
        }

        ViewBag.UserName = TempData["UserName"];
        return View();
    }
}

public class GoogleLoginRequest
{
    public string IdToken { get; set; }
}
