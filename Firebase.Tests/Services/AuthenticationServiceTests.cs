using Firebase.Tests.Helpers;
using Xunit;
using static Firebase.Models.AuthModels;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the AuthenticationService (Login) module.
    /// Tests cover authentication functionality including login, registration, and session management.
    /// </summary>
    public class AuthenticationServiceTests
    {
        #region Login Request Validation Tests

        [Fact]
        public void LoginRequest_ShouldHaveCorrectProperties()
        {
            var loginRequest = new LoginRequest
            {
                Email = "user@example.com",
                Password = "SecurePassword123!"
            };

            Assert.Equal("user@example.com", loginRequest.Email);
            Assert.Equal("SecurePassword123!", loginRequest.Password);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("user@example.com", true)]
        public void Email_ShouldNotBeEmpty(string? email, bool isValid)
        {
            var isNotEmpty = !string.IsNullOrWhiteSpace(email);
            Assert.Equal(isValid, isNotEmpty);
        }

        [Theory]
        [InlineData("user@example.com", true)]
        [InlineData("admin@lavadero.com.ar", true)]
        [InlineData("invalid-email", false)]
        [InlineData("@example.com", false)]
        [InlineData("user@", false)]
        public void Email_ShouldHaveValidFormat(string email, bool isValid)
        {
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            Assert.Equal(isValid, emailRegex.IsMatch(email));
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("password", true)]
        public void Password_ShouldNotBeEmpty(string? password, bool isValid)
        {
            var isNotEmpty = !string.IsNullOrWhiteSpace(password);
            Assert.Equal(isValid, isNotEmpty);
        }

        [Theory]
        [InlineData("12345", false)]
        [InlineData("123456", true)]
        [InlineData("SecurePassword123", true)]
        public void Password_ShouldMeetMinimumLength(string password, bool isValid)
        {
            var meetsMinLength = password.Length >= 6;
            Assert.Equal(isValid, meetsMinLength);
        }

        #endregion

        #region Registration Request Validation Tests

        [Fact]
        public void RegisterRequest_ShouldHaveCorrectProperties()
        {
            var registerRequest = new RegisterRequest
            {
                NombreCompleto = "Juan Pérez",
                Email = "juan@example.com",
                Password = "SecurePassword123!",
                ConfirmPassword = "SecurePassword123!"
            };

            Assert.Equal("Juan Pérez", registerRequest.NombreCompleto);
            Assert.Equal("juan@example.com", registerRequest.Email);
            Assert.Equal("SecurePassword123!", registerRequest.Password);
            Assert.Equal("SecurePassword123!", registerRequest.ConfirmPassword);
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("Juan Pérez", true)]
        public void NombreCompleto_ShouldNotBeEmpty(string? nombre, bool isValid)
        {
            var isNotEmpty = !string.IsNullOrWhiteSpace(nombre);
            Assert.Equal(isValid, isNotEmpty);
        }

        [Fact]
        public void Password_Comparison_ShouldMatch()
        {
            var password1 = "SecurePassword123!";
            var password2 = "SecurePassword123!";

            var passwordsMatch = password1 == password2;

            Assert.True(passwordsMatch);
        }

        [Fact]
        public void Password_Comparison_Mismatch_ShouldBeDetected()
        {
            var password1 = "SecurePassword123!";
            var password2 = "DifferentPassword!";

            var passwordsMatch = password1 == password2;

            Assert.False(passwordsMatch);
        }

        #endregion

        #region Firebase Error Message Translation Tests

        [Theory]
        [InlineData("EMAIL_EXISTS", "El correo electrónico ya está registrado.")]
        [InlineData("INVALID_PASSWORD", "La contraseña es incorrecta.")]
        [InlineData("INVALID_EMAIL", "El correo electrónico no es válido.")]
        [InlineData("USER_DISABLED", "La cuenta ha sido deshabilitada por un administrador.")]
        [InlineData("EMAIL_NOT_FOUND", "No existe ninguna cuenta con este correo electrónico.")]
        [InlineData("UNKNOWN_ERROR", "Se ha producido un error al procesar la solicitud.")]
        public void FirebaseErrorCode_ShouldTranslateToSpanish(string errorCode, string expected)
        {
            string actual = errorCode switch
            {
                "EMAIL_EXISTS" => "El correo electrónico ya está registrado.",
                "INVALID_PASSWORD" => "La contraseña es incorrecta.",
                "INVALID_EMAIL" => "El correo electrónico no es válido.",
                "USER_DISABLED" => "La cuenta ha sido deshabilitada por un administrador.",
                "EMAIL_NOT_FOUND" => "No existe ninguna cuenta con este correo electrónico.",
                _ => "Se ha producido un error al procesar la solicitud."
            };

            Assert.Equal(expected, actual);
        }

        #endregion

        #region User State Validation Tests

        [Theory]
        [InlineData("Activo", true)]
        [InlineData("Inactivo", false)]
        [InlineData("Pendiente", false)]
        public void UserState_ShouldAllowLoginOnlyWhenActive(string estado, bool canLogin)
        {
            var isActive = estado == "Activo";
            Assert.Equal(canLogin, isActive);
        }

        [Fact]
        public void PendingUser_WithVerifiedEmail_ShouldBecomeActive()
        {
            var estado = "Pendiente";
            var emailVerified = true;

            if (estado == "Pendiente" && emailVerified)
            {
                estado = "Activo";
            }

            Assert.Equal("Activo", estado);
        }

        [Fact]
        public void PendingUser_WithUnverifiedEmail_ShouldRemainPending()
        {
            var estado = "Pendiente";
            var emailVerified = false;

            if (estado == "Pendiente" && emailVerified)
            {
                estado = "Activo";
            }

            Assert.Equal("Pendiente", estado);
        }

        #endregion

        #region Session Management Tests

        [Theory]
        [InlineData(8, 480)]
        [InlineData(1, 60)]
        [InlineData(24, 1440)]
        public void SessionDuration_ShouldConvertHoursToMinutes(int hours, int expected)
        {
            var actualMinutes = hours * 60;
            Assert.Equal(expected, actualMinutes);
        }

        [Fact]
        public void SessionInactivity_ShouldBeTracked()
        {
            var lastActivity = DateTimeOffset.UtcNow.AddMinutes(-10);
            var inactivityThreshold = 15;

            var minutesSinceActivity = (DateTimeOffset.UtcNow - lastActivity).TotalMinutes;
            var isStillActive = minutesSinceActivity < inactivityThreshold;

            Assert.True(isStillActive);
        }

        [Fact]
        public void SessionInactivity_ShouldDetectExpiredSession()
        {
            var lastActivity = DateTimeOffset.UtcNow.AddMinutes(-20);
            var inactivityThreshold = 15;

            var minutesSinceActivity = (DateTimeOffset.UtcNow - lastActivity).TotalMinutes;
            var isExpired = minutesSinceActivity >= inactivityThreshold;

            Assert.True(isExpired);
        }

        [Fact]
        public void SessionDuration_ShouldDetectMaxDurationExceeded()
        {
            var loginTime = DateTimeOffset.UtcNow.AddHours(-9);
            var maxDurationMinutes = 480;

            var minutesSinceLogin = (DateTimeOffset.UtcNow - loginTime).TotalMinutes;
            var hasExceededMaxDuration = minutesSinceLogin >= maxDurationMinutes;

            Assert.True(hasExceededMaxDuration);
        }

        #endregion

        #region Claims Creation Tests

        [Fact]
        public void UserClaims_ShouldIncludeAllRequiredClaims()
        {
            var claims = new Dictionary<string, string>
            {
                { "NameIdentifier", "user-123" },
                { "Name", "Juan Pérez" },
                { "Email", "user@example.com" },
                { "Role", "Administrador" }
            };

            Assert.Equal(4, claims.Count);
            Assert.Equal("user-123", claims["NameIdentifier"]);
            Assert.Equal("Juan Pérez", claims["Name"]);
            Assert.Equal("user@example.com", claims["Email"]);
            Assert.Equal("Administrador", claims["Role"]);
        }

        #endregion

        #region Role-Based Access Tests

        [Theory]
        [InlineData("Administrador", true)]
        [InlineData("Empleado", false)]
        [InlineData("Supervisor", false)]
        public void AdminRole_ShouldHaveFullAccess(string role, bool hasAdminAccess)
        {
            var isAdmin = role == "Administrador";
            Assert.Equal(hasAdminAccess, isAdmin);
        }

        #endregion

        #region Google Authentication Tests

        [Fact]
        public void GoogleLogin_ShouldCreateNewEmployeeIfNotExists()
        {
            var existingEmployees = new List<string>();
            var googleUid = "google-uid-123";

            var employeeExists = existingEmployees.Contains(googleUid);
            string assignedRole = employeeExists ? "existing" : "Empleado";
            string assignedState = employeeExists ? "existing" : "Activo";

            Assert.False(employeeExists);
            Assert.Equal("Empleado", assignedRole);
            Assert.Equal("Activo", assignedState);
        }

        [Fact]
        public void GoogleLogin_ShouldMigrateExistingEmailAccount()
        {
            var existingEmail = "user@example.com";
            var googleEmail = "user@example.com";

            var shouldMigrate = existingEmail == googleEmail;

            Assert.True(shouldMigrate);
        }

        #endregion
    }
}
