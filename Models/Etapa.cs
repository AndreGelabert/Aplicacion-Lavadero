using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace Firebase.Models
{
    /// <summary>
    /// Modelo que representa una etapa de un servicio.
    /// Las etapas permiten dividir un servicio en pasos o fases que se pueden
    /// completar de manera independiente durante la ejecución del servicio.
    /// </summary>
    [FirestoreData]
    public class Etapa
    {
        /// <summary>
        /// Identificador único de la etapa.
        /// Se genera automáticamente al crear una nueva etapa.
        /// </summary>
        [FirestoreProperty]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Nombre descriptivo de la etapa.
        /// Debe contener al menos 3 letras.
        /// Ejemplo: "Lavado exterior", "Aspirado interior", "Encerado"
        /// </summary>
        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre de la etapa es obligatorio")]
        [MinLength(3, ErrorMessage = "El nombre debe tener al menos 3 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$", 
        ErrorMessage = "El nombre debe contener al menos 3 letras")]
        public string Nombre { get; set; } = string.Empty;
    }
}
