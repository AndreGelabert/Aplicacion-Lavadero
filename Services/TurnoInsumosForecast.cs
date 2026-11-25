using Firebase.Models;

namespace Firebase.Services;

/// <summary>Proyección de recetas suministradas, sin consultar ni reservar stock.</summary>
internal static class TurnoInsumosForecast
{
    internal record Demanda(string ServicioId, string? EtapaId, string Modalidad, string TipoId,
        string NombreTipo, string Categoria, string InsumoId, string NombreInsumo,
        string UnidadMedida, decimal DosisTeorica);

    internal static IReadOnlyList<Demanda> Proyectar(IEnumerable<Servicio> servicios)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        var resultado = new List<Demanda>();
        foreach (var servicio in servicios)
        {
            if (servicio.Etapas?.Any() == true)
                foreach (var etapa in servicio.Etapas)
                    Agregar(servicio.Id, etapa.Id, etapa.Receta ?? [], resultado);
            else
                Agregar(servicio.Id, null, servicio.Receta ?? [], resultado);
        }
        return resultado.AsReadOnly();
    }

    private static void Agregar(string servicioId, string? etapaId, IEnumerable<RecetaInsumo> receta, List<Demanda> resultado)
    {
        foreach (var item in receta)
        {
            if (item.DosisTeorica <= 0) throw new ArgumentException("La dosis debe ser mayor a cero.");
            var dosis = StockPrecision.ExigirRepresentable(item.DosisTeorica);
            resultado.Add(new Demanda(servicioId, etapaId, item.Modalidad, item.TipoId, item.NombreTipo,
                item.Categoria, item.InsumoId, item.NombreInsumo, item.UnidadMedida, dosis));
        }
    }
}
