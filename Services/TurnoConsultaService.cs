using Firebase.Models;
using Google.Cloud.Firestore;

namespace Firebase.Services;

public interface ITurnoConsultaService
{
    Task<List<TurnoConsultaViewModel>> Listar();
    Task<TurnoConsultaViewModel?> Obtener(string id);
    Task<TurnoDatosEdicion?> ObtenerDatosEdicion(string id) => throw new NotSupportedException();
    Task<List<Turno>> ProximosDeCliente(string clienteId, DateTime desdeUtc);
}

public sealed class TurnoConsultaService(FirestoreDb db) : ITurnoConsultaService
{
    private const string NoDisponible = "No disponible";

    public async Task<List<Turno>> ProximosDeCliente(string clienteId, DateTime desdeUtc)
    {
        if (string.IsNullOrWhiteSpace(clienteId) || clienteId.Contains('/') || desdeUtc.Kind != DateTimeKind.Utc)
            return new List<Turno>();
        var snapshot = await db.Collection("turnos").WhereEqualTo("ClienteId", clienteId)
            .WhereEqualTo("Estado", "Pendiente").WhereGreaterThanOrEqualTo("InicioUtc", desdeUtc)
            .OrderBy("InicioUtc").Limit(11).GetSnapshotAsync();
        return snapshot.Documents.Select(d => d.ConvertTo<Turno>()).ToList();
    }

    public async Task<List<TurnoConsultaViewModel>> Listar()
    {
        var snapshot = await db.Collection("turnos").OrderByDescending("InicioUtc").GetSnapshotAsync();
        return await Proyectar(snapshot.Documents.Select(d => d.ConvertTo<Turno>()).ToList());
    }

