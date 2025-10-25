namespace Firebase.Models
{
    /// <summary>DTO resultado de operaciones (éxito/error).</summary>
    public class ResultadoOperacion
    {
        /// <summary>Indica si la operación fue exitosa.</summary>
        public bool EsExitoso { get; private set; }
        /// <summary>Mensaje de éxito si la operación fue exitosa.</summary>
        public string MensajeExito { get; private set; }
        /// <summary>Mensaje de error si la operación falló.</summary>
        public string MensajeError { get; private set; }

        private ResultadoOperacion() { }

        /// <summary>Crea un resultado exitoso.</summary>
        public static ResultadoOperacion CrearExito(string mensaje)
            => new ResultadoOperacion { EsExitoso = true, MensajeExito = mensaje };

        /// <summary>Crea un resultado de error.</summary>
        public static ResultadoOperacion CrearError(string mensaje)
            => new ResultadoOperacion { EsExitoso = false, MensajeError = mensaje };
    }
}
