using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Integration;

public class AuditCaptureTests
{
    [LocalFirestoreFact]
    public async Task RoleControllerCapturesPersistedCreateUpdateAndStateTransitions()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var roles = new RolService(db, cache);
        var audit = new AuditService(db);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, "actor"), new Claim(ClaimTypes.Email, "actor@example.invalid")
        }, "test")) };
        var controller = new RolController(roles, audit) { ControllerContext = new ControllerContext { HttpContext = context } };
        var role = new Rol { Nombre = "Operador", Descripcion = "Turno inicial", Permisos = new() { "Clientes.Consultar" } };

        AssertSuccess(await controller.CrearRolAjax(role));
        var created = (await roles.ObtenerRol(role.Id))!;
        var createdMap = Expected(created, "Operador", "Turno inicial", "Activo", "[\"Clientes.Consultar\"]");
        await AssertEvents(db, ("Alta Rol", new(), createdMap, "Operador"));

        role.Nombre = "Supervisor";
        role.Descripcion = "Turno nuevo";
        role.Permisos = new() { "Vehiculos.Consultar", "Clientes.Consultar" };
        AssertSuccess(await controller.ActualizarRolAjax(role));
        var updated = (await roles.ObtenerRol(role.Id))!;
        var updatedMap = Expected(updated, "Supervisor", "Turno nuevo", "Activo", "[\"Clientes.Consultar\",\"Vehiculos.Consultar\"]");
        await AssertEvents(db, ("Alta Rol", new(), createdMap, "Operador"), ("Modificacion Rol", createdMap, updatedMap, "Supervisor"));

        AssertSuccess(await controller.EliminarRolAjax(role.Id));
        var inactiveMap = new Dictionary<string, string>(updatedMap) { ["Estado"] = "Inactivo" };
        await AssertEvents(db, ("Alta Rol", new(), createdMap, "Operador"), ("Modificacion Rol", createdMap, updatedMap, "Supervisor"), ("Desactivacion Rol", updatedMap, inactiveMap, "Supervisor"));
        AssertSuccess(await controller.ReactivarRolAjax(role.Id));
        await AssertEvents(db, ("Alta Rol", new(), createdMap, "Operador"), ("Modificacion Rol", createdMap, updatedMap, "Supervisor"), ("Desactivacion Rol", updatedMap, inactiveMap, "Supervisor"), ("Reactivacion Rol", inactiveMap, updatedMap, "Supervisor"));
        Assert.Equal("Activo", (await roles.ObtenerRol(role.Id))!.Estado);
    }

    private static Dictionary<string, string> Expected(Rol role, string name, string description, string state, string permissions) => new()
    {
        ["Nombre"] = name, ["Descripcion"] = description, ["Estado"] = state,
        ["Permisos"] = permissions, ["FechaCreacion"] = role.FechaCreacion.ToUniversalTime().ToString("O")
    };

    private static void AssertSuccess(IActionResult result) => Assert.True(JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value).GetProperty("success").GetBoolean());

    private static async Task AssertEvents(FirestoreDb db, params (string action, Dictionary<string, string> before, Dictionary<string, string> after, string name)[] expected)
    {
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(expected.Length, docs.Count);
        foreach (var item in expected)
        {
            var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == item.action);
            Assert.Equal("Rol", doc.GetValue<string>("TargetType"));
            Assert.Equal(item.name, doc.GetValue<string>("NombreHistorico"));
            Assert.Equal(item.before.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Antes").OrderBy(x => x.Key));
            Assert.Equal(item.after.OrderBy(x => x.Key), doc.GetValue<Dictionary<string, string>>("Despues").OrderBy(x => x.Key));
        }
    }
}
