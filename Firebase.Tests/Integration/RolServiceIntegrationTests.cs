using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public sealed class RolServiceIntegrationTests
{
    [LocalFirestoreFact]
    public async Task ActualizacionTurnosDeshabilitadaNoModificaRolesNiAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-roles-turnos-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var admin = db.Collection("roles").Document("a");
        var empleado = db.Collection("roles").Document("e");
        var actor = db.Collection("empleados").Document("actor");
        await admin.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Administrador", ["Estado"] = "Activo", ["Permisos"] = new[] { "Roles.Modificar", "Legacy.Desconocido" }, ["Extra"] = "preservar" });
        await empleado.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Empleado", ["Estado"] = "Activo", ["Permisos"] = new[] { "Clientes.Consultar" }, ["Extra"] = "preservar" });
        await actor.SetAsync(new Dictionary<string, object> { ["Rol"] = "Administrador", ["Estado"] = "Activo" });
        var a = await admin.GetSnapshotAsync(); var e = await empleado.GetSnapshotAsync(); var u = await actor.GetSnapshotAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new RolService(db, cache);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AgregarPermisosTurnosRolesBaseAsync("actor", "actor@example.invalid", new AuditService(db),
            new Dictionary<string, Timestamp> { [a.Id] = a.UpdateTime!.Value, [e.Id] = e.UpdateTime!.Value }, u.UpdateTime!.Value));
        Assert.Equal("La actualización de roles de Turnos está deshabilitada: falta verificar respaldo y recuperación seguros.", error.Message);
        Assert.Equal(a.UpdateTime, (await admin.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(e.UpdateTime, (await empleado.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(u.UpdateTime, (await actor.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }
}
