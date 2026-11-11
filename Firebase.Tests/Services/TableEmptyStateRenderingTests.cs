using Firebase.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Text.RegularExpressions;

namespace Firebase.Tests.Services;

public class TableEmptyStateRenderingTests
{
    public static IEnumerable<object[]> Tables() => new object[][]
    {
        new object[] { "Auditoria", "_AuditoriaTable", new AuditoriaViewModel(), 6, "No hay registros de auditoría para mostrar", "Prueba ajustando los filtros o el rango de fechas" },
        new object[] { "Cliente", "_ClienteTable", new List<Cliente>(), 8, "No hay clientes registrados.", "" },
        new object[] { "Personal", "_PersonalTable", new List<Empleado>(), 5, "No hay datos para mostrar.", "" },
        new object[] { "Lavados", "_LavadoTable", new List<Lavado>(), 9, "No hay lavados para mostrar.", "" },
        new object[] { "Rol", "_RolTable", new List<Rol>(), 6, "No se encontraron roles registrados que coincidan con la búsqueda", "" },
        new object[] { "PaqueteServicio", "_PaqueteServicioTable", new List<PaqueteServicio>(), 9, "No se encontraron paquetes de servicios con los filtros aplicados.", "" },
        new object[] { "Vehiculo", "_VehiculoTable", new List<Vehiculo>(), 9, "No hay vehículos registrados.", "" },
        new object[] { "Servicio", "_ServicioTable", new List<Servicio>(), 8, "No hay datos para mostrar.", "" }
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public async Task EmptyTablesRetainCellAndMessageWithSharedPresentation(string folder, string name, object model, int colspan, string message, string detail)
    {
        var html = WebUtility.HtmlDecode(await Render(folder, name, model));
        var cell = Regex.Match(html, $"<tbody>\\s*<tr>\\s*<td colspan=\"{colspan}\"[^>]*>(.*?)</td>\\s*</tr>", RegexOptions.Singleline);
        Assert.True(cell.Success);
        Assert.Contains("data-table-empty-state", cell.Value);
        Assert.Contains("aria-hidden=\"true\"", cell.Value);
        Assert.Contains("dark:bg-gray-800", cell.Value);
        Assert.Contains("text-lg font-medium", cell.Value);
        Assert.Contains(message, cell.Value);
        if (detail.Length > 0) Assert.Contains(detail, cell.Value);
    }

    [Fact]
    public async Task ExistingAuditConsumerRetainsRowsAndPaging()
    {
        var model = new AuditoriaViewModel { CurrentPage = 2, TotalPages = 3, VisiblePages = new() { 1, 2, 3 },
            Registros = new() { new AuditLogConNombre { DocId = "row", TargetName = "<historical>", Timestamp = DateTime.UtcNow } } };
        var html = await Render("Auditoria", "_AuditoriaTable", model);
        Assert.Contains("data-audit-detail-id=\"row\"", html);
        Assert.Contains("&lt;historical&gt;", html);
        Assert.Contains("reloadAuditoriaTable(3)", html);
        Assert.DoesNotContain("data-table-empty-state", html);
    }

    private static async Task<string> Render(string folder, string name, object model)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(AuditoriaController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.Services.AddControllersWithViews();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var context = new ActionContext(new DefaultHttpContext { RequestServices = scope.ServiceProvider }, new RouteData(), new ActionDescriptor());
        var view = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, $"/Views/{folder}/{name}.cshtml", false);
        Assert.True(view.Success, string.Join(", ", view.SearchedLocations ?? Array.Empty<string>()));
        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        data["CurrentPage"] = 1; data["TotalPages"] = 1; data["VisiblePages"] = new List<int> { 1 };
        using var output = new StringWriter();
        var rendered = new ViewContext(context, view.View, data, new TempDataDictionary(context.HttpContext, Mock.Of<ITempDataProvider>()), output, new HtmlHelperOptions());
        await view.View.RenderAsync(rendered);
        return output.ToString();
    }
}
