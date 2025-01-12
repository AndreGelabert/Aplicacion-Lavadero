using Firebase.Models;
using FirebaseAdmin.Auth;
using FirebaseAdmin.Auth.Hash;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly FirestoreDb _firestoreDb;

        public AuthController()
        {
            _firestoreDb = FirestoreDb.Create("aplicacion-lavadero");
        }

        [HttpPost("google-signin")]
        public async Task<IActionResult> GoogleSignIn([FromBody] string googleToken)
        {
            try
            {
                // Verificar el token de Google
                var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(googleToken);
                var email = decodedToken.Claims["email"].ToString();
                var displayName = decodedToken.Claims["name"].ToString();

                // Verificar si el empleado ya existe en Firestore
                var employeesCollection = _firestoreDb.Collection("empleados");
                var query = employeesCollection.WhereEqualTo("Email", email);
                var snapshot = await query.GetSnapshotAsync();

                if (!snapshot.Documents.Any())
                {
                    // Registrar nuevo empleado si no existe
                    var newEmployee = new
                    {
                        NombreCompleto = displayName,
                        Email = email,
                        Rol = "Empleado",
                        MetodoRegistro = "Google"
                    };
                    await employeesCollection.AddAsync(newEmployee);
                }

                return Ok(new { message = "Inicio de sesión exitoso", email, displayName });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthModels.RegisterRequest request)
        {
            try
            {
                // Verificar si el empleado ya existe
                var employeesCollection = _firestoreDb.Collection("empleados");
                var query = employeesCollection.WhereEqualTo("Email", request.Email);
                var snapshot = await query.GetSnapshotAsync();

                if (snapshot.Documents.Any())
                {
                    return BadRequest(new { message = "El correo ya está registrado." });
                }

                // Encriptar contraseña
                string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

                // Guardar en Firestore
                var newEmployee = new
                {
                    NombreCompleto = request.NombreCompleto,
                    Email = request.Email,
                    Rol = "Empleado",
                    Password = hashedPassword,
                    MetodoRegistro = "Tradicional"
                };

                await employeesCollection.AddAsync(newEmployee);

                return Ok(new { message = "Registro exitoso." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthModels.LoginRequest request)
        {
            try
            {
                var employeesCollection = _firestoreDb.Collection("empleados");
                var query = employeesCollection.WhereEqualTo("Email", request.Email);
                var snapshot = await query.GetSnapshotAsync();

                if (!snapshot.Documents.Any())
                {
                    return Unauthorized(new { message = "Correo o contraseña incorrectos." });
                }

                var employee = snapshot.Documents.First();
                var data = employee.ToDictionary();

                // Verificar que es un usuario registrado de forma tradicional
                if (data["MetodoRegistro"].ToString() != "Tradicional")
                {
                    return Unauthorized(new { message = "Por favor, use Google para iniciar sesión." });
                }

                // Verificar contraseña
                string hashedPassword = data["Password"].ToString();
                if (!BCrypt.Net.BCrypt.Verify(request.Password, hashedPassword))
                {
                    return Unauthorized(new { message = "Correo o contraseña incorrectos." });
                }

                return Ok(new { message = "Inicio de sesión exitoso.", NombreCompleto = data["NombreCompleto"] });

            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

}
