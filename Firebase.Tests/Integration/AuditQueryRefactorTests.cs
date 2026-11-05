using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Tests.Integration;

public class AuditQueryRefactorTests
{
    [LocalFirestoreFact]
    public async Task ListingsPreserveHistoricalOrderingSearchAndEndpointPaging()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-audit-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var audit = new AuditService(db);
        var controller = new AuditoriaController(audit, new PersonalService(db), null, null, null, null, null, null, null);
        var time = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        foreach (var (id, name, offset) in new[] { ("z", "Zulu", 0), ("a", "Árbol", 1), ("m", "Medio", 2) })
            await db.Collection("registros_auditoria").Document(id).SetAsync(new AuditLog
            {
                UserId = "missing-actor", UserEmail = "actor@example.invalid", Action = "Editar",
                TargetType = "Externo", TargetId = id, NombreHistorico = name,
                Antes = new() { ["Nombre"] = "Anterior" }, Despues = new() { ["Nombre"] = name },
                Timestamp = time.AddMinutes(offset)
            });

        var index = Assert.IsType<ViewResult>(await controller.Index(new AuditoriaFilter { PageSize = 2, SortBy = "TargetName", SortOrder = "asc" }));
        Assert.Equal(new[] { "a", "m" }, Rows(index).Select(r => r.DocId));
        Assert.All(Rows(index), r => Assert.Equal("actor@example.invalid", r.UserName));
        Assert.Equal(1, Page(index, "CurrentPage"));
        Assert.Equal(2, Page(index, "TotalPages"));
        var partial = Assert.IsType<PartialViewResult>(await controller.TablePartial(new AuditoriaFilter { PageNumber = 2, PageSize = 2, SortBy = "TargetName", SortOrder = "asc" }));
        Assert.Equal("_AuditoriaTable", partial.ViewName);
        Assert.Equal("z", Assert.Single(Rows(partial)).DocId);
        Assert.Equal(2, Page(partial, "CurrentPage"));

        var search = Assert.IsType<PartialViewResult>(await controller.SearchPartial(new AuditoriaFilter { SearchTerm = "arbol", PageSize = 2 }));
        var found = Assert.Single(Rows(search));
        Assert.Equal("a", found.DocId);
        Assert.Equal("Árbol", found.TargetName);
        Assert.Equal("Anterior", found.Antes["Nombre"]);
        Assert.Equal(1, Page(search, "TotalPages"));

        // Index clamps displayed metadata after fetching; TablePartial retains the requested page.
        var outside = Assert.IsType<ViewResult>(await controller.Index(new AuditoriaFilter { PageNumber = 3, PageSize = 2 }));
        Assert.Empty(Rows(outside));
        Assert.Equal(2, Page(outside, "CurrentPage"));
        var outsidePartial = Assert.IsType<PartialViewResult>(await controller.TablePartial(new AuditoriaFilter { PageNumber = 3, PageSize = 2 }));
        Assert.Empty(Rows(outsidePartial));
        Assert.Equal(3, Page(outsidePartial, "CurrentPage"));
        Assert.Equal(3, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task ScreenUsesExplicitTypedModelInsteadOfDynamicViewData()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-audit-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var controller = new AuditoriaController(new AuditService(db), new PersonalService(db), null, null, null, null, null, null, null);
        var result = Assert.IsType<ViewResult>(await controller.Index(new AuditoriaFilter()));
        Assert.Equal("AuditoriaViewModel", result.Model!.GetType().Name);
        Assert.False(result.ViewData.ContainsKey("CurrentPage"));
    }

    private static List<AuditLogConNombre> Rows(ViewResult result) => Rows(result.Model!);
    private static List<AuditLogConNombre> Rows(PartialViewResult result) => Rows(result.Model!);
    private static List<AuditLogConNombre> Rows(object model) => model as List<AuditLogConNombre>
        ?? (List<AuditLogConNombre>)model.GetType().GetProperty("Registros")!.GetValue(model)!;
    private static int Page(ViewResult result, string key) => Page(result.Model!, result.ViewData[key], key);
    private static int Page(PartialViewResult result, string key) => Page(result.Model!, result.ViewData[key], key);
    private static int Page(object model, object? legacy, string key) => (int)(legacy ?? model.GetType().GetProperty(key)!.GetValue(model)!);
}
