using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un tipo de vehículo en el sistema de lavadero.
    /// Se utiliza para categorizar los vehículos según su tamaño y características.
    /// </summary>
    /// <remarks>
    /// Este modelo se almacena en Firestore en la colección "tipos_vehiculo".
    /// Se utiliza en:
    /// - TipoVehiculoService para operaciones CRUD
    /// - ServicioController para llenar dropdowns y validaciones
    /// - Vistas de servicios para mostrar opciones disponibles
    /// - Cálculo de precios diferenciados según el tipo de vehículo
    /// 
    /// Ejemplos de tipos de vehículo: "Automóvil", "SUV", "Camioneta", "Motocicleta", etc.
    /// </remarks>
    [FirestoreData]
    public class TipoVehiculo
    {
        /// <summary>
        /// Identificador único del tipo de vehículo en Firestore.
        /// Se genera automáticamente al crear un nuevo tipo de vehículo.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre descriptivo del tipo de vehículo.
        /// Este nombre se muestra en las interfaces de usuario y formularios.
        /// Debe ser único, descriptivo y tener al menos 3 caracteres (ej: "Automóvil", "SUV", "Camioneta").
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre del tipo de vehículo es obligatorio")]
        [MinLength(3, ErrorMessage = "El nombre debe tener al menos 3 caracteres")]
        public required string Nombre { get; set; }

        /// <summary>
        /// Formato(s) de patente válidos para este tipo de vehículo.
        /// Usa 'l' para letras (A-Z), 'n' para dígitos (0-9), y símbolos como '.' o '-' como literales.
        /// Para múltiples formatos válidos, separarlos con '|';
        /// Debe tener al menos 3 caracteres (sin contar el separador '|').
        /// Ejemplos: 
        /// - "llnnnll" para formato nuevo argentino (AB123CD)
        /// - "lllnnn" para formato viejo argentino (ABC123)
        /// - "llnnnll|lllnnn" para ambos formatos
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El formato de patente es obligatorio")]
        [MinLength(3, ErrorMessage = "El formato debe tener al menos 3 caracteres")]
        [RegularExpression(@"^[nl.\-|]{3,}$", 
            ErrorMessage = "El formato solo puede contener 'n' (números), 'l' (letras), '.', '-' y '|'. Mínimo 3 caracteres")]
        public required string FormatoPatente { get; set; }

        /// <summary>
        /// Cantidad de empleados requeridos para lavar este tipo de vehículo.
        /// Define cuántos empleados se asignarán automáticamente al registrar un lavado.
        /// Ejemplo: 1 para automóviles/motos, 2 para camionetas, 3 para camiones.
        /// </summary>
        [FirestoreProperty]
        [Range(1, 10, ErrorMessage = "La cantidad de empleados debe estar entre 1 y 10")]
        public int CantidadEmpleadosRequeridos { get; set; } = 1;

        /// <summary>
        /// Convierte el formato simplificado a una expresión regular válida.
        /// 'l' se convierte a [A-Za-z] para letras.
        /// 'n' se convierte a [0-9] para dígitos.
        /// '|' separa múltiples formatos válidos.
        /// Otros caracteres se escapan como literales.
        /// </summary>
        /// <returns>Regex pattern para validación.</returns>
        public string ObtenerRegexPattern()
        {
            if (string.IsNullOrWhiteSpace(FormatoPatente))
                throw new InvalidOperationException("El formato de patente es obligatorio");

            var formatos = FormatoPatente.Split('|', System.StringSplitOptions.RemoveEmptyEntries);
            var patterns = new System.Collections.Generic.List<string>();

            foreach (var formato in formatos)
            {
                var pattern = "";
                foreach (char c in formato.Trim())
                {
                    if (c == 'l')
                        pattern += "[A-Za-z]";
                    else if (c == 'n')
                        pattern += "[0-9]";
                    else
                        pattern += System.Text.RegularExpressions.Regex.Escape(c.ToString());
                }
                if (!string.IsNullOrEmpty(pattern))
                    patterns.Add(pattern);
            }

            if (patterns.Count == 0)
                throw new InvalidOperationException("No se pudo generar un patrón válido desde el formato");

            // Combinar todos los formatos con OR y anclar al inicio/fin
            return "^(" + string.Join("|", patterns) + ")$";
        }
    }
}