    public async Task<TurnoConsultaViewModel?> Obtener(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains('/')) return null;
        var snapshot = await db.Collection("turnos").Document(id).GetSnapshotAsync();
        if (!snapshot.Exists) return null;
        var detalle = (await Proyectar(new List<Turno> { snapshot.ConvertTo<Turno>() }))[0];
        var nombres = new List<string>();
        var datos = snapshot.ToDictionary();
        if (datos.TryGetValue("LavadoId", out var vinculo) && vinculo is string lavadoId && IdValido(lavadoId))
        {
            var lavado = await db.Collection("lavados").Document(lavadoId).GetSnapshotAsync();
            var campos = lavado.Exists ? lavado.ToDictionary() : new Dictionary<string, object>();
            if (campos.TryGetValue("TurnoId", out var turnoId) && turnoId is string reciproco && reciproco == id &&
                campos.TryGetValue("EmpleadosAsignadosIds", out var asignados) && asignados is System.Collections.IEnumerable ids && asignados is not string)
            {
                foreach (var valor in ids)
                {
                    if (valor is not string empleadoId || !IdValido(empleadoId)) continue;
                    var empleado = await db.Collection("empleados").Document(empleadoId).GetSnapshotAsync();
                    var empleadoCampos = empleado.Exists ? empleado.ToDictionary() : new Dictionary<string, object>();
                    if (empleadoCampos.TryGetValue("Nombre", out var nombre) && nombre is string texto && !string.IsNullOrWhiteSpace(texto))
                        nombres.Add(texto.Trim());
                }
            }
        }
        return detalle with { EmpleadosAsignados = nombres, VersionEsperada = snapshot.UpdateTime?.ToDateTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture) };
    }

    private static bool IdValido(string id) => !string.IsNullOrWhiteSpace(id) && !id.Contains('/') && id is not "." and not "..";

    private static string NombreCliente(string nombre, string apellido)
    {
        var partes = new[] { nombre, apellido }.Where(x => x != NoDisponible).Select(x => x.Trim()).ToArray();
        return partes.Length == 0 ? NoDisponible : string.Join(" ", partes);
    }

    public async Task<TurnoDatosEdicion?> ObtenerDatosEdicion(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains('/')) return null;
        var snapshot = await db.Collection("turnos").Document(id).GetSnapshotAsync();
        if (!snapshot.Exists || snapshot.UpdateTime == null) return null;
        var turno = snapshot.ConvertTo<Turno>();
        if (turno.Id != id || turno.Estado != "Pendiente" || turno.Validar().Count != 0) return null;
        var consulta = (await Proyectar(new List<Turno> { turno }))[0];
        var vehicle = consulta.Vehiculos[0];
        var vehicleDoc = await db.Collection("vehiculos").Document(vehicle.VehiculoId).GetSnapshotAsync();
        var tipo = vehicleDoc.Exists && vehicleDoc.TryGetValue<string>("TipoVehiculo", out var value) ? value : "";
        var vehicles = new[] { new VehiculoEdicionViewModel(vehicle.Patente, vehicle.VehiculoId, tipo, vehicle.CantidadEmpleadosRequeridos, vehicle.Servicios) };
        return new TurnoDatosEdicion(turno.Id, snapshot.UpdateTime.Value.ToDateTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            turno.InicioUtc, turno.AceptaAdelanto, turno.ClienteId, turno.ClienteTrajoId, turno.ClienteRetiraId,
            turno.Notas, turno.Descuento, turno.CuponId, turno.PrecioAcordado, vehicles);
    }

    private async Task<List<TurnoConsultaViewModel>> Proyectar(List<Turno> turnos)
    {
        var referencias = new HashSet<DocumentReference>();
        void Agregar(string coleccion, string? id)
        {
            if (!string.IsNullOrWhiteSpace(id) && !id.Contains('/')) referencias.Add(db.Collection(coleccion).Document(id));
        }
        foreach (var turno in turnos)
        {
            if (string.IsNullOrWhiteSpace(turno.ClienteNombre))
                Agregar("clientes", turno.ClienteId);
            foreach (var vehiculo in turno.Vehiculos)
            {
                if (string.IsNullOrWhiteSpace(vehiculo.Patente) && string.IsNullOrWhiteSpace(turno.VehiculoPatente))
                    Agregar("vehiculos", vehiculo.VehiculoId);
                foreach (var servicio in vehiculo.Servicios) Agregar("servicios", servicio.ServicioId);
            }
        }
        var documentos = new Dictionary<DocumentReference, DocumentSnapshot>();
        foreach (var lote in referencias.Chunk(100))
            foreach (var doc in await db.GetAllSnapshotsAsync(lote)) documentos[doc.Reference] = doc;
        string Campo(string coleccion, string? id, string campo)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Contains('/') || !documentos.TryGetValue(db.Collection(coleccion).Document(id), out var doc) || !doc.Exists ||
                !doc.TryGetValue<string>(campo, out var valor) || string.IsNullOrWhiteSpace(valor)) return NoDisponible;
            return valor;
        }
        string Cliente(Turno t)
        {
            if (!string.IsNullOrWhiteSpace(t.ClienteNombre)) return t.ClienteNombre;
            var nombre = Campo("clientes", t.ClienteId, "Nombre");
            var apellido = Campo("clientes", t.ClienteId, "Apellido");
            return NombreCliente(nombre, apellido);
        }
        string Patente(Turno t, VehiculoEnTurno v)
        {
            if (!string.IsNullOrWhiteSpace(v.Patente)) return v.Patente;
            if (!string.IsNullOrWhiteSpace(t.VehiculoPatente)) return t.VehiculoPatente;
            return Campo("vehiculos", v.VehiculoId, "Patente");
        }
        return turnos.Select(t => new TurnoConsultaViewModel(t.Id, Cliente(t), t.Estado, t.InicioUtc, t.FinUtc,
            t.AceptaAdelanto, t.PrecioAcordado, t.Vehiculos.Select(v => new VehiculoConsultaViewModel(
                Patente(t, v), v.CantidadEmpleadosRequeridos,
                v.Servicios.Select(s => new ServicioConsultaViewModel(Campo("servicios", s.ServicioId, "Nombre"), s.TiempoEstimado, s.Precio, s.ServicioId, s.PaqueteId)).ToList(), v.VehiculoId)).ToList(), Descuento: t.Descuento)).ToList();
    }
}
