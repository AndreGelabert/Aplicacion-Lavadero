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
        var query = new Firebase.Services.AuditQueryService(audit, new PersonalService(db), null, null, null, null, null, null, null);
        var controller = new AuditoriaController(audit, query);
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

        var directSearch = await query.Buscar(new AuditoriaFilter { SearchTerm = "arbol", PageSize = 2 });
        var directFound = Assert.Single(directSearch.Registros);
        Assert.Equal("a", directFound.DocId);
        Assert.Equal("Árbol", directFound.TargetName);
        Assert.Equal("Anterior", directFound.Antes["Nombre"]);
        Assert.Equal(1, directSearch.TotalPages);

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
    public async Task SearchFindsInactiveActorAndDeduplicatesDifferentDocumentIds()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-audit-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        await db.Collection("empleados").Document("actor").SetAsync(new Dictionary<string, object>
        {
            ["Nombre"] = "José Inactivo", ["Email"] = "other@example.invalid", ["Estado"] = "Inactivo"
        });
        var timestamp = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        foreach (var id in new[] { "one", "two" })
            await db.Collection("registros_auditoria").Document(id).SetAsync(new AuditLog
            {
                UserId = "actor", UserEmail = "actor@example.invalid", Action = "Editar",
                TargetType = "Externo", TargetId = "object", Timestamp = timestamp
            });
        var controller = new AuditoriaController(new AuditService(db), new Firebase.Services.AuditQueryService(new AuditService(db), new PersonalService(db), null, null, null, null, null, null, null));

        var result = Assert.IsType<PartialViewResult>(await controller.SearchPartial(new AuditoriaFilter { SearchTerm = "jose", PageSize = 1 }));
        var row = Assert.Single(Rows(result));
        Assert.Contains(row.DocId, new[] { "one", "two" });
        Assert.Equal("José Inactivo", row.UserName);
        Assert.Equal(1, Page(result, "TotalPages"));
        Assert.Equal(2, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task ObjectSearchAndSortingResolveLiveAndHistoricalNamesBeforePaging()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-audit-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        await db.Collection("empleados").Document("target").SetAsync(new Dictionary<string, object>
        {
            ["Nombre"] = "Álvaro Vivo", ["Email"] = "target@example.invalid", ["Estado"] = "Inactivo"
        });
        var time = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        foreach (var (id, targetId, historical, offset) in new[]
        {
            ("live", "target", (string?)null, 0),
            ("historical", "target", "Nombre Histórico", 1),
            ("missing", "missing", "Nombre Perdido", 2)
        })
            await db.Collection("registros_auditoria").Document(id).SetAsync(new AuditLog
            {
                UserId = "unknown", UserEmail = "actor@example.invalid", Action = "Editar",
                TargetType = "Empleado", TargetId = targetId, NombreHistorico = historical,
                Timestamp = time.AddMinutes(offset)
            });
        var controller = new AuditoriaController(new AuditService(db), new Firebase.Services.AuditQueryService(new AuditService(db), new PersonalService(db), null, null, null, null, null, null, null));

        var liveSearch = Assert.IsType<PartialViewResult>(await controller.SearchPartial(new AuditoriaFilter { SearchTerm = "alvaro" }));
        var live = Assert.Single(Rows(liveSearch));
        Assert.Equal("live", live.DocId);
        Assert.Equal("Álvaro Vivo", live.TargetName);
        var historicalSearch = Assert.IsType<PartialViewResult>(await controller.SearchPartial(new AuditoriaFilter { SearchTerm = "nombre" }));
        Assert.Equal(new[] { "missing", "historical" }, Rows(historicalSearch).Select(r => r.DocId));
        Assert.Equal(new[] { "Nombre Perdido", "Nombre Histórico" }, Rows(historicalSearch).Select(r => r.TargetName));

        var first = Assert.IsType<PartialViewResult>(await controller.TablePartial(new AuditoriaFilter { SortBy = "TargetName", SortOrder = "asc", PageSize = 1 }));
        Assert.Equal("live", Assert.Single(Rows(first)).DocId);
        Assert.Equal(3, Page(first, "TotalPages"));
        var outside = Assert.IsType<PartialViewResult>(await controller.TablePartial(new AuditoriaFilter { SortBy = "TargetName", SortOrder = "asc", PageNumber = 9, PageSize = 1 }));
        Assert.Equal("missing", Assert.Single(Rows(outside)).DocId);
        Assert.Equal(3, Page(outside, "CurrentPage"));
    }

    [LocalFirestoreFact]
    public async Task DateRangesAllowEqualOrderedAndOpenEndedDatesAcrossAllListings()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-dates-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var day = new DateTime(2026, 11, 2);
        await db.Collection("registros_auditoria").Document("same-day").SetAsync(new AuditLog
        {
            UserId = "missing", UserEmail = "actor@example.invalid", Action = "Editar",
            TargetType = "Externo", TargetId = "object", NombreHistorico = "Original",
            Timestamp = DateTime.SpecifyKind(day.AddHours(12), DateTimeKind.Local).ToUniversalTime()
        });
        var audit = new AuditService(db);
        var controller = new AuditoriaController(audit, new Firebase.Services.AuditQueryService(audit,
            new PersonalService(db), null!, null!, null!, null!, null!, null!, null!));
        foreach (var (start, end) in new (DateTime?, DateTime?)[]
        {
            (day, day), (day.AddDays(-1), day.AddDays(1)), (day, null), (null, day), (null, null)
        })
        {
            foreach (var action in new[] { "Index", "TablePartial", "SearchPartial" })
            {
                var filters = new AuditoriaFilter { FechaInicio = start, FechaFin = end };
                var result = action switch
                {
                    "Index" => await controller.Index(filters),
                    "TablePartial" => await controller.TablePartial(filters),
                    _ => await controller.SearchPartial(filters)
                };
                var model = result is ViewResult view
                    ? view.Model!
                    : Assert.IsType<PartialViewResult>(result).Model!;
                Assert.Equal("same-day", Assert.Single(Rows(model)).DocId);
            }
        }
        Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ScreenUsesExplicitTypedModelInsteadOfDynamicViewData()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-audit-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var controller = new AuditoriaController(new AuditService(db), new Firebase.Services.AuditQueryService(new AuditService(db), new PersonalService(db), null, null, null, null, null, null, null));
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
