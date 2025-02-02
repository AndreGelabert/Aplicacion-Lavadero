using Firebase.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
}
