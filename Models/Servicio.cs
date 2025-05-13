using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;
namespace Firebase.Models
{
    [FirestoreData]
    public class Servicio
    {
        [FirestoreProperty]
        public required string Id { get; set; }
        [FirestoreProperty]
        public required string Nombre { get; set; }

        [FirestoreProperty]
        public decimal Precio { get; set; }

        [FirestoreProperty]
        public required string Tipo { get; set; } // Almacenar el tipo como string para permitir agregar nuevos tipos

        [FirestoreProperty]
        public required string Descripcion { get; set; }

        [FirestoreProperty]
        public required string Estado { get; set; } // Activo o Inactivo para eliminación lógica
    }
}
