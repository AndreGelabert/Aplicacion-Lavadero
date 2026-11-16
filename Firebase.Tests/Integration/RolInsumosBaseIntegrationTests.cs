using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public sealed class RolInsumosBaseIntegrationTests
{
    private static readonly string[] Admin = { "Insumos.Consultar", "Insumos.Crear", "Insumos.Modificar", "Insumos.Eliminar", "Insumos.AjustarStock", "Insumos.Recepcionar", "Insumos.Kardex" };
    private static readonly string[] Empleado = { "Insumos.Consultar", "Insumos.Recepcionar" };
    private static FirestoreDb Db() => new FirestoreDbBuilder { ProjectId = "demo-rol-base-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
    private static async Task Seed(FirestoreDb db, string id, string nombre, params string[] permisos) =>
        await db.Collection("roles").Document(id).SetAsync(new Dictionary<string, object> { ["Nombre"] = nombre, ["Estado"] = "Activo", ["Permisos"] = permisos, ["Extra"] = "preservar" });

    private static async Task Apply(FirestoreDb db, IAuditWriter writer, string actor = "actor", string email = "actor@example.invalid")
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        await new RolService(db, cache).AgregarPermisosInsumosRolesBaseAsync(actor, email, writer);
    }
    private static async Task<DocumentSnapshot> Read(FirestoreDb db, string id) => await db.Collection("roles").Document(id).GetSnapshotAsync();
    private static async Task AssertUnchanged(FirestoreDb db, params DocumentSnapshot[] before)
    {
        foreach (var snapshot in before) Assert.Equal(snapshot.UpdateTime, (await snapshot.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }
    [LocalFirestoreFact]
    public async Task AgregaSoloPermisosAutorizadosPreservaCamposYRolesPersonalizadosYEsIdempotente()
    {
        var db = Db(); await Seed(db, "a", "Administrador", "Personalizado.Legacy", "Insumos.Consultar");
        await Seed(db, "e", "Empleado", "Clientes.Consultar"); await Seed(db, "c", "Especial", "Otro.Custom");
        var custom = await Read(db, "c");
        await Apply(db, new AuditService(db));
        var a = await Read(db, "a"); var e = await Read(db, "e");
        Assert.Equal(new[] { "Personalizado.Legacy", "Insumos.Consultar" }.Concat(Admin.Skip(1)), a.GetValue<List<string>>("Permisos"));
        Assert.Equal(new[] { "Clientes.Consultar" }.Concat(Empleado), e.GetValue<List<string>>("Permisos"));
        Assert.Equal("preservar", a.GetValue<string>("Extra")); Assert.Equal("preservar", e.GetValue<string>("Extra"));
        Assert.Equal(custom.UpdateTime, (await Read(db, "c")).UpdateTime);
        var audits = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(x => x.ConvertTo<AuditLog>()).ToList();
        Assert.Equal(2, audits.Count); Assert.All(audits, x => { Assert.Equal("actor", x.UserId); Assert.Equal("actor@example.invalid", x.UserEmail); Assert.Equal("Rol", x.TargetType); Assert.NotNull(x.Antes); Assert.NotNull(x.Despues); });
        Assert.Equal(new[] { "a", "e" }, audits.Select(x => x.TargetId).OrderBy(x => x));
        await Apply(db, new AuditService(db));
        Assert.Equal(a.UpdateTime, (await Read(db, "a")).UpdateTime); Assert.Equal(e.UpdateTime, (await Read(db, "e")).UpdateTime);
        Assert.Equal(2, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
    }
    [LocalFirestoreFact]
    public async Task RolesDuplicadosOInactivosOAusentesRechazanSinEfectos()
    {
        foreach (var variant in new[] { "duplicado", "inactivo", "ausente" })
        {
            var db = Db(); await Seed(db, "a", "Administrador", "Clientes.Consultar");
            if (variant != "ausente") await Seed(db, "e", "Empleado", "Clientes.Consultar");
            if (variant == "duplicado") await Seed(db, "d", " empleado ", "Clientes.Consultar");
            if (variant == "inactivo") await db.Collection("roles").Document("e").UpdateAsync("Estado", "Inactivo");
            var before = (await db.Collection("roles").GetSnapshotAsync()).Documents.ToArray();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Apply(db, new AuditService(db)));
            Assert.Equal(variant == "inactivo" ? "Los roles base deben estar activos." : "Los roles base deben existir y ser únicos.", error.Message);
            await AssertUnchanged(db, before);
        }
    }
    [LocalFirestoreFact]
    public async Task AuditoriaFallidaRevierteAmbosRolesYNoCreaEventos()
    {
        var db = Db(); await Seed(db, "a", "Administrador", "Clientes.Consultar"); await Seed(db, "e", "Empleado", "Clientes.Consultar");
        var before = (await db.Collection("roles").GetSnapshotAsync()).Documents.ToArray();
        var writer = new FalloAudit(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Apply(db, writer));
        Assert.Equal("auditoria fallida", error.Message);
        Assert.Equal(2, writer.Llamadas);
        await AssertUnchanged(db, before);
    }
    [LocalFirestoreFact]
    public async Task ActorInvalidoOSchemaPermisosInvalidoRechazaSinEfectos()
    {
        foreach (var variant in new[] { "actor", "schema" })
        {
            var db = Db(); await Seed(db, "a", "Administrador", "Clientes.Consultar"); await Seed(db, "e", "Empleado", "Clientes.Consultar");
            if (variant == "schema") await db.Collection("roles").Document("e").UpdateAsync("Permisos", "invalido");
            var before = (await db.Collection("roles").GetSnapshotAsync()).Documents.ToArray();
            if (variant == "actor")
                Assert.Equal("actor", (await Assert.ThrowsAsync<ArgumentException>(() => Apply(db, new AuditService(db), ""))).ParamName);
            else
                Assert.Equal("Los permisos del rol base son inválidos.", (await Assert.ThrowsAsync<InvalidOperationException>(() => Apply(db, new AuditService(db)))).Message);
            await AssertUnchanged(db, before);
        }
    }
    private static async Task ApplyGuarded(FirestoreDb db, DocumentSnapshot admin, DocumentSnapshot empleado, DocumentSnapshot actor)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var expected = new Dictionary<string, Timestamp> { [admin.Id] = admin.UpdateTime!.Value, [empleado.Id] = empleado.UpdateTime!.Value };
        await new RolService(db, cache).AgregarPermisosInsumosRolesBaseAsync("actor", "actor@example.invalid", new AuditService(db), expected, actor.UpdateTime!.Value);
    }

    [LocalFirestoreFact]
    public async Task RespaldoOActorObsoletoRechazaSinEscrituras()
    {
        foreach (var variant in new[] { "admin", "empleado", "actor" })
        {
            var db = Db();
            await Seed(db, "a", "Administrador", "Roles.Modificar");
            await Seed(db, "e", "Empleado", "Clientes.Consultar");
            var actorRef = db.Collection("empleados").Document("actor");
            await actorRef.SetAsync(new Dictionary<string, object> { ["Rol"] = "Administrador", ["Estado"] = "Activo" });
            var admin = await Read(db, "a"); var empleado = await Read(db, "e"); var actor = await actorRef.GetSnapshotAsync();
            if (variant == "actor") await actorRef.UpdateAsync("Rol", "Empleado");
            else await db.Collection("roles").Document(variant == "admin" ? "a" : "e").UpdateAsync("Extra", "cambio concurrente");
            var beforeRoles = (await db.Collection("roles").GetSnapshotAsync()).Documents.ToArray();
            var beforeActor = await actorRef.GetSnapshotAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyGuarded(db, admin, empleado, actor));
            Assert.Equal("Los datos de respaldo o del actor cambiaron.", error.Message);
            await AssertUnchanged(db, beforeRoles);
            Assert.Equal(beforeActor.UpdateTime, (await actorRef.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task PrecondicionesVigentesExigenActorActivoAdministradorConPermiso()
    {
        foreach (var variant in new[] { "valido", "inactivo", "rol", "permiso" })
        {
            var db = Db();
            await Seed(db, "a", "Administrador", variant == "permiso" ? "Clientes.Consultar" : "Roles.Modificar");
            await Seed(db, "e", "Empleado", "Clientes.Consultar");
            var actorRef = db.Collection("empleados").Document("actor");
            await actorRef.SetAsync(new Dictionary<string, object> { ["Rol"] = variant == "rol" ? "Empleado" : "Administrador", ["Estado"] = variant == "inactivo" ? "Inactivo" : "Activo" });
            var admin = await Read(db, "a"); var empleado = await Read(db, "e"); var actor = await actorRef.GetSnapshotAsync();
            if (variant == "valido")
            {
                await ApplyGuarded(db, admin, empleado, actor);
                Assert.Equal(new[] { "Roles.Modificar" }.Concat(Admin), (await Read(db, "a")).GetValue<List<string>>("Permisos"));
                Assert.Equal(new[] { "Clientes.Consultar" }.Concat(Empleado), (await Read(db, "e")).GetValue<List<string>>("Permisos"));
                Assert.Equal(2, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
            }
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyGuarded(db, admin, empleado, actor));
                Assert.Equal("El actor no está autorizado.", error.Message);
                await AssertUnchanged(db, admin, empleado);
                Assert.Equal(actor.UpdateTime, (await actorRef.GetSnapshotAsync()).UpdateTime);
            }
        }
    }

    private sealed class FalloAudit : IAuditWriter
    {
        private readonly AuditService inner;
        public int Llamadas { get; private set; }
        public FalloAudit(FirestoreDb db) => inner = new AuditService(db);
        public void EnlistarEnTransaccion(Transaction transaction, AuditLog log)
        {
            if (++Llamadas == 2) throw new InvalidOperationException("auditoria fallida");
            inner.EnlistarEnTransaccion(transaction, log);
        }
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType) => throw new NotSupportedException();
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType, Dictionary<string, string> antes, Dictionary<string, string> despues, string nombreHistorico = null!) => throw new NotSupportedException();
        public Task LogSessionEvent(string userId, string userEmail, string action, string targetId, string targetType) => throw new NotSupportedException();
    }
}
