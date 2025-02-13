public class FirebaseErrorResponse
{
    public FirebaseError error { get; set; }
}

public class FirebaseError
{
    public int code { get; set; }
    public string message { get; set; }
    public List<FirebaseErrorDetail> errors { get; set; }
}

public class FirebaseErrorDetail
{
    public string message { get; set; }
    public string domain { get; set; }
    public string reason { get; set; }
}
