using Firebase.Models;
using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoLavadoSnapshotMapperTests
{
    private static Turno Crear() => new()
    {
        PrecioAcordado = 72m, Descuento = 10m,
        DesglosePrecio = new() { Version = 1, Subtotal = 80m, Total = 72m },
        Vehiculos = [new() { VehiculoId = "v", Servicios = [
            new() { ServicioId = "a", Precio = 50m, TiempoEstimado = 10, PaqueteId = "p" },
            new() { ServicioId = "b", Precio = 40m, TiempoEstimado = 20 }] }],
        RecetaOperativa = new() { Version = 1, Servicios = [
            new() { ServicioId = "a", Nombre = "A", Tipo = "T", Receta = [new() { InsumoId = "ignorado" }], Etapas = [
                new() { Id = "e2", Nombre = "Dos", Receta = [new() { InsumoId = "i2", DosisTeorica = 2, UnidadMedida = "l" }] },
                new() { Id = "e1", Nombre = "Uno", Receta = [new() { Modalidad = "Tipo", TipoId = "t", DosisTeorica = 1, UnidadMedida = "ml" }] }] },
            new() { ServicioId = "b", Nombre = "B", Tipo = "T", Receta = [new() { InsumoId = "i3", DosisTeorica = 3, UnidadMedida = "l" }] }] }
    };

    [Fact]
    public void CopiaServiciosEtapasYPreciosSinMutacion()
    {
        var turno = Crear();
        var resultado = TurnoLavadoSnapshotMapper.Mapear(turno);
        Assert.Equal((72m, 80m), (resultado.Precio, resultado.PrecioOriginal));
        Assert.Equal(new[] { "a", "b" }, resultado.Servicios.Select(s => s.ServicioId));
        Assert.Equal(new[] { 0, 1 }, resultado.Servicios.Select(s => s.Orden));
        Assert.Equal(new[] { 50m, 40m }, resultado.Servicios.Select(s => s.Precio));
        Assert.Equal("p", resultado.Servicios[0].PaqueteId);
        Assert.Empty(resultado.Servicios[0].Receta);
        Assert.Equal(new[] { "e2", "e1" }, resultado.Servicios[0].Etapas.Select(e => e.EtapaId));
        Assert.Equal("t", resultado.Servicios[0].Etapas[1].Receta[0].TipoId);
        Assert.Equal("i3", resultado.Servicios[1].Receta[0].InsumoId);
        resultado.Servicios[0].Etapas[0].Receta[0].InsumoId = "cambio";
        Assert.Equal("i2", turno.RecetaOperativa!.Servicios[0].Etapas[0].Receta[0].InsumoId);
        turno.RecetaOperativa.Servicios[1].Receta[0].InsumoId = "cambio";
        Assert.Equal("i3", resultado.Servicios[1].Receta[0].InsumoId);
    }

    [Fact]
    public void PreciosRequierenDesgloseCompletoVersionadoYConsistente()
    {
        var turno = Crear();
        Assert.Equal((72m, 80m), TurnoLavadoSnapshotMapper.ObtenerPreciosLavado(turno));
        turno.DesglosePrecio = null;
        Assert.Throws<InvalidOperationException>(() => TurnoLavadoSnapshotMapper.ObtenerPreciosLavado(turno));
        Assert.Throws<InvalidOperationException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
        foreach (var breakdown in new[] {
            new DesglosePrecioTurno { Version = 1, Subtotal = null, Total = 72m },
            new DesglosePrecioTurno { Version = 1, Subtotal = 80m, Total = null },
            new DesglosePrecioTurno { Version = 2, Subtotal = 80m, Total = 72m },
            new DesglosePrecioTurno { Version = 1, Subtotal = -1m, Total = 72m },
            new DesglosePrecioTurno { Version = 1, Subtotal = 80m, Total = 71m } })
        {
            turno.DesglosePrecio = breakdown;
            Assert.Throws<InvalidOperationException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
        }
        turno = Crear(); turno.PrecioAcordado = 0; turno.Descuento = 100;
        turno.DesglosePrecio = new() { Version = 1, Subtotal = 80, Total = 0 };
        Assert.Equal((0m, 80m), TurnoLavadoSnapshotMapper.ObtenerPreciosLavado(turno));
        turno.Descuento = 0; turno.DesglosePrecio.Subtotal = 0;
        Assert.Equal((0m, 0m), TurnoLavadoSnapshotMapper.ObtenerPreciosLavado(turno));
    }

    [Fact]
    public void RechazaCapturaAusenteIncompletaODesordenada()
    {
        var turno = Crear(); turno.RecetaOperativa = null;
        Assert.Throws<ArgumentException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
        turno = Crear(); turno.RecetaOperativa!.Servicios.RemoveAt(1);
        Assert.Throws<ArgumentException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
        turno = Crear(); turno.RecetaOperativa!.Servicios.Reverse();
        Assert.Throws<ArgumentException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
        turno = Crear(); turno.RecetaOperativa!.Servicios[0].Etapas[0].Receta = null!;
        Assert.Throws<ArgumentException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
        turno = Crear(); turno.Vehiculos[0].Servicios[0].ServicioId = "";
        Assert.Throws<ArgumentException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
        turno = Crear(); turno.RecetaOperativa!.Servicios[1].Receta[0].UnidadMedida = "";
        Assert.Throws<ArgumentException>(() => TurnoLavadoSnapshotMapper.Mapear(turno));
    }
}
