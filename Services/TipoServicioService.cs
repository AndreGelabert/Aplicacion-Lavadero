using Firebase.Models;
using Firebase.Services;
using Firebase.Utils;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de tipos de servicio en Firestore.
/// Proporciona operaciones CRUD transaccionales, validación de unicidad sin tildes y borrado lógico.
/// </summary>
public class TipoServicioService
{
    public const string Collection = "tiposServicio";

    private readonly FirestoreDb _firestore;
    private readonly IAuditWriter? _auditWriter;

    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    public TipoServicioService(FirestoreDb firestore, IAuditWriter? auditWriter = null)
    {
        _firestore = firestore;
        _auditWriter = auditWriter;
    }

    public sealed record ResultadoAuditoriaTipoServicio(
        ResultadoOperacion Resultado,
        string TipoServicioId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        (_auditWriter ?? new AuditService(_firestore)).EnlistarEnTransaccion(transaction, log);
    }

    #region Operaciones de Consulta

    public async Task<List<TipoServicio>> ObtenerTodos(bool incluirInactivos = true)
    {
        var snapshot = await _firestore.Collection(Collection).GetSnapshotAsync();
        var lista = snapshot.Documents.Select(Mapear).OrderBy(x => x.Nombre).ToList();
        return lista.Where(x => incluirInactivos || x.Estado == "Activo").ToList();
    }

    private static TipoServicio Mapear(DocumentSnapshot doc) => new()
    {
        Id = doc.Id,
        Nombre = doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") ?? string.Empty : string.Empty,
        Estado = NombreCatalogoNormalizer.NormalizarEstado(
            doc.ContainsField("Estado") ? doc.GetValue<string>("Estado") : null)
    };

