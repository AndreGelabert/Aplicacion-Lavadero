using System.Text.RegularExpressions;
using Firebase.Models;
using Firebase.Services;
using Firebase.Utils;
using Google.Cloud.Firestore;

/// <summary>
/// Servicio para la gestión de tipos de documento en Firestore.
/// Proporciona operaciones CRUD transaccionales, validación de unicidad sin tildes y borrado lógico.
/// </summary>
public class TipoDocumentoService
{
    public const string Collection = "tiposDocumento";

    private readonly FirestoreDb _firestore;
    private readonly IAuditWriter? _auditWriter;

    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    public TipoDocumentoService(FirestoreDb firestore, IAuditWriter? auditWriter = null)
    {
        _firestore = firestore;
        _auditWriter = auditWriter;
    }

    public sealed record ResultadoAuditoriaTipoDocumento(
        ResultadoOperacion Resultado,
        string TipoDocumentoId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        (_auditWriter ?? new AuditService(_firestore)).EnlistarEnTransaccion(transaction, log);
    }

    public async Task<List<TipoDocumento>> ObtenerTodos(bool incluirInactivos = true)
    {
        var snapshot = await _firestore.Collection(Collection).GetSnapshotAsync();
        var lista = snapshot.Documents.Select(Mapear).OrderBy(x => x.Nombre).ToList();
        return lista.Where(x => incluirInactivos || x.Estado == "Activo").ToList();
    }

    private static TipoDocumento Mapear(DocumentSnapshot doc) => new()
    {
        Id = doc.Id,
        Nombre = doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") ?? string.Empty : string.Empty,
        Formato = doc.ContainsField("Formato") ? doc.GetValue<string>("Formato") ?? string.Empty : string.Empty,
        Estado = NombreCatalogoNormalizer.NormalizarEstado(
            doc.ContainsField("Estado") ? doc.GetValue<string>("Estado") : null)
    };

