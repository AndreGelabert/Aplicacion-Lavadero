using Firebase.Models;
using Google.Cloud.Firestore;

namespace Firebase.Services;

/// <summary>
/// Servicio de catálogo backend para la gestión de tipos configurables de insumos (T11, S24).
/// Protege el tipo Herramienta contra eliminación y mutación, valida compatibilidad
/// de categorías y unidades resumen, y previene la alteración de tipos en uso.
/// </summary>
public sealed class TipoInsumoService(
    FirestoreDb firestore,
    IInterceptorAuditoriaCompras? interceptor = null,
    IAuditWriter? auditWriter = null)
{
    public const string Collection = "tiposInsumo";

    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        interceptor?.Interceptar(log);
        (auditWriter ?? new AuditService(firestore)).EnlistarEnTransaccion(transaction, log);
    }

    public sealed record ResultadoAuditoriaTipoInsumo(
        ResultadoOperacion Resultado,
        string TipoInsumoId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    public Task<ResultadoOperacion> Guardar(TipoInsumo tipo, bool crear, ActorAuditoria actor) =>
        Guardar(tipo, crear, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaTipoInsumo> GuardarConAuditoria(TipoInsumo tipo, bool crear, ActorAuditoria actor) =>
        GuardarConAuditoria(tipo, crear, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> Guardar(TipoInsumo tipo, bool crear, string userId = "", string userEmail = "") =>
        (await GuardarConAuditoria(tipo, crear, userId, userEmail)).Resultado;

    public Task<ResultadoOperacion> CambiarEstado(string id, string estado, ActorAuditoria actor) =>
        CambiarEstado(id, estado, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaTipoInsumo> CambiarEstadoConAuditoria(string id, string estado, ActorAuditoria actor) =>
        CambiarEstadoConAuditoria(id, estado, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> CambiarEstado(string id, string estado, string userId = "", string userEmail = "") =>
        (await CambiarEstadoConAuditoria(id, estado, userId, userEmail)).Resultado;

    public Task<ResultadoOperacion> Eliminar(string id, ActorAuditoria actor) =>
        Eliminar(id, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaTipoInsumo> EliminarConAuditoria(string id, ActorAuditoria actor) =>
        EliminarConAuditoria(id, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> Eliminar(string id, string userId = "", string userEmail = "") =>
        (await EliminarConAuditoria(id, userId, userEmail)).Resultado;

    public Task<TipoInsumo> AsegurarTipoHerramienta(ActorAuditoria actor) =>
        AsegurarTipoHerramienta(actor.UserId, actor.UserEmail);

    public async Task<List<TipoInsumo>> ObtenerTodos(bool incluirInactivos = true)
    {
        var snapshot = await firestore.Collection(Collection).GetSnapshotAsync();
        var lista = snapshot.Documents.Select(d =>
        {
            var t = d.ConvertTo<TipoInsumo>();
            t.Id = d.Id;
            return t;
        }).OrderBy(x => x.Nombre).ToList();

        return lista.Where(x => incluirInactivos || x.Estado == "Activo").ToList();
    }

    public async Task<TipoInsumo?> ObtenerPorId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var doc = await firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        if (!doc.Exists) return null;
        var tipo = doc.ConvertTo<TipoInsumo>();
        tipo.Id = doc.Id;
        return tipo;
    }

    public async Task<TipoInsumo?> ObtenerPorNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        var todos = await ObtenerTodos(true);
        var nomTrim = nombre.Trim();
        return todos.FirstOrDefault(x => string.Equals(x.Nombre, nomTrim, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> EstaEnUso(string tipoId, string tipoNombre, Transaction? transaction = null)
    {
        IReadOnlyList<DocumentSnapshot> insumosDocs;
        if (transaction != null)
        {
            var snap = await transaction.GetSnapshotAsync(firestore.Collection("insumos"));
            insumosDocs = snap.Documents;
        }
        else
        {
            var snap = await firestore.Collection("insumos").GetSnapshotAsync();
            insumosDocs = snap.Documents;
        }

        return insumosDocs.Any(d =>
        {
            var tipoIdVal = d.ContainsField("TipoId") ? d.GetValue<string>("TipoId") : null;
            if (!string.IsNullOrWhiteSpace(tipoIdVal))
            {
                // Si el insumo tiene TipoId, se vincula exclusivamente por TipoId (no por nombre de otro tipo)
                return string.Equals(tipoIdVal, tipoId, StringComparison.OrdinalIgnoreCase);
            }

            // Fallback legacy solo si no hay TipoId en el insumo
            var tipoVal = d.ContainsField("Tipo") ? d.GetValue<string>("Tipo") : null;
            return string.Equals(tipoVal, tipoNombre, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(tipoVal, tipoId, StringComparison.OrdinalIgnoreCase);
        });
    }

    public async Task<TipoInsumo> AsegurarTipoHerramienta(string userId = "", string userEmail = "")
    {
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(firestore.Collection(Collection));
            var herramientaDoc = snapshot.Documents.FirstOrDefault(d =>
                string.Equals(d.ContainsField("Nombre") ? d.GetValue<string>("Nombre") : null,
                    TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase));

            if (herramientaDoc != null)
            {
                var tipo = herramientaDoc.ConvertTo<TipoInsumo>();
                tipo.Id = herramientaDoc.Id;
                return tipo;
            }

            var docRef = firestore.Collection(Collection).Document();
            var nuevo = new TipoInsumo
            {
                Id = docRef.Id,
                Nombre = TipoInsumo.TipoHerramienta,
                Categoria = TipoInsumo.Categorias.Unidades,
                UnidadResumen = TipoInsumo.UnidadHerramienta,
                EsProtegido = true,
                EsReutilizable = true,
                Estado = "Activo"
            };

            var antes = new Dictionary<string, string>();
            var despues = AuditCampos.CapturarTipoInsumo(nuevo);

            transaction.Set(docRef, nuevo);
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = "Creacion de tipo de insumo protegido",
                TargetId = nuevo.Id,
                TargetType = "TipoInsumo",
                NombreHistorico = nuevo.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            return nuevo;
        });
    }

    public async Task<ResultadoAuditoriaTipoInsumo> GuardarConAuditoria(
        TipoInsumo tipo, bool crear, string userId = "", string userEmail = "")
    {
        tipo.Nombre = (tipo.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(tipo.Nombre))
            return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El nombre del tipo de insumo es obligatorio."));

        if (!TipoInsumo.EsCategoriaValida(tipo.Categoria))
            return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("La categoría de magnitud no es válida. Debe ser Volumen, Masa o Unidades."));

        tipo.Categoria = TipoInsumo.NormalizarCategoria(tipo.Categoria);
        tipo.UnidadResumen = (tipo.UnidadResumen ?? string.Empty).Trim();

        if (!TipoInsumo.EsUnidadValidaParaCategoria(tipo.Categoria, tipo.UnidadResumen))
            return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError(
                $"La unidad resumen '{tipo.UnidadResumen}' no es válida para la categoría '{tipo.Categoria}'."));

        if (!crear && string.IsNullOrWhiteSpace(tipo.Id))
            return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo no existe."));

        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var snapshotExistentes = await transaction.GetSnapshotAsync(firestore.Collection(Collection));
            var existentes = snapshotExistentes.Documents.Select(d =>
            {
                var t = d.ConvertTo<TipoInsumo>();
                t.Id = d.Id;
                return t;
            }).ToList();

            if (crear)
            {
                if (existentes.Any(x => string.Equals(x.Nombre, tipo.Nombre, StringComparison.OrdinalIgnoreCase)))
                    return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("Ya existe un tipo de insumo con ese nombre."));

                var docRef = firestore.Collection(Collection).Document();
                tipo.Id = docRef.Id;
                tipo.Estado = "Activo";
                tipo.EsProtegido = string.Equals(tipo.Nombre, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase);
                if (tipo.EsProtegido)
                {
                    if (tipo.Categoria != TipoInsumo.Categorias.Unidades ||
                        !string.Equals(tipo.UnidadResumen, TipoInsumo.UnidadHerramienta, StringComparison.OrdinalIgnoreCase))
                        return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("Herramienta requiere categoría Unidades y unidad resumen unidad."));
                    tipo.EsReutilizable = true;
                }
                else if (tipo.EsReutilizable && tipo.Categoria != TipoInsumo.Categorias.Unidades)
                {
                    return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("Los tipos de insumo reutilizables deben tener categoría Unidades."));
                }

                var antes = new Dictionary<string, string>();
                var despues = AuditCampos.CapturarTipoInsumo(tipo);

                transaction.Set(docRef, tipo);
                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Creacion de tipo de insumo",
                    TargetId = tipo.Id,
                    TargetType = "TipoInsumo",
                    NombreHistorico = tipo.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearExito("Tipo de insumo creado correctamente."), tipo.Id, antes, despues);
            }
            else
            {
                var docRef = firestore.Collection(Collection).Document(tipo.Id);
                var actualDoc = await transaction.GetSnapshotAsync(docRef);
                if (!actualDoc.Exists)
                    return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo no existe."));

                var actual = actualDoc.ConvertTo<TipoInsumo>();
                actual.Id = actualDoc.Id;

                // Protecciones de Herramienta
                var esHerramienta = actual.EsProtegido ||
                                    string.Equals(actual.Nombre, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase);

                if (esHerramienta)
                {
                    if (!string.Equals(actual.Nombre, tipo.Nombre, StringComparison.OrdinalIgnoreCase))
                        return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("No se puede modificar el nombre del tipo protegido Herramienta."));

                    if (!string.Equals(actual.Categoria, tipo.Categoria, StringComparison.OrdinalIgnoreCase))
                        return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("No se puede modificar la categoría fija del tipo protegido Herramienta."));

                    if (!string.Equals(actual.UnidadResumen, tipo.UnidadResumen, StringComparison.OrdinalIgnoreCase))
                        return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("No se puede modificar la unidad protegida del tipo protegido Herramienta."));

                    tipo.EsReutilizable = true;
                }
                else if (tipo.EsReutilizable && tipo.Categoria != TipoInsumo.Categorias.Unidades)
                {
                    return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("Los tipos de insumo reutilizables deben tener categoría Unidades."));
                }

                // Unicidad de nombre
                if (existentes.Any(x => x.Id != tipo.Id && string.Equals(x.Nombre, tipo.Nombre, StringComparison.OrdinalIgnoreCase)))
                    return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("Ya existe un tipo de insumo con ese nombre."));

                // Verificar si está en uso por insumos existentes
                var enUso = await EstaEnUso(actual.Id, actual.Nombre, transaction);
                if (enUso)
                {
                    if (!string.Equals(actual.Categoria, tipo.Categoria, StringComparison.OrdinalIgnoreCase))
                        return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError(
                            "No se puede modificar la categoría de un tipo de insumo que tiene productos asociados."));

                    if (!string.Equals(actual.UnidadResumen, tipo.UnidadResumen, StringComparison.OrdinalIgnoreCase))
                        return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError(
                            "No se puede modificar la unidad resumen de un tipo de insumo que tiene productos asociados."));
                }

                var tipoActualizado = new TipoInsumo
                {
                    Id = actual.Id,
                    Nombre = tipo.Nombre,
                    Categoria = tipo.Categoria,
                    UnidadResumen = tipo.UnidadResumen,
                    EsProtegido = actual.EsProtegido,
                    EsReutilizable = tipo.EsReutilizable,
                    Estado = actual.Estado
                };

                var antes = AuditCampos.CapturarTipoInsumo(actual);
                var despues = AuditCampos.CapturarTipoInsumo(tipoActualizado);

                if (!AuditCampos.HayCambios(antes, despues))
                {
                    return new ResultadoAuditoriaTipoInsumo(
                        ResultadoOperacion.CrearExito("Tipo de insumo actualizado correctamente."),
                        actual.Id,
                        antes,
                        despues);
                }

                transaction.Update(docRef, new Dictionary<string, object>
                {
                    ["Nombre"] = tipoActualizado.Nombre,
                    ["Categoria"] = tipoActualizado.Categoria,
                    ["UnidadResumen"] = tipoActualizado.UnidadResumen,
                    ["EsReutilizable"] = tipoActualizado.EsReutilizable
                });

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Actualizacion de tipo de insumo",
                    TargetId = actual.Id,
                    TargetType = "TipoInsumo",
                    NombreHistorico = tipoActualizado.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaTipoInsumo(
                    ResultadoOperacion.CrearExito("Tipo de insumo actualizado correctamente."),
                    actual.Id,
                    antes,
                    despues);
            }
        });
    }

    public async Task<ResultadoAuditoriaTipoInsumo> CambiarEstadoConAuditoria(
        string id, string estado, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo no existe."));

        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var docRef = firestore.Collection(Collection).Document(id);
            var doc = await transaction.GetSnapshotAsync(docRef);
            if (!doc.Exists)
                return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo no existe."));

            var actual = doc.ConvertTo<TipoInsumo>();
            actual.Id = doc.Id;

            var esHerramienta = actual.EsProtegido ||
                                string.Equals(actual.Nombre, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase);

            if (esHerramienta && estado == "Inactivo")
                return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo protegido Herramienta no puede ser desactivado."));

            if (estado == "Inactivo")
            {
                var enUso = await EstaEnUso(actual.Id, actual.Nombre, transaction);
                if (enUso)
                    return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError(
                        "No se puede desactivar el tipo de insumo porque existen insumos asociados a él."));
            }

            if (string.Equals(actual.Estado, estado, StringComparison.OrdinalIgnoreCase))
                return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearExito("El estado ya coincide."), actual.Id);

            var antes = AuditCampos.CapturarTipoInsumo(actual);
            actual.Estado = estado;
            var despues = AuditCampos.CapturarTipoInsumo(actual);

            transaction.Update(docRef, "Estado", estado);

            var accion = estado == "Activo" ? "Reactivacion de tipo de insumo" : "Desactivacion de tipo de insumo";
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = accion,
                TargetId = actual.Id,
                TargetType = "TipoInsumo",
                NombreHistorico = actual.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            var mensaje = estado == "Activo"
                ? "Tipo de insumo reactivado correctamente."
                : "Tipo de insumo desactivado correctamente.";
            return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearExito(mensaje), actual.Id, antes, despues);
        });
    }

    public async Task<ResultadoAuditoriaTipoInsumo> EliminarConAuditoria(
        string id, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo no existe."));

        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var docRef = firestore.Collection(Collection).Document(id);
            var doc = await transaction.GetSnapshotAsync(docRef);
            if (!doc.Exists)
                return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo no existe."));

            var actual = doc.ConvertTo<TipoInsumo>();
            actual.Id = doc.Id;

            var esHerramienta = actual.EsProtegido ||
                                string.Equals(actual.Nombre, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase);

            if (esHerramienta)
                return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError("El tipo de insumo protegido Herramienta no puede ser eliminado."));

            var enUso = await EstaEnUso(actual.Id, actual.Nombre, transaction);
            if (enUso)
                return new ResultadoAuditoriaTipoInsumo(ResultadoOperacion.CrearError(
                    "No se puede eliminar el tipo de insumo porque existen insumos asociados a él."));

            var antes = AuditCampos.CapturarTipoInsumo(actual);
            var despues = new Dictionary<string, string>();

            transaction.Delete(docRef);

            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = "Eliminacion de tipo de insumo",
                TargetId = actual.Id,
                TargetType = "TipoInsumo",
                NombreHistorico = actual.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            return new ResultadoAuditoriaTipoInsumo(
                ResultadoOperacion.CrearExito("Tipo de insumo eliminado correctamente."),
                actual.Id,
                antes,
                despues);
        });
    }
}
