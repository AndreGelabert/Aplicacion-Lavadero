using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public sealed class ProveedorEmailLockIntegrationTests
{
    [LocalFirestoreFact]
    public async Task CambioDeEmailSoloSePermiteSinActividadAbierta()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-lock-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new ProveedorService(db);
        foreach (var (collection, states) in new[]
        {
            ("solicitudesCotizacion", new[] { "Pendiente", "PendienteAprobacion", "Respondida", "Adjudicada", "Descartada", "Cancelada" }),
            ("ordenesCompra", new[] { "PendienteAprobacion", "RevisionManual", "Enviada", "Confirmada", "Despachada", "Recibida", "Cancelada" })
        })
        foreach (var state in states)
        {
            var id = collection + state;
            var original = new Proveedor { Id = id, IdentificacionTributaria = id, RazonSocial = id, Contacto = "1234567890", EmailOrdenes = "original@example.invalid" };
            await db.Collection("proveedores").Document(id).SetAsync(original);
            if (collection == "solicitudesCotizacion")
                await db.Collection(collection).Document(id).SetAsync(new SolicitudCotizacion { Id = id, ProveedorId = id, Estado = state });
            else
                await db.Collection(collection).Document(id).SetAsync(new OrdenCompra { Id = id, ProveedorId = id, Estado = state });
            var active = state is "Pendiente" or "PendienteAprobacion" or "Respondida" or "RevisionManual" or "Enviada" or "Confirmada" or "Despachada";
            Assert.Equal(active, (await service.Obtener(id))!.EmailOrdenesBloqueado);
            Assert.Equal(active, Assert.Single(await service.ObtenerTodos(), p => p.Id == id).EmailOrdenesBloqueado);
            Assert.False((await db.Collection("proveedores").Document(id).GetSnapshotAsync()).ContainsField("EmailOrdenesBloqueado"));
            var beforeAudit = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
            var edit = new Proveedor { Id = id, IdentificacionTributaria = id, RazonSocial = id, Contacto = "1234567890", EmailOrdenes = "nuevo@example.invalid" };
            var result = await service.Guardar(edit, false);
            Assert.Equal(!active, result.EsExitoso);
            Assert.Equal(active ? original.EmailOrdenes : edit.EmailOrdenes, (await service.Obtener(id))!.EmailOrdenes);
            Assert.Equal(beforeAudit + (active ? 0 : 1), (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task EmailSinCambiosPermiteEditarYProyeccionNoSePersiste()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-proj-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new ProveedorService(db);
        var original = new Proveedor { Id = "p", IdentificacionTributaria = "123", RazonSocial = "Original", Contacto = "1234567890", EmailOrdenes = "original@example.invalid", EmailOrdenesBloqueado = true };
        await db.Collection("proveedores").Document("p").SetAsync(original);
        Assert.False((await db.Collection("proveedores").Document("p").GetSnapshotAsync()).ContainsField("EmailOrdenesBloqueado"));
        await db.Collection("solicitudesCotizacion").Document("s").SetAsync(new SolicitudCotizacion { Id = "s", ProveedorId = "p", Estado = "Respondida" });
        Assert.True(Assert.Single(await service.ObtenerTodos()).EmailOrdenesBloqueado);
        original.RazonSocial = "Editado";
        Assert.True((await service.Guardar(original, false)).EsExitoso);
        Assert.Equal("Editado", (await service.Obtener("p"))!.RazonSocial);
        Assert.Equal("original@example.invalid", (await service.Obtener("p"))!.EmailOrdenes);
        Assert.False((await db.Collection("proveedores").Document("p").GetSnapshotAsync()).ContainsField("EmailOrdenesBloqueado"));
        Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task EmisionConcurrenteYEdicionDeEmailSeSerializan()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-race-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var compras = new ComprasService(db);
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });
        for (var n = 0; n < 10; n++)
        {
            var id = "p" + n;
            await db.Collection("proveedores").Document(id).SetAsync(new Proveedor { Id = id, IdentificacionTributaria = id, RazonSocial = id, Contacto = "1234567890", EmailOrdenes = "original@example.invalid" });
            var edit = new Proveedor { Id = id, IdentificacionTributaria = id, RazonSocial = id, Contacto = "1234567890", EmailOrdenes = "nuevo@example.invalid" };
            var emision = compras.CrearSolicitudes(new() { id }, new() { new() { InsumoId = "i", Cantidad = 1 } }, null);
            var cambio = proveedores.Guardar(edit, false);
            await Task.WhenAll(emision, cambio);
            Assert.True(emision.Result.EsExitoso);
            Assert.Equal(cambio.Result.EsExitoso ? "nuevo@example.invalid" : "original@example.invalid", (await proveedores.Obtener(id))!.EmailOrdenes);
            Assert.Single((await compras.ObtenerSolicitudes()).Where(s => s.ProveedorId == id));
            var audit = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
            Assert.Equal(cambio.Result.EsExitoso ? 1 : 0, audit.Count(d => d.GetValue<string>("TargetType") == "Proveedor" && d.GetValue<string>("TargetId") == id));
        }
    }

    [LocalFirestoreFact]
    public async Task EdicionInvalidaNoMutaProveedorNiAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-edit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new ProveedorService(db);
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", IdentificacionTributaria = "123", RazonSocial = "Original", Contacto = "1234567890", EmailOrdenes = "original@example.invalid" });
        foreach (var (phone, email) in new[] { ("", "a@example.invalid"), ("123456789", "a@example.invalid"), ("123456789a", "a@example.invalid"), ("1234567890", ""), ("1234567890", "invalido") })
        {
            var result = await service.Guardar(new Proveedor { Id = "p", IdentificacionTributaria = "123", RazonSocial = "Alterado", Contacto = phone, EmailOrdenes = email }, false);
            Assert.False(result.EsExitoso);
            var actual = (await service.Obtener("p"))!;
            Assert.Equal("Original", actual.RazonSocial); Assert.Equal("1234567890", actual.Contacto); Assert.Equal("original@example.invalid", actual.EmailOrdenes);
            Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task DatosInvalidosNoCreanProveedorNiAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-prov-valid-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new ProveedorService(db);
        foreach (var (phone, email) in new[] { ("", "a@example.invalid"), ("123456789", "a@example.invalid"), ("123456789a", "a@example.invalid"), ("1234567890", ""), ("1234567890", "invalido") })
        {
            var result = await service.Guardar(new Proveedor { IdentificacionTributaria = "123", RazonSocial = "Prueba", Contacto = phone, EmailOrdenes = email }, true);
            Assert.False(result.EsExitoso);
            Assert.Empty(await service.ObtenerTodos());
            Assert.Equal(0, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
        }
    }
}
