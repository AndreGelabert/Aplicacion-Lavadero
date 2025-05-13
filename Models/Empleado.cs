namespace Firebase.Models
{
    public class Empleado
    {
        public required string Id { get; set; }
        public required string NombreCompleto { get; set; }
        public required string Email { get; set; }
        public required string Rol { get; set; }
        public required string Estado { get; set; }
    }
}
