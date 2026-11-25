using Firebase.Converters;
using Firebase.Models;
using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Firebase.Services;

public static class InsumosTransacciones
{
    public static readonly TransactionOptions ContencionAlta = TransactionOptions.ForRetrySettings(
        RetrySettings.FromExponentialBackoff(
            maxAttempts: 12,
            initialBackoff: TimeSpan.FromMilliseconds(250),
            maxBackoff: TimeSpan.FromSeconds(3),
            backoffMultiplier: 1.5,
            retryFilter: TransactionOptions.Default.RetrySettings.RetryFilter,
            backoffJitter: RetrySettings.RandomJitter));

    public static async Task<T> EjecutarAsync<T>(
        FirestoreDb firestore,
        Func<Transaction, Task<T>> callback,
        Func<FirestoreDb, Func<Transaction, Task<T>>, TransactionOptions, Task<T>>? runTransaction = null)
    {
        runTransaction ??= static (db, cb, opt) => db.RunTransactionAsync(cb, opt);
        for (var intento = 0; ; intento++)
        {
            try
            {
                return await runTransaction(firestore, callback, ContencionAlta);
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Aborted && intento < 4)
            {
                var delayMs = Random.Shared.Next(100, 350) * (intento + 1);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
            }
        }
    }

    public static Task EjecutarAsync(
        FirestoreDb firestore,
        Func<Transaction, Task> callback,
        Func<FirestoreDb, Func<Transaction, Task<object?>>, TransactionOptions, Task<object?>>? runTransaction = null) =>
        EjecutarAsync<object?>(firestore, async transaction =>
        {
            await callback(transaction);
            return null;
        }, runTransaction);
}

public sealed class InsumoService(
    FirestoreDb firestore,
    IInterceptorAuditoriaCompras? interceptor = null,
    StockService? stock = null)
{
    private const string Collection = "insumos";
    private static readonly DecimalConverter DecimalFirestore = new();

    public StockService? StockService { get; set; } = stock;
    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        interceptor?.Interceptar(log);
        var docRef = firestore.Collection("registros_auditoria").Document();
        log.DocId = docRef.Id;
        transaction.Create(docRef, log);
    }

    public sealed record ResultadoAuditoriaInsumo(
        ResultadoOperacion Resultado,
        string InsumoId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    public Task<ResultadoOperacion> Guardar(Insumo insumo, bool crear, ActorAuditoria actor) =>
        Guardar(insumo, crear, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaInsumo> GuardarConAuditoria(Insumo insumo, bool crear, ActorAuditoria actor) =>
        GuardarConAuditoria(insumo, crear, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> Guardar(Insumo insumo, bool crear, string userId = "", string userEmail = "") =>
        (await GuardarConAuditoria(insumo, crear, userId, userEmail)).Resultado;

    public Task<ResultadoOperacion> CambiarEstado(string id, string estado, ActorAuditoria actor) =>
        CambiarEstado(id, estado, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaInsumo> CambiarEstadoConAuditoria(string id, string estado, ActorAuditoria actor) =>
        CambiarEstadoConAuditoria(id, estado, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> CambiarEstado(string id, string estado, string userId = "", string userEmail = "") =>
        (await CambiarEstadoConAuditoria(id, estado, userId, userEmail)).Resultado;

    public async Task<List<Insumo>> ObtenerTodos(bool incluirInactivos = true)
    {
        var snapshot = await firestore.Collection(Collection).GetSnapshotAsync();
        var lista = snapshot.Documents.Select(d => d.ConvertTo<Insumo>()).ToList();

        var tiposSnapshot = await firestore.Collection(TipoInsumoService.Collection).GetSnapshotAsync();
        if (tiposSnapshot.Count > 0)
        {
            var tiposDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in tiposSnapshot.Documents)
            {
                if (doc.ContainsField("Nombre"))
                {
                    var nombre = doc.GetValue<string>("Nombre");
                    if (!string.IsNullOrWhiteSpace(nombre))
                    {
                        tiposDict[doc.Id] = nombre;
                    }
                }
            }

            foreach (var item in lista)
            {
                if (!string.IsNullOrWhiteSpace(item.TipoId) && tiposDict.TryGetValue(item.TipoId, out var nombreActualizado))
                {
                    item.Tipo = nombreActualizado;
                }
            }
        }

        return lista.OrderBy(x => x.Nombre)
            .Where(x => incluirInactivos || x.Estado == "Activo").ToList();
    }

    public async Task<HashSet<string>> ObtenerIdsHerramientas()
    {
        var insumos = await ObtenerTodos(true);
        return insumos
            .Where(i => string.Equals(i.Tipo, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<Insumo?> Obtener(string id)
    {
        var snapshot = await firestore.Collection(Collection).Document(id).GetSnapshotAsync();
        if (!snapshot.Exists) return null;
        var insumo = snapshot.ConvertTo<Insumo>();
        if (!string.IsNullOrWhiteSpace(insumo.TipoId))
        {
            var tipoDoc = await firestore.Collection(TipoInsumoService.Collection).Document(insumo.TipoId).GetSnapshotAsync();
            if (tipoDoc.Exists && tipoDoc.ContainsField("Nombre"))
            {
                var nombre = tipoDoc.GetValue<string>("Nombre");
                if (!string.IsNullOrWhiteSpace(nombre))
                    insumo.Tipo = nombre;
            }
        }
        return insumo;
    }

    public async Task<ResultadoAuditoriaInsumo> GuardarConAuditoria(Insumo insumo, bool crear, string userId = "", string userEmail = "")
    {
        insumo.Nombre = (insumo.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(insumo.Nombre))
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El nombre del insumo es obligatorio."));
        if (insumo.StockMinimo <= 0)
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El stock mínimo debe ser mayor a cero."));
        if (insumo.StockMaximo < 0 || insumo.CantidadReabastecimiento < 0)
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("Los niveles de stock no pueden ser negativos."));
        if (insumo.StockMaximo > 0 && insumo.StockMaximo <= insumo.StockMinimo)
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El stock máximo debe ser mayor al stock mínimo."));
        if (insumo.ReabastecimientoAutomatico && insumo.StockMaximo <= insumo.StockMinimo)
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El stock máximo debe ser mayor al stock mínimo para el reabastecimiento automático."));
        if (TipoInsumo.EsUnidadEntera(insumo.UnidadMedida))
        {
            if (TipoInsumo.TieneDecimales(insumo.StockMinimo) ||
                TipoInsumo.TieneDecimales(insumo.StockMaximo) ||
                TipoInsumo.TieneDecimales(insumo.CantidadReabastecimiento))
            {
                return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("Los insumos medidos en unidades no permiten valores decimales en los niveles de stock."));
            }
        }
        insumo.ProveedoresIds = (insumo.ProveedoresIds ?? new()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (insumo.ReabastecimientoAutomatico && insumo.ProveedoresIds.Count == 0)
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("Seleccione al menos un proveedor para el reabastecimiento automático."));

        if (!crear && string.IsNullOrWhiteSpace(insumo.Id))
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El insumo no existe."));

        var evaluarReabastecimiento = false;
        var resultado = await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var existentesSnapshot = await transaction.GetSnapshotAsync(firestore.Collection(Collection));
            var existentes = existentesSnapshot.Documents.Select(d => d.ConvertTo<Insumo>()).ToList();

            if (crear)
            {
                if (existentes.Any(x => string.Equals(x.Nombre, insumo.Nombre, StringComparison.OrdinalIgnoreCase)))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("Ya existe un insumo con ese nombre."));

                // Si no se proveyó ni TipoId ni Tipo, se conserva el valor legacy predeterminado "Quimico"
                if (string.IsNullOrWhiteSpace(insumo.TipoId) && string.IsNullOrWhiteSpace(insumo.Tipo))
                {
                    insumo.Tipo = "Quimico";
                }

                // Validación transaccional de TipoInsumo configurable y compatibilidad de unidad de medida (S24, S25, T11, T12)
                var tiposSnapshot = await transaction.GetSnapshotAsync(firestore.Collection(TipoInsumoService.Collection));
                var tipos = tiposSnapshot.Documents.Select(d =>
                {
                    var t = d.ConvertTo<TipoInsumo>();
                    t.Id = d.Id;
                    return t;
                }).ToList();

                TipoInsumo? tipoConfigurado = null;
                if (!string.IsNullOrWhiteSpace(insumo.TipoId))
                {
                    tipoConfigurado = tipos.FirstOrDefault(t => string.Equals(t.Id, insumo.TipoId, StringComparison.OrdinalIgnoreCase));
                    if (tipoConfigurado == null)
                        return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError($"El tipo de insumo especificado no existe."));

                    // Validación de identidades explícitas incompatibles:
                    // Si el llamador proporciona tanto TipoId como un Tipo explícito, deben coincidir.
                    // Si el llamador omite Tipo (vacío o null), se conserva y se completará con tipoConfigurado.Nombre.
                    if (!string.IsNullOrWhiteSpace(insumo.Tipo) &&
                        !string.Equals(insumo.Tipo.Trim(), tipoConfigurado.Nombre, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(insumo.Tipo.Trim(), tipoConfigurado.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError(
                            $"El tipo de insumo '{insumo.Tipo}' no coincide con el tipo especificado '{tipoConfigurado.Nombre}'."));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(insumo.Tipo))
                {
                    tipoConfigurado = tipos.FirstOrDefault(t => string.Equals(t.Nombre, insumo.Tipo.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                if (string.Equals(insumo.Tipo, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase) ||
                    (tipoConfigurado != null && string.Equals(tipoConfigurado.Nombre, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase)))
                {
                    if (tipoConfigurado == null)
                        return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El tipo Herramienta debe crearse explícitamente antes de asignarlo."));
                    if (!string.Equals(tipoConfigurado.Categoria, TipoInsumo.Categorias.Unidades, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(tipoConfigurado.UnidadResumen, TipoInsumo.UnidadHerramienta, StringComparison.OrdinalIgnoreCase))
                        return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El tipo Herramienta existente tiene una configuración incompatible."));
                }

                if (tipoConfigurado != null)
                {
                    if (tipoConfigurado.Estado != "Activo")
                        return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError($"El tipo de insumo '{tipoConfigurado.Nombre}' se encuentra inactivo y no puede ser asignado a nuevos insumos."));

                    if (!TipoInsumo.EsUnidadValidaParaCategoria(tipoConfigurado.Categoria, insumo.UnidadMedida))
                    {
                        var permitidas = string.Join(", ", TipoInsumo.ObtenerUnidadesPermitidas(tipoConfigurado.Categoria));
                        return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError(
                            $"La unidad de medida '{insumo.UnidadMedida}' no es compatible con la categoría '{tipoConfigurado.Categoria}' del tipo '{tipoConfigurado.Nombre}'. Unidades válidas: {permitidas}."));
                    }

                    insumo.TipoId = tipoConfigurado.Id;
                    insumo.Tipo = tipoConfigurado.Nombre;
                }
                else if (!string.IsNullOrWhiteSpace(insumo.TipoId))
                {
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El tipo de insumo especificado no existe."));
                }
                else if (!string.IsNullOrWhiteSpace(insumo.Tipo))
                {
                    var tiposExistentes = tipos.Select(t => t.Nombre).ToList();
                    var esLegacyConocido = string.Equals(insumo.Tipo, "Quimico", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(insumo.Tipo, "Fisico", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(insumo.Tipo, "Herramienta", StringComparison.OrdinalIgnoreCase);

                    if (!esLegacyConocido)
                    {
                        return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError($"El tipo de insumo '{insumo.Tipo}' no existe."));
                    }
                }

                var reference = firestore.Collection(Collection).Document();
                insumo.Id = reference.Id;
                insumo.StockActual = 0;
                insumo.Estado = "Activo";

                evaluarReabastecimiento = insumo.ReabastecimientoAutomatico && insumo.StockActual <= insumo.StockMinimo;

                var antes = new Dictionary<string, string>();
                var despues = AuditCampos.CapturarInsumo(insumo);

                transaction.Set(reference, insumo);
                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Creacion de insumo",
                    TargetId = insumo.Id,
                    TargetType = "Insumo",
                    NombreHistorico = insumo.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearExito("Insumo creado correctamente."), insumo.Id, antes, despues);
            }
            else
            {
                var reference = firestore.Collection(Collection).Document(insumo.Id);
                var actualSnapshot = await transaction.GetSnapshotAsync(reference);
                if (!actualSnapshot.Exists)
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El insumo no existe."));

                var actual = actualSnapshot.ConvertTo<Insumo>();
                if (await EstaUsadoEnLavadoActivo(insumo.Id, transaction))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("No se puede modificar el insumo porque está siendo utilizado por un lavado en ejecución."));

                // Validación transaccional de inmutabilidad de Tipo, TipoId y UnidadMedida (S4, S24)
                var tiposSnapshot = await transaction.GetSnapshotAsync(firestore.Collection(TipoInsumoService.Collection));
                var tipos = tiposSnapshot.Documents.Select(d =>
                {
                    var t = d.ConvertTo<TipoInsumo>();
                    t.Id = d.Id;
                    return t;
                }).ToList();

                TipoInsumo? tipoConfigurado = null;
                if (!string.IsNullOrWhiteSpace(actual.TipoId))
                {
                    tipoConfigurado = tipos.FirstOrDefault(t => string.Equals(t.Id, actual.TipoId, StringComparison.OrdinalIgnoreCase));
                }
                if (tipoConfigurado == null && !string.IsNullOrWhiteSpace(actual.Tipo))
                {
                    tipoConfigurado = tipos.FirstOrDefault(t => string.Equals(t.Nombre, actual.Tipo, StringComparison.OrdinalIgnoreCase));
                }

                // 1. Inmutabilidad de TipoId: rechazar cualquier intento de mutación o discrepancia en TipoId
                var tipoIdActual = actual.TipoId ?? string.Empty;
                var tipoIdNuevo = insumo.TipoId ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(tipoIdNuevo) && !string.Equals(tipoIdActual, tipoIdNuevo, StringComparison.OrdinalIgnoreCase))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El tipo y la unidad de medida no se pueden modificar."));

                // 2. Inmutabilidad de Tipo:
                // Si el insumo tiene TipoId, el tipo de insumo es el referenciado por TipoId (y su denominación vigente si fue renombrado).
                // Fallback legacy: si el insumo NO tiene TipoId, se valida contra el nombre persistido o contra el tipo hallado por nombre.
                bool coincideTipo;
                if (!string.IsNullOrWhiteSpace(actual.TipoId))
                {
                    coincideTipo = string.Equals(actual.Tipo, insumo.Tipo, StringComparison.OrdinalIgnoreCase) ||
                                   (tipoConfigurado != null && string.Equals(tipoConfigurado.Nombre, insumo.Tipo, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    coincideTipo = string.Equals(actual.Tipo, insumo.Tipo, StringComparison.OrdinalIgnoreCase);
                }
                if (!coincideTipo)
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El tipo y la unidad de medida no se pueden modificar."));

                // 3. Inmutabilidad de Unidad de Medida
                if (!string.Equals(actual.UnidadMedida, insumo.UnidadMedida, StringComparison.OrdinalIgnoreCase))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El tipo y la unidad de medida no se pueden modificar."));

                if (existentes.Any(x => x.Id != insumo.Id && string.Equals(x.Nombre, insumo.Nombre, StringComparison.OrdinalIgnoreCase)))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("Ya existe un insumo con ese nombre."));

                // Si es un insumo histórico sin TipoId, preservamos su Tipo y TipoId sin backfill automático ni mutación colateral
                var nombreTipoEfectivo = !string.IsNullOrWhiteSpace(actual.TipoId) && tipoConfigurado != null
                    ? tipoConfigurado.Nombre
                    : actual.Tipo;
                var tipoIdEfectivo = actual.TipoId;

                var antes = AuditCampos.CapturarInsumo(actual);
                var insumoActualizado = new Insumo
                {
                    Id = actual.Id,
                    Nombre = insumo.Nombre,
                    TipoId = tipoIdEfectivo,
                    Tipo = nombreTipoEfectivo,
                    UnidadMedida = actual.UnidadMedida,
                    StockActual = actual.StockActual,
                    StockMinimo = insumo.StockMinimo,
                    StockMaximo = insumo.StockMaximo,
                    CantidadReabastecimiento = insumo.CantidadReabastecimiento,
                    ReabastecimientoAutomatico = insumo.ReabastecimientoAutomatico,
                    ProveedoresIds = insumo.ProveedoresIds,
                    Estado = actual.Estado
                };
                var despues = AuditCampos.CapturarInsumo(insumoActualizado);

                if (!AuditCampos.HayCambios(antes, despues))
                {
                    return new ResultadoAuditoriaInsumo(
                        ResultadoOperacion.CrearExito("Insumo actualizado correctamente."),
                        insumo.Id,
                        antes,
                        despues);
                }

                insumo.StockActual = actual.StockActual;
                insumo.Estado = actual.Estado;

                var updates = new Dictionary<string, object>
                {
                    ["Nombre"] = insumoActualizado.Nombre,
                    ["Tipo"] = insumoActualizado.Tipo,
                    ["UnidadMedida"] = insumoActualizado.UnidadMedida,
                    ["StockMinimo"] = DecimalFirestore.ToFirestore(insumoActualizado.StockMinimo),
                    ["StockMaximo"] = DecimalFirestore.ToFirestore(insumoActualizado.StockMaximo),
                    ["CantidadReabastecimiento"] = DecimalFirestore.ToFirestore(insumoActualizado.CantidadReabastecimiento),
                    ["ReabastecimientoAutomatico"] = insumoActualizado.ReabastecimientoAutomatico,
                    ["ProveedoresIds"] = insumoActualizado.ProveedoresIds
                };
                if (!string.IsNullOrWhiteSpace(tipoIdEfectivo))
                {
                    updates["TipoId"] = tipoIdEfectivo;
                }

                transaction.Update(reference, updates);

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Actualizacion de insumo",
                    TargetId = insumo.Id,
                    TargetType = "Insumo",
                    NombreHistorico = insumoActualizado.Nombre,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                evaluarReabastecimiento = insumoActualizado.Estado == "Activo" &&
                                          insumoActualizado.ReabastecimientoAutomatico &&
                                          insumoActualizado.StockActual <= insumoActualizado.StockMinimo;

                return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearExito("Insumo actualizado correctamente."), insumo.Id, antes, despues);
            }
        });

        if (resultado.Resultado.EsExitoso && evaluarReabastecimiento)
        {
            var stockEfectivo = StockService ?? stock;
            if (stockEfectivo != null)
            {
                await stockEfectivo.EvaluarReabastecimiento(resultado.InsumoId, userId ?? string.Empty, userEmail ?? string.Empty);
            }
        }

        return resultado;
    }

    public async Task<ResultadoAuditoriaInsumo> CambiarEstadoConAuditoria(string id, string estado, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El insumo no existe."));

        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var reference = firestore.Collection(Collection).Document(id);
            var snapshot = await transaction.GetSnapshotAsync(reference);
            if (!snapshot.Exists)
                return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("El insumo no existe."));

            var actual = snapshot.ConvertTo<Insumo>();
            if (string.Equals(actual.Estado, estado, StringComparison.Ordinal))
            {
                var antesNoop = AuditCampos.CapturarInsumo(actual);
                return new ResultadoAuditoriaInsumo(
                    ResultadoOperacion.CrearExito(estado == "Activo" ? "Insumo reactivado." : "Insumo desactivado."),
                    id, antesNoop, antesNoop);
            }

            if (estado == "Inactivo")
            {
                if (await EstaUsadoEnLavadoActivo(id, transaction))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("No se puede desactivar el insumo porque está siendo utilizado por un lavado en ejecución."));

                var ordenes = await transaction.GetSnapshotAsync(firestore.Collection("ordenesCompra"));
                var solicitudes = await transaction.GetSnapshotAsync(firestore.Collection("solicitudesCotizacion").WhereEqualTo("Estado", "Pendiente"));

                if (solicitudes.Documents.Any(d => d.ConvertTo<SolicitudCotizacion>().Items.Any(i => i.InsumoId == id)))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("No se puede desactivar un insumo con una solicitud de cotización abierta."));

                if (ordenes.Documents.Select(d => d.ConvertTo<OrdenCompra>()).Any(o =>
                    o.Estado is "PendienteAprobacion" or "RevisionManual" or "Enviada" or "Confirmada" or "Despachada" &&
                    o.Items.Any(i => i.InsumoId == id)))
                    return new ResultadoAuditoriaInsumo(ResultadoOperacion.CrearError("No se puede desactivar un insumo con órdenes pendientes."));
            }

            var antes = AuditCampos.CapturarInsumo(actual);
            actual.Estado = estado;
            var despues = AuditCampos.CapturarInsumo(actual);

            transaction.Update(reference, "Estado", estado);

            var accion = estado == "Inactivo" ? "Desactivacion de insumo" : estado == "Activo" ? "Reactivacion de insumo" : $"Cambio de estado de insumo a {estado}";

            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = accion,
                TargetId = id,
                TargetType = "Insumo",
                NombreHistorico = actual.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            return new ResultadoAuditoriaInsumo(
                ResultadoOperacion.CrearExito(estado == "Activo" ? "Insumo reactivado." : "Insumo desactivado."),
                id, antes, despues);
        });
    }

    public async Task<bool> EstaUsadoEnLavadoActivo(string insumoId, Transaction? transaction = null)
    {
        var snapshot = transaction != null
            ? await transaction.GetSnapshotAsync(firestore.Collection("lavados"))
            : await firestore.Collection("lavados").GetSnapshotAsync();
        foreach (var documento in snapshot.Documents)
        {
            var datos = documento.ToDictionary();
            var estadoLavado = ObtenerTexto(datos, "Estado");
            if (estadoLavado is not ("Pendiente" or "EnProceso"))
                continue;

            if (datos.TryGetValue("Servicios", out var servicios) && ContieneInsumoEnServicios(servicios, insumoId))
                return true;
        }
        return false;
    }

    private static bool ContieneInsumoEnServicios(object? valor, string insumoId)
    {
        if (valor is not IEnumerable<object> elementos)
            return false;

        foreach (var elemento in elementos)
        {
            if (elemento is not IDictionary<string, object> servicio)
                continue;

            var estadoServicio = ObtenerTexto(servicio, "Estado");
            if (estadoServicio is not ("Pendiente" or "EnProceso"))
                continue;

            if (ContieneInsumoEnReceta(servicio, "Receta", insumoId) ||
                ContieneInsumoEnReceta(servicio, "Etapas", insumoId))
                return true;
        }
        return false;
    }

    private static bool ContieneInsumoEnReceta(
        IDictionary<string, object> datos, string campo, string insumoId)
    {
        if (!datos.TryGetValue(campo, out var valor) || valor is not IEnumerable<object> elementos)
            return false;

        foreach (var elemento in elementos)
        {
            if (elemento is IDictionary<string, object> item &&
                campo == "Receta" &&
                string.Equals(ObtenerTexto(item, "InsumoId"), insumoId, StringComparison.Ordinal))
                return true;

            if (campo == "Etapas" &&
                elemento is IDictionary<string, object> etapa &&
                ContieneInsumoEnReceta(etapa, "Receta", insumoId))
                return true;
        }
        return false;
    }

    private static string? ObtenerTexto(IDictionary<string, object> datos, string campo) =>
        datos.TryGetValue(campo, out var valor) ? valor?.ToString() : null;
}

