// Models/FirebaseAuthResponses.cs
namespace Firebase.Models
{
    public class FirebaseLoginResponse
    {
        public string localId { get; set; } // UID del usuario
        public string idToken { get; set; }
        public string email { get; set; }
        public string refreshToken { get; set; }
        public string expiresIn { get; set; }
    }

    public class FirebaseErrorResponse
    {
        public FirebaseError error { get; set; }
    }

    public class FirebaseError
    {
        public int code { get; set; }
        public string message { get; set; }
        public IList<FirebaseErrorDetail> errors { get; set; }
    }

    public class FirebaseErrorDetail
    {
        public string message { get; set; }
        public string domain { get; set; }
        public string reason { get; set; }
    }
}
