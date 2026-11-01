using Firebase.Models;
using Firebase.Models.Dtos;
using Firebase.Models.WhatsApp;
using Firebase.Services;
using Firebase.Tests.Integration;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Firebase.Tests.Services;

/// <summary>
/// Tests para el flujo de confirmación antes de regenerar la clave de asociación de vehículo.
///
/// Cubre:
///   - El estado CONFIRMAR_REGENERAR_CLAVE existe y es único
///   - Seleccionar "generar_clave" muestra confirmación, NO rota la clave
///   - Confirmar SÍ rota la clave y el hash anterior invalida
///   - Confirmar NO cancela sin rotar la clave
///   - Entrada inválida en confirmación no rota la clave
///   - No-propietario principal es rechazado sin rotar (tanto desde menú como desde confirmación fabricada)
///   - Comportamiento de editar color no se rompe
/// </summary>
public class WhatsAppAssociationKeyTests
{
    // =========================================================================
    // Tests unitarios puros (sin emulador)
    // =========================================================================

    #region Estado CONFIRMAR_REGENERAR_CLAVE

    /// <summary>
    /// Verifica que el estado CONFIRMAR_REGENERAR_CLAVE esté definido.
    /// </summary>
    [Fact]
    public void FlowStates_ConfirmarRegenerarClave_Definido()
    {
        Assert.Equal("CONFIRMAR_REGENERAR_CLAVE", WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE);
    }

    /// <summary>
    /// Verifica que CONFIRMAR_REGENERAR_CLAVE sea distinto de todos los otros estados.
    /// </summary>
    [Fact]
    public void FlowStates_ConfirmarRegenerarClave_Unico()
    {
        var todos = new[]
        {
            WhatsAppFlowStates.REGISTRO_TIPO_DOCUMENTO,
            WhatsAppFlowStates.REGISTRO_NUM_DOCUMENTO,
            WhatsAppFlowStates.REGISTRO_NOMBRE,
            WhatsAppFlowStates.REGISTRO_APELLIDO,
            WhatsAppFlowStates.REGISTRO_EMAIL,
            WhatsAppFlowStates.REGISTRO_CONFIRMACION,
            WhatsAppFlowStates.VEHICULO_MENU,
            WhatsAppFlowStates.VEHICULO_TIPO,
            WhatsAppFlowStates.VEHICULO_PATENTE,
            WhatsAppFlowStates.VEHICULO_MARCA,
            WhatsAppFlowStates.VEHICULO_MODELO,
            WhatsAppFlowStates.VEHICULO_COLOR,
            WhatsAppFlowStates.VEHICULO_CONFIRMACION,
            WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO,
            WhatsAppFlowStates.MOSTRAR_DATOS,
            WhatsAppFlowStates.EDITAR_DATOS_MENU,
            WhatsAppFlowStates.EDITAR_NOMBRE,
            WhatsAppFlowStates.EDITAR_APELLIDO,
            WhatsAppFlowStates.EDITAR_EMAIL,
            WhatsAppFlowStates.CONFIRMAR_EDICION,
            WhatsAppFlowStates.MENU_VEHICULOS,
            WhatsAppFlowStates.MOSTRAR_VEHICULOS,
            WhatsAppFlowStates.SELECCIONAR_VEHICULO_MODIFICAR,
            WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU,
            WhatsAppFlowStates.MODIFICAR_VEHICULO_MODELO,
            WhatsAppFlowStates.MODIFICAR_VEHICULO_COLOR,
            WhatsAppFlowStates.CONFIRMAR_ELIMINAR_VEHICULO,
            WhatsAppFlowStates.ASOCIAR_VEHICULO_PATENTE,
            WhatsAppFlowStates.ASOCIAR_VEHICULO_CLAVE,
            WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION,
            WhatsAppFlowStates.MOSTRAR_CLAVE_VEHICULO,
            WhatsAppFlowStates.REGISTRO_VEHICULO_OPCION,
            WhatsAppFlowStates.INICIO,
        };

        foreach (var estado in todos)
        {
            Assert.NotEqual(WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE, estado);
        }
    }

