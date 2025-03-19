using Google.Cloud.Firestore;
namespace Firebase.Models
{
    [FirestoreData]
    public class Servicio
    {
        [FirestoreProperty]
        public string Id { get; set; }

        [FirestoreProperty]
        public string Nombre { get; set; }

        [FirestoreProperty]
        public decimal Precio { get; set; }

        [FirestoreProperty]
        public string Tipo { get; set; } // Almacenar el tipo como string para permitir agregar nuevos tipos

        [FirestoreProperty]
        public string Descripcion { get; set; }

        [FirestoreProperty]
        public string Estado { get; set; } // Activo o Inactivo para eliminación lógica
    }
}
