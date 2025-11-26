using FirebaseAdmin.Auth;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Text;
using Google.Cloud.Firestore;
using Firebase.Models;
using static Firebase.Models.AuthModels;

namespace Firebase.Services
{
    /// <summary>
    /// Servicio para manejar operaciones de autenticación con Firebase.
    /// Proporciona métodos para login, registro y autenticación con Google.
    /// </summary>
    public class AuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly FirestoreDb _firestore;
        private readonly AuditService _auditService;
        private readonly HttpClient _httpClient;
        private readonly string _firebaseApiKey;

        /// <summary>
        /// Constructor del servicio de autenticación.
        /// </summary>
        /// <param name="configuration">Configuración de la aplicación</param>
        /// <param name="firestore">Instancia de FirestoreDb</param>
        /// <param name="auditService">Servicio de auditoría</param>
        /// <param name="httpClient">Cliente HTTP para llamadas a Firebase</param>
        public AuthenticationService(IConfiguration configuration, FirestoreDb firestore, AuditService auditService, HttpClient httpClient)
        {
            _configuration = configuration;
            _firestore = firestore;
            _auditService = auditService;
            _httpClient = httpClient;
            _firebaseApiKey = _configuration["Firebase:ApiKey"] ?? throw new InvalidOperationException("Firebase API Key no configurada");
            
            // Aumentar timeout para navegadores con bloqueo de trackers (Brave, Firefox con privacidad estricta)
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Autentica un usuario con email y contraseña usando Firebase.
        /// </summary>
        /// <param name="email">Email del usuario</param>
        /// <param name="password">Contraseña del usuario</param>
        /// <returns>Resultado de la operación de autenticación</returns>
        public async Task<AuthenticationResult> AuthenticateWithEmailAsync(string email, string password)
        {
            try
            {
                var loginInfo = new
                {
                    email = email,
                    password = password,
                    returnSecureToken = true
                };

                var content = new StringContent(JsonConvert.SerializeObject(loginInfo), Encoding.UTF8, "application/json");
                var uri = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_firebaseApiKey}";
                var response = await _httpClient.PostAsync(uri, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    var firebaseError = JsonConvert.DeserializeObject<FirebaseErrorResponse>(errorResponse);
                    var errorCode = firebaseError?.error?.message?.Split(' ').FirstOrDefault() ?? "UNKNOWN_ERROR";

                    return AuthenticationResult.Failure(GetFirebaseErrorMessage(errorCode));
                }

                var responseData = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonConvert.DeserializeObject<FirebaseLoginResponse>(responseData);

                if (loginResponse?.localId == null)
                {
                    return AuthenticationResult.Failure("Error al procesar la respuesta de autenticación.");
                }

                // NUEVO: Verificar que el email esté verificado
                var userRecord = await FirebaseAuth.DefaultInstance.GetUserAsync(loginResponse.localId);
                if (!userRecord.EmailVerified)
                {
                    return AuthenticationResult.Failure("Debe verificar su correo electrónico antes de iniciar sesión. Revise su bandeja de entrada.");
                }

                // Verificar estado del empleado en Firestore
                var employeeDoc = await _firestore.Collection("empleados").Document(loginResponse.localId).GetSnapshotAsync();

                if (!employeeDoc.Exists)
                {
                    return AuthenticationResult.Failure("Cuenta no encontrada.");
                }

                var estado = employeeDoc.GetValue<string>("Estado");

                // NUEVO: Si el estado es "Pendiente" y el email está verificado, activar cuenta
                if (estado == "Pendiente" && userRecord.EmailVerified)
                {
                    await _firestore.Collection("empleados").Document(loginResponse.localId).UpdateAsync(new Dictionary<string, object>
            {
                { "Estado", "Activo" },
                { "EmailVerificado", true }
            });
                    estado = "Activo";
                }

                if (estado != "Activo")
                {
                    return AuthenticationResult.Failure("Cuenta inactiva, contacte con el administrador.");
                }

                var userInfo = new UserInfo
                {
                    Uid = loginResponse.localId,
                    Email = email,
                    Name = employeeDoc.GetValue<string>("Nombre"),
                    Role = employeeDoc.GetValue<string>("Rol")
                };

                await _auditService.LogEvent(
                userId: userInfo.Uid,
                userEmail: userInfo.Email,
                action: "Inicio de sesión tradicional",
                targetId: userInfo.Uid,
                targetType: "Empleado");

                return AuthenticationResult.Success(userInfo);
            }
            catch (Exception)
            {
                return AuthenticationResult.Failure("Error al iniciar sesión. Por favor, intente de nuevo.");
            }
        }

