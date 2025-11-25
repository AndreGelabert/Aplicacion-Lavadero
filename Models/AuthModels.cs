using System.ComponentModel.DataAnnotations;

namespace Firebase.Models
{
    /// <summary>
    /// Contiene modelos para las operaciones de autenticación (login y registro).
    /// Estos modelos se utilizan para validar y transportar datos del frontend al backend.
    /// </summary>
    public class AuthModels
    {
        /// <summary>
        /// Modelo para las solicitudes de registro de nuevos usuarios.
        /// Contiene validaciones específicas para garantizar datos correctos.
        /// </summary>
        /// <remarks>
        /// Se utiliza en AuthenticationService.RegisterUserAsync() y en formularios de registro.
        /// Todos los campos son obligatorios y tienen validaciones específicas.
        /// </remarks>
        public class RegisterRequest
        {
            /// <summary>
            /// Nombre completo del usuario. Solo permite letras y espacios.
            /// </summary>
            [Required(ErrorMessage = "El nombre completo es obligatorio.")]
            [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "El nombre completo solo debe contener letras.")]
            public required string NombreCompleto { get; set; }

            /// <summary>
            /// Correo electrónico del usuario. Debe tener formato válido de email.
            /// </summary>
            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
            public required string Email { get; set; }

            /// <summary>
            /// Contraseña del usuario. Debe tener al menos 6 caracteres.
            /// </summary>
            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            [MinLength(6, ErrorMessage = "La contraseña debe contener al menos 6 caracteres.")]
            public required string Password { get; set; }
        }

        /// <summary>
        /// Modelo para las solicitudes de inicio de sesión con email y contraseña.
        /// </summary>
        /// <remarks>
        /// Se utiliza en AuthenticationService.AuthenticateWithEmailAsync() y en formularios de login.
        /// Ambos campos son obligatorios.
        /// </remarks>
        public class LoginRequest
        {
            /// <summary>
            /// Correo electrónico del usuario para autenticación.
            /// </summary>
            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
            public required string Email { get; set; }

            /// <summary>
            /// Contraseña del usuario para autenticación.
            /// </summary>
            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            public required string Password { get; set; }
        }
    }
}
