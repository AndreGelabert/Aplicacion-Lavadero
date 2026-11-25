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
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]{3,}$", 
        ErrorMessage = "El nombre debe contener al menos 3 letras")]
        public string Nombre { get; set; } = string.Empty;

        [FirestoreProperty]
        public List<RecetaInsumo> Receta { get; set; } = new();
    }

    [FirestoreData]
    public class RecetaInsumo
    {
        // Los documentos anteriores sin modalidad representan productos concretos.
        [FirestoreProperty] public string Modalidad { get; set; } = "Producto";
        [FirestoreProperty] public string TipoId { get; set; } = string.Empty;
        [FirestoreProperty] public string NombreTipo { get; set; } = string.Empty;
        [FirestoreProperty] public string Categoria { get; set; } = string.Empty;
        [FirestoreProperty] public string InsumoId { get; set; } = string.Empty;
        [FirestoreProperty] public string NombreInsumo { get; set; } = string.Empty;
        [FirestoreProperty] public string UnidadMedida { get; set; } = string.Empty;
        [FirestoreProperty(ConverterType = typeof(Firebase.Converters.DecimalConverter))] public decimal DosisTeorica { get; set; }
        [FirestoreProperty] public bool EsHerramienta { get; set; }
    }
}
