using Google.Cloud.Firestore;

namespace Firebase.Converters
{
    /// <summary>
    /// Conversor personalizado para serializar/deserializar decimales en Firestore.
    /// Firestore no soporta decimal nativamente, por lo que se convierte a double.
    /// </summary>
    public class DecimalConverter : IFirestoreConverter<decimal>
    {
        public object ToFirestore(decimal value)
        {
            // Convertir decimal a double para almacenar en Firestore
            return (double)value;
        }

        public decimal FromFirestore(object value)
        {
            // Convertir de Firestore (double) a decimal
            if (value == null)
                return 0m;

            if (value is double doubleValue)
                return (decimal)doubleValue;

            if (value is long longValue)
                return (decimal)longValue;

            if (value is int intValue)
                return (decimal)intValue;

            // Intentar conversión genérica
            return Convert.ToDecimal(value);
        }
    }
}