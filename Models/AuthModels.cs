using System.ComponentModel.DataAnnotations;

namespace Firebase.Models
{
    public class AuthModels
    {
        public class RegisterRequest
        {
            [Required(ErrorMessage = "El nombre completo es obligatorio.")]
            [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "El nombre completo solo debe contener letras.")]
            public required string NombreCompleto { get; set; }

            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
            public required string Email { get; set; }

            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            [MinLength(6, ErrorMessage = "La contraseña debe contener al menos 6 caracteres.")]
            public required string Password { get; set; }
        }


        public class LoginRequest
        {
            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
            public required string Email { get; set; }

            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            public required string Password { get; set; }
        }
    }
}
