using Firebase.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[Authorize(Roles = "Administrador")]
public class PersonalController : Controller
{
    private readonly FirestoreDb _firestore;
    private readonly AuditService _auditService;

    public PersonalController(FirestoreDb firestore, AuditService auditService)
    {
        _firestore = firestore;
        _auditService = auditService;
    }
    [HttpGet]
    public async Task<IActionResult> Index(
    List<string> estados,
    string firstDocId = null,
    string lastDocId = null,
    int pageNumber = 1,
    int pageSize = 10)
    {
        // Si no hay filtros aplicados, mostrar solo "Activo" por defecto
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }
        Query query = _firestore.Collection("empleados");

        // Aplicar filtros
        if (estados.Any()) query = query.WhereIn("Estado", estados);
        else query = query.WhereEqualTo("Estado", "Activo");

        // Configurar consulta con índice compuesto
        query = query.OrderBy("Estado").OrderBy("Nombre").Limit(pageSize);

        // Manejo de cursores para paginación
        if (!string.IsNullOrEmpty(lastDocId) && pageNumber > 1)
        {
            var lastDoc = await _firestore.Collection("empleados").Document(lastDocId).GetSnapshotAsync();
            query = query.StartAfter(lastDoc);
        }
        else if (!string.IsNullOrEmpty(firstDocId) && pageNumber > 1)
        {
            var firstDoc = await _firestore.Collection("empleados").Document(firstDocId).GetSnapshotAsync();
            query = query.StartAt(firstDoc);
        }

        var snapshot = await query.GetSnapshotAsync();
        var empleados = snapshot.Documents.Select(doc => new Empleado
        {
            Id = doc.Id,
            NombreCompleto = doc.GetValue<string>("Nombre"),
            Email = doc.GetValue<string>("Email"),
            Rol = doc.GetValue<string>("Rol"),
            Estado = doc.GetValue<string>("Estado")
        }).ToList();

        // Calcular páginas
        var totalPages = await GetTotalPages(estados, pageSize);
        totalPages = Math.Max(totalPages, 1); // Asegurarse de que totalPages sea al menos 1
        var currentPage = Math.Clamp(pageNumber, 1, totalPages);
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados;
        ViewBag.PageSize = pageSize;
        ViewBag.FirstDocId = snapshot.Documents.FirstOrDefault()?.Id;
        ViewBag.LastDocId = snapshot.Documents.LastOrDefault()?.Id;

        return View(empleados);
    }


    private async Task<int> GetTotalPages(List<string> estados, int pageSize)
    {
        Query query = _firestore.Collection("empleados");
        if (estados.Any()) query = query.WhereIn("Estado", estados);

        var countQuery = query.Select("__name__");
        var snapshot = await countQuery.GetSnapshotAsync();
        return (int)Math.Ceiling(snapshot.Count / (double)pageSize);
    }

    private List<int> GetVisiblePages(int currentPage, int totalPages, int range = 2)
    {
        var start = Math.Max(1, currentPage - range);
        var end = Math.Min(totalPages, currentPage + range);
        return Enumerable.Range(start, end - start + 1).ToList();
    }
    // POST: Maneja el filtrado
    [HttpPost]
    public async Task<IActionResult> Index(List<string> estados)
    {
        // Si no hay filtros aplicados, mostrar solo "Activo" por defecto
        if (estados == null || !estados.Any())
        {
            estados = new List<string> { "Activo" };
        }

        Query query = _firestore.Collection("empleados");

        // Aplicar filtros
        if (estados.Any()) query = query.WhereIn("Estado", estados);
        else query = query.WhereEqualTo("Estado", "Activo");

        var snapshot = await query.GetSnapshotAsync();
        var empleados = snapshot.Documents.Select(doc => new Empleado
        {
            Id = doc.Id,
            NombreCompleto = doc.GetValue<string>("Nombre"),
            Email = doc.GetValue<string>("Email"),
            Rol = doc.GetValue<string>("Rol"),
            Estado = doc.GetValue<string>("Estado")
        }).ToList();

        // Calcular páginas
        var totalPages = await GetTotalPages(estados, 10); // Usar pageSize = 10 por defecto
        var currentPage = 1; // Siempre mostrar la primera página al aplicar filtros
        var visiblePages = GetVisiblePages(currentPage, totalPages);

        ViewBag.TotalPages = totalPages;
        ViewBag.VisiblePages = visiblePages;
        ViewBag.CurrentPage = currentPage;
        ViewBag.Estados = estados; // Mantener el estado de los checkboxes
        ViewBag.PageSize = 10; // Usar pageSize = 10 por defecto
        ViewBag.FirstDocId = snapshot.Documents.FirstOrDefault()?.Id;
        ViewBag.LastDocId = snapshot.Documents.LastOrDefault()?.Id;

        return View(empleados);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRole(string id, string newRole)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Rol", newRole);
        // Registrar evento de cambio de rol en Google Analytics
        TempData["RoleChangeEvent_UserId"] = id;
        TempData["RoleChangeEvent_NewRole"] = newRole;
        // Registrar evento de auditoría
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, "Modificación de rol", id, "Empleado");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateEmployee(string id)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Estado", "Inactivo");
        // Registrar evento de cambio de estado en Google Analytics
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = "Inactivo";
        // Registrar evento de auditoría
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, "Desactivación de empleado", id, "Empleado");
        return RedirectToAction("Index");
    }
    [HttpPost]
    public async Task<IActionResult> ReactivateEmployee(string id)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Estado", "Activo");
        // Registrar evento de cambio de estado en Google Analytics
        TempData["StateChangeEvent_UserId"] = id;
        TempData["StateChangeEvent_NewState"] = "Activo";
        // Registrar evento de auditoría
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        await _auditService.LogEvent(userId, userEmail, "Reactivación de empleado", id, "Empleado");
        return RedirectToAction("Index");
    }
}
