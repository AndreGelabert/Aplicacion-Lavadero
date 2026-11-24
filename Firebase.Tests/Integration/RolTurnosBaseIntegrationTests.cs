using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public sealed class RolTurnosBaseIntegrationTests
{
    private static readonly string[] TurnosPermisos = { "Turnos.Consultar", "Turnos.Crear", "Turnos.Modificar", "Turnos.Cancelar" };
    private static FirestoreDb Db() => new FirestoreDbBuilder { ProjectId = "demo-rol-turnos-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static async Task Seed(FirestoreDb db, string id, string nombre, params string[] permisos) =>
        await db.Collection("roles").Document(id).SetAsync(new Dictionary<string, object> { ["Nombre"] = nombre, ["Estado"] = "Activo", ["Permisos"] = permisos, ["Extra"] = "preservar" });

    private static async Task Apply(FirestoreDb db, IAuditWriter writer, string actor = "actor", string email = "actor@example.invalid")
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await new RolService(db, cache).AgregarPermisosTurnosRolesBaseAsync(actor, email, writer);
    }

    private static async Task<DocumentSnapshot> Read(FirestoreDb db, string id) => await db.Collection("roles").Document(id).GetSnapshotAsync();

    [LocalFirestoreFact]
    public async Task AgregaPermisosTurnosAutorizadosPreservaCamposYRolesPersonalizadosYEsIdempotente()
    {
        var db = Db();
        await Seed(db, "a", "Administrador", "Personalizado.Legacy", "Turnos.Consultar");
        await Seed(db, "e", "Empleado", "Clientes.Consultar");
        await Seed(db, "c", "Especial", "Otro.Custom");
        var custom = await Read(db, "c");
        await Apply(db, new AuditService(db));
        var a = await Read(db, "a");
        var e = await Read(db, "e");
        Assert.Equal(new[] { "Personalizado.Legacy", "Turnos.Consultar" }.Concat(TurnosPermisos.Skip(1)), a.GetValue<List<string>>("Permisos"));
        Assert.Equal(new[] { "Clientes.Consultar" }.Concat(TurnosPermisos), e.GetValue<List<string>>("Permisos"));
        Assert.Equal("preservar", a.GetValue<string>("Extra"));
        Assert.Equal("preservar", e.GetValue<string>("Extra"));
        Assert.Equal(custom.UpdateTime, (await Read(db, "c")).UpdateTime);

        var audits = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(x => x.ConvertTo<AuditLog>()).ToList();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, x => {
            Assert.Equal("actor", x.UserId);
            Assert.Equal("actor@example.invalid", x.UserEmail);
            Assert.Equal("Rol", x.TargetType);
            Assert.NotNull(x.Antes);
            Assert.NotNull(x.Despues);
        });
        Assert.Equal(new[] { "a", "e" }, audits.Select(x => x.TargetId).OrderBy(x => x));

        await Apply(db, new AuditService(db));
        Assert.Equal(a.UpdateTime, (await Read(db, "a")).UpdateTime);
        Assert.Equal(e.UpdateTime, (await Read(db, "e")).UpdateTime);
        Assert.Equal(2, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task RolesDuplicadosOInactivosOAusentesRechazanSinEfectos()
    {
        foreach (var variant in new[] { "duplicado", "inactivo", "ausente" })
        {
            var db = Db();
            await Seed(db, "a", "Administrador", "Clientes.Consultar");
            if (variant != "ausente") await Seed(db, "e", "Empleado", "Clientes.Consultar");
            if (variant == "duplicado") await Seed(db, "d", " empleado ", "Clientes.Consultar");
            if (variant == "inactivo") await db.Collection("roles").Document("e").UpdateAsync("Estado", "Inactivo");

            var before = (await db.Collection("roles").GetSnapshotAsync()).Documents.ToArray();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Apply(db, new AuditService(db)));
            Assert.Equal(variant == "inactivo" ? "Los roles base deben estar activos." : "Los roles base deben existir y ser únicos.", error.Message);

            foreach (var snapshot in before)
                Assert.Equal(snapshot.UpdateTime, (await snapshot.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        }
    }
}
