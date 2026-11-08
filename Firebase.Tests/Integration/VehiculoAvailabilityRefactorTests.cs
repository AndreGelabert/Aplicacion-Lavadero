using System.Text.Json;
using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Tests.Integration;

public class VehiculoAvailabilityRefactorTests
{
    [LocalFirestoreFact]
    public async Task AvailabilityPreservesBranchPrecedenceExactErrorsAndStoredOwnership()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-vehavailable-" + Guid.NewGuid().ToString("N")[..12],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();
        var service = new VehiculoService(db);
        var controller = new VehiculoController(service, new TipoVehiculoService(db),
            new ClienteService(db), new AuditService(db), null!, NullLogger<VehiculoController>.Instance);
        async Task Check(string plate, string brand, string model, object expected)
        {
            var result = Assert.IsType<JsonResult>(await controller.VerificarVehiculoSinDueno(plate, brand, model));
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(result.Value));
        }
        await Check("   ", "Ford", "Fiesta", new { existe = false });
        await Check("UNKNOWN", "Ford", "Fiesta", new { existe = false });
        var cases = new[]
        {
            new { Id = "orphan", Plate = "ORP123", State = "Inactivo", Owner = "", Owners = new List<string>(), Hash = (string?)null },
            new { Id = "active", Plate = "ACT123", State = "Activo", Owner = "", Owners = new List<string>(), Hash = (string?)null },
            new { Id = "primary", Plate = "PRI123", State = "Inactivo", Owner = "owner", Owners = new List<string>(), Hash = (string?)"hash" },
            new { Id = "secondary", Plate = "SEC123", State = "Inactivo", Owner = "", Owners = new List<string> { "shared" }, Hash = (string?)"hash" },
            new { Id = "keyed", Plate = "KEY123", State = "Inactivo", Owner = "", Owners = new List<string>(), Hash = (string?)"hash" }
        };
        foreach (var item in cases)
            await db.Collection("vehiculos").Document(item.Id).SetAsync(new Vehiculo
            {
                Id = item.Id, Patente = item.Plate, Marca = "Ford", Modelo = "Fiesta", Color = "Rojo", TipoVehiculo = "Auto",
                Estado = item.State, ClienteId = item.Owner, ClientesIds = item.Owners, ClaveAsociacionHash = item.Hash
            });
        // The early orphan branch wins even when brand/model do not match.
        await Check("orp123", "Toyota", "Hilux", new { existe = false, asociable = true });
        await Check("ORP123", "ford", "fiesta", new { existe = false, asociable = true });
        await Check("ACT123", "Ford", "Fiesta", new { existe = false, asociable = false,
            error = "Ya existe un vehículo activo con la patente ACT123 (Auto - Ford Fiesta). No se pueden registrar patentes duplicadas." });
        foreach (var plate in new[] { "PRI123", "SEC123" })
            await Check(plate, "Ford", "Fiesta", new { existe = false,
                error = $"Ya existe un vehículo con la patente {plate} asignado al cliente . No se pueden registrar patentes duplicadas." });
        await Check("KEY123", "Ford", "Fiesta", new { existe = false,
            error = "Ya existe un vehículo registrado con la patente KEY123, pero es un Auto Ford Fiesta. Está ingresando datos de un Ford Fiesta. Verifique la patente." });
        foreach (var item in cases)
        {
            var stored = await service.ObtenerVehiculo(item.Id);
            Assert.Equal(item.Plate, stored.Patente);
            Assert.Equal(item.State, stored.Estado);
            Assert.Equal(item.Owner, stored.ClienteId);
            Assert.Equal(item.Owners, stored.ClientesIds);
            Assert.Equal(item.Hash, stored.ClaveAsociacionHash);
            Assert.Equal("Ford", stored.Marca);
            Assert.Equal("Fiesta", stored.Modelo);
            Assert.Equal("Auto", stored.TipoVehiculo);
            Assert.Equal("Rojo", stored.Color);
        }
        Assert.Empty(await new AuditService(db).ObtenerRegistros());
    }
}