    /// <summary>
    /// Obtiene los nombres de los tipos de servicio activos.
    /// </summary>
    public async Task<List<string>> ObtenerTiposServicio()
    {
        try
        {
            return (await ObtenerTodos(incluirInactivos: false)).Select(x => x.Nombre).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de servicio: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<TipoServicio?> ObtenerPorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        return doc.Exists ? Mapear(doc) : null;
    }

    public async Task<TipoServicio?> ObtenerPorNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        var todos = await ObtenerTodos(incluirInactivos: true);
        return todos.FirstOrDefault(x => NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, nombre));
    }

    /// <summary>
    /// Obtiene el nombre de un tipo de servicio por su ID (incluye inactivos para auditoría/históricos).
    /// </summary>
    public async Task<string?> ObtenerNombrePorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        return doc.Exists && doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") : null;
    }

    /// <summary>
    /// Verifica si existe un tipo de servicio (activo o inactivo) con el nombre especificado,
    /// ignorando tildes, espacios y mayúsculas/minúsculas.
    /// </summary>
    public async Task<bool> ExisteTipoServicio(string nombre)
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

        return serviciosSnap.Documents.Any(d =>
        {
            var estado = d.ContainsField("Estado") ? d.GetValue<string>("Estado") : null;
            if (NombreCatalogoNormalizer.NormalizarEstado(estado) != "Activo") return false;
            var tipo = d.ContainsField("Tipo") ? d.GetValue<string>("Tipo") : null;
            return NombreCatalogoNormalizer.SonEquivalentes(tipo, nombre);
        });
    }

    #endregion

    #region Operaciones CRUD y Estado

    public Task<ResultadoAuditoriaTipoServicio> GuardarConAuditoria(TipoServicio tipo, bool crear, ActorAuditoria actor) =>
        GuardarConAuditoria(tipo, crear, actor.UserId, actor.UserEmail);

    public async Task<ResultadoAuditoriaTipoServicio> GuardarConAuditoria(
        TipoServicio tipo, bool crear, string userId = "", string userEmail = "")
    {
        tipo.Nombre = NombreCatalogoNormalizer.LimpiarNombre(tipo.Nombre);
        if (string.IsNullOrWhiteSpace(tipo.Nombre))
            return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("El nombre del tipo de servicio es obligatorio."));
        if (tipo.Nombre.Length < 3)
            return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("El nombre debe tener al menos 3 caracteres."));
        if (!crear && string.IsNullOrWhiteSpace(tipo.Id))
            return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("El tipo de servicio no existe."));

        return await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
        {
            var snapshotExistentes = await transaction.GetSnapshotAsync(_firestore.Collection(Collection));
            var existentes = snapshotExistentes.Documents.Select(Mapear).ToList();

            if (crear)
            {
                if (existentes.Any(x => NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, tipo.Nombre)))
                    return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("Ya existe un tipo de servicio con ese nombre."));

                var docRef = _firestore.Collection(Collection).Document();
                tipo.Id = docRef.Id;
                tipo.Estado = "Activo";

                var antes = new Dictionary<string, string>();
                var despues = AuditCampos.CapturarTipoServicio(tipo);

                transaction.Set(docRef, new Dictionary<string, object>
                {
                    ["Nombre"] = tipo.Nombre,
                    ["Estado"] = "Activo"
                });

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Creacion de tipo de servicio",
                    TargetId = tipo.Id,
                    TargetType = "TipoServicio",
                    NombreHistorico = tipo.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoServicio(
                    ResultadoOperacion.CrearExito("Tipo de servicio creado correctamente."),
                    tipo.Id,
                    antes,
                    despues);
            }
            else
            {
                var docRef = _firestore.Collection(Collection).Document(tipo.Id);
                var actualDoc = await transaction.GetSnapshotAsync(docRef);
                if (!actualDoc.Exists)
                    return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("El tipo de servicio no existe."));

                var actual = Mapear(actualDoc);

                if (existentes.Any(x => x.Id != tipo.Id && NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, tipo.Nombre)))
                    return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("Ya existe un tipo de servicio con ese nombre."));

                IReadOnlyList<DocumentSnapshot>? serviciosAfectados = null;
                if (!string.Equals(actual.Nombre, tipo.Nombre, StringComparison.Ordinal))
                {
                    var serviciosSnap = await transaction.GetSnapshotAsync(_firestore.Collection("servicios"));
                    serviciosAfectados = serviciosSnap.Documents
                        .Where(d => NombreCatalogoNormalizer.SonEquivalentes(
                            d.ContainsField("Tipo") ? d.GetValue<string>("Tipo") : null, actual.Nombre))
                        .ToList();
                }

                var actualizado = new TipoServicio
                {
                    Id = actual.Id,
                    Nombre = tipo.Nombre,
                    Estado = actual.Estado
                };

                var antes = AuditCampos.CapturarTipoServicio(actual);
                var despues = AuditCampos.CapturarTipoServicio(actualizado);

                if (!AuditCampos.HayCambios(antes, despues))
                {
                    return new ResultadoAuditoriaTipoServicio(
                        ResultadoOperacion.CrearExito("Tipo de servicio actualizado correctamente."),
                        actual.Id,
                        antes,
                        despues);
                }

                transaction.Update(docRef, new Dictionary<string, object>
                {
                    ["Nombre"] = actualizado.Nombre
                });

                if (serviciosAfectados != null)
                {
                    foreach (var s in serviciosAfectados)
                        transaction.Update(s.Reference, "Tipo", actualizado.Nombre);
                }

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Actualizacion de tipo de servicio",
                    TargetId = actual.Id,
                    TargetType = "TipoServicio",
                    NombreHistorico = actualizado.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoServicio(
                    ResultadoOperacion.CrearExito("Tipo de servicio actualizado correctamente."),
                    actual.Id,
                    antes,
                    despues);
            }
        });
    }

    public Task<ResultadoAuditoriaTipoServicio> CambiarEstadoConAuditoria(string id, string estado, ActorAuditoria actor) =>
        CambiarEstadoConAuditoria(id, estado, actor.UserId, actor.UserEmail);

    public async Task<ResultadoAuditoriaTipoServicio> CambiarEstadoConAuditoria(
        string id, string estado, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("El tipo de servicio no existe."));
        if (estado != "Activo" && estado != "Inactivo")
            return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("Estado inválido."));

        return await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
        {
            var docRef = _firestore.Collection(Collection).Document(id);
            var doc = await transaction.GetSnapshotAsync(docRef);
            if (!doc.Exists)
                return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError("El tipo de servicio no existe."));

            var actual = Mapear(doc);

            if (estado == "Inactivo" && await EstaEnUsoActivo(actual.Nombre, transaction))
            {
                return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearError(
                    "No se puede desactivar el tipo de servicio porque está en uso por servicios activos."));
            }

            if (string.Equals(actual.Estado, estado, StringComparison.OrdinalIgnoreCase))
                return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearExito("El estado ya coincide."), actual.Id);

            var antes = AuditCampos.CapturarTipoServicio(actual);
            actual.Estado = estado;
            var despues = AuditCampos.CapturarTipoServicio(actual);

            transaction.Update(docRef, "Estado", estado);

            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = estado == "Activo" ? "Reactivacion de tipo de servicio" : "Desactivacion de tipo de servicio",
                TargetId = actual.Id,
                TargetType = "TipoServicio",
                NombreHistorico = actual.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            var mensaje = estado == "Activo"
                ? "Tipo de servicio reactivado correctamente."
                : "Tipo de servicio desactivado correctamente.";
            return new ResultadoAuditoriaTipoServicio(ResultadoOperacion.CrearExito(mensaje), actual.Id, antes, despues);
        });
    }

    public async Task<string> CrearTipoServicio(string nombre)
    {
        var limpio = NombreCatalogoNormalizer.LimpiarNombre(nombre);
        var tipoRef = _firestore.Collection(Collection).Document();
        await tipoRef.SetAsync(new Dictionary<string, object>
        {
            ["Nombre"] = limpio,
            ["Estado"] = "Activo"
        });
        return tipoRef.Id;
    }

    public async Task<List<string>> EliminarTipoServicio(string nombre)
    {
        var tiposRef = _firestore.Collection(Collection);
        var snapshot = await tiposRef.WhereEqualTo("Nombre", nombre).GetSnapshotAsync();

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
