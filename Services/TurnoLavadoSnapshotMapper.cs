using Firebase.Models;

namespace Firebase.Services;

/// <summary>Proyecta únicamente datos congelados; no construye ni inicia un lavado.</summary>
internal static class TurnoLavadoSnapshotMapper
{
    internal record Resultado(IReadOnlyList<ServicioEnLavado> Servicios, decimal Precio, decimal PrecioOriginal);

    internal static (decimal Precio, decimal PrecioOriginal) ObtenerPreciosLavado(Turno turno)
    {
        var precio = turno?.DesglosePrecio;
        if (precio?.Version != 1 || precio.Subtotal is not decimal subtotal || precio.Total is not decimal total ||
            subtotal < 0 || total < 0 || turno!.Descuento is < 0 or > 100 ||
            total != subtotal - subtotal * turno.Descuento / 100m || !CoincidePrecioPersistido(total, turno.PrecioAcordado))
            throw new InvalidOperationException("Instantánea de precio incompleta o incompatible.");
        return (total, subtotal);
    }

    private static bool CoincidePrecioPersistido(decimal exacto, decimal acordado)
    {
        var converter = new Firebase.Converters.DecimalConverter();
        return converter.FromFirestore(converter.ToFirestore(exacto)) ==
            converter.FromFirestore(converter.ToFirestore(acordado));
    }

    internal static Resultado Mapear(Turno turno)
    {
        if (turno?.Vehiculos == null || turno.Vehiculos.Count != 1 || turno.Vehiculos[0]?.Servicios == null ||
            turno.Vehiculos[0].Servicios.Count == 0 || turno.RecetaOperativa?.Version != 1 ||
            turno.RecetaOperativa.Servicios == null || turno.PrecioAcordado < 0)
            throw new ArgumentException("Instantánea de turno incompleta.", nameof(turno));
        var (precio, precioOriginal) = ObtenerPreciosLavado(turno);
        var seleccion = turno.Vehiculos[0].Servicios;
        var recetas = turno.RecetaOperativa.Servicios;
        if (seleccion.Count != recetas.Count)
            throw new ArgumentException("Los servicios no coinciden con la receta congelada.", nameof(turno));
        var resultado = new List<ServicioEnLavado>();
        for (int i = 0; i < seleccion.Count; i++)
        {
            var s = seleccion[i]; var r = recetas[i];
            if (s == null || r == null || string.IsNullOrWhiteSpace(s.ServicioId) || s.ServicioId != r.ServicioId ||
                string.IsNullOrWhiteSpace(r.Nombre) || string.IsNullOrWhiteSpace(r.Tipo) || s.Precio < 0 ||
                s.TiempoEstimado <= 0 || (s.PaqueteId != null && string.IsNullOrWhiteSpace(s.PaqueteId)) ||
                r.Receta == null || r.Etapas == null)
                throw new ArgumentException("Servicio congelado incompleto o incompatible.", nameof(turno));
            var servicio = new ServicioEnLavado { ServicioId = s.ServicioId, ServicioNombre = r.Nombre,
                TipoServicio = r.Tipo, Precio = s.Precio, TiempoEstimado = s.TiempoEstimado,
                PaqueteId = s.PaqueteId, Orden = i };
            if (r.Etapas.Count == 0) servicio.Receta = Copiar(r.Receta);
            else foreach (var etapa in r.Etapas)
            {
                if (etapa == null || string.IsNullOrWhiteSpace(etapa.Id) || string.IsNullOrWhiteSpace(etapa.Nombre) || etapa.Receta == null)
                    throw new ArgumentException("Etapa congelada incompleta.", nameof(turno));
                servicio.Etapas.Add(new EtapaEnLavado { EtapaId = etapa.Id, Nombre = etapa.Nombre, Receta = Copiar(etapa.Receta) });
            }
            resultado.Add(servicio);
        }
        return new Resultado(resultado.AsReadOnly(), precio, precioOriginal);
    }

    private static List<RecetaInsumo> Copiar(List<RecetaInsumo> receta)
    {
        var copia = new List<RecetaInsumo>();
        foreach (var r in receta)
        {
            if (r == null || r.DosisTeorica <= 0 || string.IsNullOrWhiteSpace(r.UnidadMedida) ||
                (r.Modalidad == "Tipo" ? string.IsNullOrWhiteSpace(r.TipoId) :
                 r.Modalidad != "Producto" || string.IsNullOrWhiteSpace(r.InsumoId)))
                throw new ArgumentException("Receta congelada incompleta.");
            copia.Add(new RecetaInsumo { Modalidad = r.Modalidad, TipoId = r.TipoId, NombreTipo = r.NombreTipo,
                Categoria = r.Categoria, InsumoId = r.InsumoId, NombreInsumo = r.NombreInsumo,
                UnidadMedida = r.UnidadMedida, DosisTeorica = r.DosisTeorica, EsHerramienta = r.EsHerramienta });
        }
        return copia;
    }
}
