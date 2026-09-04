using System.ComponentModel.DataAnnotations;

namespace Firebase.Models;

/// <summary>
/// Modelo que representa un rol del sistema.
/// </summary>
public class Rol
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del rol es obligatorio")]
    [MinLength(3, ErrorMessage = "El nombre debe tener al menos 3 caracteres")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria")]
    [MinLength(3, ErrorMessage = "La descripción debe tener al menos 3 caracteres")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El estado es obligatorio")]
    public string Estado { get; set; } = "Activo";

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public List<string> Permisos { get; set; } = new();

    public bool EsProtegido => PermisosSistema.RolesProtegidos.Contains(Nombre);
}
