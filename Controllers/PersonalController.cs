using Firebase.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Administrador")]
public class PersonalController : Controller
{
    private readonly FirestoreDb _firestore;

    public PersonalController()
    {
        string path = AppDomain.CurrentDomain.BaseDirectory + @"Utils\loginmvc.json";
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);
        _firestore = FirestoreDb.Create("aplicacion-lavadero");
    }

    // GET: Carga inicial con empleados activos
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var empleadosCollection = _firestore.Collection("empleados");
        var query = empleadosCollection.WhereEqualTo("Estado", "Activo");
        var snapshot = await query.GetSnapshotAsync();

        var empleados = snapshot.Documents.Select(doc => new Empleado
        {
            Id = doc.Id,
            NombreCompleto = doc.GetValue<string>("Nombre"),
            Email = doc.GetValue<string>("Email"),
            Rol = doc.GetValue<string>("Rol"),
            Estado = doc.GetValue<string>("Estado")
        }).ToList();

        ViewBag.Estados = new List<string> { "Activo" }; // Estado inicial del filtro
        return View(empleados);
    }

    // POST: Maneja el filtrado
    [HttpPost]
    public async Task<IActionResult> Index(List<string> estados)
    {
        var empleadosCollection = _firestore.Collection("empleados");
        Query query;

        if (estados == null || !estados.Any())
        {
            // Si no hay filtros, no mostrar nada
            ViewBag.Estados = new List<string>();
            return View(new List<Empleado>());
        }
        else
        {
            query = empleadosCollection.WhereIn("Estado", estados);
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

        ViewBag.Estados = estados; // Mantener el estado de los checkboxes
        return View(empleados);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRole(string id, string newRole)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Rol", newRole);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeactivateEmployee(string id)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Estado", "Inactivo");
        return RedirectToAction("Index");
    }
    [HttpPost]
    public async Task<IActionResult> ReactivateEmployee(string id)
    {
        var employeeRef = _firestore.Collection("empleados").Document(id);
        await employeeRef.UpdateAsync("Estado", "Activo");
        return RedirectToAction("Index");
    }
}
