using Firebase.Models;
using Google.Cloud.Firestore;

namespace Firebase.Services;

public sealed class TurnoInicioRecursosService(FirestoreDb db)
{
    public record Opcion(string Id, string Nombre, string Unidad);
    public record Item(int Indice, string Nombre, string Unidad, decimal Dosis, IReadOnlyList<Opcion> Opciones);

    public async Task<IReadOnlyList<Item>> Obtener(string id)
    {
        var doc = await db.Collection("turnos").Document(id).GetSnapshotAsync();
        if (!doc.Exists || !doc.TryGetValue<string>("Estado", out var estado) || estado != "Pendiente" || doc.ContainsField("LavadoId"))
            throw new InvalidOperationException("Turno no disponible.");
        var servicios = TurnoLavadoSnapshotMapper.Mapear(doc.ConvertTo<Turno>()).Servicios;
        var primero = servicios.OrderBy(s => s.Orden).First();
        var receta = primero.Etapas.FirstOrDefault()?.Receta ?? primero.Receta;
        var tipos = receta.Select((r, indice) => (r, indice)).Where(x => x.r.Modalidad == "Tipo").ToList();
        if (tipos.Count == 0) return Array.Empty<Item>();
        var catalogo = await db.Collection("insumos").WhereEqualTo("Estado", "Activo").GetSnapshotAsync();
        var insumos = catalogo.Documents.Select(d => (d.Id, Insumo: d.ConvertTo<Insumo>())).ToList();
        var conversion = new ConversionUnidadesService();
        return tipos.Select(x => new Item(x.indice, x.r.NombreTipo, x.r.UnidadMedida, x.r.DosisTeorica,
            insumos.Where(i => i.Insumo.TipoId == x.r.TipoId && conversion.SonCompatibles(x.r.UnidadMedida, i.Insumo.UnidadMedida))
                .Select(i => new Opcion(i.Id, i.Insumo.Nombre, i.Insumo.UnidadMedida)).ToList())).ToList();
    }
}
