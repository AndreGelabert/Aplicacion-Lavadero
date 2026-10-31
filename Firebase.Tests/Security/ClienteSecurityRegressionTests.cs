using Firebase.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Firebase.Tests.Security;

public class ClienteSecurityRegressionTests
{
    [Theory]
    [InlineData(nameof(ClienteController.DeactivateCliente))]
    [InlineData(nameof(ClienteController.ReactivateCliente))]
    public void StateChangesRequireAntiforgery(string action)
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(ClienteController).GetMethod(action)!,
            typeof(ValidateAntiForgeryTokenAttribute)));
    }
}
