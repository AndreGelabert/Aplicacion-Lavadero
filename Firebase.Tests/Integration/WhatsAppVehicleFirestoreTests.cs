using System.Net;
using Firebase.Models;
using Firebase.Models.Dtos;
using Firebase.Models.WhatsApp;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Firebase.Tests.Integration;

public class WhatsAppVehicleFirestoreTests
{
    [LocalFirestoreFact]
    public async Task ReturningClientConfirmsManualModelAndCreatesPendingProposal()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Juan", Apellido = "Perez", Telefono = "3751123456", Email = "test@example.invalid" };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        session.CurrentState = WhatsAppFlowStates.VEHICULO_MARCA;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "Toytoa");
        Assert.Contains("Toyota", outgoing.LastBody);
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MARCA, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "1");
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MODELO, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "Modelo Nuevo");
        Assert.Contains("usar_modelo_manual", outgoing.LastBody);
        await flow.ProcessMessage(phone, "usar_modelo_manual");
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MODELO_MANUAL, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "manual_si");
        Assert.Equal("true", (await Session(db, phone)).TemporaryData["VehiculoModeloManual"]);
        await flow.ProcessMessage(phone, "Blanco");
        Assert.Equal(WhatsAppFlowStates.VEHICULO_CONFIRMACION, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "SI");

        var vehicles = await db.Collection("vehiculos").GetSnapshotAsync();
        var vehicle = Assert.Single(vehicles.Documents).ConvertTo<Vehiculo>();
        Assert.Equal("Toyota", vehicle.Marca);
        Assert.Equal("Modelo Nuevo", vehicle.Modelo);
        Assert.Equal("Pendiente", (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task InitialRegistrationAlsoCreatesVehicleAndPendingProposal()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var session = NewSession(phone);
        session.CurrentState = WhatsAppFlowStates.VEHICULO_CONFIRMACION;
        session.TemporaryData["RegistroInicial"] = "true";
        session.TemporaryData["TipoDocumento"] = "DNI";
        session.TemporaryData["NumeroDocumento"] = "12345678";
        session.TemporaryData["Nombre"] = "Juan";
        session.TemporaryData["Apellido"] = "Perez";
        session.TemporaryData["Email"] = "test@example.invalid";
        session.TemporaryData["VehiculoMarca"] = "Toyota";
        session.TemporaryData["VehiculoMarcaId"] = "1";
        session.TemporaryData["VehiculoModelo"] = "Modelo Nuevo";
        session.TemporaryData["VehiculoModeloManual"] = "true";
        session.TemporaryData["VehiculoColor"] = "Blanco";
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, _) = CreateFlow(db);

        await flow.ProcessMessage(phone, "SI");

        var vehicle = Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents).ConvertTo<Vehiculo>();
        Assert.Single((await db.Collection("clientes").GetSnapshotAsync()).Documents);
        Assert.Equal("Pendiente", (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ManualBrandRequiresExplicitConfirmationAndIsNotPublished()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Juan", Apellido = "Perez", Telefono = "3751123456", Email = "test@example.invalid" };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "Marca Nueva");
        Assert.Contains("usar_marca_manual", outgoing.LastBody);
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MARCA, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "usar_marca_manual");
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MARCA_MANUAL, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "manual_si");

        var accepted = await Session(db, phone);
        Assert.Equal("Marca Nueva", accepted.TemporaryData["VehiculoMarca"]);
        Assert.Equal("true", accepted.TemporaryData["VehiculoMarcaManual"]);
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MODELO, accepted.CurrentState);
        Assert.Empty((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("vehicleCatalogueProposals").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("approvedVehicleBrands").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConfirmedManualBrandAndModelCreateOnlyPendingProposal()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Juan", Apellido = "Perez", Telefono = "3751123456", Email = "test@example.invalid" };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, _) = CreateFlow(db);

        await flow.ProcessMessage(phone, "Marca Nueva");
        await flow.ProcessMessage(phone, "usar_marca_manual");
        await flow.ProcessMessage(phone, "manual_si");
        await flow.ProcessMessage(phone, "Modelo Nuevo");
        await flow.ProcessMessage(phone, "manual_si");
        await flow.ProcessMessage(phone, "Blanco");
        await flow.ProcessMessage(phone, "SI");

        var vehicle = Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents).ConvertTo<Vehiculo>();
        Assert.Equal("Marca Nueva", vehicle.Marca);
        Assert.Equal("Modelo Nuevo", vehicle.Modelo);
        var proposal = (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).ConvertTo<VehicleCatalogueProposal>();
        Assert.True(proposal.MarcaManual);
        Assert.True(proposal.ModeloManual);
        Assert.Equal("Pendiente", proposal.Estado);
        Assert.Empty((await db.Collection("approvedVehicleBrands").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("approvedVehicleModels").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task RejectingManualBrandAllowsReplacementWithoutPersistingRejectedValue()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Juan", Apellido = "Perez", Telefono = "3751123456", Email = "test@example.invalid" };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, outgoing) = CreateFlow(db);

        await flow.ProcessMessage(phone, "Marca Rechazada");
        await flow.ProcessMessage(phone, "usar_marca_manual");
        await flow.ProcessMessage(phone, "manual_no");
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MARCA, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "Marca Aceptada");
        Assert.Equal("Marca Aceptada", (await Session(db, phone)).TemporaryData["MarcaManualCandidata"]);
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MARCA, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "usar_marca_manual");
        await flow.ProcessMessage(phone, "manual_si");
        await flow.ProcessMessage(phone, "Modelo Aceptado");
        await flow.ProcessMessage(phone, "manual_si");
        await flow.ProcessMessage(phone, "Blanco");
        await flow.ProcessMessage(phone, "SI");

        var vehicle = Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents).ConvertTo<Vehiculo>();
        Assert.Equal("Marca Aceptada", vehicle.Marca);
        Assert.DoesNotContain("Rechazada", vehicle.Marca);
        var proposal = (await db.Collection("vehicleCatalogueProposals").Document(vehicle.Id).GetSnapshotAsync()).ConvertTo<VehicleCatalogueProposal>();
        Assert.Equal("Marca Aceptada", proposal.Marca);
        Assert.DoesNotContain("Rechazada", proposal.Marca);
        Assert.Empty((await db.Collection("approvedVehicleBrands").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task NumericModelExactChoiceDoesNotSelectAnotherNumericModel()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var session = NewSession(phone);
        session.CurrentState = WhatsAppFlowStates.VEHICULO_MODELO;
        session.TemporaryData["VehiculoMarcaId"] = "1";
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, _) = CreateFlow(db, models: new List<ModeloSimpleDto> { new("Model 3", "1"), new("Model 30", "1") });

        await flow.ProcessMessage(phone, "Model 3");

        var updated = await Session(db, phone);
        Assert.Equal("Model 3", updated.TemporaryData["VehiculoModelo"]);
        Assert.Equal(WhatsAppFlowStates.VEHICULO_COLOR, updated.CurrentState);
    }

    [LocalFirestoreFact]
    public async Task NumericNearMatchOffersOnlySameNumberUntilCustomerSelectsIt()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var session = NewSession(phone);
        session.CurrentState = WhatsAppFlowStates.VEHICULO_MODELO;
        session.TemporaryData["VehiculoMarcaId"] = "1";
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, outgoing) = CreateFlow(db, models: new List<ModeloSimpleDto> { new("Model 3", "1"), new("Model 30", "1") });

        await flow.ProcessMessage(phone, "Modle 3");

        var awaitingChoice = await Session(db, phone);
        Assert.Equal(WhatsAppFlowStates.VEHICULO_MODELO, awaitingChoice.CurrentState);
        Assert.False(awaitingChoice.TemporaryData.ContainsKey("VehiculoModelo"));
        Assert.Contains("model_3", outgoing.LastBody);
        Assert.DoesNotContain("model_30", outgoing.LastBody);

        await flow.ProcessMessage(phone, "model_3");

        var selected = await Session(db, phone);
        Assert.Equal("Model 3", selected.TemporaryData["VehiculoModelo"]);
        Assert.Equal(WhatsAppFlowStates.VEHICULO_COLOR, selected.CurrentState);
    }

    [LocalFirestoreFact]
    public async Task ManualConfirmationFromResetSessionCannotCreateVehicle()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var session = NewSession(phone);
        session.CurrentState = WhatsAppFlowStates.VEHICULO_MARCA_MANUAL;
        session.TemporaryData["MarcaManualCandidata"] = "Stale Brand";
        session.TemporaryData["VehiculoModelo"] = "Stale Model";
        session.TemporaryData["VehiculoModeloManual"] = "true";
        session.TemporaryData["VehiculoPatente"] = "AB123CD";
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, _) = CreateFlow(db);

        await new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance)
            .UpdateSessionState(phone, WhatsAppFlowStates.INICIO);
        await flow.ProcessMessage(phone, "manual_si");

        Assert.Empty((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("vehicleCatalogueProposals").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task IncomingMessageBodyIsNotWrittenToLogs()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        const string secretMessage = "private-association-key-73fbc1";
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(NewSession(phone));
        var logger = new RecordingLogger<WhatsAppFlowService>();
        var (flow, _) = CreateFlow(db, logger: logger);

        await flow.ProcessMessage(phone, secretMessage);

        Assert.DoesNotContain(logger.Messages, message => message.Contains(secretMessage, StringComparison.Ordinal));
    }

    [LocalFirestoreFact]
    public async Task UnlinkLastOwnerDeactivatesButSharedVehicleRemainsActive()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Ana", Apellido = "Perez", Telefono = "3751123456", Email = "test@example.invalid", VehiculosIds = new() { "car", "shared" } };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var vehicle = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", Estado = "Activo", ClienteId = client.Id, ClientesIds = new() { client.Id }, ClaveAsociacionHash = "old" };
        var shared = new Vehiculo { Id = "shared", Patente = "AB124CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", Estado = "Activo", ClienteId = "other", ClientesIds = new() { "other", client.Id }, ClaveAsociacionHash = "shared-key" };
        await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        await db.Collection("vehiculos").Document(shared.Id).SetAsync(shared);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        session.CurrentState = WhatsAppFlowStates.CONFIRMAR_ELIMINAR_VEHICULO;
        session.TemporaryData["vehiculo_modificar_id"] = "car";
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, _) = CreateFlow(db);
        await flow.ProcessMessage(phone, "SI");
        var service = new VehiculoService(db);
        var removed = await service.ObtenerVehiculo("car");
        Assert.Equal("Inactivo", removed!.Estado);
        Assert.Empty(removed.ClientesIds);
        Assert.Null(removed.ClaveAsociacionHash);
        var unchanged = await service.ObtenerVehiculo("shared");
        Assert.Equal("Activo", unchanged!.Estado);
        Assert.Equal("shared-key", unchanged.ClaveAsociacionHash);
    }

    [LocalFirestoreFact]
    public async Task OwnerlessVehicleCanBeClaimedWithoutAssociationKey()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "client", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Ana", Apellido = "Perez", Telefono = "3751123456", Email = "test@example.invalid" };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var vehicle = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", Estado = "Inactivo" };
        await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        session.CurrentState = WhatsAppFlowStates.ASOCIAR_VEHICULO_PATENTE;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, _) = CreateFlow(db);
        await flow.ProcessMessage(phone, "AB123CD");
        Assert.Equal(WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION, (await Session(db, phone)).CurrentState);
        await flow.ProcessMessage(phone, "SI");
        var result = await new VehiculoService(db).ObtenerVehiculo(vehicle.Id);
        Assert.Equal("Activo", result!.Estado);
        Assert.Equal(client.Id, result.ClienteId);
        Assert.Contains(client.Id, result.ClientesIds);
    }

    [LocalFirestoreFact]
    public async Task RotatedKeyCannotBeConfirmedAndLeavesOwnershipUnchanged()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "client", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "12345678", Telefono = "1234567890", Email = "test@example.invalid" };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var vehicle = new Vehiculo { Id = "car", Patente = "AB123CD", TipoVehiculo = "Automóvil", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", Estado = "Activo", ClienteId = "owner", ClientesIds = new() { "owner" }, ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("ABCD-2345") };
        await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        session.CurrentState = WhatsAppFlowStates.ASOCIAR_VEHICULO_PATENTE;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, outgoing) = CreateFlow(db);
        await flow.ProcessMessage(phone, "AB123CD");
        await flow.ProcessMessage(phone, "ABCD-2345");
        await db.Collection("vehiculos").Document(vehicle.Id).UpdateAsync("ClaveAsociacionHash", VehiculoService.HashClaveAsociacion("WXYZ-6789"));
        await flow.ProcessMessage(phone, "SI");
        var stored = await new VehiculoService(db).ObtenerVehiculo("car");
        Assert.Equal(new[] { "owner" }, stored!.ClientesIds);
        Assert.Empty((await new ClienteService(db).ObtenerCliente("client"))!.VehiculosIds);
        Assert.Contains("Error", outgoing.LastBody);
    }

    [LocalFirestoreFact]
    public async Task UnlinkPrimaryWithOtherOwnerIsRejectedWithoutChanges()
    {
        var db = NewDatabase();
        var primary = new Cliente { Id = "primary", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "12345678", Telefono = "1234567890", Email = "test@example.invalid", VehiculosIds = new() { "car" } };
        var other = new Cliente { Id = "other", Nombre = "Juan", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "87654321", Telefono = "1234567890", Email = "test@example.invalid", VehiculosIds = new() { "car" } };
        await db.Collection("clientes").Document(primary.Id).SetAsync(primary);
        await db.Collection("clientes").Document(other.Id).SetAsync(other);
        await db.Collection("vehiculos").Document("car").SetAsync(new Vehiculo { Id = "car", Patente = "AB123CD", TipoVehiculo = "Automóvil", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", Estado = "Activo", ClienteId = primary.Id, ClientesIds = new() { primary.Id, other.Id } });
        Assert.False(await new VehiculoService(db).DesvincularClienteDeVehiculo("car", primary.Id));
        var stored = await new VehiculoService(db).ObtenerVehiculo("car");
        Assert.Equal(primary.Id, stored!.ClienteId);
        Assert.Equal("Activo", stored.Estado);
        Assert.Equal(new[] { primary.Id, other.Id }, stored.ClientesIds);
        Assert.Equal(new[] { "car" }, (await new ClienteService(db).ObtenerCliente(primary.Id))!.VehiculosIds);
    }

    [LocalFirestoreFact]
    public async Task DuplicatePlateDuringRegistrationSuggestsAssociationOrOwnerlessClaim()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var vehicle = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", Estado = "Activo", ClienteId = "owner", ClientesIds = new() { "owner" }, ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("ABCD-2345") };
        await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        var session = NewSession(phone);
        session.ClienteId = "client";
        session.CurrentState = WhatsAppFlowStates.VEHICULO_PATENTE;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, outgoing) = CreateFlow(db);
        await flow.ProcessMessage(phone, "AB123CD");
        Assert.Equal(WhatsAppFlowStates.ASOCIAR_VEHICULO_CLAVE, (await Session(db, phone)).CurrentState);
        Assert.Contains("clave", outgoing.LastBody, StringComparison.OrdinalIgnoreCase);
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        vehicle.ClienteId = "";
        vehicle.ClientesIds.Clear();
        vehicle.Estado = "Inactivo";
        vehicle.ClaveAsociacionHash = null;
        await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        session.CurrentState = WhatsAppFlowStates.VEHICULO_PATENTE;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        await flow.ProcessMessage(phone, "AB123CD");
        Assert.Equal(WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION, (await Session(db, phone)).CurrentState);
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task SharedPrimaryWithOnlyVehicleReceivesWarningWithoutConfirmation()
    {
        var db = NewDatabase();
        const string phone = "543751123456";
        var client = new Cliente { Id = "primary", Nombre = "Ana", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "12345678", Telefono = "1234567890", Email = "test@example.invalid", VehiculosIds = new() { "car" } };
        var vehicle = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", Estado = "Activo", ClienteId = client.Id, ClientesIds = new() { client.Id, "other" }, ClaveAsociacionHash = "old" };
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        await db.Collection("vehiculos").Document(vehicle.Id).SetAsync(vehicle);
        var session = NewSession(phone);
        session.ClienteId = client.Id;
        session.CurrentState = WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU;
        session.TemporaryData["vehiculo_modificar_id"] = vehicle.Id;
        await db.Collection("whatsapp_sessions").Document(phone).SetAsync(session);
        var (flow, outgoing) = CreateFlow(db);
        await flow.ProcessMessage(phone, "desvincular_vehiculo");
        Assert.Contains("primero los demás deben desvincularse", System.Text.Json.JsonDocument.Parse(outgoing.LastBody).RootElement.GetProperty("text").GetProperty("body").GetString());
        var after = await Session(db, phone);
        Assert.Equal(WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU, after.CurrentState);
        Assert.Equal(vehicle.Id, after.TemporaryData["vehiculo_modificar_id"]);
        var stored = await new VehiculoService(db).ObtenerVehiculo(vehicle.Id);
        Assert.Equal(client.Id, stored!.ClienteId);
        Assert.Equal(vehicle.ClientesIds, stored.ClientesIds);
        Assert.Equal("Activo", stored.Estado);
        Assert.Equal("old", stored.ClaveAsociacionHash);
        Assert.Equal(new[] { "car" }, (await new ClienteService(db).ObtenerCliente(client.Id))!.VehiculosIds);
    }

    private static FirestoreDb NewDatabase() => new FirestoreDbBuilder
    {
        ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    private static WhatsAppSession NewSession(string phone) => new()
    {
        Id = phone, CurrentState = WhatsAppFlowStates.VEHICULO_MARCA, CreatedAt = DateTime.UtcNow, LastInteraction = DateTime.UtcNow,
        TemporaryData = new Dictionary<string, string> { ["VehiculoTipo"] = "Automóvil", ["VehiculoPatente"] = "AB123CD" }
    };

    private static async Task<WhatsAppSession> Session(FirestoreDb db, string phone) =>
        (await db.Collection("whatsapp_sessions").Document(phone).GetSnapshotAsync()).ConvertTo<WhatsAppSession>();

    private static (WhatsAppFlowService flow, CapturingHandler outgoing) CreateFlow(
        FirestoreDb db, List<ModeloSimpleDto>? models = null, ILogger<WhatsAppFlowService>? logger = null)
    {
        var outgoing = new CapturingHandler();
        var whatsapp = new MetaWhatsAppService(new HttpClient(outgoing), Options.Create(new WhatsAppSettings
        {
            AccessToken = "test", PhoneNumberId = "test", VerifyToken = "test"
        }), NullLogger<MetaWhatsAppService>.Instance);
        var catalogue = new Mock<ICarQueryService>();
        catalogue.Setup(c => c.GetMarcasPorTipoAsync("Automóvil")).ReturnsAsync(new List<MarcaSimpleDto> { new("1", "Toyota") });
        catalogue.Setup(c => c.GetModelosAsync("1", null)).ReturnsAsync(models ?? new List<ModeloSimpleDto> { new("Corolla", "1") });
        catalogue.Setup(c => c.GetColoresComunes()).ReturnsAsync(new List<string> { "Blanco" });
        var flow = new WhatsAppFlowService(new WhatsAppSessionService(db, NullLogger<WhatsAppSessionService>.Instance), whatsapp,
            new ClienteService(db), new VehiculoService(db), new TipoDocumentoService(db), new TipoVehiculoService(db),
            new LavaderoInfoService(new ConfiguracionService(db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())), new TipoServicioService(db), NullLogger<LavaderoInfoService>.Instance), catalogue.Object, logger ?? NullLogger<WhatsAppFlowService>.Instance);
        return (flow, outgoing);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string LastBody { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
