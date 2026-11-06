using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Integration;

public class AuditSessionCaptureTests
{
    [LocalFirestoreFact]
    public async Task EmployeeRoleAndStateCapturePersistedValuesAndRejectInvalidRole()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var roles = new RolService(db, cache);
        await db.Collection("roles").Document("operator").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Operador", ["Estado"] = "Activo" });
        await db.Collection("empleados").Document("employee").SetAsync(new Dictionary<string, object> { ["Nombre"] = "Persona", ["Email"] = "p@example.invalid", ["Rol"] = "Empleado", ["Estado"] = "Activo", ["Password"] = "secret" });
        var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ctx.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "actor"), new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "actor@example.invalid") }, "test"));
        var controller = new PersonalController(new PersonalService(db), roles, new AuditService(db)) { ControllerContext = new ControllerContext { HttpContext = ctx } };
        static bool Success(IActionResult r) => System.Text.Json.JsonSerializer.SerializeToElement(((JsonResult)r).Value).GetProperty("success").GetBoolean();
        Assert.False(Success(await controller.UpdateRole("employee", "no-existe")));
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        Assert.True(Success(await controller.UpdateRole("employee", "Operador")));
        Assert.True(Success(await controller.DeactivateEmployee("employee")));
        Assert.True(Success(await controller.ReactivateEmployee("employee")));
        var docs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(3, docs.Count);
        foreach (var (action, field, before, after) in new[] { ("Modificación de rol", "Rol", "Empleado", "Operador"), ("Desactivación de empleado", "Estado", "Activo", "Inactivo"), ("Reactivación de empleado", "Estado", "Inactivo", "Activo") })
        {
            var doc = Assert.Single(docs, d => d.GetValue<string>("Action") == action);
            var a = doc.GetValue<Dictionary<string, string>>("Antes");
            var b = doc.GetValue<Dictionary<string, string>>("Despues");
            Assert.Equal(before, a[field]); Assert.Equal(after, b[field]);
            Assert.Equal(new[] { "Email", "Estado", "NombreCompleto", "Rol" }, a.Keys.OrderBy(x => x));
            Assert.Equal(new[] { "Email", "Estado", "NombreCompleto", "Rol" }, b.Keys.OrderBy(x => x));
        }
        var persisted = await db.Collection("empleados").Document("employee").GetSnapshotAsync();
        Assert.Equal("Activo", persisted.GetValue<string>("Estado")); Assert.Equal("Operador", persisted.GetValue<string>("Rol"));
    }

    [LocalFirestoreFact]
    public async Task SessionAndLegacyEventsRoundTripDistinctly()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var audit = new AuditService(db);
        Firebase.Services.IAuditWriter writer = audit;
        var started = DateTime.UtcNow;
        await writer.LogSessionEvent("actor", "actor@example.invalid", "Cerrar Sesión", "actor", "Empleado");
        await writer.LogEvent("actor", "actor@example.invalid", "Legacy", "actor", "Empleado");
        await writer.LogEvent("actor", "actor@example.invalid", "Editar", "target", "Externo",
            new() { ["Nombre"] = "Anterior" }, new() { ["Nombre"] = "Nuevo" }, "Nombre histórico");
        var events = await audit.ObtenerRegistros();
        Assert.Equal(3, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal("actor", e.UserId);
            Assert.Equal("actor@example.invalid", e.UserEmail);
            Assert.InRange(e.Timestamp, started.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        });
        var change = await audit.ObtenerDetalle(Assert.Single(events, e => e.Action == "Editar").DocId);
        Assert.Equal("target", change.TargetId);
        Assert.Equal("Externo", change.TargetType);
        Assert.Equal("Nombre histórico", change.NombreHistorico);
        Assert.Equal("Anterior", Assert.Single(change.Antes).Value);
        Assert.Equal("Nuevo", Assert.Single(change.Despues).Value);
        Assert.False(change.ComparacionNoAplica);
        var session = Assert.Single(events, e => e.Action == "Cerrar Sesión");
        var detail = await audit.ObtenerDetalle(session.DocId);
        Assert.True(detail.ComparacionNoAplica);
        Assert.Empty(detail.Antes);
        Assert.Empty(detail.Despues);
        var legacy = Assert.Single(events, e => e.Action == "Legacy");
        Assert.False(legacy.ComparacionNoAplica);
        Assert.Null(legacy.Antes);
        Assert.Null(legacy.Despues);
        var controller = new AuditoriaController(audit, null!, null!, null!, null!, null!, null!, null!, null!);
        var result = Assert.IsType<JsonResult>(await controller.Detalle(session.DocId));
        var json = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.True(json.GetProperty("comparacionNoAplica").GetBoolean());
        Assert.Empty(json.GetProperty("antes").EnumerateObject());
        Assert.Empty(json.GetProperty("despues").EnumerateObject());
    }
}
