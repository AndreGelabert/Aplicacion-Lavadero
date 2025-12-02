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
            /// Nombre completo del usuario. Debe incluir al menos nombre y apellido (mínimo 2 letras por palabra).
            /// Formato requerido: Nombre(s) Apellido(s)
            /// Ejemplos válidos: "Juan Pérez", "María García López", "José Luis Rodríguez Martínez"
            /// </summary>
            [Required(ErrorMessage = "El nombre completo es obligatorio.")]
            [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ]{2,}(\s+[a-zA-ZáéíóúÁÉÍÓÚñÑ]{2,})+$", 
                ErrorMessage = "Debe ingresar al menos nombre y apellido (mínimo 2 letras por palabra). Ejemplo: Juan Pérez")]
            public required string NombreCompleto { get; set; }

            /// <summary>
            /// Correo electrónico del usuario. Debe tener formato válido de email.
            /// </summary>
            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
            public required string Email { get; set; }

            /// <summary>
            /// Contraseña del usuario. Debe tener al menos 6 caracteres, una mayúscula, una minúscula y un número.
            /// </summary>
            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            [MinLength(6, ErrorMessage = "La contraseña debe contener al menos 6 caracteres.")]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$", 
                ErrorMessage = "La contraseña debe contener al menos una mayúscula, una minúscula y un número.")]
            public required string Password { get; set; }

            /// <summary>
            /// Confirmación de la contraseña. Debe coincidir con Password.
            /// </summary>
            [Required(ErrorMessage = "Debe confirmar la contraseña.")]
            [Compare("Password", ErrorMessage = "Las contraseñas no coinciden.")]
            public required string ConfirmPassword { get; set; }
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
