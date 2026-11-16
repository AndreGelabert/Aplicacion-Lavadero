using System.Text.Json;
using Firebase.Models;

namespace Firebase.Tests.Services;

public sealed class RecetaUnidadTests
{
    [Fact]
    public void OpcionDeInsumoNoExponeStockNiMinimos()
    {
        var opcion = new ServicioController.InsumoRecetaOpcion("i1", "Jabon", "ml");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(opcion));
        Assert.Equal("i1", json.RootElement.GetProperty("Id").GetString());
        Assert.Equal("Jabon", json.RootElement.GetProperty("Nombre").GetString());
        Assert.Equal("ml", json.RootElement.GetProperty("UnidadMedida").GetString());
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void RecetaPorTipoConservaElSnapshotYLaUnidadSeleccionadaEnJson()
    {
        const string json = "{\"Modalidad\":\"Tipo\",\"TipoId\":\"tipo1\",\"NombreTipo\":\"Jabón\",\"Categoria\":\"Volumen\",\"UnidadMedida\":\"ml\",\"DosisTeorica\":250}";
        var receta = JsonSerializer.Deserialize<RecetaInsumo>(json)!;
        using var snapshot = JsonDocument.Parse(JsonSerializer.Serialize(receta));
        Assert.Equal("Tipo", snapshot.RootElement.GetProperty("Modalidad").GetString());
        Assert.Equal("tipo1", snapshot.RootElement.GetProperty("TipoId").GetString());
        Assert.Equal("Jabón", snapshot.RootElement.GetProperty("NombreTipo").GetString());
        Assert.Equal("Volumen", snapshot.RootElement.GetProperty("Categoria").GetString());
        Assert.Equal("ml", snapshot.RootElement.GetProperty("UnidadMedida").GetString());
        Assert.Equal(250m, snapshot.RootElement.GetProperty("DosisTeorica").GetDecimal());
    }
}
