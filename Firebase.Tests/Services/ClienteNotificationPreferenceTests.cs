using Firebase.Models;
using Firebase.Services;
using System.ComponentModel.DataAnnotations;

namespace Firebase.Tests.Services;

public class ClienteNotificationPreferenceTests
{
    private static Cliente Cliente(bool? whatsapp = null, bool? email = null) => new()
    {
        Id = "cliente-1",
        TipoDocumento = "DNI",
        NumeroDocumento = "12345678",
        Nombre = "Ana",
        Apellido = "Prueba",
        Telefono = "3751000000",
        Email = "ana@example.com",
        NotificarPorWhatsApp = whatsapp,
        NotificarPorEmail = email
    };

    [Fact]
    public void LegacyClient_UsesEmailAsEffectiveDefault()
    {
        var cliente = Cliente();

        cliente.NormalizarPreferenciasNotificacion();

        Assert.False(cliente.PrefiereWhatsApp);
        Assert.True(cliente.PrefiereEmail);
        Assert.True(cliente.NotificarPorEmail);
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public void ExplicitPreferences_ArePreserved(bool whatsapp, bool email, bool expectedWhatsApp, bool expectedEmail)
    {
        var cliente = Cliente(whatsapp, email);

        cliente.NormalizarPreferenciasNotificacion(solicitudExplicita: true);

        Assert.Equal(expectedWhatsApp, cliente.PrefiereWhatsApp);
        Assert.Equal(expectedEmail, cliente.PrefiereEmail);
    }

    [Fact]
    public void ExplicitlyEmptyPreferences_AreRejected()
    {
        var cliente = Cliente(false, false);

        Assert.Throws<ValidationException>(() =>
            cliente.NormalizarPreferenciasNotificacion(solicitudExplicita: true));
    }

    [Fact]
    public void CancellationMessage_ContainsReasonAndDoesNotClaimCompletion()
    {
        var lavado = new Lavado
        {
            Id = "lavado-1",
            Estado = "Cancelado",
            ClienteId = "cliente-1",
            VehiculoId = "vehiculo-1",
            VehiculoPatente = "ABC123",
            Servicios = new()
        };

        var message = ClienteNotificationService.BuildMessage(
            lavado, null, "cancelacion_lavado", null, null, "Falta de insumos");

        Assert.Contains("cancelado", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Falta de insumos", message);
        Assert.DoesNotContain("listo para retirar", message, StringComparison.OrdinalIgnoreCase);
    }
}
