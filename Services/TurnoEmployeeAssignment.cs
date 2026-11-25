namespace Firebase.Services;

/// <summary>Selección pura sobre instantáneas; no lee ni reserva empleados.
/// El llamador debe aportar únicamente IDs ocupados por lavados EnProceso.</summary>
internal static class TurnoEmployeeAssignment
{
    internal static IReadOnlyList<string> Seleccionar(int cantidad, IEnumerable<string> activos,
        IEnumerable<IEnumerable<string>> asignacionesOcupadas, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);
        ArgumentNullException.ThrowIfNull(activos);
        ArgumentNullException.ThrowIfNull(asignacionesOcupadas);
        var empleados = new List<string>();
        var activosUnicos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in activos)
        {
            ValidarId(id);
            if (activosUnicos.Add(id)) empleados.Add(id);
        }
        var ocupados = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asignacion in asignacionesOcupadas)
        {
            if (asignacion == null) throw new ArgumentException("Asignación inválida.", nameof(asignacionesOcupadas));
            foreach (var id in asignacion)
            {
                ValidarId(id);
                ocupados.Add(id);
            }
        }
        var disponibles = empleados.Where(id => !ocupados.Contains(id)).ToList();
        if (disponibles.Count < cantidad)
            throw new InvalidOperationException($"No hay suficientes empleados disponibles. Se requieren {cantidad} pero solo hay {disponibles.Count} disponibles.");
        var rng = random ?? Random.Shared;
        return disponibles.OrderBy(_ => rng.Next()).Take(cantidad).ToList().AsReadOnly();
    }

    private static void ValidarId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains('/') || id is "." or ".." ||
            (id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) ||
            System.Text.Encoding.UTF8.GetByteCount(id) > 1500)
            throw new ArgumentException("Identificador de empleado inválido.");
    }
}
