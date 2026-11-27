using System.Text.RegularExpressions;
using Firebase.Models;
using Firebase.Services;
using Firebase.Utils;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de tipos de vehículo en Firestore.
/// Proporciona operaciones CRUD transaccionales, validación de unicidad sin tildes y borrado lógico.
/// </summary>
public class TipoVehiculoService
{
    public const string Collection = "tiposVehiculos";

    private readonly FirestoreDb _firestore;
    private readonly IAuditWriter? _auditWriter;

    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    public TipoVehiculoService(FirestoreDb firestore, IAuditWriter? auditWriter = null)
    {
        _firestore = firestore;
        _auditWriter = auditWriter;
    }

    public sealed record ResultadoAuditoriaTipoVehiculo(
        ResultadoOperacion Resultado,
        string TipoVehiculoId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        (_auditWriter ?? new AuditService(_firestore)).EnlistarEnTransaccion(transaction, log);
    }

    #region Operaciones de Consulta

    public async Task<List<TipoVehiculo>> ObtenerTodos(bool incluirInactivos = true)
    {
        var snapshot = await _firestore.Collection(Collection).GetSnapshotAsync();
        var lista = snapshot.Documents.Select(Mapear).OrderBy(x => x.Nombre).ToList();
        return lista.Where(x => incluirInactivos || x.Estado == "Activo").ToList();
    }

    private static TipoVehiculo Mapear(DocumentSnapshot doc) => new()
    {
        Id = doc.Id,
        Nombre = doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") ?? string.Empty : string.Empty,
        FormatoPatente = doc.ContainsField("FormatoPatente") ? doc.GetValue<string>("FormatoPatente") ?? string.Empty : string.Empty,
        CantidadEmpleadosRequeridos = doc.ContainsField("CantidadEmpleadosRequeridos") ? doc.GetValue<int>("CantidadEmpleadosRequeridos") : 1,
        Estado = NombreCatalogoNormalizer.NormalizarEstado(
            doc.ContainsField("Estado") ? doc.GetValue<string>("Estado") : null)
    };

    /// <summary>
    /// Obtiene los nombres de los tipos de vehículo activos.
    /// </summary>
    public async Task<List<string>> ObtenerTiposVehiculos()
    {
        try
        {
            return (await ObtenerTodos(incluirInactivos: false)).Select(x => x.Nombre).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de vehículo: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Obtiene los tipos de vehículo con sus formatos de patente (por defecto solo activos).
    /// </summary>
    public async Task<List<TipoVehiculo>> ObtenerTiposVehiculosCompletos(bool incluirInactivos = false)
    {
        try
        {
            return await ObtenerTodos(incluirInactivos);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de vehículo: {ex.Message}");
            return new List<TipoVehiculo>();
        }
    }

    public async Task<TipoVehiculo?> ObtenerPorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        return doc.Exists ? Mapear(doc) : null;
    }

    /// <summary>
    /// Obtiene un tipo de vehículo por su nombre (incluye inactivos para lecturas históricas/runtime).
    /// </summary>
    public async Task<TipoVehiculo?> ObtenerTipoVehiculoPorNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        try
        {
            var todos = await ObtenerTodos(incluirInactivos: true);
            return todos.FirstOrDefault(x => string.Equals(x.Nombre, nombre.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? todos.FirstOrDefault(x => NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, nombre));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipo de vehículo: {ex.Message}");
            return null;
        }
    }

    /// <summary>Lee tipos para auditoría sin ocultar errores de Firestore.</summary>
    public async Task<List<TipoVehiculo>> ObtenerParaAuditoriaPorNombre(string nombre)
    {
        var snapshot = await _firestore.Collection(Collection).WhereEqualTo("Nombre", nombre).GetSnapshotAsync();
        return snapshot.Documents.Select(Mapear).ToList();
    }

    /// <summary>Lee el tipo persistido por ID sin ocultar errores de Firestore.</summary>
    public async Task<TipoVehiculo?> ObtenerParaAuditoriaPorId(string id)
    {
        var doc = await _firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        return doc.Exists ? Mapear(doc) : null;
    }

    /// <summary>
    /// Obtiene el nombre de un tipo de vehículo por su ID.
    /// </summary>
    public async Task<string?> ObtenerNombrePorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        return doc.Exists && doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") : null;
    }