    /// <summary>
    /// Obtiene los nombres de los tipos de documento activos.
    /// </summary>
    public async Task<List<string>> ObtenerTiposDocumento()
    {
        try
        {
            return (await ObtenerTodos(incluirInactivos: false)).Select(x => x.Nombre).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de documento: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Obtiene los tipos de documento con sus formatos (por defecto solo activos).
    /// </summary>
    public async Task<List<TipoDocumento>> ObtenerTiposDocumentoCompletos(bool incluirInactivos = false)
    {
        try
        {
            return await ObtenerTodos(incluirInactivos);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener tipos de documento: {ex.Message}");
            return new List<TipoDocumento>();
        }
    }

    public async Task<TipoDocumento?> ObtenerPorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await _firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        return doc.Exists ? Mapear(doc) : null;
    }

    /// <summary>
    /// Obtiene un tipo de documento por su nombre (incluye inactivos para lecturas históricas/clientes existentes).
    /// </summary>
    public async Task<TipoDocumento?> ObtenerTipoDocumentoPorNombre(string nombre)
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
            Console.WriteLine($"Error al obtener tipo de documento: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Verifica si existe un tipo de documento (activo o inactivo) con el nombre especificado,
    /// ignorando tildes, espacios y mayúsculas/minúsculas.
    /// </summary>
    public async Task<bool> ExisteTipoDocumento(string nombre)
    {
        var tiposExistentes = await ObtenerTodos(incluirInactivos: true);
        return tiposExistentes.Any(t => NombreCatalogoNormalizer.SonEquivalentes(t.Nombre, nombre));
    }

    public async Task<bool> EstaEnUsoActivo(string nombre, Transaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return false;

        var clientesSnap = transaction != null
            ? await transaction.GetSnapshotAsync(_firestore.Collection("clientes"))
            : await _firestore.Collection("clientes").GetSnapshotAsync();

        return clientesSnap.Documents.Any(d =>
            NombreCatalogoNormalizer.NormalizarEstado(d.ContainsField("Estado") ? d.GetValue<string>("Estado") : null) == "Activo" &&
            NombreCatalogoNormalizer.SonEquivalentes(d.ContainsField("TipoDocumento") ? d.GetValue<string>("TipoDocumento") : null, nombre));
    }

    public Task<ResultadoAuditoriaTipoDocumento> GuardarConAuditoria(TipoDocumento tipo, bool crear, ActorAuditoria actor) =>
        GuardarConAuditoria(tipo, crear, actor.UserId, actor.UserEmail);

    public async Task<ResultadoAuditoriaTipoDocumento> GuardarConAuditoria(
        TipoDocumento tipo, bool crear, string userId = "", string userEmail = "")
    {
        tipo.Nombre = NombreCatalogoNormalizer.LimpiarNombre(tipo.Nombre);
        tipo.Formato = (tipo.Formato ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(tipo.Nombre))
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("El nombre del tipo de documento es obligatorio."));
        if (tipo.Nombre.Length < 3)
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("El nombre debe tener al menos 3 caracteres."));
        if (string.IsNullOrWhiteSpace(tipo.Formato))
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("El formato del documento es obligatorio."));
        if (tipo.Formato.Length < 3 || !Regex.IsMatch(tipo.Formato, @"^[nl.\-]{3,}$"))
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError(
                "El formato solo puede contener 'n' (números), 'l' (letras), '.' y '-'. Mínimo 3 caracteres."));
        if (!crear && string.IsNullOrWhiteSpace(tipo.Id))
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("El tipo de documento no existe."));

        return await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
        {
            var snapshotExistentes = await transaction.GetSnapshotAsync(_firestore.Collection(Collection));
            var existentes = snapshotExistentes.Documents.Select(Mapear).ToList();

            if (crear)
            {
                if (existentes.Any(x => NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, tipo.Nombre)))
                    return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("Ya existe un tipo de documento con ese nombre."));

                var docRef = _firestore.Collection(Collection).Document();
                tipo.Id = docRef.Id;
                tipo.Estado = "Activo";

                var antes = new Dictionary<string, string>();
                var despues = AuditCampos.CapturarTipoDocumento(tipo);

                transaction.Set(docRef, new Dictionary<string, object>
                {
                    ["Nombre"] = tipo.Nombre,
                    ["Formato"] = tipo.Formato,
                    ["Estado"] = "Activo"
                });

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Creacion de tipo de documento",
                    TargetId = tipo.Id,
                    TargetType = "TipoDocumento",
                    NombreHistorico = tipo.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoDocumento(
                    ResultadoOperacion.CrearExito("Tipo de documento creado correctamente."),
                    tipo.Id,
                    antes,
                    despues);
            }
            else
            {
                var docRef = _firestore.Collection(Collection).Document(tipo.Id);
                var actualDoc = await transaction.GetSnapshotAsync(docRef);
                if (!actualDoc.Exists)
                    return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("El tipo de documento no existe."));

                var actual = Mapear(actualDoc);

                if (existentes.Any(x => x.Id != tipo.Id && NombreCatalogoNormalizer.SonEquivalentes(x.Nombre, tipo.Nombre)))
                    return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("Ya existe un tipo de documento con ese nombre."));

                IReadOnlyList<DocumentSnapshot>? clientesAfectados = null;
                if (!string.Equals(actual.Nombre, tipo.Nombre, StringComparison.Ordinal))
                {
                    var clientesSnap = await transaction.GetSnapshotAsync(_firestore.Collection("clientes"));
                    clientesAfectados = clientesSnap.Documents
                        .Where(d => NombreCatalogoNormalizer.SonEquivalentes(
                            d.ContainsField("TipoDocumento") ? d.GetValue<string>("TipoDocumento") : null, actual.Nombre))
                        .ToList();
                }

                var actualizado = new TipoDocumento
                {
                    Id = actual.Id,
                    Nombre = tipo.Nombre,
                    Formato = tipo.Formato,
                    Estado = actual.Estado
                };

                var antes = AuditCampos.CapturarTipoDocumento(actual);
                var despues = AuditCampos.CapturarTipoDocumento(actualizado);

                if (!AuditCampos.HayCambios(antes, despues))
                {
                    return new ResultadoAuditoriaTipoDocumento(
                        ResultadoOperacion.CrearExito("Tipo de documento actualizado correctamente."),
                        actual.Id,
                        antes,
                        despues);
                }

                transaction.Update(docRef, new Dictionary<string, object>
                {
                    ["Nombre"] = actualizado.Nombre,
                    ["Formato"] = actualizado.Formato
                });

                if (clientesAfectados != null)
                {
                    foreach (var c in clientesAfectados)
                        transaction.Update(c.Reference, "TipoDocumento", actualizado.Nombre);
                }

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Actualizacion de tipo de documento",
                    TargetId = actual.Id,
                    TargetType = "TipoDocumento",
                    NombreHistorico = actualizado.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoDocumento(
                    ResultadoOperacion.CrearExito("Tipo de documento actualizado correctamente."),
                    actual.Id,
                    antes,
                    despues);
            }
        });
    }

    public Task<ResultadoAuditoriaTipoDocumento> CambiarEstadoConAuditoria(string id, string estado, ActorAuditoria actor) =>
        CambiarEstadoConAuditoria(id, estado, actor.UserId, actor.UserEmail);

    public async Task<ResultadoAuditoriaTipoDocumento> CambiarEstadoConAuditoria(
        string id, string estado, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("El tipo de documento no existe."));
        if (estado != "Activo" && estado != "Inactivo")
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("Estado inválido."));

        return await InsumosTransacciones.EjecutarAsync(_firestore, async transaction =>
        {
            var docRef = _firestore.Collection(Collection).Document(id);
            var doc = await transaction.GetSnapshotAsync(docRef);
            if (!doc.Exists)
                return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError("El tipo de documento no existe."));

            var actual = Mapear(doc);

            if (estado == "Inactivo" && await EstaEnUsoActivo(actual.Nombre, transaction))
            {
                return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearError(
                    "No se puede desactivar el tipo de documento porque está en uso por clientes activos."));
            }

            if (string.Equals(actual.Estado, estado, StringComparison.OrdinalIgnoreCase))
                return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearExito("El estado ya coincide."), actual.Id);

            var antes = AuditCampos.CapturarTipoDocumento(actual);
            actual.Estado = estado;
            var despues = AuditCampos.CapturarTipoDocumento(actual);

            transaction.Update(docRef, "Estado", estado);

            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = estado == "Activo" ? "Reactivacion de tipo de documento" : "Desactivacion de tipo de documento",
                TargetId = actual.Id,
                TargetType = "TipoDocumento",
                NombreHistorico = actual.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            var mensaje = estado == "Activo"
                ? "Tipo de documento reactivado correctamente."
                : "Tipo de documento desactivado correctamente.";
            return new ResultadoAuditoriaTipoDocumento(ResultadoOperacion.CrearExito(mensaje), actual.Id, antes, despues);
        });
    }

    public async Task<string> CrearTipoDocumento(string nombre, string? formato = null)
    {
        var tipoRef = _firestore.Collection(Collection).Document();
        var tipo = new Dictionary<string, object>
        {
            ["Nombre"] = NombreCatalogoNormalizer.LimpiarNombre(nombre),
            ["Estado"] = "Activo"
        };

        if (!string.IsNullOrWhiteSpace(formato))
        {
            tipo["Formato"] = formato.Trim();
        }

        await tipoRef.SetAsync(tipo);
        return tipoRef.Id;
    }

    public async Task<List<TipoDocumento>> ObtenerParaAuditoria(string nombre)
    {
        var snapshot = await _firestore.Collection(Collection).WhereEqualTo("Nombre", nombre).GetSnapshotAsync();
        return snapshot.Documents.Select(Mapear).ToList();
    }

    public async Task<TipoDocumento> ObtenerParaAuditoriaPorId(string id)
    {
        var doc = await _firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        if (!doc.Exists) throw new InvalidOperationException("No se encontró el tipo de documento creado.");
        return Mapear(doc);
    }

    public async Task<List<string>> EliminarTipoDocumento(string nombre, IReadOnlySet<string>? idsCapturados = null)
    {
        var tiposRef = _firestore.Collection(Collection);
        var snapshot = await tiposRef.WhereEqualTo("Nombre", nombre).GetSnapshotAsync();

        if (idsCapturados != null && snapshot.Documents.Any(doc => !idsCapturados.Contains(doc.Id)))
            throw new InvalidOperationException("No se capturaron todos los tipos de documento a eliminar.");

        var eliminados = new List<string>();
        foreach (var documento in snapshot.Documents)
        {
            eliminados.Add(documento.Id);
            await documento.Reference.DeleteAsync();
        }
        return eliminados;
    }
}
