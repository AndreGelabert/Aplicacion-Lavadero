using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;

namespace Firebase.Tests.Integration;

public class PersonalQueryRefactorTests
{
    private static FirestoreDb Database() => new FirestoreDbBuilder
    {
        ProjectId = "demo-personal-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static (PersonalController controller, RolService roles) CreateController(FirestoreDb db)
    {
        var roles = new RolService(db, new MemoryCache(new MemoryCacheOptions()));
        var personal = new PersonalService(db);
        return (new PersonalController(personal, new Firebase.Services.PersonalQueryService(personal), roles, null!), roles);
    }

    private static async Task Seed(FirestoreDb db)
    {
        foreach (var (id, name, email, role, state) in new[]
        {
            ("a", "Ana", "ana@example.invalid", "Empleado", "Activo"),
            ("b", "Bruno", "bruno@example.invalid", "Supervisor", "Activo"),
            ("c", "Carlos", "carlos@example.invalid", "Empleado", "Activo"),
            ("d", "Diana", "diana@example.invalid", "Supervisor", "Inactivo")
        })
            await db.Collection("empleados").Document(id).SetAsync(new Dictionary<string, object>
            {
                ["Nombre"] = name, ["Email"] = email, ["Rol"] = role, ["Estado"] = state
            });
    }

    private static async Task AssertRolesInitialized(RolService roles, ViewDataDictionary data)
    {
        var names = (await roles.ObtenerRoles()).Select(r => r.Nombre).ToArray();
        Assert.Equal(new[] { "Administrador", "Empleado" }, names);
        Assert.Equal(names, Assert.IsType<List<string>>(data["TodosLosRoles"]));
    }

    private static void AssertMetadata(ViewDataDictionary data, int current, int total, int[] visible,
        string[] states, string[]? roles, string sortBy, string sortOrder)
    {
        Assert.Equal(current, data["CurrentPage"]);
        Assert.Equal(total, data["TotalPages"]);
        Assert.Equal(visible, Assert.IsType<List<int>>(data["VisiblePages"]));
        Assert.Equal(states, Assert.IsType<List<string>>(data["Estados"]));
        if (roles == null) Assert.Null(data["Roles"]);
        else Assert.Equal(roles, Assert.IsType<List<string>>(data["Roles"]));
        Assert.Equal(sortBy, data["SortBy"]);
        Assert.Equal(sortOrder, data["SortOrder"]);
    }

    private static List<Empleado> Rows(object? model) => Assert.IsType<List<Empleado>>(model);

    [LocalFirestoreFact]
    public async Task IndexDefaultsFiltersSortsAndClampsDisplayWithoutRefetch()
    {
        var db = Database();
        await Seed(db);
        var (controller, roles) = CreateController(db);
        Assert.Empty(await roles.ObtenerRoles());
        var states = new List<string>();
        var index = Assert.IsType<ViewResult>(await controller.Index(states, null!, pageSize: 2));
        Assert.Null(index.ViewName);
        Assert.Equal(new[] { "a", "b" }, Rows(index.Model).Select(e => e.Id));
        Assert.Equal(new[] { "Activo" }, states);
        AssertMetadata(index.ViewData, 1, 2, new[] { 1, 2 }, new[] { "Activo" }, null, "NombreCompleto", "asc");
        Assert.Equal(2, index.ViewData["PageSize"]);
        Assert.False(index.ViewData.ContainsKey("SearchTerm"));
        await AssertRolesInitialized(roles, index.ViewData);

        var filtered = Assert.IsType<ViewResult>(await controller.Index(new() { "Activo", "Inactivo" },
            new() { "Supervisor" }, pageNumber: 2, pageSize: 1, sortBy: "Email", sortOrder: "desc"));
        Assert.Equal("b", Assert.Single(Rows(filtered.Model)).Id);
        AssertMetadata(filtered.ViewData, 2, 2, new[] { 1, 2 }, new[] { "Activo", "Inactivo" },
            new[] { "Supervisor" }, "Email", "desc");
        var outside = Assert.IsType<ViewResult>(await controller.Index(null!, null!, pageNumber: 3, pageSize: 2));
        Assert.Empty(Rows(outside.Model));
        AssertMetadata(outside.ViewData, 2, 2, new[] { 1, 2 }, new[] { "Activo" }, null, "NombreCompleto", "asc");
    }

    [LocalFirestoreFact]
    public async Task TablePartialPreservesRawPageMetadataAndFilters()
    {
        var db = Database();
        await Seed(db);
        var (controller, roles) = CreateController(db);
        Assert.Empty(await roles.ObtenerRoles());
        var table = Assert.IsType<PartialViewResult>(await controller.TablePartial(new() { "Activo", "Inactivo" },
            new() { "Supervisor" }, pageNumber: 2, pageSize: 1, sortBy: "NombreCompleto", sortOrder: "desc"));
        Assert.Equal("_PersonalTable", table.ViewName);
        Assert.Equal("b", Assert.Single(Rows(table.Model)).Id);
        AssertMetadata(table.ViewData, 2, 2, new[] { 1, 2 }, new[] { "Activo", "Inactivo" },
            new[] { "Supervisor" }, "NombreCompleto", "desc");
        Assert.False(table.ViewData.ContainsKey("PageSize"));
        Assert.False(table.ViewData.ContainsKey("SearchTerm"));
        await AssertRolesInitialized(roles, table.ViewData);

        var outside = Assert.IsType<PartialViewResult>(await controller.TablePartial(null!, null!, pageNumber: 3, pageSize: 2));
        Assert.Empty(Rows(outside.Model));
        AssertMetadata(outside.ViewData, 3, 2, new[] { 1, 2 }, new[] { "Activo" }, null, "NombreCompleto", "asc");
    }

    [LocalFirestoreFact]
    public async Task SearchPartialMatchesNameEmailRoleAndStateWithRawMetadata()
    {
        var db = Database();
        await Seed(db);
        var (controller, roles) = CreateController(db);
        Assert.Empty(await roles.ObtenerRoles());
        var search = Assert.IsType<PartialViewResult>(await controller.SearchPartial("  BRU  ", null!, null!, pageSize: 1));
        Assert.Equal("_PersonalTable", search.ViewName);
        Assert.Equal("b", Assert.Single(Rows(search.Model)).Id);
        AssertMetadata(search.ViewData, 1, 1, new[] { 1 }, new[] { "Activo" }, null, "NombreCompleto", "asc");
        Assert.Equal("  BRU  ", search.ViewData["SearchTerm"]);
        Assert.False(search.ViewData.ContainsKey("PageSize"));
        await AssertRolesInitialized(roles, search.ViewData);

        foreach (var (term, expected) in new[] { ("CARLOS@", "c"), ("superVIS", "b") })
        {
            var result = Assert.IsType<PartialViewResult>(await controller.SearchPartial(term, null!, null!));
            Assert.Equal(expected, Assert.Single(Rows(result.Model)).Id);
            Assert.Equal(term, result.ViewData["SearchTerm"]);
        }
        var filtered = Assert.IsType<PartialViewResult>(await controller.SearchPartial("inactivo",
            new() { "Inactivo" }, new() { "Supervisor" }, sortBy: "Email", sortOrder: "desc"));
        Assert.Equal("d", Assert.Single(Rows(filtered.Model)).Id);
        AssertMetadata(filtered.ViewData, 1, 1, new[] { 1 }, new[] { "Inactivo" }, new[] { "Supervisor" }, "Email", "desc");
        var outside = Assert.IsType<PartialViewResult>(await controller.SearchPartial("Empleado", null!, null!, pageNumber: 3, pageSize: 2));
        Assert.Empty(Rows(outside.Model));
        AssertMetadata(outside.ViewData, 3, 1, new[] { 1 }, new[] { "Activo" }, null, "NombreCompleto", "asc");
    }

    [LocalFirestoreFact]
    public async Task EmptyResultsKeepOnePageAndDefaultStatesMutateEmptyInput()
    {
        var db = Database();
        var (controller, roles) = CreateController(db);
        var states = new List<string>();
        var index = Assert.IsType<ViewResult>(await controller.Index(states, new()));
        Assert.Empty(Rows(index.Model));
        Assert.Equal(new[] { "Activo" }, states);
        AssertMetadata(index.ViewData, 1, 1, new[] { 1 }, new[] { "Activo" }, Array.Empty<string>(), "NombreCompleto", "asc");
        var table = Assert.IsType<PartialViewResult>(await controller.TablePartial(new(), null!));
        Assert.Empty(Rows(table.Model));
        AssertMetadata(table.ViewData, 1, 1, new[] { 1 }, new[] { "Activo" }, null, "NombreCompleto", "asc");
        var search = Assert.IsType<PartialViewResult>(await controller.SearchPartial("missing", new(), null!));
        Assert.Empty(Rows(search.Model));
        AssertMetadata(search.ViewData, 1, 1, new[] { 1 }, new[] { "Activo" }, null, "NombreCompleto", "asc");
        await AssertRolesInitialized(roles, search.ViewData);
    }
}