        /// <summary>
        /// Registra un nuevo usuario en Firebase Authentication y Firestore.
        /// AHORA: Envía email de verificación en lugar de autenticar automáticamente
        /// </summary>
        /// <param name="request">Datos de registro del usuario</param>
        /// <returns>Resultado de la operación de registro</returns>
        public async Task<AuthenticationResult> RegisterUserAsync(RegisterRequest request)
        {
            try
            {
                // 1. Crear usuario en Firebase Authentication
                var userRecordArgs = new UserRecordArgs
                {
                    Email = request.Email,
                    Password = request.Password,
                    DisplayName = request.NombreCompleto,
                    EmailVerified = false
                };

                var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userRecordArgs);

                // 2. Guardar datos en Firestore con estado Pendiente
                var employeeData = new Dictionary<string, object>
        {
            { "Uid", userRecord.Uid },
            { "Nombre", request.NombreCompleto },
            { "Email", request.Email },
            { "Rol", "Empleado" },
            { "Estado", "Pendiente" },
            { "EmailVerificado", false }
        };

                await _firestore.Collection("empleados")
                    .Document(userRecord.Uid)
                    .SetAsync(employeeData);

                // 3. Disparar envío de email de verificación usando plantilla de Firebase
                try
                {
                    await TriggerFirebaseTemplateVerificationEmailAsync(request.Email, request.Password);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error enviando verificación: {ex.Message}");
                    // No abortar el registro, solo log
                }

                // 4. Auditoría
                await _auditService.LogEvent(
                    userRecord.Uid,
                    request.Email,
                    "Registro de usuario (pendiente verificación)",
                    userRecord.Uid,
                    "Empleado");

                // 5. Devolver éxito sin autenticar
                return AuthenticationResult.Success(new UserInfo
                {
                    Uid = userRecord.Uid,
                    Email = request.Email,
                    Name = request.NombreCompleto,
                    Role = "Empleado"
                });
            }
            catch (FirebaseAuthException ex)
            {
                var errorCode = ex.AuthErrorCode.ToString();
                return AuthenticationResult.Failure($"Error al registrar el usuario: {GetFirebaseErrorMessage(errorCode)}");
            }
            catch (Exception ex)
            {
                return AuthenticationResult.Failure($"Error al registrar el usuario: {ex.Message}");
            }
        }

        /// <summary>
        /// Envía un correo de recuperación de contraseña usando la plantilla de Firebase.
        /// </summary>
        /// <param name="email">Email del usuario que solicita recuperar la contraseña</param>
        /// <returns>Resultado de la operación</returns>
        public async Task<AuthenticationResult> SendPasswordResetEmailAsync(string email)
        {
            try
            {
                var resetPayload = new
                {
                    requestType = "PASSWORD_RESET",
                    email = email
                };

                var resetContent = new StringContent(
                    JsonConvert.SerializeObject(resetPayload),
                    Encoding.UTF8,
                    "application/json");

                var resetUri = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_firebaseApiKey}";
                var resetResponse = await _httpClient.PostAsync(resetUri, resetContent);

                if (!resetResponse.IsSuccessStatusCode)
                {
                    var errorResponse = await resetResponse.Content.ReadAsStringAsync();
                    var firebaseError = JsonConvert.DeserializeObject<FirebaseErrorResponse>(errorResponse);
                    var errorCode = firebaseError?.error?.message?.Split(' ').FirstOrDefault() ?? "UNKNOWN_ERROR";
                    
                    return AuthenticationResult.Failure(GetFirebaseErrorMessage(errorCode));
                }

                Console.WriteLine("Correo de recuperación de contraseña enviado mediante plantilla Firebase.");
                return AuthenticationResult.Success(new UserInfo 
                { 
                    Uid = "", 
                    Email = email, 
                    Name = "", 
                    Role = "" 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando correo de recuperación: {ex.Message}");
                return AuthenticationResult.Failure("Error al enviar el correo de recuperación. Por favor, intente de nuevo.");
            }
        }

        /// <summary>
        /// Firma en Firebase REST para obtener idToken y dispara el envío del correo
        /// usando la plantilla configurada en Authentication.
        /// No autentica al usuario en tu aplicación.
        /// </summary>
        private async Task TriggerFirebaseTemplateVerificationEmailAsync(string email, string plainPassword)
        {
            // 1. Sign in técnico para obtener idToken (no genera cookie de tu app)
            var signInPayload = new
            {
                email = email,
                password = plainPassword,
                returnSecureToken = true
            };

            var signInContent = new StringContent(
                JsonConvert.SerializeObject(signInPayload),
                Encoding.UTF8,
                "application/json");

            var signInUri = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_firebaseApiKey}";
            var signInResponse = await _httpClient.PostAsync(signInUri, signInContent);

            if (!signInResponse.IsSuccessStatusCode)
            {
                var raw = await signInResponse.Content.ReadAsStringAsync();
                throw new Exception($"Fallo en signIn técnico: {raw}");
            }

            var signInJson = await signInResponse.Content.ReadAsStringAsync();
            dynamic signInObj = JsonConvert.DeserializeObject(signInJson);
            string idToken = signInObj.idToken;

            if (string.IsNullOrWhiteSpace(idToken))
                throw new Exception("No se obtuvo idToken para verificación.");

            // 2. Enviar la verificación (usa plantilla de Firebase)
            var verifyPayload = new
            {
                requestType = "VERIFY_EMAIL",
                idToken = idToken
            };

            var verifyContent = new StringContent(
                JsonConvert.SerializeObject(verifyPayload),
                Encoding.UTF8,
                "application/json");

            var verifyUri = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_firebaseApiKey}";
            var verifyResponse = await _httpClient.PostAsync(verifyUri, verifyContent);

            if (!verifyResponse.IsSuccessStatusCode)
            {
                var raw = await verifyResponse.Content.ReadAsStringAsync();
                throw new Exception($"Fallo al solicitar envío de verificación: {raw}");
            }

            Console.WriteLine("Correo de verificación enviado mediante plantilla Firebase.");
        }

        /// <summary>
        /// Verifica un token de Google y autentica al usuario.
        /// </summary>
        /// <param name="idToken">Token de ID de Google</param>
        /// <returns>Resultado de la operación de autenticación</returns>
        public async Task<AuthenticationResult> AuthenticateWithGoogleAsync(string idToken)
        {
            try
            {
                var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken);
                var email = decodedToken.Claims["email"].ToString();
                var displayName = decodedToken.Claims["name"].ToString();
                var uid = decodedToken.Uid;

                var employeesCollection = _firestore.Collection("empleados");
                var employeeDoc = await employeesCollection.Document(uid).GetSnapshotAsync();

                string role;
                string estado;

                if (!employeeDoc.Exists)
                {
                    // Verificar si existe un empleado con este email (migración de cuentas)
                    var emailQuery = employeesCollection.WhereEqualTo("Email", email);
                    var emailSnapshot = await emailQuery.GetSnapshotAsync();

                    if (emailSnapshot.Count > 0)
                    {
                        // Migrar empleado existente
                        var existingEmployee = emailSnapshot.Documents[0];
                        role = existingEmployee.GetValue<string>("Rol");
                        estado = existingEmployee.GetValue<string>("Estado");

                        if (estado != "Activo")
                        {
                            return AuthenticationResult.Failure("Su cuenta está inactiva. Por favor, contacte al administrador.");
                        }

                        // Migrar: eliminar documento viejo y crear con UID correcto
                        await existingEmployee.Reference.DeleteAsync();

                        var migratedEmployee = new Dictionary<string, object>
                        {
                            { "Uid", uid },
                            { "Nombre", displayName },
                            { "Email", email },
                            { "Rol", role },
                            { "Estado", estado }
                        };
                        await employeesCollection.Document(uid).SetAsync(migratedEmployee);

                        await _auditService.LogEvent(uid, email, "Migración a Google Auth", uid, "Empleado");
                    }
                    else
                    {
                        // Crear nuevo empleado
                        role = "Empleado";
                        estado = "Activo";

                        var newEmployee = new Dictionary<string, object>
                        {
                            { "Uid", uid },
                            { "Nombre", displayName },
                            { "Email", email },
                            { "Rol", role },
                            { "Estado", estado }
                        };
                        await employeesCollection.Document(uid).SetAsync(newEmployee);

                        await _auditService.LogEvent(uid, email, "Registro con Google", uid, "Empleado");
                    }
                }
                else
                {
                    // Empleado ya existe
                    role = employeeDoc.GetValue<string>("Rol");
                    estado = employeeDoc.GetValue<string>("Estado");

                    if (estado != "Activo")
                    {
                        return AuthenticationResult.Failure("Su cuenta está inactiva. Por favor, contacte al administrador.");
                    }
                }

                var userInfo = new UserInfo
                {
                    Uid = uid,
                    Email = email,
                    Name = displayName,
                    Role = role
                };

                // Registrar evento de auditoría
                await _auditService.LogEvent(
                userId: uid,
                userEmail: email,
                action: "Inicio de sesión con Google",
                targetId: uid,
                targetType: "Empleado");

                return AuthenticationResult.Success(userInfo);
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                return AuthenticationResult.Failure("Error de autenticación: " + ex.Message);
            }
            catch (Exception)
            {
                return AuthenticationResult.Failure("Error al autenticar con Google. Por favor, intente de nuevo.");
            }
        }

        /// <summary>
        /// Crea los claims de autenticación para un usuario.
        /// </summary>
        /// <param name="userInfo">Información del usuario</param>
        /// <returns>Lista de claims</returns>
        public List<Claim> CreateUserClaims(UserInfo userInfo)
        {
            return new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userInfo.Uid),
                new Claim(ClaimTypes.Name, userInfo.Name),
                new Claim(ClaimTypes.Email, userInfo.Email),
                new Claim(ClaimTypes.Role, userInfo.Role)
            };
        }

        /// <summary>
        /// Traduce códigos de error de Firebase a mensajes en español.
        /// </summary>
        /// <param name="errorCode">Código de error de Firebase</param>
        /// <returns>Mensaje de error en español</returns>
        private string GetFirebaseErrorMessage(string errorCode)
        {
            return errorCode switch
            {
                "EMAIL_EXISTS" => "El correo electrónico ya está registrado.",
                "INVALID_PASSWORD" => "La contraseña es incorrecta.",
                "INVALID_EMAIL" => "El correo electrónico no es válido.",
                "USER_DISABLED" => "La cuenta ha sido deshabilitada por un administrador.",
                "EMAIL_NOT_FOUND" => "No existe ninguna cuenta con este correo electrónico.",
                "OPERATION_NOT_ALLOWED" => "Operación no permitida.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Demasiados intentos fallidos. Inténtalo más tarde.",
                "INVALID_LOGIN_CREDENTIALS" => "Credenciales de inicio de sesión inválidas.",
                _ => "Se ha producido un error al procesar la solicitud."
            };
        }
    }

    /// <summary>
    /// Resultado de una operación de autenticación.
    /// </summary>
    public class AuthenticationResult
    {
        /// <summary>
        /// Indica si la operación fue exitosa.
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// Mensaje de error si la operación falló.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Información del usuario si la operación fue exitosa.
        /// </summary>
        public UserInfo? UserInfo { get; private set; }

        private AuthenticationResult() { }

        /// <summary>
        /// Crea un resultado exitoso.
        /// </summary>
        /// <param name="userInfo">Información del usuario</param>
        /// <returns>Resultado exitoso</returns>
        public static AuthenticationResult Success(UserInfo userInfo)
        {
            return new AuthenticationResult
            {
                IsSuccess = true,
                UserInfo = userInfo
            };
        }

        /// <summary>
        /// Crea un resultado fallido.
        /// </summary>
        /// <param name="errorMessage">Mensaje de error</param>
        /// <returns>Resultado fallido</returns>
        public static AuthenticationResult Failure(string errorMessage)
        {
            return new AuthenticationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Información básica del usuario autenticado.
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// Identificador único del usuario.
        /// </summary>
        public required string Uid { get; set; }

        /// <summary>
        /// Email del usuario.
        /// </summary>
        public required string Email { get; set; }

        /// <summary>
        /// Nombre completo del usuario.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Rol del usuario en el sistema.
        /// </summary>
        public required string Role { get; set; }
    }
}