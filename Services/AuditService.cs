using Google.Cloud.Firestore;

public class AuditService
{
    private readonly FirestoreDb _firestore;

    public AuditService(FirestoreDb firestore)
    {
        _firestore = firestore;
    }

    public async Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            TargetId = targetId,
            TargetType = targetType,
            Timestamp = DateTime.UtcNow
        };

        await _firestore.Collection("registros_auditoria").AddAsync(auditLog);
    }
}
