using System.Security.Claims;
using System.Text.Json;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class VehicleOwnershipRegressionTests
{
    private static Cliente MakeClient(string id, string document) => new() { Id = id, TipoDocumento = "DNI", NumeroDocumento = document, Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "test@example.invalid", Estado = "Activo" };
    private static ClienteController Controller(FirestoreDb db, DefaultHttpContext context) => new(new ClienteService(db), new TipoDocumentoService(db), new VehiculoService(db), new AuditService(db), NullLogger<ClienteController>.Instance, new ClienteLifecycleService(new ClienteService(db), new VehiculoService(db), new AuditService(db)), new ClienteVehicleService(new ClienteService(db), new VehiculoService(db), new AuditService(db))) { ControllerContext = new ControllerContext { HttpContext = context } };
    private static DefaultHttpContext Context() => new() { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permiso", "Clientes.Crear"), new Claim("permiso", "Clientes.Modificar"), new Claim("permiso", "Vehiculos.Crear"), new Claim(ClaimTypes.NameIdentifier, "test") }, "test")) };
    private static FirestoreDb Database() => new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

    [LocalFirestoreFact]
    public async Task ConcurrentOrphanRegistrationsCannotReplaceTheWinningOwnerOrKey()
    {
        var db = Database();
        await db.Collection("vehiculos").Document("orphan").SetAsync(new Vehiculo { Id = "orphan", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Inactivo" });
        async Task<(bool Won, Cliente Owner, Vehiculo Input)> Claim(string number, string color, string key)
        {
            var owner = MakeClient("", number);
            var input = new Vehiculo { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = color, TipoVehiculo = "Auto", ClaveAsociacionHash = VehiculoService.HashClaveAsociacion(key) };
            var won = await new ClienteService(db).GuardarClienteConVehiculos(owner, true,
                new() { new() { Nuevo = input, HuerfanoEsperadoId = "orphan" } }, new(), new(), permitirReclamarHuerfano: true);
            return (won, owner, input);
        }
        var results = await Task.WhenAll(Claim("11111111", "Rojo", "ABCD-2345"), Claim("22222222", "Azul", "WXYZ-6789"));
        var winner = Assert.Single(results, x => x.Won);
        Assert.Single(results, x => !x.Won);
        var stored = (await new VehiculoService(db).ObtenerVehiculo("orphan"))!;
        Assert.Equal(winner.Owner.Id, stored.ClienteId);
        Assert.Equal(new[] { winner.Owner.Id }, stored.ClientesIds);
        Assert.Equal(winner.Input.ClaveAsociacionHash, stored.ClaveAsociacionHash);
        Assert.Equal(winner.Input.Color, stored.Color);
        Assert.Equal("orphan", winner.Input.Id);
        Assert.Equal("Activo", stored.Estado);
        Assert.Single((await db.Collection("clientes").GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ExplicitOrphanClaimCannotSucceedWithoutIssuingANewKey()
    {
        var db = Database();
        var client = MakeClient("owner", "11111111");
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var orphan = new Vehiculo { Id = "orphan", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Inactivo" };
        await db.Collection("vehiculos").Document(orphan.Id).SetAsync(orphan);
        Assert.False(await new VehiculoService(db).ReclamarVehiculoSinDueno(orphan.Id, client));
        Assert.False(await new ClienteService(db).CrearClienteYVincular(MakeClient("", "22222222"), orphan.Id, "", true));
        Assert.Equal("Inactivo", (await new VehiculoService(db).ObtenerVehiculo(orphan.Id))!.Estado);
        Assert.Single((await db.Collection("clientes").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task MissingExpectedOrphanCannotBecomeANewRegistration()
    {
        var db = Database();
        var input = new Vehiculo { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Rojo", TipoVehiculo = "Auto", ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("ABCD-2345") };
        var client = MakeClient("", "11111111");
        var audits = new List<ClienteService.AuditoriaVehiculo>();
        var clientAudits = new List<ClienteService.AuditoriaCliente>();
        Assert.False(await new ClienteService(db).GuardarClienteConVehiculos(client, true,
            new() { new() { Nuevo = input, HuerfanoEsperadoId = "disappeared" } }, audits, clientAudits, permitirReclamarHuerfano: true));
        Assert.Empty((await db.Collection("clientes").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("vehiclePlateGuards").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("vehicleCatalogueProposals").GetSnapshotAsync()).Documents);
        Assert.Empty(audits); Assert.Empty(clientAudits);
    }

    [LocalFirestoreFact]
    public async Task OrphanRegistrationReturnsThePersistedIdAndFreshKey()
    {
        var db = Database();
        var orphan = new Vehiculo { Id = "orphan", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Inactivo" };
        await db.Collection("vehiculos").Document(orphan.Id).SetAsync(orphan);
        var client = MakeClient("", "11111111");
        var input = new Vehiculo { Id = "", Patente = orphan.Patente, Marca = "Ford", Modelo = "Ka", Color = "Rojo", TipoVehiculo = "Auto", ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("ABCD-2345") };
        Assert.True(await new ClienteService(db).GuardarClienteConVehiculos(client, true,
            new() { new() { Nuevo = input } }, new(), new(), permitirReclamarHuerfano: true));
        Assert.Equal(orphan.Id, input.Id);
        var actual = (await new VehiculoService(db).ObtenerVehiculo(input.Id))!;
        Assert.Equal(client.Id, actual.ClienteId);
        Assert.Equal("Rojo", actual.Color); Assert.Equal("Ford", actual.Marca); Assert.Equal("Ka", actual.Modelo);
        Assert.Equal(input.ClaveAsociacionHash, actual.ClaveAsociacionHash);
        Assert.Equal(new[] { orphan.Id }, (await new ClienteService(db).ObtenerCliente(client.Id))!.VehiculosIds);
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task VehicleServiceRejectsExistingPlateWithoutCreatingAnotherVehicle()
    {
        var db = Database();
        var service = new VehiculoService(db);
        var existing = new Vehiculo { Id = "legacy", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo" };
        await db.Collection("vehiculos").Document(existing.Id).SetAsync(existing);
        var duplicate = new Vehiculo { Id = "", Patente = existing.Patente, Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo" };
        await Assert.ThrowsAsync<VehiclePlateAlreadyExistsException>(() => service.CrearVehiculo(duplicate));
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("vehicleCatalogueProposals").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ConcurrentWebAndServiceCreationHasOneWinnerAndDeletedPlateCanBeReused()
    {
        var db = Database();
        var service = new VehiculoService(db);
        Vehiculo Car() => new() { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo" };
        var client = MakeClient("", "11111111");
        var candidate = Car();
        var audits = new List<ClienteService.AuditoriaVehiculo>();
        var clientAudits = new List<ClienteService.AuditoriaCliente>();
        var web = new ClienteService(db).GuardarClienteConVehiculos(client, true,
            new() { new() { Nuevo = candidate } }, audits, clientAudits);
        async Task<bool> ServiceCreate()
        {
            try { await service.CrearVehiculo(Car()); return true; }
            catch (VehiclePlateAlreadyExistsException) { return false; }
        }
        var other = ServiceCreate();
        await Task.WhenAll(web, other);
        Assert.True(web.Result ^ other.Result);
        var only = Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        Assert.Equal(web.Result ? 1 : 0, (await db.Collection("clientes").GetSnapshotAsync()).Count);
        Assert.Equal(web.Result ? 1 : 0, audits.Count);
        Assert.Equal(web.Result ? 1 : 0, clientAudits.Count);
        Assert.Empty((await db.Collection("vehicleCatalogueProposals").GetSnapshotAsync()).Documents);
        await service.EliminarVehiculo(only.Id);
        await service.CrearVehiculo(Car());
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task ScopedAppendPreservesCurrentClientAndBothInverseRelationshipKinds()
    {
        var db = Database();
        var service = new ClienteService(db);
        var client = MakeClient("owner", "11111111");
        var stale = MakeClient(client.Id, client.NumeroDocumento);
        client.Nombre = "Nombre actual"; client.Email = "current@example.invalid"; client.Estado = "Inactivo";
        client.VehiculosIds = new();
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        Vehiculo Car(string id, string plate, string titular, List<string> associated) => new()
        {
            Id = id, Patente = plate, Marca = "Toyota", Modelo = "Corolla", Color = "Blanco",
            TipoVehiculo = "Auto", Estado = "Activo", ClienteId = titular, ClientesIds = associated,
            ClaveAsociacionHash = "unchanged"
        };
        var primary = Car("primary", "AB123CD", client.Id, new());
        var secondary = Car("secondary", "AB124CD", "someone", new() { client.Id });
        var unrelated = Car("unrelated", "AB125CD", "someone", new() { "someone" });
        foreach (var car in new[] { primary, secondary, unrelated })
            await db.Collection("vehiculos").Document(car.Id).SetAsync(car);
        var newCar = Car("", "AB126CD", client.Id, new() { client.Id });
        Assert.True(await service.GuardarClienteConVehiculos(stale, false,
            new() { new() { Nuevo = newCar } }, new(), new(), conservarExistentes: true));
        var stored = (await service.ObtenerCliente(client.Id))!;
        Assert.Equal(client.Nombre, stored.Nombre); Assert.Equal(client.Email, stored.Email);
        Assert.Equal(client.Telefono, stored.Telefono); Assert.Equal(client.Estado, stored.Estado);
        Assert.Equal(new[] { primary.Id, secondary.Id, newCar.Id }.OrderBy(x => x), stored.VehiculosIds.OrderBy(x => x));
        foreach (var car in new[] { primary, secondary, unrelated })
        {
            var actual = (await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>();
            Assert.Equal(car.ClienteId, actual.ClienteId);
            var expectedIds = car.Id == primary.Id ? new[] { client.Id } : car.ClientesIds.ToArray();
            Assert.Equal(expectedIds, actual.ClientesIds);
            Assert.Equal(car.ClaveAsociacionHash, actual.ClaveAsociacionHash);
            Assert.Equal(car.Estado, actual.Estado);
        }
    }

    [LocalFirestoreFact]
    public async Task CurrentTitularNamesHandleRepeatedAndMissingClientsWithoutPersistingDisplayChanges()
    {
        var db = Database();
        var client = MakeClient("owner", "11111111"); client.Nombre = "Actual";
        await db.Collection("clientes").Document(client.Id).SetAsync(client);
        var vehicles = new VehiculoService(db);
        foreach (var id in new[] { "one", "two", "missing" })
            await db.Collection("vehiculos").Document(id).SetAsync(new Vehiculo
            {
                Id = id, Patente = id, Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto",
                Estado = "Activo", ClienteId = id == "missing" ? "gone" : client.Id, ClienteNombreCompleto = "Viejo"
            });
        foreach (var id in new[] { "one", "two", "missing" })
            await db.Collection("vehiculos").Document(id).UpdateAsync("ClienteNombreCompleto", "Viejo");
        var listed = await vehicles.ObtenerVehiculos("", new(), new(), new(), 1, 20, "Patente", "asc");
        Assert.Equal(client.NombreCompleto, (await vehicles.ObtenerVehiculo("one"))!.ClienteNombreCompleto);
        Assert.Equal(client.NombreCompleto, (await vehicles.ObtenerVehiculoPorPatente("one"))!.ClienteNombreCompleto);
        Assert.Equal(2, (await vehicles.ObtenerVehiculosPorCliente(client.Id)).Count(v => v.ClienteNombreCompleto == client.NombreCompleto));
        Assert.Equal(new[] { "missing", "one", "two" }, listed.Select(v => v.Patente));
        Assert.Null(listed[0].ClienteNombreCompleto);
        Assert.All(listed.Skip(1), v => Assert.Equal(client.NombreCompleto, v.ClienteNombreCompleto));
        foreach (var car in listed)
            Assert.Equal("Viejo", (await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).GetValue<string>("ClienteNombreCompleto"));
        Assert.Equal(2, (await vehicles.ObtenerVehiculos("Actual", new(), new(), new(), 1, 20, "Patente", "asc")).Count);
    }

    [LocalFirestoreFact]
    public async Task PublicCreateAssociatesOwnedVehicleOnlyWithValidKey()
    {
        var db = Database();
        var owner = MakeClient("owner", "11111111"); owner.VehiculosIds = new() { "car" };
        var car = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo", ClienteId = owner.Id, ClientesIds = new() { owner.Id }, ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("ABCD-2345") };
        await db.Collection("clientes").Document(owner.Id).SetAsync(owner);
        await db.Collection("vehiculos").Document(car.Id).SetAsync(car);
        var context = Context(); var controller = Controller(db, context);
        var candidate = MakeClient("", "22222222");
        await controller.CrearClienteAjax(candidate, JsonSerializer.Serialize(new[] { new { id = car.Id, patente = car.Patente, esAsociacion = true, claveAsociacion = "WRNG-2345" } }));
        Assert.Equal("false", context.Response.Headers["X-Form-Valid"]);
        Assert.Single((await db.Collection("clientes").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        var unchangedOwner = (await db.Collection("clientes").Document(owner.Id).GetSnapshotAsync()).ConvertTo<Cliente>();
        Assert.Equal(owner.VehiculosIds, unchangedOwner.VehiculosIds);
        var unchanged = (await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>();
        Assert.Equal(owner.Id, unchanged.ClienteId); Assert.Equal(car.ClientesIds, unchanged.ClientesIds);
        Assert.Equal(car.ClaveAsociacionHash, unchanged.ClaveAsociacionHash); Assert.Equal("Activo", unchanged.Estado);
        context = Context(); controller = Controller(db, context); candidate = MakeClient("", "22222222");
        await controller.CrearClienteAjax(candidate, JsonSerializer.Serialize(new[] { new { id = car.Id, patente = car.Patente, esAsociacion = true, claveAsociacion = "ABCD-2345" } }));
        Assert.Equal("true", context.Response.Headers["X-Form-Valid"]);
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        var linked = (await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>();
        Assert.Equal(owner.Id, linked.ClienteId); Assert.Contains(owner.Id, linked.ClientesIds); Assert.Contains(candidate.Id, linked.ClientesIds);
        Assert.Equal(car.ClaveAsociacionHash, linked.ClaveAsociacionHash); Assert.Equal("Activo", linked.Estado);
        Assert.Equal(new[] { car.Id }, (await db.Collection("clientes").Document(candidate.Id).GetSnapshotAsync()).ConvertTo<Cliente>().VehiculosIds);
        Assert.Equal(new[] { car.Id }, (await db.Collection("clientes").Document(owner.Id).GetSnapshotAsync()).ConvertTo<Cliente>().VehiculosIds);
    }

    [LocalFirestoreFact]
    public async Task PublicCreateReassignsInactiveOrphanWithoutKey()
    {
        var db = Database(); var context = Context(); var client = MakeClient("", "22222222");
        var car = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Inactivo", ClienteId = "", ClientesIds = new() };
        await db.Collection("vehiculos").Document(car.Id).SetAsync(car);
        await Controller(db, context).CrearClienteAjax(client, JsonSerializer.Serialize(new[] { new { id = car.Id, patente = car.Patente, esAsociacion = true, claveAsociacion = "" } }));
        Assert.Equal("true", context.Response.Headers["X-Form-Valid"]);
        Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
        var result = (await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>();
        Assert.Equal("Activo", result.Estado); Assert.Equal(client.Id, result.ClienteId); Assert.Equal(new[] { client.Id }, result.ClientesIds);
        Assert.False(string.IsNullOrEmpty(result.ClaveAsociacionHash));
        Assert.Equal("no-cache, no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
        Assert.Equal(new[] { car.Id }, (await db.Collection("clientes").Document(client.Id).GetSnapshotAsync()).ConvertTo<Cliente>().VehiculosIds);
    }

    [LocalFirestoreFact]
    public async Task BlankKeyRejectsNonOrphansAndTransactionRevalidates()
    {
        foreach (var variant in new[] { "active", "owner", "secondary", "hash" })
        {
            var db = Database();
            var car = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Inactivo" };
            await db.Collection("vehiculos").Document(car.Id).SetAsync(car);
            var vehicles = new VehiculoService(db);
            Assert.NotNull(await vehicles.ValidarClaveYObtenerVehiculo(car.Patente, ""));
            if (variant == "active") car.Estado = "Activo";
            if (variant == "owner") car.ClienteId = "owner";
            if (variant == "secondary") car.ClientesIds.Add("owner");
            if (variant == "hash") car.ClaveAsociacionHash = "stored";
            await db.Collection("vehiculos").Document(car.Id).SetAsync(car);
            Assert.Null(await vehicles.ValidarClaveYObtenerVehiculo(car.Patente, ""));
            var audits = new List<ClienteService.AuditoriaVehiculo>();
            Assert.False(await new ClienteService(db).GuardarClienteConVehiculos(MakeClient("", "22222222"), true,
                new() { new() { Patente = car.Patente, Asociacion = true, NuevaClaveHash = "new" } }, audits, new()));
            Assert.Empty(audits);
            Assert.Empty((await db.Collection("clientes").GetSnapshotAsync()).Documents);
            var stored = (await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>();
            Assert.Equal(car.Estado, stored.Estado); Assert.Equal(car.ClienteId, stored.ClienteId);
            Assert.Equal(car.ClientesIds, stored.ClientesIds); Assert.Equal(car.ClaveAsociacionHash, stored.ClaveAsociacionHash);
        }
    }

    [LocalFirestoreFact]
    public async Task WhatsAppOptInReusesOrphanAndRejectsChangedEligibility()
    {
        foreach (var changed in new[] { false, true })
        {
            var db = Database();
            var car = new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Old", Modelo = "Old", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Inactivo" };
            await db.Collection("vehiculos").Document(car.Id).SetAsync(car);
            Assert.NotNull(await new VehiculoService(db).ValidarClaveYObtenerVehiculo(car.Patente, ""));
            if (changed) await db.Collection("vehiculos").Document(car.Id).UpdateAsync("ClaveAsociacionHash", "other");
            var candidate = new Vehiculo { Id = "", Patente = car.Patente, Marca = "New", Modelo = "New", Color = "Rojo", TipoVehiculo = "SUV", ClaveAsociacionHash = "fresh" };
            var client = MakeClient("", "22222222");
            var saved = await new ClienteService(db).GuardarClienteConVehiculos(client, true,
                new() { new() { Nuevo = candidate } }, new(), new(), permitirReclamarHuerfano: true);
            Assert.Equal(!changed, saved);
            Assert.Single((await db.Collection("vehiculos").GetSnapshotAsync()).Documents);
            var stored = (await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>();
            Assert.Equal(changed ? "other" : "fresh", stored.ClaveAsociacionHash);
            Assert.Equal(changed ? "Old" : "New", stored.Marca);
            Assert.Equal(changed ? "Blanco" : "Rojo", stored.Color);
            Assert.Equal(changed ? "" : client.Id, stored.ClienteId);
            Assert.Equal(changed ? 0 : 1, (await db.Collection("clientes").GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task NewVehicleSerializationOmitsDisplayName()
    {
        var db = Database();
        var car = new Vehiculo { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", ClienteNombreCompleto = "display" };
        await new VehiculoService(db).CrearVehiculo(car);
        Assert.False((await db.Collection("vehiculos").Document(car.Id).GetSnapshotAsync()).ContainsField("ClienteNombreCompleto"));
    }

    [LocalFirestoreFact]
    public async Task PublicEditRejectsSharedTitularRemovalWithoutAnyWrites()
    {
        var db = Database(); var context = Context(); var owner = MakeClient("owner", "11111111"); var other = MakeClient("other", "22222222");
        owner.VehiculosIds = new() { "shared", "kept" }; other.VehiculosIds = new() { "shared" };
        var shared = new Vehiculo { Id = "shared", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo", ClienteId = owner.Id, ClientesIds = new() { owner.Id, other.Id }, ClaveAsociacionHash = "key" };
        var kept = new Vehiculo { Id = "kept", Patente = "AB124CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo", ClienteId = owner.Id, ClientesIds = new() { owner.Id } };
        await db.Collection("clientes").Document(owner.Id).SetAsync(owner); await db.Collection("clientes").Document(other.Id).SetAsync(other);
        await db.Collection("vehiculos").Document(shared.Id).SetAsync(shared); await db.Collection("vehiculos").Document(kept.Id).SetAsync(kept);
        var edited = MakeClient(owner.Id, owner.NumeroDocumento); edited.Nombre = "Cambiado";
        await Controller(db, context).ActualizarClienteAjax(edited, JsonSerializer.Serialize(new[] { new { id = kept.Id, patente = kept.Patente, color = kept.Color, tipoVehiculo = kept.TipoVehiculo } }));
        Assert.Equal("false", context.Response.Headers["X-Form-Valid"]);
        var storedOwner = (await db.Collection("clientes").Document(owner.Id).GetSnapshotAsync()).ConvertTo<Cliente>();
        Assert.Equal(owner.Nombre, storedOwner.Nombre); Assert.Equal(owner.VehiculosIds, storedOwner.VehiculosIds);
        Assert.Equal(other.VehiculosIds, (await db.Collection("clientes").Document(other.Id).GetSnapshotAsync()).ConvertTo<Cliente>().VehiculosIds);
        var result = (await db.Collection("vehiculos").Document(shared.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>();
        Assert.Equal(shared.ClienteId, result.ClienteId); Assert.Equal(shared.ClientesIds, result.ClientesIds); Assert.Equal(shared.ClaveAsociacionHash, result.ClaveAsociacionHash); Assert.Equal(shared.Estado, result.Estado);
        Assert.Equal(kept.ClientesIds, (await db.Collection("vehiculos").Document(kept.Id).GetSnapshotAsync()).ConvertTo<Vehiculo>().ClientesIds);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task VehiclePublicPartialsUseCurrentTitularAndAssociatedPeopleWithPermission()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clients = new ClienteService(db);
        var vehicles = new VehiculoService(db);
        var owner = new Cliente { Id = "owner", Nombre = "Nombre actualizado", Apellido = "Perez", TipoDocumento = "DNI", NumeroDocumento = "12345678", Telefono = "1234567890", Email = "test@example.invalid", Estado = "Activo" };
        var other = new Cliente { Id = "other", Nombre = "Juan", Apellido = "Gomez", TipoDocumento = "DNI", NumeroDocumento = "87654321", Telefono = "1234567890", Email = "test@example.invalid", Estado = "Activo" };
        await db.Collection("clientes").Document(owner.Id).SetAsync(owner);
        await db.Collection("clientes").Document(other.Id).SetAsync(other);
        await db.Collection("vehiculos").Document("car").SetAsync(new Vehiculo { Id = "car", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Auto", Estado = "Activo", ClienteId = owner.Id, ClienteNombreCompleto = "Nombre viejo", ClientesIds = new() { owner.Id, other.Id } });
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permiso", "Vehiculos.Consultar"), new Claim("permiso", "Clientes.Consultar") }, "test")) };
        var controller = new VehiculoController(vehicles, new TipoVehiculoService(db), clients, new AuditService(db), null!, NullLogger<VehiculoController>.Instance) { ControllerContext = new ControllerContext { HttpContext = context } };
        var table = Assert.IsType<PartialViewResult>(await controller.SearchPartial(null!, new(), new(), new(), new()));
        Assert.Equal(owner.NombreCompleto, Assert.Single(Assert.IsType<List<Vehiculo>>(table.Model)).ClienteNombreCompleto);
        var partial = Assert.IsType<PartialViewResult>(await controller.AsociadosPartial("car"));
        Assert.Equal("_Asociados", partial.ViewName);
        Assert.Equal("AB123CD", controller.ViewBag.Patente);
        Assert.Equal(owner.Id, controller.ViewBag.TitularId);
        var associated = Assert.IsType<List<Cliente>>(partial.Model);
        Assert.Equal(new[] { owner.NombreCompleto, other.NombreCompleto }.OrderBy(x => x), associated.Select(x => x.NombreCompleto).OrderBy(x => x));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permiso", "Vehiculos.Consultar") }, "test"));
        Assert.IsType<ForbidResult>(await controller.AsociadosPartial("car"));
    }

    [LocalFirestoreFact]
    public async Task EditingClientWithNoVehiclesIsRejectedWithoutUnlinking()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clients = new ClienteService(db);
        var vehicles = new VehiculoService(db);
        var client = new Cliente { Id = "", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "test@example.invalid" };
        await clients.CrearCliente(client);
        var vehicle = new Vehiculo { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", ClienteId = client.Id, ClientesIds = new() { client.Id }, Estado = "Activo", ClaveAsociacionHash = "old" };
        await vehicles.CrearVehiculo(vehicle);
        client.VehiculosIds.Add(vehicle.Id);
        await clients.ActualizarCliente(client);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permiso", "Clientes.Modificar"), new Claim("permiso", "Vehiculos.Crear"), new Claim(ClaimTypes.NameIdentifier, "test") }, "test")) };
        var controller = new ClienteController(clients, new TipoDocumentoService(db), vehicles, new AuditService(db), NullLogger<ClienteController>.Instance, new ClienteLifecycleService(clients, vehicles, new AuditService(db)), new ClienteVehicleService(clients, vehicles, new AuditService(db))) { ControllerContext = new ControllerContext { HttpContext = context } };
        await controller.ActualizarClienteAjax(client, "[]");
        Assert.Equal("false", context.Response.Headers["X-Form-Valid"]);
        Assert.Equal(new[] { vehicle.Id }, (await clients.ObtenerCliente(client.Id))!.VehiculosIds);
        var result = await vehicles.ObtenerVehiculo(vehicle.Id);
        Assert.Equal("Activo", result!.Estado);
        Assert.Equal(new[] { client.Id }, result.ClientesIds);
        Assert.Equal("old", result.ClaveAsociacionHash);
    }

    [LocalFirestoreFact]
    public async Task ClientCreationRejectsAssociationToInactiveOwnedVehicleWithoutWrites()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clients = new ClienteService(db);
        var vehicles = new VehiculoService(db);
        var vehicle = new Vehiculo { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", ClienteId = "owner", ClientesIds = new() { "owner" }, Estado = "Inactivo", ClaveAsociacionHash = VehiculoService.HashClaveAsociacion("ABCD-2345") };
        await vehicles.CrearVehiculo(vehicle);
        var client = new Cliente { Id = "", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "test@example.invalid" };
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permiso", "Clientes.Crear"), new Claim("permiso", "Vehiculos.Crear"), new Claim(ClaimTypes.NameIdentifier, "test") }, "test")) };
        var controller = new ClienteController(clients, new TipoDocumentoService(db), vehicles, new AuditService(db), NullLogger<ClienteController>.Instance, new ClienteLifecycleService(clients, vehicles, new AuditService(db)), new ClienteVehicleService(clients, vehicles, new AuditService(db))) { ControllerContext = new ControllerContext { HttpContext = context } };
        await controller.CrearClienteAjax(client, JsonSerializer.Serialize(new[] { new { patente = vehicle.Patente, esAsociacion = true, claveAsociacion = "ABCD-2345" } }));
        Assert.Equal("false", context.Response.Headers["X-Form-Valid"]);
        Assert.Empty((await db.Collection("clientes").GetSnapshotAsync()).Documents);
        var result = await vehicles.ObtenerVehiculo(vehicle.Id);
        Assert.Equal("Inactivo", result!.Estado);
        Assert.Equal(new[] { "owner" }, result.ClientesIds);
        Assert.Equal(vehicle.ClaveAsociacionHash, result.ClaveAsociacionHash);
    }

    [LocalFirestoreFact]
    public async Task EditingStaleVehicleCannotRestoreRemovedOwnerOrKey()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clients = new ClienteService(db);
        var vehicles = new VehiculoService(db);
        var client = new Cliente { Id = "", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "test@example.invalid" };
        await clients.CrearCliente(client);
        var vehicle = new Vehiculo { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", ClienteId = client.Id, ClientesIds = new() { client.Id }, Estado = "Activo", ClaveAsociacionHash = "old" };
        await vehicles.CrearVehiculo(vehicle);
        var stale = await vehicles.ObtenerVehiculo(vehicle.Id);
        Assert.True(await vehicles.DesvincularClienteDeVehiculo(vehicle.Id, client.Id));
        stale!.Color = "Rojo";
        await vehicles.ActualizarDatosVehiculo(stale);
        var result = await vehicles.ObtenerVehiculo(vehicle.Id);
        Assert.Equal("Rojo", result!.Color);
        Assert.Equal("Inactivo", result.Estado);
        Assert.True(string.IsNullOrEmpty(result.ClienteId));
        Assert.Empty(result.ClientesIds);
        Assert.Null(result.ClaveAsociacionHash);
    }

    [LocalFirestoreFact]
    public async Task ClientEditUnlinksVehicleEvenWhenClientListIsStale()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-lava-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var clients = new ClienteService(db);
        var vehicles = new VehiculoService(db);
        var client = new Cliente { Id = "", TipoDocumento = "DNI", NumeroDocumento = "12345678", Nombre = "Ana", Apellido = "Perez", Telefono = "1234567890", Email = "test@example.invalid" };
        await clients.CrearCliente(client);
        var removed = new Vehiculo { Id = "", Patente = "AB123CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", ClienteId = client.Id, ClientesIds = new() { client.Id }, Estado = "Activo", ClaveAsociacionHash = "old" };
        var kept = new Vehiculo { Id = "", Patente = "AB124CD", Marca = "Toyota", Modelo = "Corolla", Color = "Blanco", TipoVehiculo = "Automóvil", ClienteId = client.Id, ClientesIds = new() { client.Id }, Estado = "Activo" };
        await vehicles.CrearVehiculo(removed);
        await vehicles.CrearVehiculo(kept);
        client.VehiculosIds = new() { kept.Id };
        await clients.ActualizarCliente(client);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permiso", "Clientes.Modificar"), new Claim("permiso", "Vehiculos.Crear"), new Claim(ClaimTypes.NameIdentifier, "test") }, "test")) };
        var controller = new ClienteController(clients, new TipoDocumentoService(db), vehicles, new AuditService(db), NullLogger<ClienteController>.Instance, new ClienteLifecycleService(clients, vehicles, new AuditService(db)), new ClienteVehicleService(clients, vehicles, new AuditService(db))) { ControllerContext = new ControllerContext { HttpContext = context } };
        await controller.ActualizarClienteAjax(client, JsonSerializer.Serialize(new[] { new { id = kept.Id, color = kept.Color, tipoVehiculo = kept.TipoVehiculo } }));
        Assert.Equal("true", context.Response.Headers["X-Form-Valid"]);
        var result = await vehicles.ObtenerVehiculo(removed.Id);
        Assert.Equal("Inactivo", result!.Estado);
        Assert.Empty(result.ClientesIds);
        Assert.True(string.IsNullOrEmpty(result.ClienteId));
        Assert.True(string.IsNullOrEmpty(result.ClaveAsociacionHash));
        Assert.Equal("Activo", (await vehicles.ObtenerVehiculo(kept.Id))!.Estado);
    }
}
