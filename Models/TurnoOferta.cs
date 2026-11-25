using Google.Cloud.Firestore;

namespace Firebase.Models;

[FirestoreData]
internal sealed class TurnoOferta
{
    [FirestoreProperty] public string CascadeId { get; set; } = "";
    [FirestoreProperty] public string AppointmentId { get; set; } = "";
    [FirestoreProperty] public DateTime CreatedUtc { get; set; }
    [FirestoreProperty] public DateTime ExpiresUtc { get; set; }
    [FirestoreProperty] public DateTime AnchorUtc { get; set; }
    [FirestoreProperty] public DateTime OriginalUtc { get; set; }
    [FirestoreProperty] public DateTime ProposedUtc { get; set; }
    [FirestoreProperty] public DateTime ProposedEndUtc { get; set; }
    [FirestoreProperty] public string ClienteId { get; set; } = "";
    [FirestoreProperty] public string Estado { get; set; } = "";
}
