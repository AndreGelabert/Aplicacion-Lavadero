using Firebase.Models;
using Microsoft.AspNetCore.Mvc;

namespace Firebase.Tests.Services;

public class AuditDateRangeTests
{
    [Theory]
    [InlineData("Index")]
    [InlineData("TablePartial")]
    [InlineData("SearchPartial")]
    public async Task InvertedDatesReturn400BeforeReadingStorage(string action)
    {
        var controller = new AuditoriaController(null!, null!);
        var filters = new AuditoriaFilter
        {
            FechaInicio = new DateTime(2026, 11, 2),
            FechaFin = new DateTime(2026, 10, 6)
        };
        var result = action switch
        {
            "Index" => await controller.Index(filters),
            "TablePartial" => await controller.TablePartial(filters),
            _ => await controller.SearchPartial(filters)
        };
        var error = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("La fecha Desde no puede ser posterior a la fecha Hasta.", error.Value);
        Assert.Null(filters.SortBy);
        Assert.Null(filters.SortOrder);
    }
}
