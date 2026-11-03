using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Integration;

public class AuditDetailTests
{
    [LocalFirestoreFact]
    public async Task DetailReadsCapturedHistoryAndDocumentIdWithoutMutation()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var service = new AuditService(db);
        var doc = db.Collection("registros_auditoria").Document("detail-test");
        var source = db.Collection("servicios").Document("target");
        await source.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Anterior" });
        await doc.SetAsync(new AuditLog { UserId = "actor", UserEmail = "actor@example.invalid", Action = "Editar", TargetType = "Servicio", TargetId = "target", NombreHistorico = "Anterior", Antes = new() { ["Nombre"] = "Anterior" }, Despues = new() { ["Nombre"] = "Nuevo" }, Timestamp = DateTime.UtcNow });
        await source.UpdateAsync("Nombre", "Renombrado posteriormente");
        var read = await service.ObtenerDetalle(doc.Id);
        Assert.Equal(doc.Id, read.DocId);
        Assert.Equal("Anterior", read.Antes["Nombre"]);
        Assert.Equal("Nuevo", read.Despues["Nombre"]);
        Assert.Contains(await service.ObtenerRegistros(), r => r.DocId == doc.Id);
        Assert.Contains(await service.ObtenerRegistros(sortBy: "Action"), r => r.DocId == doc.Id);
        Assert.Contains(await service.BuscarRegistros("Editar"), r => r.DocId == doc.Id);
        var controller = new AuditoriaController(service, null, null, null, null, null, null, null, null);
        var result = Assert.IsType<JsonResult>(await controller.Detalle(doc.Id));
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(doc.Id, payload.GetProperty("docId").GetString());
        Assert.Equal("Anterior", payload.GetProperty("nombreHistorico").GetString());
        Assert.Equal("Anterior", payload.GetProperty("antes").GetProperty("Nombre").GetString());
        Assert.Equal("Nuevo", payload.GetProperty("despues").GetProperty("Nombre").GetString());
        Assert.False(payload.GetProperty("comparacionNoAplica").GetBoolean());
        Assert.Equal("Renombrado posteriormente", (await source.GetSnapshotAsync()).GetValue<string>("Nombre"));
        Assert.IsType<NotFoundResult>(await controller.Detalle("missing"));
        Assert.IsType<NotFoundResult>(await controller.Detalle("bad/id"));
        Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        var session = db.Collection("registros_auditoria").Document("session");
        await session.SetAsync(new AuditLog { Action = "Cerrar Sesión", Antes = new(), Despues = new(), ComparacionNoAplica = true, Timestamp = DateTime.UtcNow });
        var sessionResult = Assert.IsType<JsonResult>(await controller.Detalle(session.Id));
        Assert.True(System.Text.Json.JsonSerializer.SerializeToElement(sessionResult.Value).GetProperty("comparacionNoAplica").GetBoolean());
    }
}
