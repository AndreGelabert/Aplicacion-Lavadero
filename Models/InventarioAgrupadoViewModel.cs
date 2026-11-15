using Firebase.Services;

namespace Firebase.Models;

public sealed class InventarioAgrupadoViewModel
{
    public string Nombre { get; private init; } = "";
    public string UnidadResumen { get; private init; } = "";
    public decimal? TotalFisico { get; private init; }
    public List<ProductoInventario> Productos { get; private init; } = new();

    public sealed record ProductoInventario(Insumo Insumo, decimal? Reservado, decimal? Disponible);

    public sealed class IdentidadTipos
    {
        private readonly Dictionary<string, TipoInsumo> porId;
        private readonly Dictionary<string, TipoInsumo> porNombre;

        public IdentidadTipos(IEnumerable<TipoInsumo> tipos)
        {
            var lista = tipos.ToList();
            porId = lista.Where(t => !string.IsNullOrWhiteSpace(t.Id)).GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            porNombre = lista.Where(t => !string.IsNullOrWhiteSpace(t.Nombre))
                .GroupBy(t => t.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        public TipoInsumo? Resolver(Insumo item)
        {
            if (!string.IsNullOrWhiteSpace(item.TipoId))
                return porId.TryGetValue(item.TipoId, out var porIdentificador) ? porIdentificador : null;
            return porNombre.TryGetValue(item.Tipo?.Trim() ?? "", out var porDenominacion) ? porDenominacion : null;
        }

        public string Clave(Insumo item)
        {
            if (!string.IsNullOrWhiteSpace(item.TipoId)) return "id:" + item.TipoId;
            var tipo = Resolver(item);
            return tipo != null && !string.IsNullOrWhiteSpace(tipo.Id) ? "id:" + tipo.Id : "legacy:" + item.Tipo;
        }

        public bool CoincideFiltro(Insumo item, string filtro)
        {
            if (porId.TryGetValue(filtro, out var porIdentificador))
                return string.Equals(Clave(item), "id:" + porIdentificador.Id, StringComparison.OrdinalIgnoreCase);
            if (porNombre.TryGetValue(filtro, out var porDenominacion) && !string.IsNullOrWhiteSpace(porDenominacion.Id))
                return string.Equals(Clave(item), "id:" + porDenominacion.Id, StringComparison.OrdinalIgnoreCase);
            if (string.Equals(Clave(item), "id:" + filtro, StringComparison.OrdinalIgnoreCase))
                return true;
            var nombre = !string.IsNullOrWhiteSpace(item.TipoId) ? Resolver(item)?.Nombre : item.Tipo;
            return string.Equals(nombre?.Trim(), filtro, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static List<InventarioAgrupadoViewModel> Crear(IEnumerable<Insumo> todos, IEnumerable<TipoInsumo> tipos,
        IReadOnlyDictionary<string, decimal?> reservas, IEnumerable<Insumo> pagina)
    {
        var lista = todos.ToList();
        var visibles = pagina.ToHashSet();
        var identidad = new IdentidadTipos(tipos);
        var convertidor = new ConversionUnidadesService();
        return lista.GroupBy(identidad.Clave, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Any(visibles.Contains)).Select(g =>
            {
                var primero = g.First();
                var tipo = identidad.Resolver(primero);
                decimal? total = tipo == null || !TipoInsumo.EsUnidadValidaParaCategoria(tipo.Categoria, tipo.UnidadResumen) ? null : 0m;
                if (total.HasValue)
                {
                    foreach (var item in g)
                    {
                        if (!TipoInsumo.EsUnidadValidaParaCategoria(tipo!.Categoria, item.UnidadMedida) ||
                            !convertidor.TryConvertir(item.StockActual, item.UnidadMedida, tipo.UnidadResumen, out var valor, out _)) { total = null; break; }
                        try { total = checked(total.Value + valor); }
                        catch (OverflowException) { total = null; break; }
                    }
                }
                return new InventarioAgrupadoViewModel {
                    Nombre = tipo?.Nombre ?? primero.Tipo, UnidadResumen = tipo?.UnidadResumen ?? "", TotalFisico = total,
                    Productos = g.Where(visibles.Contains).Select(i => {
                        reservas.TryGetValue(i.Id, out var reservado);
                        decimal? disponible = reservado.HasValue && reservado >= 0 && reservado <= i.StockActual ? i.StockActual - reservado : null;
                        return new ProductoInventario(i, reservado is >= 0 ? reservado : null, disponible);
                    }).ToList()
                };
            }).ToList();
    }
}