public sealed class ProveedorService(FirestoreDb firestore, IInterceptorAuditoriaCompras? interceptor = null)
{
    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        interceptor?.Interceptar(log);
        var docRef = firestore.Collection("registros_auditoria").Document();
        log.DocId = docRef.Id;
        transaction.Create(docRef, log);
    }

    public sealed record ResultadoAuditoriaProveedor(
        ResultadoOperacion Resultado,
        string ProveedorId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    public Task<ResultadoOperacion> Guardar(Proveedor proveedor, bool crear, ActorAuditoria actor) =>
        Guardar(proveedor, crear, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaProveedor> GuardarConAuditoria(Proveedor proveedor, bool crear, ActorAuditoria actor) =>
        GuardarConAuditoria(proveedor, crear, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> Guardar(Proveedor proveedor, bool crear, string userId = "", string userEmail = "") =>
        (await GuardarConAuditoria(proveedor, crear, userId, userEmail)).Resultado;

    public Task<ResultadoOperacion> CambiarEstado(string id, string estado, ActorAuditoria actor) =>
        CambiarEstado(id, estado, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaProveedor> CambiarEstadoConAuditoria(string id, string estado, ActorAuditoria actor) =>
        CambiarEstadoConAuditoria(id, estado, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> CambiarEstado(string id, string estado, string userId = "", string userEmail = "") =>
        (await CambiarEstadoConAuditoria(id, estado, userId, userEmail)).Resultado;

    private static bool SolicitudActiva(string estado) => estado is "Pendiente" or "PendienteAprobacion" or "Respondida";
    private static bool OrdenActiva(string estado) => estado is "PendienteAprobacion" or "RevisionManual" or "Enviada" or "Confirmada" or "Despachada";

    public async Task<List<Proveedor>> ObtenerTodos(bool incluirInactivos = true)
    {
        var snapshot = await firestore.Collection("proveedores").GetSnapshotAsync();
        var solicitudes = await firestore.Collection("solicitudesCotizacion").GetSnapshotAsync();
        var ordenes = await firestore.Collection("ordenesCompra").GetSnapshotAsync();
        var bloqueados = solicitudes.Documents.Select(d => d.ConvertTo<SolicitudCotizacion>())
            .Where(s => SolicitudActiva(s.Estado)).Select(s => s.ProveedorId)
            .Concat(ordenes.Documents.Select(d => d.ConvertTo<OrdenCompra>()).Where(o => OrdenActiva(o.Estado)).Select(o => o.ProveedorId))
            .ToHashSet(StringComparer.Ordinal);
        var lista = snapshot.Documents.Select(d => d.ConvertTo<Proveedor>()).Where(x => incluirInactivos || x.Estado == "Activo").OrderBy(x => x.RazonSocial).ToList();
        foreach (var proveedor in lista) proveedor.EmailOrdenesBloqueado = bloqueados.Contains(proveedor.Id);
        return lista;
    }

    public async Task<Proveedor?> Obtener(string id)
    {
        var snapshot = await firestore.Collection("proveedores").Document(id).GetSnapshotAsync();
        if (!snapshot.Exists) return null;
        var proveedor = snapshot.ConvertTo<Proveedor>();
        var solicitudes = await firestore.Collection("solicitudesCotizacion").WhereEqualTo("ProveedorId", id).GetSnapshotAsync();
        var ordenes = await firestore.Collection("ordenesCompra").WhereEqualTo("ProveedorId", id).GetSnapshotAsync();
        proveedor.EmailOrdenesBloqueado = solicitudes.Documents.Any(d => SolicitudActiva(d.ConvertTo<SolicitudCotizacion>().Estado)) ||
            ordenes.Documents.Any(d => OrdenActiva(d.ConvertTo<OrdenCompra>().Estado));
        return proveedor;
    }

    public async Task<ResultadoAuditoriaProveedor> GuardarConAuditoria(Proveedor proveedor, bool crear, string userId = "", string userEmail = "")
    {
        proveedor.IdentificacionTributaria = (proveedor.IdentificacionTributaria ?? string.Empty).Trim();
        proveedor.RazonSocial = (proveedor.RazonSocial ?? string.Empty).Trim();
        proveedor.Contacto = (proveedor.Contacto ?? string.Empty).Trim();
        proveedor.EmailOrdenes = (proveedor.EmailOrdenes ?? string.Empty).Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(proveedor.Contacto, @"^[0-9]{10}$"))
            return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("El teléfono debe contener exactamente 10 dígitos."));
        if (!System.Text.RegularExpressions.Regex.IsMatch(proveedor.EmailOrdenes, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("Ingrese un correo electrónico válido"));

        if (!crear && string.IsNullOrWhiteSpace(proveedor.Id))
            return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("El proveedor no existe."));

        if (crear)
        {
            if (!Firebase.Utils.CuitHelper.EsValido(proveedor.IdentificacionTributaria, out var errorCuit))
                return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError(errorCuit));

            proveedor.IdentificacionTributaria = Firebase.Utils.CuitHelper.Formatear(proveedor.IdentificacionTributaria);
        }

        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var snapshotExistentes = await transaction.GetSnapshotAsync(firestore.Collection("proveedores"));
            var existentes = snapshotExistentes.Documents.Select(d => d.ConvertTo<Proveedor>()).ToList();

            if (crear)
            {
                if (existentes.Any(x => string.Equals(Firebase.Utils.CuitHelper.Limpiar(x.IdentificacionTributaria), Firebase.Utils.CuitHelper.Limpiar(proveedor.IdentificacionTributaria), StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(x.IdentificacionTributaria, proveedor.IdentificacionTributaria, StringComparison.OrdinalIgnoreCase)))
                    return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("Ya existe un proveedor con esa identificación fiscal."));

                if (existentes.Any(x => string.Equals(x.RazonSocial, proveedor.RazonSocial, StringComparison.OrdinalIgnoreCase)))
                    return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("Ya existe un proveedor con esa razón social."));

                var reference = firestore.Collection("proveedores").Document();
                proveedor.Id = reference.Id;
                proveedor.Estado = "Activo";

                var antes = new Dictionary<string, string>();
                var despues = AuditCampos.CapturarProveedor(proveedor);

                transaction.Set(reference, proveedor);
                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Creacion de proveedor",
                    TargetId = proveedor.Id,
                    TargetType = "Proveedor",
                    NombreHistorico = proveedor.RazonSocial,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearExito("Proveedor creado correctamente."), proveedor.Id, antes, despues);
            }
            else
            {
                var reference = firestore.Collection("proveedores").Document(proveedor.Id);
                var snapshot = await transaction.GetSnapshotAsync(reference);
                if (!snapshot.Exists)
                    return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("El proveedor no existe."));

                var actual = snapshot.ConvertTo<Proveedor>();
                if (!string.Equals(actual.EmailOrdenes, proveedor.EmailOrdenes, StringComparison.Ordinal))
                {
                    var solicitudes = await transaction.GetSnapshotAsync(firestore.Collection("solicitudesCotizacion").WhereEqualTo("ProveedorId", proveedor.Id));
                    var ordenes = await transaction.GetSnapshotAsync(firestore.Collection("ordenesCompra").WhereEqualTo("ProveedorId", proveedor.Id));
                    if (solicitudes.Documents.Any(d => SolicitudActiva(d.ConvertTo<SolicitudCotizacion>().Estado)) ||
                        ordenes.Documents.Any(d => OrdenActiva(d.ConvertTo<OrdenCompra>().Estado)))
                        return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("No se puede modificar el email de órdenes mientras existan cotizaciones u órdenes activas."));
                }
                if (!string.Equals(actual.IdentificacionTributaria, proveedor.IdentificacionTributaria, StringComparison.OrdinalIgnoreCase))
                    return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("La identificación fiscal no se puede modificar."));

                if (existentes.Any(x => x.Id != proveedor.Id && string.Equals(x.RazonSocial, proveedor.RazonSocial, StringComparison.OrdinalIgnoreCase)))
                    return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("Ya existe un proveedor con esa razón social."));

                var antes = AuditCampos.CapturarProveedor(actual);
                var proveedorActualizado = new Proveedor
                {
                    Id = actual.Id,
                    IdentificacionTributaria = actual.IdentificacionTributaria,
                    RazonSocial = proveedor.RazonSocial,
                    Contacto = proveedor.Contacto,
                    EmailOrdenes = proveedor.EmailOrdenes,
                    Estado = actual.Estado
                };
                var despues = AuditCampos.CapturarProveedor(proveedorActualizado);

                if (!AuditCampos.HayCambios(antes, despues))
                {
                    return new ResultadoAuditoriaProveedor(
                        ResultadoOperacion.CrearExito("Proveedor actualizado correctamente."),
                        proveedor.Id,
                        antes,
                        despues);
                }

                proveedor.Estado = actual.Estado;

                transaction.Update(reference, new Dictionary<string, object>
                {
                    ["RazonSocial"] = proveedorActualizado.RazonSocial,
                    ["Contacto"] = proveedorActualizado.Contacto,
                    ["EmailOrdenes"] = proveedorActualizado.EmailOrdenes
                });

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Actualizacion de proveedor",
                    TargetId = proveedor.Id,
                    TargetType = "Proveedor",
                    NombreHistorico = proveedorActualizado.RazonSocial,
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });

                return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearExito("Proveedor actualizado correctamente."), proveedor.Id, antes, despues);
            }
        });
    }

    public async Task<ResultadoAuditoriaProveedor> CambiarEstadoConAuditoria(string id, string estado, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("El proveedor no existe."));

        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var proveedorRef = firestore.Collection("proveedores").Document(id);
            var snapshot = await transaction.GetSnapshotAsync(proveedorRef);
            if (!snapshot.Exists)
                return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("El proveedor no existe."));

            var actual = snapshot.ConvertTo<Proveedor>();
            if (string.Equals(actual.Estado, estado, StringComparison.Ordinal))
            {
                var antesNoop = AuditCampos.CapturarProveedor(actual);
                return new ResultadoAuditoriaProveedor(
                    ResultadoOperacion.CrearExito("Estado del proveedor actualizado."),
                    id, antesNoop, antesNoop);
            }

            if (estado == "Inactivo")
            {
                var ordenes = await transaction.GetSnapshotAsync(firestore.Collection("ordenesCompra").WhereEqualTo("ProveedorId", id));
                if (ordenes.Documents.Any(d =>
                    d.ConvertTo<OrdenCompra>().Estado is "PendienteAprobacion" or "RevisionManual" or "Enviada" or "Confirmada" or "Despachada"))
                    return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearError("No se puede desactivar un proveedor con órdenes pendientes."));
            }

            var antes = AuditCampos.CapturarProveedor(actual);
            actual.Estado = estado;
            var despues = AuditCampos.CapturarProveedor(actual);

            transaction.Update(proveedorRef, "Estado", estado);

            var accion = estado == "Inactivo" ? "Desactivacion de proveedor" : estado == "Activo" ? "Reactivacion de proveedor" : $"Cambio de estado de proveedor a {estado}";

            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = accion,
                TargetId = id,
                TargetType = "Proveedor",
                NombreHistorico = actual.RazonSocial,
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });

            return new ResultadoAuditoriaProveedor(ResultadoOperacion.CrearExito("Estado del proveedor actualizado."), id, antes, despues);
        });
    }
}

