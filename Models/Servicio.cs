using Google.Cloud.Firestore;
namespace Firebase.Models
{
    [FirestoreData]
    public class Servicio
    {
        [FirestoreProperty]
        public string Id { get; set; } = string.Empty; // Quitar required y asignar valor por defecto  

        [FirestoreProperty]
        public required string Nombre { get; set; }

        // Cambiar el atributo incorrecto a FirestoreProperty y usar el convertidor manualmente  
        [FirestoreProperty]
        public decimal Precio { get; set; }

        [FirestoreProperty]
        public required string Tipo { get; set; }

        [FirestoreProperty]
        public required string TipoVehiculo { get; set; }

        [FirestoreProperty]
        public int TiempoEstimado { get; set; }

        [FirestoreProperty]
        public required string Descripcion { get; set; }

        [FirestoreProperty]
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
