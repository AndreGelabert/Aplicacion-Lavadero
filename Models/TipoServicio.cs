using Google.Cloud.Firestore;
namespace Firebase.Models
{
    [FirestoreData]
    public class TipoServicio
    {
        [FirestoreProperty]
        public string Id { get; set; }

        [FirestoreProperty]
        public string Nombre { get; set; }
    }
}