public sealed class StockService(
    FirestoreDb firestore,
    IEmailSender emailSender,
    ProveedorService proveedores,
    ILogger<StockService> logger,
    IInterceptorAuditoriaCompras? interceptor = null)
{
    private static readonly DecimalConverter DecimalFirestore = new();

    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    public StockService(FirestoreDb firestore, IEmailSender emailSender, ProveedorService proveedores, ILogger<StockService> logger)
        : this(firestore, emailSender, proveedores, logger, null)
    {
    }

    public StockService(FirestoreDb firestore)
        : this(firestore, new NoopEmailSender(), new ProveedorService(firestore), NullLogger<StockService>.Instance, null)
    {
    }

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        interceptor?.Interceptar(log);
        var docRef = firestore.Collection("registros_auditoria").Document();
        log.DocId = docRef.Id;
        transaction.Create(docRef, log);
    }

    private sealed class NoopEmailSender : IEmailSender
    {
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoEnvioEmail(false, "Transporte no disponible en esta instancia."));
    }

    // Consulta informativa: un contador ausente equivale a cero según las transacciones existentes.
    // Un contador ilegible o una falla de lectura se representa como desconocido, nunca como cero.
    public async Task<Dictionary<string, decimal?>> ObtenerReservasInventario(IEnumerable<string> insumoIds)
    {
        var resultado = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        foreach (var id in insumoIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal))
        {
            try
            {
                var doc = await firestore.Collection("reservasHerramientasTotales").Document(id).GetSnapshotAsync();
                if (!doc.Exists) { resultado[id] = 0m; continue; }
                var cantidad = Convert.ToDecimal(doc.GetValue<double>("Cantidad"));
                resultado[id] = cantidad >= 0 ? cantidad : null;
            }
            catch (Exception ex) when (ex is Grpc.Core.RpcException or InvalidOperationException or ArgumentException or OverflowException)
            {
                logger.LogWarning(ex, "No se pudo consultar la reserva del insumo {InsumoId}", id);
                resultado[id] = null;
            }
        }
        return resultado;
    }

    internal record ReservaServicio(DocumentReference Reserva, DocumentReference Contador, decimal Cantidad, decimal Total, bool Existe, bool Liberada);
    internal record RecetaServicio(List<ConsumoReceta> Consumos, List<ReservaServicio> Reservas);

    internal sealed class DisponibilidadInsuficienteException(string mensaje) : InvalidOperationException(mensaje);

    internal async Task<RecetaServicio> LeerRecetaServicio(Transaction transaction, string referencia, IEnumerable<RecetaInsumo> receta, bool liberar, RecetaServicio? liberacionPrevia = null, IReadOnlyCollection<ConsumoReceta>? consumosPrevios = null, IReadOnlyDictionary<int, string>? seleccion = null, IReadOnlyDictionary<string, decimal>? reservasPendientes = null)
    {
        var consumos = new List<ConsumoReceta>();
        var reservas = new List<ReservaServicio>();
        var items = receta.ToList();
        if (!liberar && seleccion != null && seleccion.Keys.Any(i => i < 0 || i >= items.Count || items[i].Modalidad != "Tipo"))
            throw new InvalidOperationException("La selección contiene ítems inválidos.");
        var preparados = new List<ConsumoReceta>();
        for (var i = 0; i < items.Count; i++)
        {
            var r = items[i];
            var esTipo = string.Equals(r.Modalidad, "Tipo", StringComparison.Ordinal);
            var id = esTipo && !liberar ? (seleccion != null && seleccion.TryGetValue(i, out var elegido) ? elegido : "") : r.InsumoId;
            if (liberar && esTipo && string.IsNullOrWhiteSpace(id)) continue;
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Debe seleccionar un producto para cada ítem por tipo.");
            var snap = await transaction.GetSnapshotAsync(firestore.Collection("insumos").Document(id));
            if (!snap.Exists) throw new InvalidOperationException("La receta contiene un insumo inexistente.");
            var insumo = snap.ConvertTo<Insumo>();
            if (!liberar && (insumo.Estado != "Activo" || (esTipo && insumo.TipoId != r.TipoId)))
                throw new InvalidOperationException("El producto seleccionado no está activo o no pertenece al tipo requerido.");
            var dosis = StockPrecision.ExigirRepresentable(r.DosisTeorica);
            if (!string.IsNullOrWhiteSpace(r.UnidadMedida))
            {
                var conversion = new ConversionUnidadesService();
                var convertida = conversion.Convertir(dosis, r.UnidadMedida, insumo.UnidadMedida);
                if (conversion.Convertir(convertida, insumo.UnidadMedida, r.UnidadMedida) != dosis)
                    throw new InvalidOperationException("La conversión de la dosis pierde precisión.");
                dosis = StockPrecision.ExigirRepresentable(convertida);
            }
            if (dosis <= 0) throw new InvalidOperationException("La dosis debe ser mayor a cero.");
            preparados.Add(new ConsumoReceta(id, dosis));
            if (esTipo && !liberar) { r.InsumoId = id; r.NombreInsumo = insumo.Nombre; }
        }
        var agrupados = preparados.GroupBy(x => x.InsumoId, StringComparer.Ordinal).Select(g => new ConsumoReceta(g.Key, g.Aggregate(0m, (total, x) => StockPrecision.Sumar(total, x.Cantidad))));
        // Orden estable de lecturas para que recetas concurrentes no adquieran los mismos locks en orden inverso.
        foreach (var item in agrupados.OrderBy(x => x.InsumoId, StringComparer.Ordinal))
        {
            var insumoRef = firestore.Collection("insumos").Document(item.InsumoId);
            var insumoSnap = await transaction.GetSnapshotAsync(insumoRef);
            if (!insumoSnap.Exists) throw new InvalidOperationException("La receta contiene un insumo inexistente.");
            var insumo = insumoSnap.ConvertTo<Insumo>();
            var herramienta = await EsHerramienta(transaction, insumo);
            if (!herramienta) consumos.Add(item);
            var clave = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(referencia + "_" + item.InsumoId)));
            var reservaRef = firestore.Collection("reservasHerramientas").Document(clave);
            var contadorRef = firestore.Collection("reservasHerramientasTotales").Document(item.InsumoId);
            var reserva = await transaction.GetSnapshotAsync(reservaRef);
            var contador = await transaction.GetSnapshotAsync(contadorRef);
            var total = contador.Exists ? Convert.ToDecimal(contador.GetValue<double>("Cantidad")) : 0m;
            if (reserva.Exists && (reserva.GetValue<string>("InsumoId") != item.InsumoId || Convert.ToDecimal(reserva.GetValue<double>("Cantidad")) != item.Cantidad))
                throw new InvalidOperationException("La referencia ya corresponde a otra reserva.");
            var liberada = reserva.Exists && reserva.GetValue<bool>("Liberada");
            if (!liberar && liberada) throw new InvalidOperationException("La reserva ya fue liberada.");
            if (!liberar && liberacionPrevia != null)
                total -= liberacionPrevia.Reservas.Where(r => r.Contador.Id == item.InsumoId && r.Existe && !r.Liberada).Sum(r => r.Cantidad);
            if (!liberar && reservasPendientes != null && reservasPendientes.TryGetValue(item.InsumoId, out var pendiente))
                total = StockPrecision.Sumar(total, pendiente);
            var consumoPrevio = consumosPrevios?.FirstOrDefault(c => c.InsumoId == item.InsumoId)?.Cantidad ?? 0m;
            if (!liberar && !reserva.Exists && insumo.StockActual - total - consumoPrevio < item.Cantidad)
                throw new DisponibilidadInsuficienteException(herramienta ? "No hay herramientas suficientes disponibles." : $"No hay stock suficiente de '{insumo.Nombre}' para iniciar la operación.");
            if (liberar && reserva.Exists && !liberada && total < item.Cantidad)
                throw new InvalidOperationException("El contador de reservas es inconsistente.");
            reservas.Add(new ReservaServicio(reservaRef, contadorRef, item.Cantidad, total, reserva.Exists, liberada));
        }
        return new RecetaServicio(consumos, reservas);
    }

    internal static void EscribirReservasServicio(Transaction transaction, RecetaServicio receta, bool liberar)
    {
        foreach (var reserva in receta.Reservas)
        {
            if (liberar)
            {
                if (!reserva.Existe || reserva.Liberada) continue;
                transaction.Update(reserva.Contador, "Cantidad", DecimalFirestore.ToFirestore(StockPrecision.Restar(reserva.Total, reserva.Cantidad)));
                transaction.Update(reserva.Reserva, "Liberada", true);
            }
            else if (!reserva.Existe)
            {
                transaction.Set(reserva.Contador, new Dictionary<string, object> { ["Cantidad"] = DecimalFirestore.ToFirestore(StockPrecision.Sumar(reserva.Total, reserva.Cantidad)) });
                transaction.Create(reserva.Reserva, new Dictionary<string, object> { ["InsumoId"] = reserva.Contador.Id, ["Cantidad"] = DecimalFirestore.ToFirestore(reserva.Cantidad), ["Liberada"] = false });
            }
        }
    }

    // El contador por insumo serializa reservas y egresos sin alterar StockActual.
    public async Task<ResultadoOperacion> ReservarHerramienta(string referenciaId, string insumoId, decimal cantidad)
    {
        if (string.IsNullOrWhiteSpace(referenciaId) || string.IsNullOrWhiteSpace(insumoId) || cantidad <= 0)
            return ResultadoOperacion.CrearError("La referencia, herramienta y cantidad deben ser válidas.");
        var clave = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(referenciaId)));
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var reservaRef = firestore.Collection("reservasHerramientas").Document(clave);
            var reserva = await transaction.GetSnapshotAsync(reservaRef);
            if (reserva.Exists)
            {
                if (reserva.GetValue<string>("InsumoId") != insumoId ||
                    Convert.ToDecimal(reserva.GetValue<double>("Cantidad")) != cantidad)
                    return ResultadoOperacion.CrearError("La referencia ya corresponde a otra reserva.");
                if (reserva.GetValue<bool>("Liberada"))
                    return ResultadoOperacion.CrearError("La reserva ya fue liberada.");
                return ResultadoOperacion.CrearExito("Reserva ya registrada.");
            }
            var insumoRef = firestore.Collection("insumos").Document(insumoId);
            var contadorRef = firestore.Collection("reservasHerramientasTotales").Document(insumoId);
            var insumoSnap = await transaction.GetSnapshotAsync(insumoRef);
            var contador = await transaction.GetSnapshotAsync(contadorRef);
            if (!insumoSnap.Exists) return ResultadoOperacion.CrearError("El insumo no existe.");
            var insumo = insumoSnap.ConvertTo<Insumo>();
            if (!await EsHerramienta(transaction, insumo))
                return ResultadoOperacion.CrearError("El insumo no es una herramienta.");
            var reservado = contador.Exists ? Convert.ToDecimal(contador.GetValue<double>("Cantidad")) : 0m;
            if (insumo.StockActual - reservado < cantidad)
                return ResultadoOperacion.CrearError("No hay herramientas suficientes disponibles.");
            transaction.Set(contadorRef, new Dictionary<string, object> { ["Cantidad"] = DecimalFirestore.ToFirestore(reservado + cantidad) });
            transaction.Create(reservaRef, new Dictionary<string, object> {
                ["InsumoId"] = insumoId, ["Cantidad"] = DecimalFirestore.ToFirestore(cantidad), ["Liberada"] = false });
            return ResultadoOperacion.CrearExito("Herramienta reservada.");
        });
    }

    public async Task<ResultadoOperacion> LiberarHerramienta(string referenciaId, string insumoId)
    {
        if (string.IsNullOrWhiteSpace(referenciaId) || string.IsNullOrWhiteSpace(insumoId))
            return ResultadoOperacion.CrearError("La referencia y herramienta deben ser válidas.");
        var clave = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(referenciaId)));
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var reservaRef = firestore.Collection("reservasHerramientas").Document(clave);
            var reserva = await transaction.GetSnapshotAsync(reservaRef);
            if (!reserva.Exists || reserva.GetValue<string>("InsumoId") != insumoId)
                return ResultadoOperacion.CrearError("La reserva no existe.");
            if (reserva.GetValue<bool>("Liberada")) return ResultadoOperacion.CrearExito("Reserva ya liberada.");
            var contadorRef = firestore.Collection("reservasHerramientasTotales").Document(insumoId);
            var contador = await transaction.GetSnapshotAsync(contadorRef);
            var total = Convert.ToDecimal(contador.GetValue<double>("Cantidad"));
            var cantidad = Convert.ToDecimal(reserva.GetValue<double>("Cantidad"));
            if (total < cantidad) throw new InvalidOperationException("El contador de reservas es inconsistente.");
            transaction.Update(contadorRef, "Cantidad", DecimalFirestore.ToFirestore(total - cantidad));
            transaction.Update(reservaRef, "Liberada", true);
            return ResultadoOperacion.CrearExito("Herramienta liberada.");
        });
    }

    public async Task<List<string>> ObtenerAlertasPendientes()
    {
        var snapshot = await firestore.Collection("alertasStock")
            .WhereEqualTo("Estado", "Pendiente").GetSnapshotAsync();
        return snapshot.Documents
            .Select(x => x.ContainsField("Mensaje") ? x.GetValue<string>("Mensaje") : string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }
    public async Task ConsumirReceta(
        string referenciaId,
        IEnumerable<RecetaInsumo> receta,
        IReadOnlyDictionary<string, decimal>? cantidadesReales = null,
        string justificacion = "")
    {
        var consumos = PrepararConsumos(receta, cantidadesReales);
        await InsumosTransacciones.EjecutarAsync(firestore, transaction => ConsumirEnTransaccion(transaction, referenciaId, consumos, justificacion));
        await EvaluarConsumos(consumos.Select(x => x.InsumoId));
    }

    internal record ConsumoReceta(string InsumoId, decimal Cantidad);

    internal static List<ConsumoReceta> PrepararConsumos(IEnumerable<RecetaInsumo> receta, IReadOnlyDictionary<string, decimal>? cantidadesReales = null) =>
        receta.GroupBy(item => item.InsumoId, StringComparer.Ordinal).Select(group => new ConsumoReceta(
            group.Key,
            cantidadesReales != null && cantidadesReales.TryGetValue(group.Key, out var real)
                ? real : group.Sum(item => item.DosisTeorica)))
            .Where(x => x.Cantidad > 0).ToList();

    internal async Task<List<ConsumoReceta>> ConvertirConsumosReales(Transaction transaction, IEnumerable<RecetaInsumo> receta, IReadOnlyDictionary<string, decimal> reales)
    {
        var resultado = new List<ConsumoReceta>();
        foreach (var grupo in receta.GroupBy(r => r.InsumoId, StringComparer.Ordinal))
        {
            var items = grupo.ToList();
            if (!reales.TryGetValue(grupo.Key, out var cantidad)) continue;
            if (items.Select(r => r.UnidadMedida).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1)
                throw new InvalidOperationException("El consumo real requiere una única unidad de receta por producto.");
            var snapshot = await transaction.GetSnapshotAsync(firestore.Collection("insumos").Document(grupo.Key));
            if (!snapshot.Exists) throw new InvalidOperationException("La receta contiene un insumo inexistente.");
            var insumoPersistido = snapshot.ConvertTo<Insumo>();
            var unidad = items[0].UnidadMedida;
            StockPrecision.ExigirRepresentable(cantidad);
            if (!string.IsNullOrWhiteSpace(unidad))
            {
                var destino = insumoPersistido.UnidadMedida;
                var conversion = new ConversionUnidadesService();
                var convertida = conversion.Convertir(cantidad, unidad, destino);
                if (conversion.Convertir(convertida, destino, unidad) != cantidad)
                    throw new InvalidOperationException("La conversión del consumo pierde precisión.");
                cantidad = StockPrecision.ExigirRepresentable(convertida);
            }
            if (TipoInsumo.EsUnidadEntera(insumoPersistido.UnidadMedida) || TipoInsumo.EsUnidadEntera(unidad))
            {
                if (TipoInsumo.TieneDecimales(cantidad))
                    throw new InvalidOperationException($"El insumo '{insumoPersistido.Nombre}' medido en unidades no permite consumos decimales.");
            }
            resultado.Add(new ConsumoReceta(grupo.Key, cantidad));
        }
        return resultado;
    }

    private async Task<bool> EsHerramienta(Transaction transaction, Insumo insumo)
    {
        if (!string.IsNullOrWhiteSpace(insumo.TipoId))
        {
            var tipo = await transaction.GetSnapshotAsync(firestore.Collection(TipoInsumoService.Collection).Document(insumo.TipoId));
            if (tipo.Exists) return string.Equals(tipo.ConvertTo<TipoInsumo>().Nombre, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(insumo.Tipo, TipoInsumo.TipoHerramienta, StringComparison.OrdinalIgnoreCase);
    }

    internal async Task ConsumirMermaEnTransaccion(Transaction transaction, string referencia, IReadOnlyDictionary<string, decimal> cantidades, string motivo, RecetaServicio reservas)
    {
        var saldos = new List<(DocumentReference Documento, decimal Saldo, decimal Cantidad)>();
        foreach (var par in cantidades.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(par.Key) || par.Value < 0m)
                throw new InvalidOperationException("Las cantidades de merma deben ser válidas y no negativas.");
            StockPrecision.ExigirRepresentable(par.Value);
            if (par.Value == 0m) continue;
            var doc = firestore.Collection("insumos").Document(par.Key);
            var snapshot = await transaction.GetSnapshotAsync(doc);
            if (!snapshot.Exists) throw new InvalidOperationException("El insumo declarado no existe.");
            var insumo = snapshot.ConvertTo<Insumo>();
            if (await EsHerramienta(transaction, insumo))
                throw new InvalidOperationException("No se pueden consumir herramientas.");
            var contador = await transaction.GetSnapshotAsync(firestore.Collection("reservasHerramientasTotales").Document(par.Key));
            var total = contador.Exists ? Convert.ToDecimal(contador.GetValue<double>("Cantidad")) : 0m;
            var propia = reservas.Reservas.Where(r => r.Contador.Id == par.Key && r.Existe && !r.Liberada)
                .Aggregate(0m, (sum, r) => StockPrecision.Sumar(sum, r.Cantidad));
            if (propia > total) throw new InvalidOperationException("El contador de reservas es inconsistente.");
            var saldo = StockPrecision.Restar(insumo.StockActual, par.Value);
            if (saldo < StockPrecision.Restar(total, propia)) throw new InvalidOperationException($"Stock insuficiente para el insumo '{insumo.Nombre}'.");
            saldos.Add((doc, saldo, par.Value));
        }
        foreach (var item in saldos)
        {
            transaction.Update(item.Documento, "StockActual", DecimalFirestore.ToFirestore(item.Saldo));
            var doc = firestore.Collection("movimientosStock").Document();
            transaction.Create(doc, new MovimientoStock { Id = doc.Id, InsumoId = item.Documento.Id, Tipo = nameof(TipoMovimientoStock.MermaCancelacion), Cantidad = -item.Cantidad, SaldoResultante = item.Saldo, ReferenciaId = referencia, Justificacion = motivo });
        }
    }

    internal async Task EvaluarConsumos(IEnumerable<string> ids)
    {
        foreach (var id in ids) await EvaluarReabastecimiento(id);
    }

    internal async Task ConsumirEnTransaccion(Transaction transaction, string referenciaId, List<ConsumoReceta> consumos, string justificacion, RecetaServicio? reservaPropia = null)
    {
        var markerRef = firestore.Collection("consumosEtapa").Document(referenciaId);
        if ((await transaction.GetSnapshotAsync(markerRef)).Exists) return;
        var saldos = new List<(string Id, decimal Saldo, decimal Cantidad)>();
        foreach (var consumo in consumos)
        {
            var refInsumo = firestore.Collection("insumos").Document(consumo.InsumoId);
            var snapshot = await transaction.GetSnapshotAsync(refInsumo);
            if (!snapshot.Exists) throw new InvalidOperationException("La receta contiene un insumo inexistente.");
            var insumo = snapshot.ConvertTo<Insumo>();
            if (await EsHerramienta(transaction, insumo))
                throw new InvalidOperationException("No se pueden consumir herramientas.");
            var contador = await transaction.GetSnapshotAsync(firestore.Collection("reservasHerramientasTotales").Document(consumo.InsumoId));
            var reservado = contador.Exists ? Convert.ToDecimal(contador.GetValue<double>("Cantidad")) : 0m;
            var propio = reservaPropia?.Reservas.Where(r => r.Contador.Id == consumo.InsumoId && r.Existe && !r.Liberada).Sum(r => r.Cantidad) ?? 0m;
            if (reservado < propio) throw new InvalidOperationException("El contador de reservas es inconsistente.");
            var saldo = StockPrecision.Restar(insumo.StockActual, consumo.Cantidad);
            if (saldo < StockPrecision.Restar(reservado, propio)) throw new InvalidOperationException($"Stock insuficiente para el insumo '{insumo.Nombre}'.");
            saldos.Add((consumo.InsumoId, saldo, consumo.Cantidad));
        }
        foreach (var saldo in saldos)
        {
            var refInsumo = firestore.Collection("insumos").Document(saldo.Id);
            transaction.Update(refInsumo, "StockActual", DecimalFirestore.ToFirestore(saldo.Saldo));
            var movimiento = firestore.Collection("movimientosStock").Document();
            transaction.Create(movimiento, new MovimientoStock
            {
                Id = movimiento.Id, InsumoId = saldo.Id, Tipo = nameof(TipoMovimientoStock.ConsumoEtapa),
                Cantidad = -saldo.Cantidad, SaldoResultante = saldo.Saldo, ReferenciaId = referenciaId, Justificacion = justificacion
            });
        }
        transaction.Create(markerRef, new Dictionary<string, object> { ["Fecha"] = Timestamp.GetCurrentTimestamp() });
    }

    public async Task<ResultadoOperacion> ValidarDisponibilidad(IEnumerable<RecetaInsumo> receta)
    {
        var cantidades = receta.Where(x => x.DosisTeorica > 0)
            .GroupBy(x => x.InsumoId)
            .Select(x => new { InsumoId = x.Key, Cantidad = x.Sum(y => y.DosisTeorica) });
        foreach (var item in cantidades)
        {
            var snapshot = await firestore.Collection("insumos").Document(item.InsumoId).GetSnapshotAsync();
            if (!snapshot.Exists)
                return ResultadoOperacion.CrearError("La receta contiene un insumo inexistente.");
            var insumo = snapshot.ConvertTo<Insumo>();
            if (insumo.StockActual < item.Cantidad)
                return ResultadoOperacion.CrearError($"No hay stock suficiente de '{insumo.Nombre}' para iniciar la operación.");
        }
        return ResultadoOperacion.CrearExito("Stock suficiente.");
    }

    public async Task Consumir(string referenciaId, string insumoId, decimal cantidad, string justificacion = "")
    {
        if (cantidad <= 0) return;
        var evaluarReabastecimiento = false;
        await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var markerRef = firestore.Collection("consumosEtapa").Document(referenciaId + "_" + insumoId);
            var marker = await transaction.GetSnapshotAsync(markerRef);
            if (marker.Exists) return;
            var insumoRef = firestore.Collection("insumos").Document(insumoId);
            var snapshot = await transaction.GetSnapshotAsync(insumoRef);
            if (!snapshot.Exists) throw new InvalidOperationException("El insumo de la receta no existe.");
            var insumo = snapshot.ConvertTo<Insumo>();
            if (!string.Equals(insumo.Tipo, "Quimico", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(insumo.Tipo, "Fisico", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Solo se pueden consumir insumos químicos o físicos.");
            var contador = await transaction.GetSnapshotAsync(firestore.Collection("reservasHerramientasTotales").Document(insumoId));
            var reservado = contador.Exists ? Convert.ToDecimal(contador.GetValue<double>("Cantidad")) : 0m;
            var saldo = insumo.StockActual - cantidad;
            if (saldo < reservado) throw new InvalidOperationException($"Stock insuficiente para el insumo '{insumo.Nombre}'.");
            transaction.Update(insumoRef, "StockActual", DecimalFirestore.ToFirestore(saldo));
            var movimiento = new MovimientoStock
            {
                Id = firestore.Collection("movimientosStock").Document().Id,
                InsumoId = insumoId,
                Tipo = nameof(TipoMovimientoStock.ConsumoEtapa),
                Cantidad = -cantidad,
                SaldoResultante = saldo,
                ReferenciaId = referenciaId,
                Justificacion = justificacion
            };
            transaction.Create(firestore.Collection("movimientosStock").Document(movimiento.Id), movimiento);
            transaction.Create(markerRef, new Dictionary<string, object> { ["Fecha"] = Timestamp.GetCurrentTimestamp() });
            if (insumo.Estado == "Activo" && insumo.ReabastecimientoAutomatico && saldo <= insumo.StockMinimo)
                evaluarReabastecimiento = true;
        });
        if (evaluarReabastecimiento)
            await EvaluarReabastecimiento(insumoId);
    }

    public sealed record ResultadoAuditoriaStock(
        ResultadoOperacion Resultado,
        string InsumoId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    public Task<ResultadoOperacion> Ajustar(string insumoId, decimal cantidad, bool ingreso, string justificacion, ActorAuditoria actor) =>
        Ajustar(insumoId, cantidad, ingreso, justificacion, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaStock> AjustarConAuditoria(string insumoId, decimal cantidad, bool ingreso, string justificacion, ActorAuditoria actor) =>
        AjustarConAuditoria(insumoId, cantidad, ingreso, justificacion, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> Ajustar(string insumoId, decimal cantidad, bool ingreso, string justificacion, string usuarioId) =>
        Ajustar(insumoId, cantidad, ingreso, justificacion, usuarioId, string.Empty);

    public async Task<ResultadoOperacion> Ajustar(string insumoId, decimal cantidad, bool ingreso, string justificacion, string userId, string userEmail) =>
        (await AjustarConAuditoria(insumoId, cantidad, ingreso, justificacion, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaStock> AjustarConAuditoria(string insumoId, decimal cantidad, bool ingreso, string justificacion, string userId = "", string userEmail = "")
    {
        if (cantidad <= 0)
            return new ResultadoAuditoriaStock(ResultadoOperacion.CrearError("La cantidad debe ser mayor a cero."));
        if (string.IsNullOrWhiteSpace(justificacion))
            return new ResultadoAuditoriaStock(ResultadoOperacion.CrearError("La justificación es obligatoria."));

        var evaluarReabastecimiento = false;
        var resultado = await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var insumoRef = firestore.Collection("insumos").Document(insumoId);
            var insumoSnapshot = await transaction.GetSnapshotAsync(insumoRef);
            if (!insumoSnapshot.Exists)
                return new ResultadoAuditoriaStock(ResultadoOperacion.CrearError("El insumo no existe."));

            var insumo = insumoSnapshot.ConvertTo<Insumo>();
            if (TipoInsumo.EsUnidadEntera(insumo.UnidadMedida) && TipoInsumo.TieneDecimales(cantidad))
                return new ResultadoAuditoriaStock(ResultadoOperacion.CrearError("Los insumos medidos en unidades no permiten ajustes con cantidades decimales."));
            var delta = ingreso ? cantidad : -cantidad;
            var saldo = insumo.StockActual + delta;

            if (!ingreso && saldo < 0)
                return new ResultadoAuditoriaStock(ResultadoOperacion.CrearError("El egreso supera el stock disponible."));

            if (!ingreso)
            {
                var contador = await transaction.GetSnapshotAsync(firestore.Collection("reservasHerramientasTotales").Document(insumoId));
                var reservado = contador.Exists ? Convert.ToDecimal(contador.GetValue<double>("Cantidad")) : 0m;
                if (saldo < reservado)
                    return new ResultadoAuditoriaStock(ResultadoOperacion.CrearError("El egreso supera el stock disponible."));

                if (insumo.Estado == "Activo" && insumo.ReabastecimientoAutomatico && saldo <= insumo.StockMinimo)
                    evaluarReabastecimiento = true;
            }

            var antes = AuditCampos.CapturarInsumo(insumo);
            transaction.Update(insumoRef, "StockActual", DecimalFirestore.ToFirestore(saldo));

            var movimiento = new MovimientoStock
            {
                Id = firestore.Collection("movimientosStock").Document().Id,
                InsumoId = insumoId,
                Tipo = ingreso ? nameof(TipoMovimientoStock.IngresoManual) : nameof(TipoMovimientoStock.EgresoManual),
                Cantidad = delta,
                SaldoResultante = saldo,
                UsuarioId = userId ?? string.Empty,
                Justificacion = justificacion.Trim()
            };
            transaction.Create(firestore.Collection("movimientosStock").Document(movimiento.Id), movimiento);

            var insumoDespues = new Insumo
            {
                Id = insumo.Id,
                Nombre = insumo.Nombre,
                Tipo = insumo.Tipo,
                UnidadMedida = insumo.UnidadMedida,
                StockActual = saldo,
                StockMinimo = insumo.StockMinimo,
                StockMaximo = insumo.StockMaximo,
                CantidadReabastecimiento = insumo.CantidadReabastecimiento,
                ReabastecimientoAutomatico = insumo.ReabastecimientoAutomatico,
                ProveedoresIds = insumo.ProveedoresIds,
                Estado = insumo.Estado
            };
            var despues = AuditCampos.CapturarInsumo(insumoDespues);

            var ahora = DateTime.UtcNow;
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId ?? string.Empty,
                UserEmail = userEmail ?? string.Empty,
                Action = ingreso ? "Ingreso manual de stock" : "Egreso manual de stock",
                TargetId = insumoId,
                TargetType = "Insumo",
                NombreHistorico = insumo.Nombre,
                Antes = antes,
                Despues = despues,
                Timestamp = new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc)
            });

            return new ResultadoAuditoriaStock(
                ResultadoOperacion.CrearExito(ingreso ? "Ingreso registrado correctamente." : "Egreso registrado correctamente."),
                insumoId,
                antes,
                despues);
        });

        if (resultado.Resultado.EsExitoso && evaluarReabastecimiento)
            await EvaluarReabastecimiento(insumoId, userId ?? string.Empty, userEmail ?? string.Empty);

        return resultado;
    }

    private Query ConstruirQueryMovimientos(
        string? insumoId,
        string? tipo,
        DateTime? desde,
        DateTime? hasta)
    {
        Query query = firestore.Collection("movimientosStock");
        if (!string.IsNullOrWhiteSpace(insumoId))
        {
            query = query.WhereEqualTo("InsumoId", insumoId);
        }
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            query = query.WhereEqualTo("Tipo", tipo);
        }
        if (desde.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(desde.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.WhereGreaterThanOrEqualTo("Fecha", startUtc);
        }
        if (hasta.HasValue)
        {
            var endUtcExclusive = DateTime.SpecifyKind(hasta.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.WhereLessThan("Fecha", endUtcExclusive);
        }
        return query;
    }

    private static MovimientoStock MapDocumentToMovimiento(DocumentSnapshot d)
    {
        var m = d.ConvertTo<MovimientoStock>();
        if (string.IsNullOrEmpty(m.Id)) m.Id = d.Id;
        return m;
    }

    private static string CrearCursorKardex(string direccion, DocumentSnapshot documento)
    {
        return KardexCursor.Crear(direccion, documento.GetValue<Timestamp>("Fecha").ToDateTime(), documento.Id);
    }

    // Each partition has at most 25 disjunctions; the bounded page merge avoids a collection scan.
    public async Task<ResultadoKardexPaginado> ObtenerKardexMultifiltro(
        IEnumerable<string> insumos, IEnumerable<string> tipos, DateTime? desde = null,
        DateTime? hasta = null, int pageSize = 20, string? sortOrder = "desc", string? cursor = null)
    {
        var ids = insumos.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        var kinds = tipos.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        KardexCursor? decoded = null;
        if (!string.IsNullOrWhiteSpace(cursor) && !KardexCursor.TratarDeDecodificar(cursor, out decoded, out var error))
            throw new ArgumentException("Cursor no válido: " + error, nameof(cursor));
        pageSize = pageSize > 0 ? pageSize : 20;
        bool asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        bool previous = decoded?.Direccion == "prev";
        var idChunks = ids.Length == 0 ? new[] { Array.Empty<string>() } : ids.Chunk(5).ToArray();
        var typeChunks = kinds.Length == 0 ? new[] { Array.Empty<string>() } : kinds.Chunk(5).ToArray();
        var documents = new List<DocumentSnapshot>();
        foreach (var idChunk in idChunks)
        foreach (var typeChunk in typeChunks)
        {
            Query q = ConstruirQueryMovimientos(null, null, desde, hasta);
            if (idChunk.Length == 1) q = q.WhereEqualTo("InsumoId", idChunk[0]);
            else if (idChunk.Length > 1) q = q.WhereIn("InsumoId", idChunk);
            if (typeChunk.Length == 1) q = q.WhereEqualTo("Tipo", typeChunk[0]);
            else if (typeChunk.Length > 1) q = q.WhereIn("Tipo", typeChunk);
            q = asc ? q.OrderBy("Fecha").OrderBy(FieldPath.DocumentId) : q.OrderByDescending("Fecha").OrderByDescending(FieldPath.DocumentId);
            if (decoded != null) q = previous ? q.EndBefore(decoded.FechaUtc, decoded.DocumentId) : q.StartAfter(decoded.FechaUtc, decoded.DocumentId);
            q = previous ? q.LimitToLast(pageSize + 1) : q.Limit(pageSize + 1);
            documents.AddRange((await q.GetSnapshotAsync()).Documents);
        }
        var ordered = asc
            ? documents.OrderBy(d => d.GetValue<Timestamp>("Fecha")).ThenBy(d => d.Id, StringComparer.Ordinal).ToList()
            : documents.OrderByDescending(d => d.GetValue<Timestamp>("Fecha")).ThenByDescending(d => d.Id, StringComparer.Ordinal).ToList();
        bool extra = ordered.Count > pageSize;
        var selected = previous ? ordered.TakeLast(pageSize).ToList() : ordered.Take(pageSize).ToList();
        bool hasPrevious = previous ? extra : decoded != null;
        bool hasNext = previous ? decoded != null : extra;
        return new ResultadoKardexPaginado
        {
            Movimientos = selected.Select(MapDocumentToMovimiento).ToList(),
            TienePaginaAnterior = hasPrevious, TienePaginaSiguiente = hasNext,
            CursorAnterior = hasPrevious && selected.Count > 0 ? CrearCursorKardex("prev", selected.First()) : null,
            CursorSiguiente = hasNext && selected.Count > 0 ? CrearCursorKardex("next", selected.Last()) : null,
            PaginaActual = 1, TotalPaginas = 0, TotalRegistros = 0
        };
    }

    public async Task<ResultadoKardexPaginado> ObtenerKardexPaginado(
        string? insumoId = null,
        string? tipo = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        int page = 1,
        int pageSize = 20,
        string? sortOrder = "desc",
        string? cursor = null)
    {
        KardexCursor? decodedCursor = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!KardexCursor.TratarDeDecodificar(cursor, out decodedCursor, out var error))
                throw new ArgumentException("Cursor no válido: " + error, nameof(cursor));
        }

        pageSize = pageSize > 0 ? pageSize : 20;
        bool asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var baseQuery = ConstruirQueryMovimientos(insumoId, tipo, desde, hasta);

        if (decodedCursor == null)
        {
            // Página inicial (sin salto lejano ni escaneo total de offset)
            Query pagedQuery = asc
                ? baseQuery.OrderBy("Fecha").OrderBy(FieldPath.DocumentId).Limit(pageSize + 1)
                : baseQuery.OrderByDescending("Fecha").OrderByDescending(FieldPath.DocumentId).Limit(pageSize + 1);

            var snapshot = await pagedQuery.GetSnapshotAsync();
            var docs = snapshot.Documents;
            bool hasNext = docs.Count > pageSize;
            var pageDocs = hasNext ? docs.Take(pageSize).ToList() : docs.ToList();
            var movimientos = pageDocs.Select(MapDocumentToMovimiento).ToList();

            string? cursorSiguiente = hasNext && pageDocs.Count > 0
                ? CrearCursorKardex("next", pageDocs.Last())
                : null;

            return new ResultadoKardexPaginado
            {
                Movimientos = movimientos,
                TienePaginaSiguiente = hasNext,
                TienePaginaAnterior = false,
                CursorSiguiente = cursorSiguiente,
                CursorAnterior = null,
                PaginaActual = 1,
                TotalPaginas = hasNext ? 2 : 1,
                TotalRegistros = hasNext ? (pageSize + 1) : movimientos.Count
            };
        }
        else if (decodedCursor.Direccion == "next")
        {
            // Navegación hacia adelante usando StartAfter en (Fecha, DocumentId)
            Query pagedQuery = asc
                ? baseQuery.OrderBy("Fecha").OrderBy(FieldPath.DocumentId).StartAfter(decodedCursor.FechaUtc, decodedCursor.DocumentId).Limit(pageSize + 1)
                : baseQuery.OrderByDescending("Fecha").OrderByDescending(FieldPath.DocumentId).StartAfter(decodedCursor.FechaUtc, decodedCursor.DocumentId).Limit(pageSize + 1);

            var snapshot = await pagedQuery.GetSnapshotAsync();
            var docs = snapshot.Documents;
            bool hasNext = docs.Count > pageSize;
            var pageDocs = hasNext ? docs.Take(pageSize).ToList() : docs.ToList();
            var movimientos = pageDocs.Select(MapDocumentToMovimiento).ToList();

            string? cursorAnterior = pageDocs.Count > 0
                ? CrearCursorKardex("prev", pageDocs.First())
                : null;
            string? cursorSiguiente = hasNext && pageDocs.Count > 0
                ? CrearCursorKardex("next", pageDocs.Last())
                : null;

            return new ResultadoKardexPaginado
            {
                Movimientos = movimientos,
                TienePaginaSiguiente = hasNext,
                TienePaginaAnterior = true,
                CursorSiguiente = cursorSiguiente,
                CursorAnterior = cursorAnterior,
                PaginaActual = 2,
                TotalPaginas = hasNext ? 3 : 2
            };
        }
        else
        {
            // Navegación hacia atrás usando EndBefore y LimitToLast en (Fecha, DocumentId)
            Query pagedQuery = asc
                ? baseQuery.OrderBy("Fecha").OrderBy(FieldPath.DocumentId).EndBefore(decodedCursor.FechaUtc, decodedCursor.DocumentId).LimitToLast(pageSize + 1)
                : baseQuery.OrderByDescending("Fecha").OrderByDescending(FieldPath.DocumentId).EndBefore(decodedCursor.FechaUtc, decodedCursor.DocumentId).LimitToLast(pageSize + 1);

            var snapshot = await pagedQuery.GetSnapshotAsync();
            var docs = snapshot.Documents;
            bool hasPrev = docs.Count > pageSize;
            var pageDocs = hasPrev ? docs.Skip(docs.Count - pageSize).Take(pageSize).ToList() : docs.ToList();
            var movimientos = pageDocs.Select(MapDocumentToMovimiento).ToList();

            string? cursorAnterior = hasPrev && pageDocs.Count > 0
                ? CrearCursorKardex("prev", pageDocs.First())
                : null;
            string? cursorSiguiente = pageDocs.Count > 0
                ? CrearCursorKardex("next", pageDocs.Last())
                : null;

            return new ResultadoKardexPaginado
            {
                Movimientos = movimientos,
                TienePaginaSiguiente = true,
                TienePaginaAnterior = hasPrev,
                CursorSiguiente = cursorSiguiente,
                CursorAnterior = cursorAnterior,
                PaginaActual = hasPrev ? 2 : 1,
                TotalPaginas = 2
            };
        }
    }

    public async Task<List<MovimientoStock>> ObtenerMovimientos(
        string? insumoId = null,
        string? tipo = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        string? sortOrder = null)
    {
        var query = ConstruirQueryMovimientos(insumoId, tipo, desde, hasta);

        if (!string.IsNullOrWhiteSpace(sortOrder))
        {
            bool asc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
            query = asc
                ? query.OrderBy("Fecha").OrderBy(FieldPath.DocumentId)
                : query.OrderByDescending("Fecha").OrderByDescending(FieldPath.DocumentId);
        }

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Select(MapDocumentToMovimiento).ToList();
    }

    internal async Task<List<MovimientoStock>> LeerMermasPendientes(Transaction transaction, IEnumerable<string> referencias)
    {
        var claves = referencias.ToHashSet(StringComparer.Ordinal);
        if (claves.Count == 0) return new();
        var snapshot = await transaction.GetSnapshotAsync(firestore.Collection("movimientosStock").WhereEqualTo("Tipo", nameof(TipoMovimientoStock.ConsumoEtapa)));
        var pendientes = new List<MovimientoStock>();
        foreach (var documento in snapshot.Documents)
        {
            var movimiento = MapDocumentToMovimiento(documento);
            if (!claves.Contains(movimiento.ReferenciaId)) continue;
            if (!(await transaction.GetSnapshotAsync(firestore.Collection("mermasCancelacion").Document(movimiento.Id))).Exists)
                pendientes.Add(movimiento);
        }
        return pendientes;
    }

    internal void EscribirMermasPendientes(Transaction transaction, IEnumerable<MovimientoStock> consumos, string motivo)
    {
        foreach (var consumo in consumos)
        {
            var referencia = firestore.Collection("movimientosStock").Document();
            transaction.Create(referencia, new MovimientoStock { Id = referencia.Id, InsumoId = consumo.InsumoId, Tipo = nameof(TipoMovimientoStock.MermaCancelacion), Cantidad = 0, SaldoResultante = consumo.SaldoResultante, ReferenciaId = consumo.ReferenciaId, Justificacion = motivo });
            transaction.Create(firestore.Collection("mermasCancelacion").Document(consumo.Id), new Dictionary<string, object> { ["MovimientoId"] = consumo.Id, ["Fecha"] = Timestamp.GetCurrentTimestamp() });
        }
    }

    public async Task RegistrarMermaCancelacion(string referenciaLavado, string motivo)
    {
        var movimientos = await ObtenerMovimientos();
        var consumos = movimientos.Where(x =>
            x.Tipo == nameof(TipoMovimientoStock.ConsumoEtapa) &&
            x.ReferenciaId.StartsWith(referenciaLavado + "_", StringComparison.Ordinal));

        foreach (var consumo in consumos)
        {
            var markerRef = firestore.Collection("mermasCancelacion").Document(consumo.Id);
            await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
            {
                var marker = await transaction.GetSnapshotAsync(markerRef);
                if (marker.Exists) return;
                var referencia = firestore.Collection("movimientosStock").Document();
                transaction.Create(referencia, new MovimientoStock
                {
                    Id = referencia.Id,
                    InsumoId = consumo.InsumoId,
                    Tipo = nameof(TipoMovimientoStock.MermaCancelacion),
                    Cantidad = 0,
                    SaldoResultante = consumo.SaldoResultante,
                    ReferenciaId = consumo.ReferenciaId,
                    Justificacion = motivo
                });
                transaction.Create(markerRef, new Dictionary<string, object>
                {
                    ["MovimientoId"] = consumo.Id,
                    ["Fecha"] = Timestamp.GetCurrentTimestamp()
                });
            });
        }
    }

    public Task EvaluarReabastecimiento(string insumoId) =>
        EvaluarReabastecimiento(insumoId, ActorAuditoria.Sistema);

    public Task EvaluarReabastecimiento(string insumoId, ActorAuditoria actor) =>
        EvaluarReabastecimiento(insumoId, actor?.UserId ?? "sistema", actor?.UserEmail ?? "sistema");

    public async Task EvaluarReabastecimiento(string insumoId, string userId, string userEmail)
    {
        var insumoRef = firestore.Collection("insumos").Document(insumoId);
        var insumoPreSnapshot = await insumoRef.GetSnapshotAsync();
        if (!insumoPreSnapshot.Exists) return;
        var insumoPre = insumoPreSnapshot.ConvertTo<Insumo>();
        if (insumoPre.Estado != "Activo" || !insumoPre.ReabastecimientoAutomatico || insumoPre.StockActual > insumoPre.StockMinimo)
            return;

        var directiva = await new ComprasService(firestore).ObtenerDirectiva();
        var solicitudes = await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var creadas = new List<SolicitudCotizacion>();
            var insumoSnapshot = await transaction.GetSnapshotAsync(insumoRef);
            if (!insumoSnapshot.Exists) return creadas;
            var insumo = insumoSnapshot.ConvertTo<Insumo>();
            if (insumo.Estado != "Activo" || !insumo.ReabastecimientoAutomatico || insumo.StockActual > insumo.StockMinimo) return creadas;

            var abiertas = await transaction.GetSnapshotAsync(firestore.Collection("solicitudesCotizacion"));
            if (abiertas.Documents.Select(x => x.ConvertTo<SolicitudCotizacion>())
                .Any(x => x.Estado is "Pendiente" or "PendienteAprobacion" or "Respondida" &&
                          x.Items.Any(i => i.InsumoId == insumoId))) return creadas;
            var ordenes = await transaction.GetSnapshotAsync(firestore.Collection("ordenesCompra"));
            if (ordenes.Documents.Select(x => x.ConvertTo<OrdenCompra>())
                .Any(x => x.Estado is "PendienteAprobacion" or "RevisionManual" or "Enviada" or "Confirmada" or "Despachada" &&
                          x.Items.Any(i => i.InsumoId == insumoId))) return creadas;

            var proveedorIds = insumo.ProveedoresIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var configRef = firestore.Collection("configuracion").Document("system_config");
            var docRefs = new List<DocumentReference>(1 + proveedorIds.Count) { configRef };
            docRefs.AddRange(proveedorIds.OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => firestore.Collection("proveedores").Document(id)));
            var docsPorPath = (await transaction.GetAllSnapshotsAsync(docRefs))
                .ToDictionary(d => d.Reference.Path, StringComparer.Ordinal);

            var proveedoresActivos = new List<string>();
            foreach (var id in proveedorIds)
            {
                var path = firestore.Collection("proveedores").Document(id).Path;
                if (docsPorPath.TryGetValue(path, out var proveedor) &&
                    proveedor.Exists &&
                    proveedor.ConvertTo<Proveedor>().Estado == "Activo")
                    proveedoresActivos.Add(id);
            }
            if (proveedoresActivos.Count == 0)
            {
                transaction.Set(firestore.Collection("alertasStock").Document(insumoId), new Dictionary<string, object>
                {
                    ["InsumoId"] = insumoId,
                    ["Mensaje"] = $"El insumo '{insumo.Nombre}' alcanzó el stock mínimo y no tiene proveedores.",
                    ["Fecha"] = Timestamp.GetCurrentTimestamp(),
                    ["Estado"] = "Pendiente"
                });
                return creadas;
            }
            var vigencia = docsPorPath.TryGetValue(configRef.Path, out var configSnapshot) && configSnapshot.Exists
                ? configSnapshot.ConvertTo<Configuracion>().VigenciaCompraRecienteDias
                : 14;
            var ahora = DateTime.UtcNow;

            var ordenesRecibidasElegibles = ordenes.Documents.Select(d => d.ConvertTo<OrdenCompra>())
                .Where(o => o.Estado == "Recibida" && proveedoresActivos.Contains(o.ProveedorId) &&
                            o.Items.Any(i => i.InsumoId == insumoId && i.PrecioUnitario > 0))
                .ToList();

            var recepciones = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            if (ordenesRecibidasElegibles.Count > 0)
            {
                var movimientosSnapshot = await transaction.GetSnapshotAsync(firestore.Collection("movimientosStock")
                    .WhereEqualTo("InsumoId", insumoId)
                    .WhereEqualTo("Tipo", nameof(TipoMovimientoStock.RecepcionOrden)));
                recepciones = movimientosSnapshot.Documents.Select(d => d.ConvertTo<MovimientoStock>())
                    .Where(m => m.Cantidad > 0 && m.Fecha <= ahora)
                    .GroupBy(m => m.ReferenciaId, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Max(m => m.Fecha), StringComparer.Ordinal);
            }

            var antecedente = ordenesRecibidasElegibles
                .Where(o => recepciones.ContainsKey(o.Id))
                .SelectMany(o => o.Items.Where(i => i.InsumoId == insumoId && i.PrecioUnitario > 0)
                    .Select(i => new { Orden = o, Item = i, Fecha = recepciones[o.Id] }))
                .Where(x => vigencia >= 0 && (ahora - x.Fecha).TotalDays <= vigencia)
                .OrderByDescending(x => x.Fecha).ThenBy(x => x.Orden.Id, StringComparer.Ordinal)
                .ThenBy(x => x.Orden.ProveedorId, StringComparer.Ordinal).FirstOrDefault();
            // Serializa la creación de actividad contra cambios de correo del proveedor.
            foreach (var id in proveedoresActivos.OrderBy(id => id, StringComparer.Ordinal))
                transaction.Update(firestore.Collection("proveedores").Document(id), "Estado", "Activo");
            // Serializa evaluaciones del mismo insumo sin modificar su saldo.
            transaction.Update(insumoRef, "StockActual", DecimalFirestore.ToFirestore(insumo.StockActual));
            if (antecedente != null)
            {
                var ahoraUtc = new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
                var nueva = firestore.Collection("ordenesCompra").Document();
                var orden = new OrdenCompra
                {
                    Id = nueva.Id,
                    ProveedorId = antecedente.Orden.ProveedorId,
                    Estado = "PendienteAprobacion",
                    Notas = "El precio y la disponibilidad requieren confirmación del proveedor.",
                    Items = new() { new OrdenCompraItem { InsumoId = insumo.Id, NombreInsumo = insumo.Nombre,
                        Cantidad = insumo.CalcularCantidadReposicion(), PrecioUnitario = antecedente.Item.PrecioUnitario } },
                    FechaEmision = ahoraUtc
                };
                transaction.Create(nueva, orden);
                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = string.IsNullOrWhiteSpace(userId) ? "sistema" : userId,
                    UserEmail = string.IsNullOrWhiteSpace(userEmail) ? "sistema" : userEmail,
                    Action = "Creacion de orden de compra",
                    TargetId = orden.Id,
                    TargetType = "OrdenCompra",
                    NombreHistorico = $"Orden {orden.Id}",
                    Antes = new Dictionary<string, string>(),
                    Despues = AuditCampos.CapturarOrdenCompra(orden),
                    Timestamp = ahora
                });
                return creadas;
            }
            var grupo = Guid.NewGuid().ToString("N");
            foreach (var proveedorId in proveedoresActivos)
            {
                var reference = firestore.Collection("solicitudesCotizacion").Document();
                var solicitud = new SolicitudCotizacion
                {
                    Id = reference.Id, GrupoId = grupo, ProveedorId = proveedorId,
                    Estado = directiva.ModoEmision == "RequiereAprobacion" ? "PendienteAprobacion" : "Pendiente",
                    Items = new List<OrdenCompraItem>
                    {
                        new() { InsumoId = insumo.Id, NombreInsumo = insumo.Nombre, Cantidad = insumo.CalcularCantidadReposicion() }
                    }
                };
                transaction.Create(reference, solicitud);
                creadas.Add(solicitud);
            }
            return creadas;
        });

        if (directiva.ModoEmision == "EnvioDirecto")
        {
            foreach (var solicitud in solicitudes)
            {
                var proveedor = await proveedores.Obtener(solicitud.ProveedorId);
                if (proveedor == null || proveedor.Estado != "Activo" || string.IsNullOrWhiteSpace(proveedor.EmailOrdenes))
                {
                    logger.LogWarning("No se pudo enviar la solicitud automática {SolicitudId}: proveedor sin email.", solicitud.Id);
                    continue;
                }
                var contexto = await new ComprasService(firestore).ObtenerContextoEmail(solicitud.Items);
                var resultado = await emailSender.EnviarAsync(InsumosEmailFormatter.FormatearSolicitud(
                    directiva, solicitud, proveedor, contexto.Configuracion, contexto.Unidades));
                if (!resultado.Enviado)
                    logger.LogWarning("No se pudo enviar la solicitud automática {SolicitudId}: {Mensaje}", solicitud.Id, resultado.Mensaje);
            }
        }
    }
}

