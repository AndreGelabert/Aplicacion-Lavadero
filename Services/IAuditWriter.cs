namespace Firebase.Services;

/// <summary>
/// Records audit events without exposing the audit query API.
/// Implementations retain existing persistence and exception semantics.
/// </summary>
public interface IAuditWriter
{
    /// <summary>Records a legacy event without before/after snapshots.</summary>
    Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType);

    /// <summary>Records the existing auditable snapshots and historical display name.</summary>
    Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType,
        Dictionary<string, string> antes, Dictionary<string, string> despues, string nombreHistorico = null);

    /// <summary>Records a session event for which data comparison does not apply.</summary>
    Task LogSessionEvent(string userId, string userEmail, string action, string targetId, string targetType);
}
