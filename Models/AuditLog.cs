using Google.Cloud.Firestore;

[FirestoreData]
public class AuditLog
{
    [FirestoreProperty]
    public string UserId { get; set; }

    [FirestoreProperty]
    public string UserEmail { get; set; }

    [FirestoreProperty]
    public string Action { get; set; }

    [FirestoreProperty]
    public string TargetId { get; set; }

    [FirestoreProperty]
    public string TargetType { get; set; }

    [FirestoreProperty]
    public DateTime Timestamp { get; set; }
}
