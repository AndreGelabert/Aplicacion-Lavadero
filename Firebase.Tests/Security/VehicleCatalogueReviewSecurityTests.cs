using Firebase.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Firebase.Tests.Security;

public class VehicleCatalogueReviewSecurityTests
{
    [Fact]
    public void ReviewControllerRequiresAuthenticationAndVehicleModificationPermission()
    {
        var controller = typeof(VehicleCatalogueReviewController);
        Assert.NotEmpty(controller.GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var action = controller.GetMethod("Index")!;
        Assert.Contains(action.GetCustomAttributes(true), attribute =>
            attribute is IAuthorizeData authorization && authorization.Policy == "Permiso:Vehiculos.Modificar");
    }

    [Fact]
    public void ReviewMutationRequiresPermissionAndAntiforgery()
    {
        var method = typeof(VehicleCatalogueReviewController).GetMethod("Resolve")!;
        Assert.Contains(method.GetCustomAttributes(true), attribute =>
            attribute is IAuthorizeData authorization && authorization.Policy == "Permiso:Vehiculos.Modificar");
        Assert.NotEmpty(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute), true));
    }
}
