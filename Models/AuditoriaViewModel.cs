namespace Firebase.Models;

/// <summary>Query parameters shared by the audit screen and its partial requests.</summary>
public sealed class AuditoriaFilter
{
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public List<string>? Acciones { get; set; }
    public List<string>? TiposObjetivo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
    public string? SearchTerm { get; set; }
}

/// <summary>Explicit screen data; no MVC or Firestore dependency.</summary>
public sealed class AuditoriaViewModel
{
    public List<AuditLogConNombre> Registros { get; set; } = new();
    public AuditoriaFilter Filters { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public List<int> VisiblePages { get; set; } = new();
    public List<string> TodasLasAcciones { get; set; } = new();
    public List<string> TodosLosTiposObjetivo { get; set; } = new();
}
