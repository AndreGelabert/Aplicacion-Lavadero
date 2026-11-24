using Firebase.Services;

namespace Firebase.Tests.Integration;

public class TurnoEmployeeAssignmentTests
{
    [Fact]
    public void SeleccionaCantidadExactaSinOcupadosNiLimiteGlobal()
    {
        string[] activos = ["a", "b", "c", "d", "e", "f"];
        string[][] ocupados = [["b"], ["e"]];
        var resultado = TurnoEmployeeAssignment.Seleccionar(4, activos, ocupados);
        Assert.Equal(new[] { "a", "c", "d", "f" }, resultado);
        Assert.Equal(new[] { "a", "b", "c", "d", "e", "f" }, activos);
        Assert.Equal(new[] { "b" }, ocupados[0]);
    }

    [Fact]
    public void RechazaCapacidadInsuficienteInclusoConOcupadosInactivos()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            TurnoEmployeeAssignment.Seleccionar(2, ["a", "b"], [["b"], ["otro"]]));
        Assert.Equal("No hay suficientes empleados disponibles. Se requieren 2 pero solo hay 1 disponibles.", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RechazaCantidadNoPositiva(int cantidad) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TurnoEmployeeAssignment.Seleccionar(cantidad, ["a"], []));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a/b")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("__reserved__")]
    [InlineData("____")]
    public void RechazaIdsInvalidosEnAmbasFuentes(string id)
    {
        Assert.Throws<ArgumentException>(() => TurnoEmployeeAssignment.Seleccionar(1, [id], []));
        Assert.Throws<ArgumentException>(() => TurnoEmployeeAssignment.Seleccionar(1, ["a"], [[id]]));
    }

    [Fact]
    public void NormalizaDuplicadosYConservaOrdenSinContarOcupados()
    {
        string[] activos = ["c", "a", "c", "b", "a", "d"];
        string[][] ocupados = [["b", "b"], ["b", "externo", "externo"]];
        Assert.Equal(new[] { "c", "a", "d" }, TurnoEmployeeAssignment.Seleccionar(3, activos, ocupados));
        var error = Assert.Throws<InvalidOperationException>(() => TurnoEmployeeAssignment.Seleccionar(4, activos, ocupados));
        Assert.Equal("No hay suficientes empleados disponibles. Se requieren 4 pero solo hay 3 disponibles.", error.Message);
        Assert.Equal(new[] { "c", "a", "c", "b", "a", "d" }, activos);
        Assert.Equal(new[] { "b", "b" }, ocupados[0]);
    }

    [Fact]
    public void AceptaIdsNoReservadosYLongitudMaxima()
    {
        var id = new string('a', 1500);
        Assert.Equal(new[] { "__prefijo", "sufijo__", id },
            TurnoEmployeeAssignment.Seleccionar(3, ["__prefijo", "sufijo__", id], []));
        Assert.Throws<ArgumentException>(() => TurnoEmployeeAssignment.Seleccionar(1, [id + "a"], []));
        Assert.Throws<ArgumentException>(() => TurnoEmployeeAssignment.Seleccionar(1, ["a"], [[id + "a"]]));
    }
}
