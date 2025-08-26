namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un empleado en el sistema.
    /// Se utiliza para transferir información de empleados entre el frontend y backend.
    /// </summary>
    /// <remarks>
    /// Este modelo se utiliza principalmente en:
    /// - PersonalService para obtener y actualizar información de empleados
    /// - PersonalController para manejar operaciones CRUD de empleados
    /// - AuthenticationService para verificar el estado de empleados durante autenticación
    /// 
    /// Los datos se almacenan en Firestore en la colección "empleados".
    /// El ID corresponde al UID de Firebase Authentication del empleado.
    /// </remarks>
    public class Empleado
    {
        /// <summary>
        /// Identificador único del empleado.
        /// Corresponde al UID de Firebase Authentication.
        /// </summary>
        public required string Id { get; set; }
        
        /// <summary>
        /// Nombre completo del empleado.
        /// Se muestra en la interfaz de gestión de personal.
        /// </summary>
        public required string NombreCompleto { get; set; }
        
        /// <summary>
        /// Correo electrónico del empleado.
        /// Debe ser único en el sistema y se usa para autenticación.
        /// </summary>
        public required string Email { get; set; }
        
        /// <summary>
        /// Rol del empleado en el sistema.
        /// Valores comunes: "Administrador", "Empleado"
        /// Determina los permisos y accesos del usuario.
        /// </summary>
        public required string Rol { get; set; }
        
        /// <summary>
        /// Estado del empleado en el sistema.
        /// Valores: "Activo", "Inactivo"
        /// Solo empleados con estado "Activo" pueden autenticarse.
        /// </summary>
        public required string Estado { get; set; }
    }
}
