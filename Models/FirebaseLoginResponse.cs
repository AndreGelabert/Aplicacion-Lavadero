// Models/FirebaseAuthResponses.cs
namespace Firebase.Models
{
    /// <summary>
    /// Modelos para manejar respuestas de la API REST de Firebase Authentication.
    /// Estos modelos se utilizan en AuthenticationService para deserializar las respuestas HTTP de Firebase.
    /// </summary>
    
    /// <summary>
    /// Respuesta exitosa de Firebase Authentication para operaciones de login.
    /// Contiene los tokens y información básica del usuario autenticado.
    /// </summary>
    /// <remarks>
    /// Se utiliza para deserializar respuestas de:
    /// - signInWithPassword (login con email/password)
    /// - signInWithCustomToken (login personalizado)
    /// Usado en AuthenticationService.AuthenticateWithEmailAsync()
    /// </remarks>
    public class FirebaseLoginResponse
    {
        /// <summary>
        /// UID único del usuario en Firebase Authentication.
        /// </summary>
        public string localId { get; set; } // UID del usuario
        
        /// <summary>
        /// Token de ID JWT para autenticar al usuario en el frontend.
        /// </summary>
        public string idToken { get; set; }
        
        /// <summary>
        /// Correo electrónico del usuario autenticado.
        /// </summary>
        public string email { get; set; }
        
        /// <summary>
        /// Token de actualización para renovar el idToken cuando expire.
        /// </summary>
        public string refreshToken { get; set; }
        
        /// <summary>
        /// Tiempo en segundos hasta que expire el idToken.
        /// </summary>
        public string expiresIn { get; set; }
    }

    /// <summary>
    /// Respuesta de error de Firebase Authentication cuando una operación falla.
    /// </summary>
    /// <remarks>
    /// Se utiliza para deserializar respuestas de error HTTP de Firebase Authentication.
    /// Usado en AuthenticationService.AuthenticateWithEmailAsync() para manejar errores.
    /// </remarks>
    public class FirebaseErrorResponse
    {
        /// <summary>
        /// Objeto que contiene los detalles del error.
        /// </summary>
        public FirebaseError error { get; set; }
    }

    /// <summary>
    /// Información detallada de un error de Firebase Authentication.
    /// </summary>
    public class FirebaseError
    {
        /// <summary>
        /// Código HTTP del error.
        /// </summary>
        public int code { get; set; }
        
        /// <summary>
        /// Mensaje de error principal. Puede contener códigos como "EMAIL_NOT_FOUND", "INVALID_PASSWORD", etc.
        /// </summary>
        public string message { get; set; }
        
        /// <summary>
        /// Lista de errores detallados adicionales.
        /// </summary>
        public IList<FirebaseErrorDetail> errors { get; set; }
    }

    /// <summary>
    /// Detalles específicos de un error individual de Firebase.
    /// </summary>
    public class FirebaseErrorDetail
    {
        /// <summary>
        /// Mensaje descriptivo del error específico.
        /// </summary>
        public string message { get; set; }
        
        /// <summary>
        /// Dominio al que pertenece el error (ej: "global", "usageLimits").
        /// </summary>
        public string domain { get; set; }
        
        /// <summary>
        /// Razón específica del error (ej: "invalid", "required").
        /// </summary>
        public string reason { get; set; }
    }
}
