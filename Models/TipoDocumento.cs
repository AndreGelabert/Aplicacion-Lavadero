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
        /// Usa 'n' para dígitos (0-9), 'l' para letras (A-Z), y símbolos como '.' o '-' como literales.
        /// Debe tener al menos 3 caracteres.
        /// Ejemplos: 
        /// - "nnnnnnn" para 7 dígitos
        /// - "lnn.nnn.nnn" para formato con letras y números separados por puntos
        /// - "lllnnnnnn" para pasaporte (3 letras + 6 números)
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El formato del documento es obligatorio")]
        [MinLength(3, ErrorMessage = "El formato debe tener al menos 3 caracteres")]
        [RegularExpression(@"^[nl.\-]{3,}$", 
      ErrorMessage = "El formato solo puede contener 'n' (números), 'l' (letras), '.' y '-'. Mínimo 3 caracteres")]
        public required string Formato { get; set; }

        /// <summary>
        /// Convierte el formato simplificado a una expresión regular válida.
        /// 'n' se convierte a [0-9] para representar un dígito.
        /// 'l' se convierte a [A-Za-z] para representar una letra.
        /// Otros caracteres se escapan como literales.
        /// </summary>
        /// <returns>Regex pattern para validación.</returns>
   public string ObtenerRegexPattern()
        {
  if (string.IsNullOrWhiteSpace(Formato))
     throw new InvalidOperationException("El formato es obligatorio");

// Construir regex: 'n' -> [0-9], 'l' -> [A-Za-z], otros caracteres se escapan
     var pattern = "^";
            foreach (char c in Formato)
       {
   if (c == 'n')
    pattern += "[0-9]";
        else if (c == 'l')
                pattern += "[A-Za-z]";
        else
    pattern += System.Text.RegularExpressions.Regex.Escape(c.ToString());
            }
    pattern += "$";
     return pattern;
        }
    }
}