    /// <summary>
    /// Verifica si existe un tipo de vehículo (activo o inactivo) con el nombre especificado,
    /// ignorando tildes, espacios y mayúsculas/minúsculas.
    /// </summary>
    public async Task<bool> ExisteTipoVehiculo(string nombre)
    {
        var tiposExistentes = await ObtenerTodos(incluirInactivos: true);
        return tiposExistentes.Any(t => NombreCatalogoNormalizer.SonEquivalentes(t.Nombre, nombre));
    }

    public async Task<bool> EstaEnUsoActivo(string nombre, Transaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return false;

        var serviciosSnap = transaction != null
            ? await transaction.GetSnapshotAsync(_firestore.Collection("servicios"))
            : await _firestore.Collection("servicios").GetSnapshotAsync();

        if (serviciosSnap.Documents.Any(d =>
            NombreCatalogoNormalizer.NormalizarEstado(d.ContainsField("Estado") ? d.GetValue<string>("Estado") : null) == "Activo" &&
            NombreCatalogoNormalizer.SonEquivalentes(d.ContainsField("TipoVehiculo") ? d.GetValue<string>("TipoVehiculo") : null, nombre)))
        {
            return true;
        }

        var paquetesSnap = transaction != null
            ? await transaction.GetSnapshotAsync(_firestore.Collection("paquetesServicio"))
            : await _firestore.Collection("paquetesServicio").GetSnapshotAsync();

        if (paquetesSnap.Documents.Any(d =>
            NombreCatalogoNormalizer.NormalizarEstado(d.ContainsField("Estado") ? d.GetValue<string>("Estado") : null) == "Activo" &&
            NombreCatalogoNormalizer.SonEquivalentes(d.ContainsField("TipoVehiculo") ? d.GetValue<string>("TipoVehiculo") : null, nombre)))
        {
            return true;
        }

        var vehiculosSnap = transaction != null
            ? await transaction.GetSnapshotAsync(_firestore.Collection("vehiculos"))
            : await _firestore.Collection("vehiculos").GetSnapshotAsync();

        return vehiculosSnap.Documents.Any(d =>
            NombreCatalogoNormalizer.NormalizarEstado(d.ContainsField("Estado") ? d.GetValue<string>("Estado") : null) == "Activo" &&
            NombreCatalogoNormalizer.SonEquivalentes(d.ContainsField("TipoVehiculo") ? d.GetValue<string>("TipoVehiculo") : null, nombre));
    }

    #endregion

    #region Operaciones CRUD y Estado

    public Task<ResultadoAuditoriaTipoVehiculo> GuardarConAuditoria(TipoVehiculo tipo, bool crear, ActorAuditoria actor) =>
        GuardarConAuditoria(tipo, crear, actor.UserId, actor.UserEmail);