public sealed record ActorAuditoria(string UserId, string UserEmail)
{
    public static readonly ActorAuditoria Sistema = new("sistema", "sistema");
}

public interface IInterceptorAuditoriaCompras
{
    void Interceptar(AuditLog log);
}

public sealed class ComprasService(FirestoreDb firestore, IInterceptorAuditoriaCompras? interceptor = null, TimeProvider? reloj = null)
{
    private static readonly DecimalConverter DecimalFirestore = new();
    public static readonly TimeZoneInfo ZonaEntrega = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    private DateTime AhoraUtc => (reloj ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public ResultadoOperacion ValidarPreferencia(string? fecha, string? hora, out DateTime? preferenciaUtc)
    {
        preferenciaUtc = null;
        if (string.IsNullOrWhiteSpace(fecha) && string.IsNullOrWhiteSpace(hora))
            return ResultadoOperacion.CrearExito("");
        if (!DateTime.TryParseExact(fecha + " " + hora, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var local) || ZonaEntrega.IsInvalidTime(local) || ZonaEntrega.IsAmbiguousTime(local))
            return ResultadoOperacion.CrearError("Ingrese una fecha y hora preferidas válidas, o deje ambas vacías.");
        preferenciaUtc = TimeZoneInfo.ConvertTimeToUtc(local, ZonaEntrega);
        if (preferenciaUtc < AhoraUtc)
            return ResultadoOperacion.CrearError("La fecha y hora preferidas no pueden ser anteriores al momento actual.");
        return ResultadoOperacion.CrearExito("");
    }

    public Action<AuditLog>? InterceptorAuditoria { get; set; }

    private void RegistrarAuditoriaEnTransaccion(Transaction transaction, AuditLog log)
    {
        InterceptorAuditoria?.Invoke(log);
        interceptor?.Interceptar(log);
        var docRef = firestore.Collection("registros_auditoria").Document();
        log.DocId = docRef.Id;
        transaction.Create(docRef, log);
    }

    private static bool HayCambiosAuditoria(IReadOnlyDictionary<string, string>? antes, IReadOnlyDictionary<string, string>? despues)
    {
        if (antes == null && despues == null) return false;
        if (antes == null || despues == null) return true;
        return antes.Count != despues.Count ||
               antes.Any(kv => !despues.TryGetValue(kv.Key, out var valor) || !string.Equals(kv.Value, valor, StringComparison.Ordinal));
    }

    public async Task<DirectivaReabastecimiento> ObtenerDirectiva()
    {
        var snapshot = await firestore.Collection("directivasReabastecimiento").Document("principal").GetSnapshotAsync();
        return snapshot.Exists ? snapshot.ConvertTo<DirectivaReabastecimiento>() : new DirectivaReabastecimiento();
    }

    public async Task<ResultadoOperacion> GuardarDirectiva(DirectivaReabastecimiento directiva)
    {
        if (directiva.ModoEmision is not ("EnvioDirecto" or "RequiereAprobacion"))
            return ResultadoOperacion.CrearError("El modo de emisión no es válido.");
        if (!InsumosEmailFormatter.PlantillaValida(directiva.AsuntoCotizacion, true) ||
            !InsumosEmailFormatter.PlantillaValida(directiva.AsuntoOrden, true) ||
            !InsumosEmailFormatter.PlantillaValida(directiva.CuerpoCotizacion, false) ||
            !InsumosEmailFormatter.PlantillaValida(directiva.CuerpoOrden, false))
            return ResultadoOperacion.CrearError("La plantilla contiene marcadores no válidos o excede la longitud permitida.");
        directiva.Id = "principal";
        await firestore.Collection("directivasReabastecimiento").Document("principal").SetAsync(directiva);
        return ResultadoOperacion.CrearExito("Directiva de reabastecimiento guardada.");
    }

    public async Task<(Configuracion? Configuracion, Dictionary<string, string> Unidades)> ObtenerContextoEmail(IEnumerable<OrdenCompraItem> items)
    {
        var config = await firestore.Collection("configuracion").Document("system_config").GetSnapshotAsync();
        var unidades = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in items.Select(i => i.InsumoId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            var doc = await firestore.Collection("insumos").Document(id).GetSnapshotAsync();
            if (doc.Exists) unidades[id] = doc.ConvertTo<Insumo>().UnidadMedida;
        }
        return (config.Exists ? config.ConvertTo<Configuracion>() : null, unidades);
    }

    public sealed record ResultadoAuditoriaOrden(
        ResultadoOperacion Resultado,
        string OrdenId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null,
        bool TransicionARevisionManual = false);

    public Task<ResultadoOperacion> EditarOrdenPendiente(string id, string insumoId, decimal cantidad, decimal precio, ActorAuditoria actor) =>
        EditarOrdenPendiente(id, insumoId, cantidad, precio, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaOrden> EditarOrdenPendienteConAuditoria(string id, string insumoId, decimal cantidad, decimal precio, ActorAuditoria actor) =>
        EditarOrdenPendienteConAuditoria(id, insumoId, cantidad, precio, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> AprobarOrden(string id, IEmailSender sender, ActorAuditoria actor) =>
        AprobarOrden(id, sender, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaOrden> AprobarOrdenConAuditoria(string id, IEmailSender sender, ActorAuditoria actor) =>
        AprobarOrdenConAuditoria(id, sender, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> CambiarEstadoOrden(string id, string estado, string? motivo, ActorAuditoria actor) =>
        CambiarEstadoOrden(id, estado, motivo, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaOrden> CambiarEstadoOrdenConAuditoria(string id, string estado, string? motivo, ActorAuditoria actor) =>
        CambiarEstadoOrdenConAuditoria(id, estado, motivo, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> CrearOrdenManual(OrdenCompra orden, ActorAuditoria actor) =>
        CrearOrdenManual(orden, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaOrden> CrearOrdenManualConAuditoria(OrdenCompra orden, ActorAuditoria actor) =>
        CrearOrdenManualConAuditoria(orden, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> CrearOrdenManual(OrdenCompra orden, IEmailSender sender, ActorAuditoria actor) =>
        CrearOrdenManual(orden, sender, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaOrden> CrearOrdenManualConAuditoria(OrdenCompra orden, IEmailSender sender, ActorAuditoria actor) =>
        CrearOrdenManualConAuditoria(orden, sender, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> Adjudicar(string solicitudId, string? respuestaId, ActorAuditoria actor) =>
        Adjudicar(solicitudId, respuestaId, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> Recibir(string id, IReadOnlyDictionary<string, decimal>? cantidades, bool cancelarConcurrentes, ActorAuditoria actor) =>
        Recibir(id, cantidades, cancelarConcurrentes, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaOrden> RecibirConAuditoria(string id, IReadOnlyDictionary<string, decimal>? cantidades, bool cancelarConcurrentes, ActorAuditoria actor) =>
        RecibirConAuditoria(id, cantidades, cancelarConcurrentes, actor.UserId, actor.UserEmail);

    public async Task<ResultadoOperacion> EditarOrdenPendiente(string id, string insumoId, decimal cantidad, decimal precio, string userId = "", string userEmail = "") =>
        (await EditarOrdenPendienteConAuditoria(id, insumoId, cantidad, precio, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaOrden> EditarOrdenPendienteConAuditoria(string id, string insumoId, decimal cantidad, decimal precio, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(insumoId) || cantidad <= 0 || precio < 0)
            return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("Ingrese una cantidad mayor a cero y un precio no negativo."));
        var referencia = firestore.Collection("ordenesCompra").Document(id);
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaccion =>
        {
            var snapshot = await transaccion.GetSnapshotAsync(referencia);
            if (!snapshot.Exists) return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden no existe."));
            var orden = snapshot.ConvertTo<OrdenCompra>();
            if (orden.Estado != "PendienteAprobacion") return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden no está pendiente de aprobación."));
            var items = orden.Items.Where(x => x.InsumoId == insumoId).ToList();
            if (items.Count != 1) return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("El insumo no existe o está duplicado en la orden."));
            var antes = AuditCampos.CapturarOrdenCompra(orden);
            items[0].Cantidad = DecimalFirestore.FromFirestore(DecimalFirestore.ToFirestore(cantidad));
            items[0].PrecioUnitario = DecimalFirestore.FromFirestore(DecimalFirestore.ToFirestore(precio));
            var despues = AuditCampos.CapturarOrdenCompra(orden);
            var ordenId = string.IsNullOrEmpty(orden.Id) ? snapshot.Id : orden.Id;
            if (!HayCambiosAuditoria(antes, despues))
            {
                return new ResultadoAuditoriaOrden(
                    ResultadoOperacion.CrearExito("Orden actualizada. El precio y la disponibilidad requieren confirmación del proveedor."),
                    ordenId,
                    antes,
                    despues);
            }
            transaccion.Update(referencia, "Items", orden.Items);
            RegistrarAuditoriaEnTransaccion(transaccion, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = "Edicion de orden de compra pendiente",
                TargetId = ordenId,
                TargetType = "OrdenCompra",
                NombreHistorico = $"Orden {ordenId}",
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });
            return new ResultadoAuditoriaOrden(
                ResultadoOperacion.CrearExito("Orden actualizada. El precio y la disponibilidad requieren confirmación del proveedor."),
                ordenId,
                antes,
                despues);
        });
    }

    public async Task<ResultadoOperacion> AprobarOrden(string id, IEmailSender sender, string userId = "", string userEmail = "") =>
        (await AprobarOrdenConAuditoria(id, sender, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaOrden> AprobarOrdenConAuditoria(string id, IEmailSender sender, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id)) return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden no existe."));
        var reference = firestore.Collection("ordenesCompra").Document(id);
        var transicion1 = await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var snap = await transaction.GetSnapshotAsync(reference);
            if (!snap.Exists) return null;
            var actual = snap.ConvertTo<OrdenCompra>();
            if (actual.Estado != "PendienteAprobacion") return null;
            var ordenId = string.IsNullOrEmpty(actual.Id) ? snap.Id : actual.Id;
            var antes = AuditCampos.CapturarOrdenCompra(actual);
            actual.Estado = "RevisionManual";
            var despuesRevision = AuditCampos.CapturarOrdenCompra(actual);
            transaction.Update(reference, "Estado", "RevisionManual");
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = "Paso de orden de compra a revision manual",
                TargetId = ordenId,
                TargetType = "OrdenCompra",
                NombreHistorico = $"Orden {ordenId}",
                Antes = antes,
                Despues = despuesRevision,
                Timestamp = DateTime.UtcNow
            });
            return new { Orden = actual, OrdenId = ordenId, Antes = antes, DespuesRevision = despuesRevision };
        });
        if (transicion1 == null)
            return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden no está pendiente de aprobación; revise manualmente el resultado del envío."));
        try
        {
            var orden = transicion1.Orden;
            var proveedor = await firestore.Collection("proveedores").Document(orden.ProveedorId).GetSnapshotAsync();
            if (!proveedor.Exists)
                return new ResultadoAuditoriaOrden(
                    ResultadoOperacion.CrearError("Resultado incierto. Se requiere revisión manual."),
                    transicion1.OrdenId,
                    transicion1.Antes,
                    transicion1.DespuesRevision,
                    TransicionARevisionManual: true);
            var proveedorDatos = proveedor.ConvertTo<Proveedor>();
            var email = proveedorDatos.EmailOrdenes;
            if (string.IsNullOrWhiteSpace(email))
                return new ResultadoAuditoriaOrden(
                    ResultadoOperacion.CrearError("Resultado incierto. Se requiere revisión manual."),
                    transicion1.OrdenId,
                    transicion1.Antes,
                    transicion1.DespuesRevision,
                    TransicionARevisionManual: true);
            var directiva = await ObtenerDirectiva();
            var contexto = await ObtenerContextoEmail(orden.Items);
            string? plazo = null;
            if (!string.IsNullOrWhiteSpace(orden.GrupoCotizacionId))
            {
                var solicitudes = await firestore.Collection("solicitudesCotizacion").WhereEqualTo("GrupoId", orden.GrupoCotizacionId).GetSnapshotAsync();
                var elegidas = solicitudes.Documents.Select(d => (Documento: d, Solicitud: d.ConvertTo<SolicitudCotizacion>()))
                    .Where(x => x.Solicitud.Estado == "Adjudicada" && x.Solicitud.ProveedorId == orden.ProveedorId).ToList();
                if (elegidas.Count == 1)
                {
                    var respuestas = await firestore.Collection("cotizacionesRespuestas").WhereEqualTo("SolicitudId", elegidas[0].Documento.Id).GetSnapshotAsync();
                    if (respuestas.Count == 1) plazo = respuestas.Documents[0].ConvertTo<CotizacionRespuesta>().PlazoEntrega;
                }
            }
            var envio = await sender.EnviarAsync(InsumosEmailFormatter.FormatearOrden(directiva, orden, proveedorDatos, contexto.Configuracion, plazo, contexto.Unidades));
            if (!envio.Enviado)
                return new ResultadoAuditoriaOrden(
                    ResultadoOperacion.CrearError("Resultado incierto. Se requiere revisión manual."),
                    transicion1.OrdenId,
                    transicion1.Antes,
                    transicion1.DespuesRevision,
                    TransicionARevisionManual: true);
            var despuesEnviada = await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
            {
                var snap = await transaction.GetSnapshotAsync(reference);
                if (!snap.Exists) return null;
                var actual = snap.ConvertTo<OrdenCompra>();
                if (actual.Estado != "RevisionManual") return null;
                var ordenId = string.IsNullOrEmpty(actual.Id) ? snap.Id : actual.Id;
                var antesEnviada = AuditCampos.CapturarOrdenCompra(actual);
                actual.Estado = "Enviada";
                var despues = AuditCampos.CapturarOrdenCompra(actual);
                transaction.Update(reference, "Estado", "Enviada");
                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Aprobacion de orden de compra",
                    TargetId = ordenId,
                    TargetType = "OrdenCompra",
                    NombreHistorico = $"Orden {ordenId}",
                    Antes = antesEnviada,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });
                return despues;
            });
            if (despuesEnviada == null)
                return new ResultadoAuditoriaOrden(
                    ResultadoOperacion.CrearError("Resultado incierto. Se requiere revisión manual."),
                    transicion1.OrdenId,
                    transicion1.Antes,
                    transicion1.DespuesRevision,
                    TransicionARevisionManual: true);
            return new ResultadoAuditoriaOrden(
                ResultadoOperacion.CrearExito("El transporte aceptó la solicitud; esto no confirma entrega ni lectura."),
                transicion1.OrdenId,
                transicion1.Antes,
                despuesEnviada);
        }
        catch (Exception)
        {
            return new ResultadoAuditoriaOrden(
                ResultadoOperacion.CrearError("Resultado incierto. Se requiere revisión manual."),
                transicion1.OrdenId,
                transicion1.Antes,
                transicion1.DespuesRevision,
                TransicionARevisionManual: true);
        }
    }

    public async Task<ResultadoOperacion> CambiarEstadoOrden(string id, string estado, string? motivo = null, string userId = "", string userEmail = "") =>
        (await CambiarEstadoOrdenConAuditoria(id, estado, motivo, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaOrden> CambiarEstadoOrdenConAuditoria(string id, string estado, string? motivo = null, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden no existe."));
        var reference = firestore.Collection("ordenesCompra").Document(id);
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(reference);
            if (!snapshot.Exists) return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden no existe."));
            var orden = snapshot.ConvertTo<OrdenCompra>();
            var ordenId = string.IsNullOrEmpty(orden.Id) ? snapshot.Id : orden.Id;
            if (orden.Estado == "RevisionManual")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("Se requiere revisión manual del resultado del envío."));
            if (orden.Estado == "PendienteAprobacion" && estado == "Enviada")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden requiere aprobación y despacho mediante el transporte de correo."));
            var estadosValidos = new[] { "Enviada", "Despachada", "Confirmada", "Recibida", "Cancelada" };
            if (!estadosValidos.Contains(estado, StringComparer.Ordinal))
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("El estado de la orden no es válido."));
            if (orden.Estado is "Recibida" or "RecibidaParcial" or "Cancelada")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La orden ya no admite cambios."));
            if (orden.Estado == estado)
            {
                var estadoActual = AuditCampos.CapturarOrdenCompra(orden);
                return new ResultadoAuditoriaOrden(
                    ResultadoOperacion.CrearExito("La orden ya se encuentra en el estado indicado."),
                    ordenId,
                    estadoActual,
                    estadoActual);
            }
            if (estado == "Confirmada" && orden.Estado != "Enviada")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("Solo se pueden confirmar órdenes enviadas."));
            if (estado == "Despachada" && orden.Estado != "Confirmada")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("Solo se pueden despachar órdenes confirmadas."));
            if (estado == "Recibida")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("La recepción debe registrarse desde la operación de recepción."));
            if (orden.Estado == "Recibida" && estado == "Cancelada")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("No se puede cancelar una orden recibida."));
            if (estado == "Cancelada" && string.IsNullOrWhiteSpace(motivo))
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("El motivo de cancelación es obligatorio."));

            var antes = AuditCampos.CapturarOrdenCompra(orden);
            orden.Estado = estado;
            orden.MotivoCancelacion = motivo ?? string.Empty;
            var despues = AuditCampos.CapturarOrdenCompra(orden);

            if (!HayCambiosAuditoria(antes, despues))
            {
                return new ResultadoAuditoriaOrden(
                    ResultadoOperacion.CrearExito("Estado de la orden actualizado."),
                    ordenId,
                    antes,
                    despues);
            }

            transaction.Update(reference, new Dictionary<string, object>
            {
                ["Estado"] = estado,
                ["MotivoCancelacion"] = motivo ?? string.Empty
            });
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = $"Cambio de estado de orden a {estado}",
                TargetId = ordenId,
                TargetType = "OrdenCompra",
                NombreHistorico = $"Orden {ordenId}",
                Antes = antes,
                Despues = despues,
                Timestamp = DateTime.UtcNow
            });
            return new ResultadoAuditoriaOrden(
                ResultadoOperacion.CrearExito("Estado de la orden actualizado."),
                ordenId,
                antes,
                despues);
        });
    }

    public async Task<List<SolicitudCotizacion>> ObtenerSolicitudes() =>
        (await firestore.Collection("solicitudesCotizacion").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<SolicitudCotizacion>()).OrderByDescending(x => x.Fecha).ToList();

    public async Task<List<CotizacionRespuesta>> ObtenerRespuestas(string grupoId)
    {
        var solicitudes = await firestore.Collection("solicitudesCotizacion")
            .WhereEqualTo("GrupoId", grupoId).GetSnapshotAsync();
        var ids = solicitudes.Documents.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        return (await firestore.Collection("cotizacionesRespuestas").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<CotizacionRespuesta>())
            .Where(x => ids.Contains(x.SolicitudId)).ToList();
    }

    public async Task<List<ComparacionCotizacion>> ObtenerComparacion(string grupoId)
    {
        var solicitudes = (await firestore.Collection("solicitudesCotizacion")
                .WhereEqualTo("GrupoId", grupoId).GetSnapshotAsync())
            .Documents.Select(x => x.ConvertTo<SolicitudCotizacion>()).ToList();
        var solicitudesIds = solicitudes.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var respuestas = (await firestore.Collection("cotizacionesRespuestas").GetSnapshotAsync())
            .Documents.Select(x => x.ConvertTo<CotizacionRespuesta>())
            .Where(x => solicitudesIds.Contains(x.SolicitudId))
            .GroupBy(x => x.SolicitudId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.Fecha).ThenBy(r => r.Id, StringComparer.Ordinal).First(), StringComparer.Ordinal);
        return solicitudes.Select(s => new ComparacionCotizacion
        {
            Solicitud = s,
            Respuesta = respuestas.GetValueOrDefault(s.Id),
            Total = respuestas.TryGetValue(s.Id, out var respuesta)
                ? respuesta.Items.Sum(x => x.Cantidad * x.PrecioUnitario)
                : 0
        }).OrderBy(x => x.Total == 0 ? 1 : 0).ThenBy(x => x.Total).ToList();
    }

    public sealed record ResultadoAuditoriaSolicitud(
        ResultadoOperacion Resultado,
        string SolicitudId = "",
        Dictionary<string, string>? Antes = null,
        Dictionary<string, string>? Despues = null);

    public Task<ResultadoOperacion> AprobarSolicitud(string id, ActorAuditoria actor) =>
        AprobarSolicitud(id, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaSolicitud> AprobarSolicitudConAuditoria(string id, ActorAuditoria actor) =>
        AprobarSolicitudConAuditoria(id, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> AprobarSolicitud(string id) =>
        AprobarSolicitud(id, string.Empty, string.Empty);

    public async Task<ResultadoOperacion> AprobarSolicitud(string id, string userId = "", string userEmail = "") =>
        (await AprobarSolicitudConAuditoria(id, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaSolicitud> AprobarSolicitudConAuditoria(string id, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            return new ResultadoAuditoriaSolicitud(ResultadoOperacion.CrearError("La solicitud no existe."));

        var solicitudRef = firestore.Collection("solicitudesCotizacion").Document(id);
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(solicitudRef);
            if (!snapshot.Exists)
                return new ResultadoAuditoriaSolicitud(ResultadoOperacion.CrearError("La solicitud no existe."));

            var solicitud = snapshot.ConvertTo<SolicitudCotizacion>();
            var solId = string.IsNullOrEmpty(solicitud.Id) ? snapshot.Id : solicitud.Id;

            if (solicitud.Estado == "Pendiente")
            {
                var estadoActual = AuditCampos.CapturarSolicitudCotizacion(solicitud);
                return new ResultadoAuditoriaSolicitud(
                    ResultadoOperacion.CrearExito("La solicitud ya se encuentra aprobada."),
                    solId,
                    estadoActual,
                    estadoActual);
            }

            if (solicitud.Estado != "PendienteAprobacion")
                return new ResultadoAuditoriaSolicitud(ResultadoOperacion.CrearError("La solicitud no está pendiente de aprobación."));

            var antes = AuditCampos.CapturarSolicitudCotizacion(solicitud);
            solicitud.Estado = "Pendiente";
            var despues = AuditCampos.CapturarSolicitudCotizacion(solicitud);

            transaction.Update(solicitudRef, "Estado", "Pendiente");

            var ahora = DateTime.UtcNow;
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId ?? string.Empty,
                UserEmail = userEmail ?? string.Empty,
                Action = "Aprobación de solicitud de cotización",
                TargetId = solId,
                TargetType = "SolicitudCotizacion",
                NombreHistorico = $"Solicitud {solId}",
                Antes = antes,
                Despues = despues,
                Timestamp = new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc)
            });

            return new ResultadoAuditoriaSolicitud(
                ResultadoOperacion.CrearExito("Solicitud aprobada."),
                solId,
                antes,
                despues);
        });
    }

    public sealed record ResultadoAuditoriaSolicitudesGrupo(
        ResultadoOperacion Resultado,
        string GrupoId = "",
        List<string>? SolicitudesAprobadas = null);

    public Task<ResultadoOperacion> AprobarGrupoSolicitudes(string grupoId, ActorAuditoria actor) =>
        AprobarGrupoSolicitudes(grupoId, actor.UserId, actor.UserEmail);

    public Task<ResultadoAuditoriaSolicitudesGrupo> AprobarGrupoSolicitudesConAuditoria(string grupoId, ActorAuditoria actor) =>
        AprobarGrupoSolicitudesConAuditoria(grupoId, actor.UserId, actor.UserEmail);

    public Task<ResultadoOperacion> AprobarGrupoSolicitudes(string grupoId) =>
        AprobarGrupoSolicitudes(grupoId, string.Empty, string.Empty);

    public async Task<ResultadoOperacion> AprobarGrupoSolicitudes(string grupoId, string userId = "", string userEmail = "") =>
        (await AprobarGrupoSolicitudesConAuditoria(grupoId, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaSolicitudesGrupo> AprobarGrupoSolicitudesConAuditoria(string grupoId, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(grupoId))
            return new ResultadoAuditoriaSolicitudesGrupo(ResultadoOperacion.CrearError("El concurso de cotización no existe."));

        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var query = firestore.Collection("solicitudesCotizacion").WhereEqualTo("GrupoId", grupoId);
            var snapshot = await transaction.GetSnapshotAsync(query);
            if (snapshot.Count == 0)
                return new ResultadoAuditoriaSolicitudesGrupo(ResultadoOperacion.CrearError("El concurso de cotización no existe."));

            var pendientes = new List<(DocumentReference Ref, SolicitudCotizacion Solicitud, string SolId)>();
            var yaAprobadas = 0;

            foreach (var doc in snapshot.Documents)
            {
                var solicitud = doc.ConvertTo<SolicitudCotizacion>();
                var solId = string.IsNullOrEmpty(solicitud.Id) ? doc.Id : solicitud.Id;

                if (solicitud.Estado == "PendienteAprobacion")
                {
                    pendientes.Add((doc.Reference, solicitud, solId));
                }
                else if (solicitud.Estado == "Pendiente")
                {
                    yaAprobadas++;
                }
            }

            if (pendientes.Count == 0)
            {
                if (yaAprobadas > 0)
                    return new ResultadoAuditoriaSolicitudesGrupo(
                        ResultadoOperacion.CrearExito("El concurso de cotización ya se encuentra aprobado."),
                        grupoId,
                        new List<string>());

                return new ResultadoAuditoriaSolicitudesGrupo(
                    ResultadoOperacion.CrearError("El concurso no tiene solicitudes pendientes de aprobación."),
                    grupoId);
            }

            var ahora = DateTime.UtcNow;
            var timestamp = new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
            var aprobadasIds = new List<string>();

            foreach (var (docRef, solicitud, solId) in pendientes)
            {
                var antes = AuditCampos.CapturarSolicitudCotizacion(solicitud);
                solicitud.Estado = "Pendiente";
                var despues = AuditCampos.CapturarSolicitudCotizacion(solicitud);

                transaction.Update(docRef, "Estado", "Pendiente");

                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId ?? string.Empty,
                    UserEmail = userEmail ?? string.Empty,
                    Action = "Aprobación de solicitud de cotización (concurso)",
                    TargetId = solId,
                    TargetType = "SolicitudCotizacion",
                    NombreHistorico = $"Solicitud {solId}",
                    Antes = antes,
                    Despues = despues,
                    Timestamp = timestamp
                });

                aprobadasIds.Add(solId);
            }

            return new ResultadoAuditoriaSolicitudesGrupo(
                ResultadoOperacion.CrearExito($"Concurso de cotización aprobado ({aprobadasIds.Count} solicitudes autorizadas)."),
                grupoId,
                aprobadasIds);
        });
    }

    public async Task<ResultadoOperacion> RegistrarRespuesta(CotizacionRespuesta respuesta, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(respuesta.SolicitudId)) return ResultadoOperacion.CrearError("La solicitud de cotización no existe.");
        var solicitudRef = firestore.Collection("solicitudesCotizacion").Document(respuesta.SolicitudId);
        var reference = firestore.Collection("cotizacionesRespuestas").Document();
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var solicitud = await transaction.GetSnapshotAsync(solicitudRef);
            if (!solicitud.Exists) return ResultadoOperacion.CrearError("La solicitud de cotización no existe.");
            var datos = solicitud.ConvertTo<SolicitudCotizacion>();
            if (datos.Estado != "Pendiente") return ResultadoOperacion.CrearError("La solicitud ya no está pendiente.");
            if (!int.TryParse(respuesta.PlazoEntrega, out var plazo) || plazo < 0 || respuesta.Items == null || respuesta.Items.Count != datos.Items.Count || respuesta.Items.Any(x => x.PrecioUnitario < 0) || respuesta.Items.Select(x => x.InsumoId).Distinct(StringComparer.Ordinal).Count() != datos.Items.Count || datos.Items.Any(x => !respuesta.Items.Any(y => y.InsumoId == x.InsumoId)))
                return ResultadoOperacion.CrearError("Ingrese precios y plazo de entrega válidos para todos los ítems.");
            foreach (var item in respuesta.Items)
            {
                var original = datos.Items.Single(x => x.InsumoId == item.InsumoId);
                item.NombreInsumo = original.NombreInsumo;
                item.Cantidad = original.Cantidad;
            }
            var anteriores = await transaction.GetSnapshotAsync(firestore.Collection("cotizacionesRespuestas").WhereEqualTo("SolicitudId", respuesta.SolicitudId));
            if (anteriores.Count > 0) return ResultadoOperacion.CrearError("La solicitud ya tiene una respuesta registrada.");
            respuesta.Id = reference.Id;
            respuesta.ProveedorId = datos.ProveedorId;
            respuesta.Fecha = DateTime.UtcNow;
            transaction.Create(reference, respuesta);
            transaction.Update(solicitudRef, "Estado", "Respondida");
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog { UserId = userId, UserEmail = userEmail, Action = "Registro Respuesta Cotizacion", TargetType = "SolicitudCotizacion", TargetId = respuesta.SolicitudId, NombreHistorico = $"Solicitud {respuesta.SolicitudId}", Antes = new() { ["Estado"] = datos.Estado }, Despues = new() { ["Estado"] = "Respondida" }, Timestamp = DateTime.UtcNow });
            return ResultadoOperacion.CrearExito("Respuesta de cotización registrada.");
        });
    }

    public async Task<ResultadoOperacion> CrearSolicitudes(List<string> proveedorIds, List<OrdenCompraItem> items, string? notas, string userId = "", string userEmail = "", DateTime? fechaEntregaPreferidaUtc = null)
    {
        if (fechaEntregaPreferidaUtc.HasValue && (fechaEntregaPreferidaUtc.Value.Kind != DateTimeKind.Utc || fechaEntregaPreferidaUtc.Value < AhoraUtc))
            return ResultadoOperacion.CrearError("La fecha y hora preferidas no pueden ser anteriores al momento actual.");
        proveedorIds = (proveedorIds ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
        items ??= new();
        if (items.Any(x => x.Cantidad < 0)) return ResultadoOperacion.CrearError("Seleccione proveedores e insumos válidos con cantidades mayores a cero.");
        items = items.Where(x => !string.IsNullOrWhiteSpace(x.InsumoId) && x.Cantidad > 0).ToList();
        if (proveedorIds.Count == 0 || items.Count == 0 || items.Select(x => x.InsumoId).Distinct(StringComparer.Ordinal).Count() != items.Count)
            return ResultadoOperacion.CrearError("Seleccione proveedores e insumos válidos con cantidades mayores a cero.");
        var grupo = Guid.NewGuid().ToString("N");
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var docRefs = items.Select(i => i.InsumoId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => firestore.Collection("insumos").Document(id))
                .Concat(proveedorIds.OrderBy(id => id, StringComparer.Ordinal)
                    .Select(id => firestore.Collection("proveedores").Document(id)))
                .ToList();
            var snaps = (await transaction.GetAllSnapshotsAsync(docRefs))
                .ToDictionary(s => s.Reference.Path, StringComparer.Ordinal);
            foreach (var id in proveedorIds)
            {
                var proveedor = snaps[firestore.Collection("proveedores").Document(id).Path];
                if (!proveedor.Exists || proveedor.ConvertTo<Proveedor>().Estado != "Activo")
                    return ResultadoOperacion.CrearError("Seleccione proveedores activos.");
            }
            foreach (var item in items)
            {
                var snap = snaps[firestore.Collection("insumos").Document(item.InsumoId).Path];
                if (!snap.Exists || snap.ConvertTo<Insumo>().Estado != "Activo")
                    return ResultadoOperacion.CrearError("Seleccione insumos activos.");
                item.NombreInsumo = snap.ConvertTo<Insumo>().Nombre;
            }
            foreach (var item in items.OrderBy(i => i.InsumoId, StringComparer.Ordinal))
                transaction.Update(firestore.Collection("insumos").Document(item.InsumoId), "Estado", "Activo");
            foreach (var id in proveedorIds.OrderBy(id => id, StringComparer.Ordinal))
                transaction.Update(firestore.Collection("proveedores").Document(id), "Estado", "Activo");
            var solicitudesAuditadas = new List<Dictionary<string, string>>();
            foreach (var id in proveedorIds)
            {
                var reference = firestore.Collection("solicitudesCotizacion").Document();
                var solicitud = new SolicitudCotizacion { Id = reference.Id, GrupoId = grupo, ProveedorId = id, Items = items, Notas = notas ?? string.Empty, FechaEntregaPreferidaUtc = fechaEntregaPreferidaUtc };
                transaction.Create(reference, solicitud);
                var captura = AuditCampos.CapturarSolicitudCotizacion(solicitud);
                captura["Id"] = solicitud.Id;
                solicitudesAuditadas.Add(captura);
            }
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog { UserId = userId, UserEmail = userEmail, Action = "Emision Solicitud Cotizacion", TargetType = "SolicitudCotizacion", TargetId = grupo, NombreHistorico = $"Grupo {grupo}", Antes = new(), Despues = new() { ["GrupoId"] = grupo, ["Solicitudes"] = System.Text.Json.JsonSerializer.Serialize(solicitudesAuditadas) }, Timestamp = DateTime.UtcNow });
            return ResultadoOperacion.CrearExito("Solicitudes de cotización creadas.");
        });
    }

    public async Task<ResultadoOperacion> MarcarEmailSolicitudEnviado(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return ResultadoOperacion.CrearError("La solicitud no existe.");
        var docRef = firestore.Collection("solicitudesCotizacion").Document(id);
        var snap = await docRef.GetSnapshotAsync();
        if (!snap.Exists) return ResultadoOperacion.CrearError("La solicitud no existe.");

        var ahora = DateTime.UtcNow;
        await docRef.UpdateAsync(new Dictionary<string, object>
        {
            { "EmailEnviado", true },
            { "FechaEnvioEmailUtc", ahora }
        });
        return ResultadoOperacion.CrearExito("Email registrado como enviado.");
    }

    public Task<ResultadoOperacion> CrearSolicitud(SolicitudCotizacion solicitud) =>
        Task.FromResult(ResultadoOperacion.CrearError("Utilice el formulario de cotizaciones para emitir la solicitud."));

    public Task<ResultadoOperacion> Adjudicar(string solicitudId, string? respuestaId = null, string userId = "", string userEmail = "") =>
        Adjudicar(solicitudId, respuestaId, null, userId, userEmail);

    public async Task<ResultadoOperacion> Adjudicar(string solicitudId, string? respuestaId, IEmailSender? sender, string userId = "", string userEmail = "")
    {
        var selectedRef = firestore.Collection("solicitudesCotizacion").Document(solicitudId);
        var initial = await selectedRef.GetSnapshotAsync();
        if (!initial.Exists) throw new InvalidOperationException("La solicitud no existe.");
        var grupoId = initial.ConvertTo<SolicitudCotizacion>().GrupoId;
        var ordenId = await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var group = await transaction.GetSnapshotAsync(firestore.Collection("solicitudesCotizacion").WhereEqualTo("GrupoId", grupoId));
            var selectedSnapshot = group.Documents.FirstOrDefault(d => d.Id == solicitudId);
            if (selectedSnapshot == null) throw new InvalidOperationException("La solicitud no existe.");
            var selected = selectedSnapshot.ConvertTo<SolicitudCotizacion>();
            if (selected.Estado is not ("Pendiente" or "Respondida")) throw new InvalidOperationException("La solicitud ya fue resuelta.");
            DocumentSnapshot? responseSnapshot = null;
            if (!string.IsNullOrWhiteSpace(respuestaId))
            {
                var responseRef = firestore.Collection("cotizacionesRespuestas").Document(respuestaId);
                responseSnapshot = await transaction.GetSnapshotAsync(responseRef);
                if (!responseSnapshot.Exists)
                    throw new InvalidOperationException("La respuesta de cotización no existe.");
                var selectedResponse = responseSnapshot.ConvertTo<CotizacionRespuesta>();
                if (selectedResponse.SolicitudId != solicitudId)
                    throw new InvalidOperationException("La respuesta no corresponde a la solicitud.");
            }
            if (responseSnapshot == null)
            {
                var respuestas = await transaction.GetSnapshotAsync(firestore.Collection("cotizacionesRespuestas").WhereEqualTo("SolicitudId", solicitudId));
                responseSnapshot = respuestas.Documents.OrderBy(d => d.ConvertTo<CotizacionRespuesta>().Fecha).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault();
            }
            if (responseSnapshot == null)
                throw new InvalidOperationException("La solicitud no tiene una respuesta de cotización.");
            var response = responseSnapshot?.Exists == true
                ? responseSnapshot.ConvertTo<CotizacionRespuesta>() : null;
            var itemsOrden = response?.Items?.Count > 0 ? response.Items : selected.Items;
            var proveedorRef = firestore.Collection("proveedores").Document(selected.ProveedorId);
            var insumoIdsOrdenados = itemsOrden.Select(x => x.InsumoId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var insumoRefs = insumoIdsOrdenados.Select(id => firestore.Collection("insumos").Document(id)).ToList();
            var docRefs = new List<DocumentReference>(1 + insumoIdsOrdenados.Count);
            docRefs.AddRange(insumoRefs);
            docRefs.Add(proveedorRef);
            var snaps = (await transaction.GetAllSnapshotsAsync(docRefs))
                .ToDictionary(s => s.Reference.Path, StringComparer.Ordinal);
            var proveedor = snaps[proveedorRef.Path];
            if (!proveedor.Exists || proveedor.GetValue<string>("Estado") != "Activo")
                throw new InvalidOperationException("El proveedor no existe o está inactivo.");
            if (group.Documents.Any(d => d.Id != solicitudId && d.GetValue<string>("Estado") == "Adjudicada"))
                throw new InvalidOperationException("El grupo de cotización ya fue adjudicado.");
            var orderRef = firestore.Collection("ordenesCompra").Document();
            foreach (var insumoRef in insumoRefs)
            {
                var insumo = snaps[insumoRef.Path];
                if (!insumo.Exists || insumo.ConvertTo<Insumo>().Estado != "Activo")
                    throw new InvalidOperationException("El insumo no existe o está inactivo.");
            }
            foreach (var insumoRef in insumoRefs) transaction.Update(insumoRef, "Estado", "Activo");
            transaction.Update(proveedorRef, "Estado", "Activo");
            var ahora = DateTime.UtcNow;
            var orden = new OrdenCompra
            {
                Id = orderRef.Id,
                ProveedorId = selected.ProveedorId,
                GrupoCotizacionId = selected.GrupoId,
                Items = itemsOrden,
                Estado = "PendienteAprobacion",
                FechaEmision = new DateTime(ahora.Ticks - (ahora.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc)
            };
            transaction.Create(orderRef, orden);
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = "Creacion de orden de compra",
                TargetId = orden.Id,
                TargetType = "OrdenCompra",
                NombreHistorico = $"Orden {orden.Id}",
                Antes = null,
                Despues = AuditCampos.CapturarOrdenCompra(orden),
                Timestamp = ahora
            });
            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Action = "Adjudicación de cotización",
                TargetId = solicitudId,
                TargetType = "SolicitudCotizacion",
                NombreHistorico = $"Solicitud {solicitudId}",
                Antes = new Dictionary<string, string> { ["Estado"] = selected.Estado },
                Despues = new Dictionary<string, string> { ["Estado"] = "Adjudicada" },
                Timestamp = ahora
            });
            foreach (var item in group.Documents)
                transaction.Update(item.Reference, "Estado", item.Id == solicitudId ? "Adjudicada" : "Descartada");
            return orden.Id;
        });
        return (await AprobarOrdenConAuditoria(ordenId, sender ?? new TransporteCotizacionNoDisponible(), userId, userEmail)).Resultado;
    }

    private sealed class TransporteCotizacionNoDisponible : IEmailSender
    {
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoEnvioEmail(false, "Transporte no disponible."));
    }

    public async Task<ResultadoOperacion> CrearOrdenManual(OrdenCompra orden, string userId = "", string userEmail = "") =>
        (await CrearOrdenManualConAuditoria(orden, userId, userEmail)).Resultado;

    public async Task<ResultadoOperacion> CrearOrdenManual(OrdenCompra orden, IEmailSender sender, string userId = "", string userEmail = "") =>
        (await CrearOrdenManualConAuditoria(orden, sender, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaOrden> CrearOrdenManualConAuditoria(OrdenCompra orden, IEmailSender sender, string userId = "", string userEmail = "")
    {
        return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("Debe adjudicar una cotización o preparar una repetición reciente para aprobación."));
    }
    public async Task<ResultadoAuditoriaOrden> CrearOrdenManualConAuditoria(OrdenCompra orden, string userId = "", string userEmail = "")
    {
        return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearError("Debe adjudicar una cotización o preparar una repetición reciente para aprobación."));
    }
    public async Task<List<OrdenCompra>> ObtenerOrdenes() =>
        (await firestore.Collection("ordenesCompra").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<OrdenCompra>()).OrderByDescending(x => x.FechaEmision).ToList();

    public async Task<ResultadoOperacion> Recibir(string id, IReadOnlyDictionary<string, decimal>? cantidades = null, bool cancelarConcurrentes = false, string userId = "", string userEmail = "") =>
        (await RecibirConAuditoria(id, cantidades, cancelarConcurrentes, userId, userEmail)).Resultado;

    public async Task<ResultadoAuditoriaOrden> RecibirConAuditoria(string id, IReadOnlyDictionary<string, decimal>? cantidades = null, bool cancelarConcurrentes = false, string userId = "", string userEmail = "")
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("La orden no existe.");
        return await InsumosTransacciones.EjecutarAsync(firestore, async transaction =>
        {
            var orderRef = firestore.Collection("ordenesCompra").Document(id);
            var orderSnapshot = await transaction.GetSnapshotAsync(orderRef);
            if (!orderSnapshot.Exists) throw new InvalidOperationException("La orden no existe.");
            var order = orderSnapshot.ConvertTo<OrdenCompra>();
            var ordenId = string.IsNullOrEmpty(order.Id) ? orderSnapshot.Id : order.Id;
            if (order.Estado == "Recibida")
                return new ResultadoAuditoriaOrden(ResultadoOperacion.CrearExito("Orden recibida correctamente."), ordenId);
            if (order.Estado == "Cancelada") throw new InvalidOperationException("La orden está cancelada.");
            if (order.Estado != "Enviada" && order.Estado != "Confirmada" && order.Estado != "Despachada")
                throw new InvalidOperationException("La orden no está disponible para recepción.");
            if (cantidades != null && (cantidades.Count == 0 || !cantidades.Any(x => x.Value > 0) || cantidades.Any(x => x.Value < 0 || !order.Items.Any(i => i.InsumoId == x.Key))))
                throw new InvalidOperationException("Las cantidades de recepción deben ser válidas y mayores a cero.");
            if (cantidades != null)
            {
                foreach (var (insumoId, cantidadSolicitada) in cantidades)
                {
                    var item = order.Items.FirstOrDefault(i => i.InsumoId == insumoId);
                    if (item == null)
                        throw new InvalidOperationException("Las cantidades de recepción deben ser válidas y mayores a cero.");
                    var restante = item.Cantidad - item.CantidadRecibida;
                    if (cantidadSolicitada > restante)
                        throw new InvalidOperationException($"La recepción de '{item.NombreInsumo}' supera la cantidad pendiente.");
                }
            }
            var antes = AuditCampos.CapturarOrdenCompra(order);
            QuerySnapshot? concurrentes = null;
            if (cancelarConcurrentes && !string.IsNullOrWhiteSpace(order.GrupoCotizacionId))
                concurrentes = await transaction.GetSnapshotAsync(firestore.Collection("ordenesCompra").WhereEqualTo("GrupoCotizacionId", order.GrupoCotizacionId));
            var saldos = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var insumoId in order.Items.Where(x => x.Cantidad - x.CantidadRecibida > 0)
                         .Select(x => x.InsumoId).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                var snap = await transaction.GetSnapshotAsync(firestore.Collection("insumos").Document(insumoId));
                if (!snap.Exists) throw new InvalidOperationException("El insumo de la orden no existe.");
                saldos[insumoId] = snap.ConvertTo<Insumo>().StockActual;
            }
            foreach (var item in order.Items.Where(x => x.Cantidad - x.CantidadRecibida > 0))
            {
                if (!saldos.TryGetValue(item.InsumoId, out var saldoActual)) continue;
                var refInsumo = firestore.Collection("insumos").Document(item.InsumoId);
                var restante = item.Cantidad - item.CantidadRecibida;
                var recibido = cantidades != null && cantidades.TryGetValue(item.InsumoId, out var solicitado)
                    ? solicitado : cantidades == null ? restante : 0;
                if (recibido <= 0) continue;
                if (recibido > restante)
                    throw new InvalidOperationException($"La recepción de '{item.NombreInsumo}' supera la cantidad pendiente.");
                recibido = DecimalFirestore.FromFirestore(DecimalFirestore.ToFirestore(recibido));
                var saldo = saldoActual + recibido;
                saldos[item.InsumoId] = saldo;
                transaction.Update(refInsumo, "StockActual", DecimalFirestore.ToFirestore(saldo));
                var movimiento = new MovimientoStock
                {
                    Id = firestore.Collection("movimientosStock").Document().Id,
                    InsumoId = item.InsumoId,
                    Tipo = nameof(TipoMovimientoStock.RecepcionOrden),
                    Cantidad = recibido,
                    SaldoResultante = saldo,
                    ReferenciaId = id,
                    Justificacion = "Recepción de orden de compra"
                };
                transaction.Create(firestore.Collection("movimientosStock").Document(movimiento.Id), movimiento);
                item.CantidadRecibida = DecimalFirestore.FromFirestore(DecimalFirestore.ToFirestore(item.CantidadRecibida + recibido));
            }
            var parcial = order.Items.Any(x => x.CantidadRecibida < x.Cantidad);
            if (!parcial && concurrentes != null)
                foreach (var rival in concurrentes.Documents.Where(x => x.Id != id))
                {
                    var rivalOrder = rival.ConvertTo<OrdenCompra>();
                    var estado = rivalOrder.Estado;
                    if (estado != "Recibida" && estado != "RecibidaParcial" && estado != "Cancelada" && estado != "RevisionManual")
                    {
                        var rivalId = string.IsNullOrEmpty(rivalOrder.Id) ? rival.Id : rivalOrder.Id;
                        var antesRival = AuditCampos.CapturarOrdenCompra(rivalOrder);
                        rivalOrder.Estado = "Cancelada";
                        rivalOrder.MotivoCancelacion = "Adjudicacion orden " + id;
                        var despuesRival = AuditCampos.CapturarOrdenCompra(rivalOrder);
                        if (HayCambiosAuditoria(antesRival, despuesRival))
                        {
                            transaction.Update(rival.Reference, new Dictionary<string, object>
                            {
                                ["Estado"] = rivalOrder.Estado,
                                ["MotivoCancelacion"] = rivalOrder.MotivoCancelacion
                            });
                            RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                            {
                                UserId = userId,
                                UserEmail = userEmail,
                                Action = "Cambio de estado de orden a Cancelada",
                                TargetId = rivalId,
                                TargetType = "OrdenCompra",
                                NombreHistorico = $"Orden {rivalId}",
                                Antes = antesRival,
                                Despues = despuesRival,
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                }
            order.Estado = parcial ? "RecibidaParcial" : "Recibida";
            var despues = AuditCampos.CapturarOrdenCompra(order);
            if (HayCambiosAuditoria(antes, despues))
            {
                transaction.Update(orderRef, new Dictionary<string, object>
                {
                    ["Estado"] = order.Estado,
                    ["Items"] = order.Items
                });
                RegistrarAuditoriaEnTransaccion(transaction, new AuditLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    Action = "Recepción de orden de compra",
                    TargetId = ordenId,
                    TargetType = "OrdenCompra",
                    NombreHistorico = $"Orden {ordenId}",
                    Antes = antes,
                    Despues = despues,
                    Timestamp = DateTime.UtcNow
                });
            }
            return new ResultadoAuditoriaOrden(
                ResultadoOperacion.CrearExito("Orden recibida correctamente."),
                ordenId,
                antes,
                despues);
        });
    }
}
