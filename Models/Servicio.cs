using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Google.Cloud.Firestore;
namespace Firebase.Models
{
    [FirestoreData]
    public class Servicio
    {
        [FirestoreProperty]
        public required string Id { get; set; }

        [FirestoreProperty]
        [Required(ErrorMessage = "El nombre del servicio es obligatorio")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras y espacios")]
        public required string Nombre { get; set; }

        [FirestoreProperty]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser igual o mayor a 0")]
        public decimal Precio { get; set; }

        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de servicio es obligatorio")]
        public required string Tipo { get; set; }

        [FirestoreProperty]
        [Required(ErrorMessage = "El tipo de vehículo es obligatorio")]
        public required string TipoVehiculo { get; set; }

        [FirestoreProperty]
        [Range(1, int.MaxValue, ErrorMessage = "El tiempo estimado debe ser mayor a 0")]
        public int TiempoEstimado { get; set; }

        [FirestoreProperty]
        [Required(ErrorMessage = "La descripción es obligatoria")]
        public required string Descripcion { get; set; }

        [FirestoreProperty]
        [Required(ErrorMessage = "El estado es obligatorio")]
        public required string Estado { get; set; }
    }

    // Converter personalizado para Decimal
    public class DecimalConverter : IFirestoreConverter<decimal>
    {
        public object ToFirestore(decimal value) => (double)value;

        public decimal FromFirestore(object value)
        {
            if (value is double doubleValue)
                return (decimal)doubleValue;
            return 0;
        }
    }
}