    public async Task<ResultadoAuditoriaTipoVehiculo> GuardarConAuditoria(
        TipoVehiculo tipo, bool crear, string userId = "", string userEmail = "")
    {
        tipo.Nombre = NombreCatalogoNormalizer.LimpiarNombre(tipo.Nombre);
        tipo.FormatoPatente = (tipo.FormatoPatente ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(tipo.Nombre))
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("El nombre del tipo de vehículo es obligatorio."));
        if (tipo.Nombre.Length < 3)
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("El nombre debe tener al menos 3 caracteres."));
        if (string.IsNullOrWhiteSpace(tipo.FormatoPatente))
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("El formato de patente es obligatorio."));
        if (tipo.FormatoPatente.Length < 3 || !Regex.IsMatch(tipo.FormatoPatente, @"^[nl.\-|]{3,}$"))
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError(
                "El formato solo puede contener 'n' (números), 'l' (letras), '.', '-' y '|'. Mínimo 3 caracteres."));
        if (tipo.CantidadEmpleadosRequeridos < 1 || tipo.CantidadEmpleadosRequeridos > 10)
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("La cantidad de empleados debe estar entre 1 y 10."));
        if (!crear && string.IsNullOrWhiteSpace(tipo.Id))
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("El tipo de vehículo no existe."));

        return await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
        {
            var snapshotExistentes = await transaction.GetSnapshotAsync(_firestore.Collection(Collection));
            var existentes = snapshotExistentes.Documents.Select(Mapear).ToList();

            if (crear)
            {
                if (existentes.Any(x => NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, tipo.Nombre)))
                    return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("Ya existe un tipo de vehículo con ese nombre."));

                var docRef = _firestore.Collection(Collection).Document();
                tipo.Id = docRef.Id;
                tipo.Estado = "Activo";

                var antes = new Dictionary<string, string>();
                var despues = AuditCampos.CapturarTipoVehiculo(tipo);

                transaction.Set(docRef, new Dictionary<string, object>
                {
                    ["Nombre"] = tipo.Nombre,
                    ["FormatoPatente"] = tipo.FormatoPatente,
                    ["CantidadEmpleadosRequeridos"] = tipo.CantidadEmpleadosRequeridos,
                    ["Estado"] = "Activo"
                });

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Creacion de tipo de vehiculo",
                    TargetId = tipo.Id,
                    TargetType = "TipoVehiculo",
                    NombreHistorico = tipo.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoVehiculo(
                    ResultadoOperacion.CrearExito("Tipo de vehículo creado correctamente."),
                    tipo.Id,
                    antes,
                    despues);
            }
            else
            {
                var docRef = _firestore.Collection(Collection).Document(tipo.Id);
                var actualDoc = await transaction.GetSnapshotAsync(docRef);
                if (!actualDoc.Exists)
                    return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("El tipo de vehículo no existe."));

                var actual = Mapear(actualDoc);

                if (existentes.Any(x => x.Id != tipo.Id && NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, tipo.Nombre)))
                    return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("Ya existe un tipo de vehículo con ese nombre."));

                List<DocumentReference>? refsAfectadas = null;
                if (!string.Equals(actual.Nombre, tipo.Nombre, StringComparison.Ordinal))
                {
                    refsAfectadas = new List<DocumentReference>();
                    foreach (var col in new[] { "servicios", "paquetesServicio", "vehiculos" })
                    {
                        var snap = await transaction.GetSnapshotAsync(_firestore.Collection(col));
                        refsAfectadas.AddRange(snap.Documents
                            .Where(d => NombreCatalogoNormalizer.SonEquivalentes(
                                d.ContainsField("TipoVehiculo") ? d.GetValue<string>("TipoVehiculo") : null, actual.Nombre))
                            .Select(d => d.Reference));
                    }
                }

                var actualizado = new TipoVehiculo
                {
                    Id = actual.Id,
                    Nombre = tipo.Nombre,
                    FormatoPatente = tipo.FormatoPatente,
                    CantidadEmpleadosRequeridos = tipo.CantidadEmpleadosRequeridos,
                    Estado = actual.Estado
                };

                var antes = AuditCampos.CapturarTipoVehiculo(actual);
                var despues = AuditCampos.CapturarTipoVehiculo(actualizado);

                if (!AuditCampos.HayCambios(antes, despues))
                {
                    return new ResultadoAuditoriaTipoVehiculo(
                        ResultadoOperacion.CrearExito("Tipo de vehículo actualizado correctamente."),
                        actual.Id,
                        antes,
                        despues);
                }

                transaction.Update(docRef, new Dictionary<string, object>
                {
                    ["Nombre"] = actualizado.Nombre,
                    ["FormatoPatente"] = actualizado.FormatoPatente,
                    ["CantidadEmpleadosRequeridos"] = actualizado.CantidadEmpleadosRequeridos
                });

                if (refsAfectadas != null)
                {
                    foreach (var r in refsAfectadas)
                        transaction.Update(r, "TipoVehiculo", actualizado.Nombre);
                }

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Actualizacion de tipo de vehiculo",
                    TargetId = actual.Id,
                    TargetType = "TipoVehiculo",
                    NombreHistorico = actualizado.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoVehiculo(
                    ResultadoOperacion.CrearExito("Tipo de vehículo actualizado correctamente."),
                    actual.Id,
                    antes,
                    despues);
            }
        });
    }

    public Task<ResultadoAuditoriaTipoVehiculo> CambiarEstadoConAuditoria(string id, string estado, ActorAuditoria actor) =>
        CambiarEstadoConAuditoria(id, estado, actor.UserId, actor.UserEmail);

    public async Task<ResultadoAuditoriaTipoVehiculo> CambiarEstadoConAuditoria(
        string id, string estado, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("El tipo de vehículo no existe."));
        if (estado != "Activo" && estado != "Inactivo")
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("Estado inválido."));

        return await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
        {
            var docRef = _firestore.Collection(Collection).Document(id);
            var doc = await transaction.GetSnapshotAsync(docRef);
            if (!doc.Exists)
                return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError("El tipo de vehículo no existe."));

            var actual = Mapear(doc);

            if (estado == "Inactivo" && await EstaEnUsoActivo(actual.Nombre, transaction))
            {
                return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearError(
                    "No se puede desactivar el tipo de vehículo porque está en uso por servicios, paquetes o vehículos activos."));
            }

            if (string.Equals(actual.Estado, estado, StringComparison.OrdinalIgnoreCase))
                return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearExito("El estado ya coincide."), actual.Id);

            var antes = AuditCampos.CapturarTipoVehiculo(actual);
            actual.Estado = estado;
            var despues = AuditCampos.CapturarTipoVehiculo(actual);

            transaction.Update(docRef, "Estado", estado);

            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = estado == "Activo" ? "Reactivacion de tipo de vehiculo" : "Desactivacion de tipo de vehiculo",
                TargetId = actual.Id,
                TargetType = "TipoVehiculo",
                NombreHistorico = actual.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            var mensaje = estado == "Activo"
                ? "Tipo de vehículo reactivado correctamente."
                : "Tipo de vehículo desactivado correctamente.";
            return new ResultadoAuditoriaTipoVehiculo(ResultadoOperacion.CrearExito(mensaje), actual.Id, antes, despues);
        });
    }

    public async Task<string> CrearTipoVehiculo(string nombre, string? formatoPatente = null, int cantidadEmpleadosRequeridos = 1)
    {
        var tipoRef = _firestore.Collection(Collection).Document();
        var tipo = new Dictionary<string, object>
        {
            ["Nombre"] = NombreCatalogoNormalizer.LimpiarNombre(nombre),
            ["CantidadEmpleadosRequeridos"] = cantidadEmpleadosRequeridos,
            ["Estado"] = "Activo"
        };

        if (!string.IsNullOrWhiteSpace(formatoPatente))
        {
            tipo["FormatoPatente"] = formatoPatente.Trim();
        }

        await tipoRef.SetAsync(tipo);
        return tipoRef.Id;
    }

    public async Task<List<string>> EliminarTipoVehiculo(string nombre, IReadOnlySet<string>? idsCapturados = null)
    {
        var tiposRef = _firestore.Collection(Collection);
        var snapshot = await tiposRef.WhereEqualTo("Nombre", nombre).GetSnapshotAsync();

        if (idsCapturados != null && !idsCapturados.SetEquals(snapshot.Documents.Select(doc => doc.Id)))
            throw new InvalidOperationException("Los tipos de vehículo cambiaron desde la captura para auditoría.");

        var eliminados = new List<string>();
        foreach (var documento in snapshot.Documents)
        {
            eliminados.Add(documento.Id);
            await documento.Reference.DeleteAsync();
        }
        return eliminados;
    }

    #endregion
}
