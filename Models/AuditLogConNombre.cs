namespace Firebase.Models
{
    /// <summary>
    /// Modelo auxiliar para auditoría con nombre de usuario
    /// </summary>
    public class AuditLogConNombre
    {
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string TargetId { get; set; }
        public string TargetType { get; set; }
        public string TargetName { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
