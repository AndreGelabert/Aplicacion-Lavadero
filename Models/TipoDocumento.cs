using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa un tipo de documento de identidad.
    /// </summary>
    [FirestoreData]
    public class TipoDocumento
    {
        /// <summary>
        /// Identificador único del tipo de documento.
        /// </summary>
        [FirestoreProperty]
        public required string Id { get; set; }

        /// <summary>
        /// Nombre del tipo de documento (ej: DNI, Pasaporte, CUIT).
        /// Debe tener al menos 3 caracteres.
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre del tipo de documento es obligatorio")]
        [MinLength(3, ErrorMessage = "El nombre debe tener al menos 3 caracteres")]
        public required string Nombre { get; set; }

        /// <summary>
        /// Formato regex para validar el número de documento.
        /// Usa 'n' para dígitos (0-9) y cualquier otro carácter como literal.
        /// Ejemplo: "nn.nnn.nnn" para DNI argentino (12.345.678).
        /// </summary>
        [FirestoreProperty]
        public string? Formato { get; set; }

        /// <summary>
        /// Convierte el formato simplificado a una expresión regular válida.
        /// 'n' se convierte a [0-9] para representar un dígito.
        /// </summary>
        /// <returns>Regex pattern para validación, o null si no hay formato definido.</returns>
        public string? ObtenerRegexPattern()
        {
            if (string.IsNullOrWhiteSpace(Formato))
                return null;

            // Construir regex: 'n' -> [0-9], otros caracteres se escapan
            var pattern = "^";
            foreach (char c in Formato)
            {
                if (c == 'n')
                    pattern += "[0-9]";
                else
                    pattern += System.Text.RegularExpressions.Regex.Escape(c.ToString());
            }
            pattern += "$";
            return pattern;
        }
    }
}