    #endregion

    // =========================================================================
    // Tests de integración con emulador Firestore
    // =========================================================================

    /// <summary>
    /// Al seleccionar "generar_clave" desde el menú de modificación,
    /// el sistema muestra la confirmación y NO rota la clave (hash se mantiene).
    /// </summary>
    [LocalFirestoreFact]
    public async Task SeleccionarGenerarClave_MuestraConfirmacion_SinRotarHash()
    {
        var db = NewDatabase();
        const string phone = "543751100001";
        const string clienteId = "cli-owner";
        const string hashOriginal = "hashoriginal123";

        var vehiculo = NewVehiculo("veh-1", clienteId, hashOriginal);
        await db.Collection("vehiculos").Document(vehiculo.Id).SetAsync(vehiculo);

        var cliente = NewCliente(clienteId, "3751100001");
        await db.Collection("clientes").Document(clienteId).SetAsync(cliente);

        var session = NewSession(phone, clienteId, WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU);
        session.TemporaryData["vehiculo_modificar_id"] = vehiculo.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);

        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "generar_clave");

        // El estado debe ser CONFIRMAR_REGENERAR_CLAVE
        var sessionActualizada = await GetSession(db, phone);
        Assert.Equal(WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE, sessionActualizada.CurrentState);

        // El mensaje debe advertir sobre la clave anterior
        Assert.Contains("anterior", outgoing.LastBody, StringComparison.OrdinalIgnoreCase);

        // El hash en Firestore NO debe haber cambiado
        var vehiculoPostRequest = (await db.Collection("vehiculos").Document(vehiculo.Id).GetSnapshotAsync())
            .ConvertTo<Vehiculo>();
        Assert.Equal(hashOriginal, vehiculoPostRequest.ClaveAsociacionHash);
    }

    /// <summary>
    /// Al confirmar con "SÍ", se rota la clave:
    ///   - El nuevo hash es distinto al original
    ///   - El hash anterior ya no valida con VehiculoService.ValidarClaveAsociacion
    ///   - Las asociaciones (ClientesIds) se preservan intactas
    /// </summary>
    [LocalFirestoreFact]
    public async Task ConfirmarSi_RotaHash_OldHashInvalido_AsociacionesPreservadas()
    {
        var db = NewDatabase();
        const string phone = "543751100002";
        const string clienteId = "cli-owner";
        const string asociadoId = "cli-asociado";

        var claveOriginal = VehiculoService.GenerarClaveAsociacion();
        var hashOriginal = VehiculoService.HashClaveAsociacion(claveOriginal);

        var vehiculo = NewVehiculo("veh-2", clienteId, hashOriginal);
        vehiculo.ClientesIds = new List<string> { clienteId, asociadoId };
        await db.Collection("vehiculos").Document(vehiculo.Id).SetAsync(vehiculo);

        var cliente = NewCliente(clienteId, "3751100002");
        await db.Collection("clientes").Document(clienteId).SetAsync(cliente);

        var session = NewSession(phone, clienteId, WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE);
        session.TemporaryData["vehiculo_modificar_id"] = vehiculo.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);

        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "SÍ");

        var vehiculoActualizado = (await db.Collection("vehiculos").Document(vehiculo.Id).GetSnapshotAsync())
            .ConvertTo<Vehiculo>();

        // El hash debe haber cambiado
        Assert.NotEqual(hashOriginal, vehiculoActualizado.ClaveAsociacionHash);
        Assert.False(string.IsNullOrEmpty(vehiculoActualizado.ClaveAsociacionHash));

        // La clave anterior ya no es válida
        Assert.False(VehiculoService.ValidarClaveAsociacion(claveOriginal, vehiculoActualizado.ClaveAsociacionHash!));

        // Las asociaciones existentes se preservan
        Assert.NotNull(vehiculoActualizado.ClientesIds);
        Assert.Contains(clienteId, vehiculoActualizado.ClientesIds);
        Assert.Contains(asociadoId, vehiculoActualizado.ClientesIds);
        Assert.Equal(vehiculo.ClientesIds, vehiculoActualizado.ClientesIds);

        var texts = outgoing.Bodies.Select(body => System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body))
            .Where(payload => payload.TryGetProperty("text", out _))
            .Select(payload => payload.GetProperty("text").GetProperty("body").GetString() ?? "");
        var keyMessage = Assert.Single(texts.Where(text => text.Contains("*Nueva clave:*")));
        var nuevaClave = keyMessage.Split('`')[1];
        Assert.True(VehiculoService.ValidarClaveAsociacion(nuevaClave, vehiculoActualizado.ClaveAsociacionHash!));
        var stored = (await db.Collection("vehiculos").Document(vehiculo.Id).GetSnapshotAsync()).ToDictionary();
        Assert.DoesNotContain(stored.Values, value => value is string text && text.Contains(nuevaClave));
    }

    /// <summary>
    /// Al confirmar con "NO", el hash no cambia y se vuelve al menú de vehículos.
    /// </summary>
    [LocalFirestoreFact]
    public async Task ConfirmarNo_NoRotaHash_VuelveAlMenu()
    {
        var db = NewDatabase();
        const string phone = "543751100003";
        const string clienteId = "cli-owner";
        const string hashOriginal = "hashoriginal-no-test";

        var vehiculo = NewVehiculo("veh-3", clienteId, hashOriginal);
        await db.Collection("vehiculos").Document(vehiculo.Id).SetAsync(vehiculo);

        var cliente = NewCliente(clienteId, "3751100003");
        await db.Collection("clientes").Document(clienteId).SetAsync(cliente);

        var session = NewSession(phone, clienteId, WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE);
        session.TemporaryData["vehiculo_modificar_id"] = vehiculo.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);

        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "NO");

        var vehiculoPostCancelacion = (await db.Collection("vehiculos").Document(vehiculo.Id).GetSnapshotAsync())
            .ConvertTo<Vehiculo>();

        // El hash no debe haber cambiado
        Assert.Equal(hashOriginal, vehiculoPostCancelacion.ClaveAsociacionHash);

        // El mensaje debe indicar cancelación
        Assert.Contains(outgoing.Bodies, body => body.Contains("cancelad", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(WhatsAppFlowStates.MENU_VEHICULOS, (await GetSession(db, phone)).CurrentState);
    }

    /// <summary>
    /// Entrada inválida en el estado de confirmación no rota la clave
    /// y permanece en CONFIRMAR_REGENERAR_CLAVE.
    /// </summary>
    [LocalFirestoreFact]
    public async Task EntradaInvalida_NoRotaHash_PermanenceEnConfirmacion()
    {
        var db = NewDatabase();
        const string phone = "543751100004";
        const string clienteId = "cli-owner";
        const string hashOriginal = "hashoriginal-invalido-test";

        var vehiculo = NewVehiculo("veh-4", clienteId, hashOriginal);
        await db.Collection("vehiculos").Document(vehiculo.Id).SetAsync(vehiculo);

        var cliente = NewCliente(clienteId, "3751100004");
        await db.Collection("clientes").Document(clienteId).SetAsync(cliente);

        var session = NewSession(phone, clienteId, WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE);
        session.TemporaryData["vehiculo_modificar_id"] = vehiculo.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);

        var (flow, outgoing) = CreateFlow(db);

        // Entradas inválidas (no SÍ ni NO)
        await flow.ProcessMessage(phone, "quizas");
        var estadoDespues = (await GetSession(db, phone)).CurrentState;
        Assert.Equal(WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE, estadoDespues);

        var vehiculoPostInvalido = (await db.Collection("vehiculos").Document(vehiculo.Id).GetSnapshotAsync())
            .ConvertTo<Vehiculo>();
        Assert.Equal(hashOriginal, vehiculoPostInvalido.ClaveAsociacionHash);

        using var payload = System.Text.Json.JsonDocument.Parse(outgoing.LastBody);
        Assert.Contains("SÍ", payload.RootElement.GetProperty("text").GetProperty("body").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Un cliente que NO es dueño principal no puede generar clave desde el menú de modificación.
    /// El hash no cambia.
    /// </summary>
    [LocalFirestoreFact]
    public async Task NoPropietarioPrincipal_DesdeMenu_Rechazado_SinRotarHash()
    {
        var db = NewDatabase();
        const string phone = "543751100005";
        const string propietarioId = "cli-owner";
        const string otroClienteId = "cli-otro";
        const string hashOriginal = "hashoriginal-unauthorized";

        var vehiculo = NewVehiculo("veh-5", propietarioId, hashOriginal);
        vehiculo.ClientesIds = new List<string> { propietarioId, otroClienteId };
        await db.Collection("vehiculos").Document(vehiculo.Id).SetAsync(vehiculo);

        var otroCliente = NewCliente(otroClienteId, "3751100005");
        await db.Collection("clientes").Document(otroClienteId).SetAsync(otroCliente);

        // La sesión pertenece a otroClienteId (no al propietario)
        var session = NewSession(phone, otroClienteId, WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU);
        session.TemporaryData["vehiculo_modificar_id"] = vehiculo.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);

        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "generar_clave");

        // El hash no debe haber cambiado
        var vehiculoPostIntento = (await db.Collection("vehiculos").Document(vehiculo.Id).GetSnapshotAsync())
            .ConvertTo<Vehiculo>();
        Assert.Equal(hashOriginal, vehiculoPostIntento.ClaveAsociacionHash);

        // El estado NO debe ser CONFIRMAR_REGENERAR_CLAVE (fue rechazado)
        var sessionFinal = await GetSession(db, phone);
        Assert.NotEqual(WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE, sessionFinal.CurrentState);
    }

    /// <summary>
    /// Un cliente que fabrica un mensaje en estado CONFIRMAR_REGENERAR_CLAVE
    /// para un vehículo cuyo propietario es otro NO puede rotar la clave.
    /// </summary>
    [LocalFirestoreFact]
    public async Task EntradaFabricada_ConfirmacionEstado_NoPropietario_Rechazado()
    {
        var db = NewDatabase();
        const string phone = "543751100006";
        const string propietarioId = "cli-owner";
        const string otroClienteId = "cli-atacante";
        const string hashOriginal = "hashoriginal-crafted";

        var vehiculo = NewVehiculo("veh-6", propietarioId, hashOriginal);
        await db.Collection("vehiculos").Document(vehiculo.Id).SetAsync(vehiculo);

        var otroCliente = NewCliente(otroClienteId, "3751100006");
        await db.Collection("clientes").Document(otroClienteId).SetAsync(otroCliente);

        // El atacante pone directamente el estado CONFIRMAR_REGENERAR_CLAVE con el vehículo de otro
        var session = NewSession(phone, otroClienteId, WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE);
        session.TemporaryData["vehiculo_modificar_id"] = vehiculo.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);

        var (flow, _) = CreateFlow(db);

        await flow.ProcessMessage(phone, "SÍ");

        // El hash NO debe haber cambiado
        var vehiculoPostAtaque = (await db.Collection("vehiculos").Document(vehiculo.Id).GetSnapshotAsync())
            .ConvertTo<Vehiculo>();
        Assert.Equal(hashOriginal, vehiculoPostAtaque.ClaveAsociacionHash);
    }

    /// <summary>
    /// La opción de editar color sigue funcionando correctamente (no se rompe con el cambio).
    /// </summary>
    [LocalFirestoreFact]
    public async Task EditarColor_SigueFuncionandoCorrectamente()
    {
        var db = NewDatabase();
        const string phone = "543751100007";
        const string clienteId = "cli-owner";

        var vehiculo = NewVehiculo("veh-7", clienteId, "somehash");
        vehiculo.Color = "Blanco";
        await db.Collection("vehiculos").Document(vehiculo.Id).SetAsync(vehiculo);

        var cliente = NewCliente(clienteId, "3751100007");
        await db.Collection("clientes").Document(clienteId).SetAsync(cliente);

        var session = NewSession(phone, clienteId, WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU);
        session.TemporaryData["vehiculo_modificar_id"] = vehiculo.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);

        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "modificar_color");

        var estadoDespues = (await GetSession(db, phone)).CurrentState;
        Assert.Equal(WhatsAppFlowStates.MODIFICAR_VEHICULO_COLOR, estadoDespues);
        Assert.Contains("color", outgoing.LastBody, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // Infraestructura de tests
    // =========================================================================

    private static FirestoreDb NewDatabase() => new FirestoreDbBuilder
    {
        ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static Vehiculo NewVehiculo(string id, string clienteId, string hash) => new()
    {
        Id = id,
        Patente = "ABC" + id.Replace("-", "").ToUpperInvariant()[..3],
        Marca = "Toyota",
        Modelo = "Corolla",
        Color = "Blanco",
        TipoVehiculo = "Automóvil",
        ClienteId = clienteId,
        ClientesIds = new List<string> { clienteId },
        ClaveAsociacionHash = hash,
        Estado = "Activo"
    };

    private static Cliente NewCliente(string id, string telefono) => new()
    {
        Id = id,
        TipoDocumento = "DNI",
        NumeroDocumento = "12345678",
        Nombre = "Test",
        Apellido = "Owner",
        Telefono = telefono,
        Email = "test@example.invalid",
        Estado = "Activo",
        VehiculosIds = new List<string>()
    };

    private static WhatsAppSession NewSession(string phone, string clienteId, string estado) => new()
    {
        Id = phone,
        ClienteId = clienteId,
        CurrentState = estado,
        CreatedAt = DateTime.UtcNow,
        LastInteraction = DateTime.UtcNow,
        TemporaryData = new Dictionary<string, string>()
    };

    private static async Task<WhatsAppSession> GetSession(FirestoreDb db, string phone) =>
        (await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync()).ConvertTo<WhatsAppSession>();

    private static (WhatsAppFlowService flow, CapturingHandler outgoing) CreateFlow(FirestoreDb db)
    {
        var outgoing = new CapturingHandler();
        var whatsapp = new MetaWhatsAppService(
            new HttpClient(outgoing),
            Options.Create(new WhatsAppSettings { AccessToken = "test", PhoneNumberId = "test", VerifyToken = "test" }),
            NullLogger<MetaWhatsAppService>.Instance);

        var catalogue = new Mock<ICarQueryService>();
        catalogue.Setup(c => c.GetMarcasPorTipoAsync("Automóvil"))
            .ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        catalogue.Setup(c => c.GetModelosAsync("1", null))
            .ReturnsAsync(new List<ModeloSimpleDto> { new("Corolla", "1") });
        catalogue.Setup(c => c.GetColoresComunes())
            .ReturnsAsync(new List<string> { "Blanco", "Negro" });

        var flow = new WhatsAppFlowService(
            new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance),
            whatsapp,
            new ClienteService(db),
            new VehiculoService(db),
            new TipoDocumentoService(db),
            new TipoVehiculoService(db),
            null!,
            catalogue.Object,
            NullLogger<WhatsAppFlowService>.Instance);

        return (flow, outgoing);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string LastBody { get; private set; } = "";
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Bodies.Add(LastBody);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("{}")
            };
        }
    }
}
