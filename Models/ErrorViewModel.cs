namespace FirebaseLoginCustom.Models
{
    /// <summary>
    /// Modelo para manejar la información de errores en las vistas.
    /// Se utiliza en la vista de error compartida (Error.cshtml) para mostrar información de depuración.
    /// </summary>
    /// <remarks>
    /// Este modelo se pasa a la vista Error.cshtml cuando ocurre una excepción no controlada.
    /// Se crea en LavadosController.Error() y se usa para mostrar el ID de la solicitud
    /// que causó el error, facilitando la depuración en entornos de desarrollo.
    /// </remarks>
    public class ErrorViewModel
    {
        /// <summary>
        /// ID único de la solicitud que causó el error.
        /// Puede ser el ID de la actividad actual o el TraceIdentifier del contexto HTTP.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Indica si se debe mostrar el RequestId en la vista.
        /// Retorna true si RequestId no está vacío o es nulo.
        /// </summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}