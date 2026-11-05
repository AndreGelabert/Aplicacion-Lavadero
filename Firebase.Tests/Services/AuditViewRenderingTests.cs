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

namespace Firebase.Tests.Services;

public class AuditViewRenderingTests
{
    [Fact]
    public async Task CompiledTableRendersTypedPagingAndEscapesHistoricalNames()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AuditoriaController).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.Services.AddControllersWithViews();
        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var context = new ActionContext(new DefaultHttpContext { RequestServices = scope.ServiceProvider },
            new RouteData(), new ActionDescriptor());
        var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
        var view = engine.GetView(null, "/Views/Auditoria/_AuditoriaTable.cshtml", false);
        Assert.True(view.Success, string.Join(", ", view.SearchedLocations ?? Array.Empty<string>()));
        var model = new AuditoriaViewModel
        {
            CurrentPage = 2, TotalPages = 3, VisiblePages = new() { 1, 2, 3 },
            Filters = new() { SortBy = "TargetName", SortOrder = "asc", SearchTerm = "<search>" },
            Registros = new() { new AuditLogConNombre { DocId = "row", UserName = "Actor", UserEmail = "actor@example.invalid",
                Action = "Editar", TargetType = "Externo", TargetName = "<script>alert(1)</script>", Timestamp = DateTime.UtcNow } }
        };
        using var output = new StringWriter();
        var data = new ViewDataDictionary<AuditoriaViewModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        var rendered = new ViewContext(context, view.View, data,
            new TempDataDictionary(context.HttpContext, Mock.Of<ITempDataProvider>()), output, new HtmlHelperOptions());
        await view.View.RenderAsync(rendered);
        var html = output.ToString();
        Assert.Contains("id=\"current-page-value\" value=\"2\"", html);
        Assert.Contains("id=\"current-sort-by\" value=\"TargetName\"", html);
        Assert.Contains("id=\"current-sort-order\" value=\"asc\"", html);
        Assert.Contains("id=\"current-search-term\" value=\"&lt;search&gt;\"", html);
        Assert.Contains("data-audit-detail-id=\"row\"", html);
        Assert.Contains("reloadAuditoriaTable(3)", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert", html);
    }
}
