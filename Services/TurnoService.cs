using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Firebase.Models;
using Firebase.Utils;
using Google.Cloud.Firestore;
using Grpc.Core;

namespace Firebase.Services;

/// <summary>Internal persistence primitive; caller-supplied snapshots are not authoritative product validation.</summary>
internal sealed class TurnoService(FirestoreDb db)
{
    // Internal per-instance synchronization point for isolated transaction tests.
    internal Func<Task>? BeforeMembershipRead { get; set; }
    internal Func<Task>? BeforeConfigRead { get; set; }
    internal Func<Task>? AfterConfigRead { get; set; }
    internal Func<DateTime> CancellationUtcNow { get; set; } = () => DateTime.UtcNow;
    internal record SnapshotSelection(string ServicioId, string? PaqueteId);
    internal record SnapshotRequest(string Id, string ClienteId, string VehiculoId, string Origen,
        string ClaveIdempotencia, DateTime InicioUtc, bool AceptaAdelanto, IReadOnlyList<SnapshotSelection> Servicios,
        string? ClienteTrajoId = null, string? ClienteRetiraId = null, decimal Descuento = 0, string? Notas = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? CuponId = null);

    internal record EditRequest(SnapshotRequest Solicitud, string ClaveEdicion, Timestamp? VersionEsperada = null);

    /// <summary>Internal edit only; authorization remains the caller's responsibility.</summary>
    internal async Task<bool> Editar(EditRequest input) => (await EditarConDetalle(input)).Success;

    internal async Task<TurnoBookingResult> EditarConDetalle(EditRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var request = input.Solicitud ?? throw new ArgumentException("Datos de turno inválidos.");
        if (!ValidDocumentId(request.Id) || !ValidDocumentId(request.ClienteId) || !ValidDocumentId(request.VehiculoId) ||
            string.IsNullOrWhiteSpace(request.Origen) || string.IsNullOrWhiteSpace(request.ClaveIdempotencia) ||
            string.IsNullOrWhiteSpace(input.ClaveEdicion) || request.InicioUtc == default || request.InicioUtc.Kind != DateTimeKind.Utc ||
            request.InicioUtc.Ticks % 10 != 0 || request.Servicios == null || request.Servicios.Count is < 1 or > 100 ||
            request.Servicios.Any(s => s == null || !ValidDocumentId(s.ServicioId) || (s.PaqueteId != null && !ValidDocumentId(s.PaqueteId))) ||
            (request.ClienteTrajoId != null && !ValidDocumentId(request.ClienteTrajoId)) ||
            (request.ClienteRetiraId != null && !ValidDocumentId(request.ClienteRetiraId)) || request.Descuento is < 0 or > 100)
            throw new ArgumentException("Datos de turno inválidos.");
        request = request with { Servicios = request.Servicios.ToArray() };
        var json = JsonSerializer.Serialize(request);
        var markerRef = db.Collection("turno_ediciones").Document(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ClaveEdicion))));
        var reference = db.Collection("turnos").Document(request.Id);
        var lockRef = db.Collection("coordinacion").Document("turnos");
        return await RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            var capacity = await tx.GetSnapshotAsync(lockRef);
            var marker = await tx.GetSnapshotAsync(markerRef);
            var document = await tx.GetSnapshotAsync(reference);
            if (!document.Exists) throw new InvalidOperationException("Turno no encontrado.");
            var current = document.ConvertTo<Turno>();
            if (current.Id != request.Id || current.Validar().Count != 0 || current.Estado != "Pendiente")
                throw new InvalidOperationException("El turno no está pendiente o es inválido.");
            var bookingMarker = await tx.GetSnapshotAsync(db.Collection("turno_solicitudes").Document(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(current.ClaveIdempotencia)))));
            var couponRebuild = current.CuponId != null && (current.Vehiculos[0].VehiculoId != request.VehiculoId ||
                !current.Vehiculos[0].Servicios.Select(s => new SnapshotSelection(s.ServicioId, s.PaqueteId)).SequenceEqual(request.Servicios));
            if (current.CuponId != null || request.CuponId != null || (bookingMarker.Exists && bookingMarker.TryGetValue<string>("Esquema", out var bookingSchema) && (bookingSchema == "ids-v3" || bookingSchema == "ids-v4")))
            {
                SnapshotRequest? original = null;
                if (bookingMarker.Exists && bookingMarker.TryGetValue<string>("Esquema", out var schema) && (schema == "ids-v3" || schema == "ids-v4") &&
                    bookingMarker.TryGetValue<string>("Solicitud", out var stored))
                {
                    try { original = JsonSerializer.Deserialize<SnapshotRequest>(stored); }
                    catch (JsonException) { }
                    if (original != null && (!MatchesMarker(schema, stored, original) ||
                        (schema == "ids-v4" && !ValidFrozenCoupon(bookingMarker, original, document)))) original = null;
                }
                if (original == null || current.CuponId == null || original.CuponId != current.CuponId || request.CuponId != current.CuponId ||
                    original.Id != current.Id || original.ClienteId != current.ClienteId ||
                    original.Origen != current.Origen || original.ClaveIdempotencia != current.ClaveIdempotencia || original.Descuento != 0 ||
                    original.Servicios == null || (couponRebuild && (!bookingMarker.TryGetValue<string>("Esquema", out var rebuildSchema) || rebuildSchema != "ids-v4")) ||
                    request.ClienteId != current.ClienteId || request.Descuento != 0 ||
                    (request.ClienteTrajoId ?? current.ClienteTrajoId) != current.ClienteTrajoId ||
                    (request.ClienteRetiraId ?? current.ClienteRetiraId) != current.ClienteRetiraId || (request.Notas ?? string.Empty) != current.Notas)
                    throw new InvalidOperationException("Edición de cupón no admitida.");
            }
            if (marker.Exists)
            {
                if (!marker.TryGetValue<string>("Solicitud", out var previous) || previous != json ||
                    !marker.TryGetValue<string>("Resultado", out var result) || result != JsonSerializer.Serialize(current))
                    throw new InvalidOperationException("Edición incompatible.");
                return new TurnoBookingResult(true);
            }
            if (input.VersionEsperada != null && document.UpdateTime != input.VersionEsperada)
                throw new InvalidOperationException("Turno modificado concurrentemente.");
            if (current.Origen != request.Origen || current.ClaveIdempotencia != request.ClaveIdempotencia)
                throw new InvalidOperationException("Identidad de turno incompatible.");
            var offers = await tx.GetSnapshotAsync(db.Collection("turno_ofertas").WhereEqualTo("AppointmentId", request.Id).Limit(1));
            var anchor = await tx.GetSnapshotAsync(db.Collection("turno_adelanto_anclas").Document(request.Id));
            if (offers.Count != 0 || anchor.Exists) throw new InvalidOperationException("Historial de adelanto incompatible.");
            var selections = current.Vehiculos[0].Servicios.Select(s => new SnapshotSelection(s.ServicioId, s.PaqueteId)).ToArray();
            var rebuild = current.ClienteId != request.ClienteId || current.Vehiculos[0].VehiculoId != request.VehiculoId ||
                (current.CuponId == null && current.Descuento != request.Descuento) || !selections.SequenceEqual(request.Servicios);
            var brought = request.ClienteTrajoId ?? current.ClienteTrajoId;
            var pickup = request.ClienteRetiraId ?? current.ClienteRetiraId;
            var notes = request.Notas ?? string.Empty;
            var same = !rebuild && current.InicioUtc == request.InicioUtc && current.AceptaAdelanto == request.AceptaAdelanto &&
                current.ClienteTrajoId == brought && current.ClienteRetiraId == pickup && current.Notas == notes;
            if (!rebuild) TurnoLavadoSnapshotMapper.Mapear(current);
            if (same)
            {
                tx.Create(markerRef, new Dictionary<string, object> { ["Solicitud"] = json, ["Resultado"] = JsonSerializer.Serialize(current) });
                return new TurnoBookingResult(true);
            }
            var membership = await EmployeeMembershipVersion.ReadAsync(db, tx);
            var employees = await tx.GetSnapshotAsync(db.Collection("empleados").WhereEqualTo("Estado", "Activo"));
            var reservations = await tx.GetSnapshotAsync(db.Collection("turnos").WhereEqualTo("Estado", "Pendiente"));
            var started = await tx.GetSnapshotAsync(db.Collection("turnos").WhereEqualTo("Estado", "EnProceso"));
            var configDoc = await tx.GetSnapshotAsync(db.Collection("configuracion").Document("system_config"));
            if (!configDoc.Exists) throw new InvalidOperationException("Configuración no encontrada.");
            var config = configDoc.ConvertTo<Configuracion>();
            if (config.CapacidadMaximaConcurrente <= 0 || config.IntervaloInicioTurnosMinutos <= 0)
                throw new InvalidOperationException("Configuración inválida.");
            Turno candidate;
            if (rebuild || current.ClienteTrajoId != brought || current.ClienteRetiraId != pickup)
            {
                var snapshotRequest = request with { ClienteTrajoId = brought, ClienteRetiraId = pickup,
                    Descuento = rebuild && current.CuponId != null ? current.Descuento : request.Descuento };
                var validated = await LeerSnapshot(tx, snapshotRequest, request.Servicios.ToArray(), forecast: rebuild);
                validated.CuponId = current.CuponId;
                validated.FechaCreacionUtc = current.FechaCreacionUtc;
                candidate = rebuild ? validated : current;
            }
            else candidate = current;
            if (rebuild) { }
            else
            {
                candidate = current;
                candidate.InicioUtc = request.InicioUtc;
                candidate.FinUtc = request.InicioUtc.AddTicks(current.FinUtc.Ticks - document.GetValue<DateTime>("InicioUtc").Ticks);
                candidate.AceptaAdelanto = request.AceptaAdelanto;
                candidate.ClienteTrajoId = brought; candidate.ClienteRetiraId = pickup; candidate.Notas = notes;
            }
            await ValidarLavadosIniciados(tx, started.Documents);
            var intervals = reservations.Documents.Concat(started.Documents).Where(d => d.Id != request.Id).Select(d =>
            {
                var item = d.ConvertTo<Turno>();
                if (item.Id != d.Id || item.Validar().Count != 0 || (item.Estado == "EnProceso" &&
                    (!d.TryGetValue<string>("LavadoId", out var washId) || !ValidDocumentId(washId))))
                    throw new InvalidOperationException("Turno persistido inválido.");
                return new TurnoInterval(item.InicioUtc, item.FinUtc, item.Vehiculos[0].CantidadEmpleadosRequeridos);
            }).ToArray();
            var openings = ObtenerAperturas(config, candidate.InicioUtc, out var gridAnchor);
            if (employees.Count == 0)
                return new TurnoBookingResult(false, "No hay empleados activos en el lavadero para atender turnos.");
            var availability = TurnoAvailabilityPolicy.CheckAvailability(candidate.InicioUtc,
                candidate.Vehiculos[0].Servicios.Select(s => s.TiempoEstimado), candidate.Vehiculos[0].CantidadEmpleadosRequeridos,
                config.CapacidadMaximaConcurrente, employees.Count, openings, intervals, DateTime.UtcNow, gridAnchor, config.IntervaloInicioTurnosMinutos);
            if (!availability.IsAvailable)
                return new TurnoBookingResult(false, availability.Message);
            var updates = new Dictionary<string, object?> { ["ClienteId"] = candidate.ClienteId, ["InicioUtc"] = candidate.InicioUtc,
                ["FinUtc"] = candidate.FinUtc, ["AceptaAdelanto"] = candidate.AceptaAdelanto, ["ClienteTrajoId"] = candidate.ClienteTrajoId,
                ["ClienteRetiraId"] = candidate.ClienteRetiraId, ["Notas"] = candidate.Notas,
                ["ClienteNombre"] = candidate.ClienteNombre, ["ClienteTrajoNombre"] = candidate.ClienteTrajoNombre, ["ClienteRetiraNombre"] = candidate.ClienteRetiraNombre,
                ["VehiculoPatente"] = candidate.VehiculoPatente, ["TipoVehiculo"] = candidate.TipoVehiculo };
            if (rebuild)
            {
                updates["Descuento"] = (double)candidate.Descuento; updates["PrecioAcordado"] = (double)candidate.PrecioAcordado;
                updates["DesglosePrecio"] = candidate.DesglosePrecio; updates["Vehiculos"] = candidate.Vehiculos;
                updates["InsumosForecast"] = candidate.InsumosForecast; updates["RecetaOperativa"] = candidate.RecetaOperativa;
            }
            tx.Update(reference, updates);
            tx.Create(markerRef, new Dictionary<string, object> { ["Solicitud"] = json, ["Resultado"] = JsonSerializer.Serialize(candidate) });
            var version = capacity.Exists ? capacity.GetValue<long>("Version") : 0;
            tx.Set(lockRef, new Dictionary<string, object> { ["Version"] = checked(version + 1) });
            EmployeeMembershipVersion.Advance(tx, membership);
            return new TurnoBookingResult(true);
        }));
    }

    /// <summary>Read-only preview. It becomes stale after this transaction and must not authorize Crear.</summary>
    internal async Task<Turno> ConstruirSnapshot(SnapshotRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ValidDocumentId(input.Id) || !ValidDocumentId(input.ClienteId) || !ValidDocumentId(input.VehiculoId) ||
            input.InicioUtc == default || input.InicioUtc.Kind != DateTimeKind.Utc ||
            input.Servicios == null || input.Servicios.Count is < 1 or > 100 ||
            input.Servicios.Any(s => s == null || !ValidDocumentId(s.ServicioId) ||
                (s.PaqueteId != null && !ValidDocumentId(s.PaqueteId))))
            throw new ArgumentException("Datos de turno inválidos.", nameof(input));
        if ((input.ClienteTrajoId != null && !ValidDocumentId(input.ClienteTrajoId)) ||
            (input.ClienteRetiraId != null && !ValidDocumentId(input.ClienteRetiraId)) || input.Descuento is < 0 or > 100)
            throw new ArgumentException("Datos de turno inválidos.", nameof(input));
        if (input.CuponId != null) throw new ArgumentException("Cupón no admitido en vista previa.");
        var selections = input.Servicios.ToArray();
        return await RetryAborted(() => db.RunTransactionAsync(tx => LeerSnapshot(tx, input, selections)));
    }

    private async Task<Turno> LeerSnapshot(Transaction tx, SnapshotRequest input, SnapshotSelection[] selections, bool forecast = false)
    {
            var client = await tx.GetSnapshotAsync(db.Collection("clientes").Document(input.ClienteId));
            var vehicleDoc = await tx.GetSnapshotAsync(db.Collection("vehiculos").Document(input.VehiculoId));
            if (!client.Exists || !vehicleDoc.Exists) throw new InvalidOperationException("Cliente o vehículo no encontrado.");
            var vehicle = vehicleDoc.ConvertTo<Vehiculo>();
            if (vehicle.ClienteId != input.ClienteId && !(vehicle.ClientesIds?.Contains(input.ClienteId) ?? false))
                throw new InvalidOperationException("Vehículo no asociado al cliente.");
            foreach (var person in new[] { input.ClienteTrajoId, input.ClienteRetiraId }.Where(id => id != null).Distinct())
            {
                if (person != vehicle.ClienteId && !(vehicle.ClientesIds?.Contains(person!) ?? false))
                    throw new InvalidOperationException("Cliente no asociado al vehículo.");
                if (!(await tx.GetSnapshotAsync(db.Collection("clientes").Document(person!))).Exists)
                    throw new InvalidOperationException("Cliente no encontrado.");
            }
            var types = await tx.GetSnapshotAsync(db.Collection("tiposVehiculos").WhereEqualTo("Nombre", vehicle.TipoVehiculo).Limit(2));
            if (types.Count != 1) throw new InvalidOperationException("Tipo de vehículo inexistente o ambiguo.");
            var services = new List<TurnoSnapshotBuilder.Seleccion>();
            foreach (var selection in selections)
            {
                var doc = await tx.GetSnapshotAsync(db.Collection("servicios").Document(selection.ServicioId));
                if (!doc.Exists) throw new InvalidOperationException("Servicio no encontrado.");
                var service = doc.ConvertTo<Servicio>();
                service.Id = doc.Id;
                services.Add(new(service, selection.PaqueteId));
            }
            var packages = new List<PaqueteServicio>();
            foreach (var id in selections.Select(s => s.PaqueteId).Where(id => id != null).Distinct())
            {
                var doc = await tx.GetSnapshotAsync(db.Collection("paquetes_servicios").Document(id!));
                if (!doc.Exists) throw new InvalidOperationException("Paquete no encontrado.");
                var package = doc.ConvertTo<PaqueteServicio>();
                package.Id = doc.Id;
                packages.Add(package);
            }
            vehicle.Id = vehicleDoc.Id;
            string? clientNombre = null;
            if (client.Exists)
            {
                var d = client.ToDictionary();
                if (d.TryGetValue("NombreCompleto", out var nc) && nc is string nct && !string.IsNullOrWhiteSpace(nct))
                    clientNombre = nct.Trim();
                else
                {
                    var n = d.TryGetValue("Nombre", out var nv) && nv is string nt ? nt.Trim() : "";
                    var a = d.TryGetValue("Apellido", out var av) && av is string at ? at.Trim() : "";
                    var full = $"{n} {a}".Trim();
                    clientNombre = string.IsNullOrWhiteSpace(full) ? null : full;
                }
            }
            string? trajoNombre = clientNombre;
            string? retiraNombre = clientNombre;
            if (input.ClienteTrajoId != null && input.ClienteTrajoId != input.ClienteId)
            {
                var trajoDoc = await tx.GetSnapshotAsync(db.Collection("clientes").Document(input.ClienteTrajoId));
                if (trajoDoc.Exists)
                {
                    var td = trajoDoc.ToDictionary();
                    if (td.TryGetValue("NombreCompleto", out var tnc) && tnc is string tnct && !string.IsNullOrWhiteSpace(tnct))
                        trajoNombre = tnct.Trim();
                    else
                    {
                        var n = td.TryGetValue("Nombre", out var nv) && nv is string nt ? nt.Trim() : "";
                        var a = td.TryGetValue("Apellido", out var av) && av is string at ? at.Trim() : "";
                        var full = $"{n} {a}".Trim();
                        trajoNombre = string.IsNullOrWhiteSpace(full) ? null : full;
                    }
                }
            }
            if (input.ClienteRetiraId != null && input.ClienteRetiraId != input.ClienteId)
            {
                if (input.ClienteRetiraId == input.ClienteTrajoId) retiraNombre = trajoNombre;
                else
                {
                    var retiraDoc = await tx.GetSnapshotAsync(db.Collection("clientes").Document(input.ClienteRetiraId));
                    if (retiraDoc.Exists)
                    {
                        var rd = retiraDoc.ToDictionary();
                        if (rd.TryGetValue("NombreCompleto", out var rnc) && rnc is string rnct && !string.IsNullOrWhiteSpace(rnct))
                            retiraNombre = rnct.Trim();
                        else
                        {
                            var n = rd.TryGetValue("Nombre", out var nv) && nv is string nt ? nt.Trim() : "";
                            var a = rd.TryGetValue("Apellido", out var av) && av is string at ? at.Trim() : "";
                            var full = $"{n} {a}".Trim();
                            retiraNombre = string.IsNullOrWhiteSpace(full) ? null : full;
                        }
                    }
                }
            }
            var result = TurnoSnapshotBuilder.Build(new(input.Id, input.ClienteId, input.Origen, input.ClaveIdempotencia,
                input.InicioUtc, input.AceptaAdelanto, vehicle, types.Documents[0].ConvertTo<TipoVehiculo>(), services, packages,
                input.Descuento, input.ClienteTrajoId, input.ClienteRetiraId, input.Notas,
                clientNombre, trajoNombre, retiraNombre));
            if (forecast)
            {
                // These are the same catalog objects read inside the reservation transaction.
                result.RecetaOperativa = TurnoSnapshotBuilder.CapturarRecetaOperativa(services.Select(s => s.Servicio));
                result.InsumosForecast = TurnoInsumosForecast.Proyectar(services.Select(s => s.Servicio)).Select(d => new DemandaInsumoTurno
                {
                    ServicioId = d.ServicioId, EtapaId = d.EtapaId, Modalidad = d.Modalidad, TipoId = d.TipoId,
                    NombreTipo = d.NombreTipo, Categoria = d.Categoria, InsumoId = d.InsumoId,
                    NombreInsumo = d.NombreInsumo, UnidadMedida = d.UnidadMedida, DosisTeorica = d.DosisTeorica
                }).ToList();
            }
            return result;
    }

    internal record BookingAuthorization(string Phone, string Incarnation, long Revision, string Selection, int Tolerance);

    internal Task<bool> Reservar(SnapshotRequest input) => ReservarCore(input, null).ContinueWith(t => t.Result.Success);

    internal Task<TurnoBookingResult> ReservarConDetalle(SnapshotRequest input) => ReservarCore(input, null);

    internal Task<bool> ReservarWhatsApp(SnapshotRequest input, BookingAuthorization authorization) =>
        ReservarWhatsAppConDetalle(input, authorization).ContinueWith(t => t.Result.Success);

    internal Task<TurnoBookingResult> ReservarWhatsAppConDetalle(SnapshotRequest input, BookingAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        if (!ValidDocumentId(authorization.Phone) || string.IsNullOrWhiteSpace(authorization.Incarnation) ||
            authorization.Revision < 0 || authorization.Revision == long.MaxValue ||
            string.IsNullOrWhiteSpace(authorization.Selection) || authorization.Tolerance is < 0 or > 525600 ||
            input == null || input.Origen != "WhatsApp")
            throw new ArgumentException("Autorización de sesión inválida.");
        return ReservarCore(input, authorization);
    }

    private async Task<TurnoBookingResult> ReservarCore(SnapshotRequest input, BookingAuthorization? authorization)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ValidDocumentId(input.Id) || !ValidDocumentId(input.ClienteId) || !ValidDocumentId(input.VehiculoId) ||
            string.IsNullOrWhiteSpace(input.Origen) || string.IsNullOrWhiteSpace(input.ClaveIdempotencia) ||
            input.InicioUtc == default || input.InicioUtc.Kind != DateTimeKind.Utc || input.Servicios == null ||
            input.Servicios.Count is < 1 or > 100 || input.Servicios.Any(s => s == null || !ValidDocumentId(s.ServicioId) || (s.PaqueteId != null && !ValidDocumentId(s.PaqueteId))))
            throw new ArgumentException("Datos de turno inválidos.", nameof(input));
        if ((input.ClienteTrajoId != null && !ValidDocumentId(input.ClienteTrajoId)) ||
            (input.ClienteRetiraId != null && !ValidDocumentId(input.ClienteRetiraId)) || input.Descuento is < 0 or > 100)
            throw new ArgumentException("Datos de turno inválidos.", nameof(input));
        if (input.CuponId != null && (!ValidDocumentId(input.CuponId) || input.Descuento != 0))
            throw new ArgumentException("Cupón inválido.", nameof(input));
        var frozen = input with { Servicios = input.Servicios.ToArray() };
        var json = JsonSerializer.Serialize(frozen);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(frozen.ClaveIdempotencia)));
        var requestRef = db.Collection("turno_solicitudes").Document(key);
        var appointmentRef = db.Collection("turnos").Document(frozen.Id);
        var lockRef = db.Collection("coordinacion").Document("turnos");
        return await RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            var capacity = await tx.GetSnapshotAsync(lockRef);
            var request = await tx.GetSnapshotAsync(requestRef);
            var appointment = await tx.GetSnapshotAsync(appointmentRef);
            if (request.Exists)
            {
                if (!request.TryGetValue<string>("Esquema", out var schema) ||
                    !request.TryGetValue<string>("Solicitud", out var stored) || !MatchesMarker(schema, stored, frozen))
                    throw new InvalidOperationException("Clave de idempotencia reutilizada con otro turno.");
                if (!appointment.Exists)
                    throw new InvalidOperationException("El turno de la solicitud no existe.");
                if (!ValidReplayAppointment(appointment))
                    throw new InvalidOperationException("Turno persistido inválido.");
                if (schema == "ids-v4" && !ValidFrozenCoupon(request, frozen, appointment))
                    throw new InvalidOperationException("Términos de cupón inválidos.");
                return new TurnoBookingResult(true);
            }
            if (appointment.Exists) throw new InvalidOperationException("El turno ya existe.");
            DocumentSnapshot? session = null;
            if (authorization is { } auth)
            {
                session = await tx.GetSnapshotAsync(db.Collection("whatsapp_sessions").Document(auth.Phone));
                var data = session.Exists ? session.ToDictionary() : null;
                if (data == null || !data.TryGetValue("Id", out var phone) || !Equals(phone, auth.Phone) ||
                    !data.TryGetValue("ClienteId", out var client) || !Equals(client, frozen.ClienteId) ||
                    !data.TryGetValue("IncarnationId", out var incarnation) || !Equals(incarnation, auth.Incarnation) ||
                    !data.TryGetValue("Revision", out var revision) || !Equals(revision, auth.Revision) ||
                    !data.TryGetValue("CurrentState", out var state) || !Equals(state, "TURNO_RESERVA_CONFIRMAR") ||
                    !data.TryGetValue("SelectionId", out var selection) || !Equals(selection, auth.Selection) ||
                    !data.TryGetValue("BookingRequest", out var booking) || !Equals(booking, json) ||
                    !data.TryGetValue("BookingTolerance", out var tolerance) || !Equals(tolerance, (long)auth.Tolerance))
                    throw new InvalidOperationException("Autorización de sesión inválida.");
            }
            if (BeforeMembershipRead != null) await BeforeMembershipRead();
            var membership = await EmployeeMembershipVersion.ReadAsync(db, tx);
            var employees = await tx.GetSnapshotAsync(db.Collection("empleados").WhereEqualTo("Estado", "Activo"));
            var reservations = await tx.GetSnapshotAsync(db.Collection("turnos").WhereEqualTo("Estado", "Pendiente"));
            var started = await tx.GetSnapshotAsync(db.Collection("turnos").WhereEqualTo("Estado", "EnProceso"));
            if (BeforeConfigRead != null) await BeforeConfigRead();
            var configDoc = await tx.GetSnapshotAsync(db.Collection("configuracion").Document("system_config"));
            if (AfterConfigRead != null) await AfterConfigRead();
            if (!configDoc.Exists) throw new InvalidOperationException("Configuración no encontrada.");
            if (!configDoc.ToDictionary().TryGetValue("ToleranciaLlegadaTurnosMinutos", out var rawGrace) ||
                rawGrace is not long grace || grace < 0 || grace > 525600)
                throw new InvalidOperationException(authorization == null ? "Tolerancia de llegada no disponible." : "Tolerancia de llegada modificada o no disponible.");
            if (authorization is { } disclosed && grace != disclosed.Tolerance)
                throw new InvalidOperationException("Tolerancia de llegada modificada o no disponible.");
            var config = configDoc.ConvertTo<Configuracion>();
            var couponRef = frozen.CuponId == null ? null : db.Collection("turno_cupones").Document(frozen.CuponId);
            var percentage = couponRef == null ? 0m : (decimal)await TurnoCuponRedemption.Preparar(tx, couponRef, frozen.ClienteId, false);
            var candidate = await LeerSnapshot(tx, frozen with { Descuento = couponRef == null ? frozen.Descuento : percentage }, frozen.Servicios.ToArray(), forecast: true);
            candidate.CuponId = frozen.CuponId;
            var ahoraUtc = DateTime.UtcNow;
            candidate.FechaCreacionUtc = new DateTime(ahoraUtc.Ticks - (ahoraUtc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
            var openings = ObtenerAperturas(config, candidate.InicioUtc, out var anchor);
            await ValidarLavadosIniciados(tx, started.Documents);
            var intervals = reservations.Documents.Concat(started.Documents).Select(doc =>
            {
                var item = doc.ConvertTo<Turno>();
                if (item.Id != doc.Id || item.Validar().Count != 0 || (item.Estado == "EnProceso" &&
                    (!doc.TryGetValue<string>("LavadoId", out var washId) || !ValidDocumentId(washId))))
                    throw new InvalidOperationException("Turno persistido inválido.");
                return new TurnoInterval(item.InicioUtc, item.FinUtc, item.Vehiculos[0].CantidadEmpleadosRequeridos);
            }).ToArray();
            if (config.CapacidadMaximaConcurrente <= 0 || config.IntervaloInicioTurnosMinutos <= 0)
                throw new InvalidOperationException("Configuración inválida.");
            if (employees.Count == 0)
                return new TurnoBookingResult(false, "No hay empleados activos en el lavadero para atender turnos.");
            var availability = TurnoAvailabilityPolicy.CheckAvailability(candidate.InicioUtc,
                candidate.Vehiculos[0].Servicios.Select(s => s.TiempoEstimado), candidate.Vehiculos[0].CantidadEmpleadosRequeridos,
                config.CapacidadMaximaConcurrente, employees.Count, openings, intervals, DateTime.UtcNow, anchor, config.IntervaloInicioTurnosMinutos);
            if (!availability.IsAvailable)
                return new TurnoBookingResult(false, availability.Message);
            if (couponRef != null) tx.Update(couponRef, "Consumed", true);
            if (authorization is { } confirmed && session != null)
                tx.Update(session.Reference, new Dictionary<string, object> {
                    ["Revision"] = confirmed.Revision + 1, ["CurrentState"] = "MENU_CLIENTE_AUTENTICADO",
                    ["SelectionId"] = FieldValue.Delete, ["BookingRequest"] = FieldValue.Delete,
                    ["BookingTolerance"] = FieldValue.Delete, ["LastInteraction"] = DateTime.UtcNow });
            tx.Create(appointmentRef, candidate);
            var marker = new Dictionary<string, object> { ["Esquema"] = couponRef == null ? "ids-v2" : "ids-v4", ["Solicitud"] = json };
            if (couponRef != null)
            {
                marker["CuponId"] = frozen.CuponId!;
                marker["CuponOwner"] = frozen.ClienteId;
                marker["CuponPorcentaje"] = (double)percentage;
            }
            tx.Create(requestRef, marker);
            var version = capacity.Exists ? capacity.GetValue<long>("Version") : 0;
            tx.Set(lockRef, new Dictionary<string, object> { ["Version"] = checked(version + 1) });
            EmployeeMembershipVersion.Advance(tx, membership);
            return new TurnoBookingResult(true);
        }));
    }

    private async Task ValidarLavadosIniciados(Transaction tx, IEnumerable<DocumentSnapshot> appointments)
    {
        foreach (var appointment in appointments)
        {
            if (!appointment.TryGetValue<string>("LavadoId", out var washId) || !ValidDocumentId(washId))
                throw new InvalidOperationException("Turno persistido inválido.");
            var wash = await tx.GetSnapshotAsync(db.Collection("lavados").Document(washId));
            if (!wash.Exists || !wash.TryGetValue<string>("TurnoId", out var linked) || linked != appointment.Id ||
                !wash.TryGetValue<string>("Estado", out var state) || state != "EnProceso")
                throw new InvalidOperationException("Turno persistido inválido.");
        }
    }

    private static bool ValidReplayAppointment(DocumentSnapshot appointment)
    {
        var fields = appointment.ToDictionary();
        return fields.TryGetValue("Estado", out var state) && state is string status &&
            status is "Pendiente" or "Cancelado" or "Ausente" or "EnProceso" or "Completado" &&
            appointment.ConvertTo<Turno>().Validar().Count == 0;
    }

    private static bool ValidFrozenCoupon(DocumentSnapshot marker, SnapshotRequest request, DocumentSnapshot appointment)
    {
        var fields = marker.ToDictionary();
        var saved = appointment.ToDictionary();
        return fields.TryGetValue("CuponId", out var id) && id is string couponId && couponId == request.CuponId &&
            fields.TryGetValue("CuponOwner", out var owner) && owner is string couponOwner && couponOwner == request.ClienteId &&
            fields.TryGetValue("CuponPorcentaje", out var value) && value is double percentage &&
            double.IsFinite(percentage) && percentage >= 0 && percentage <= 100 &&
            saved.TryGetValue("Id", out var appointmentId) && appointmentId is string aId && aId == request.Id &&
            saved.TryGetValue("ClienteId", out var clientId) && clientId is string client && client == couponOwner &&
            saved.TryGetValue("CuponId", out var savedId) && savedId is string savedCoupon && savedCoupon == couponId &&
            saved.TryGetValue("Descuento", out var discount) && discount is double savedPercentage && savedPercentage == percentage;
    }

    internal static bool MatchesMarker(string? schema, string? json, SnapshotRequest request)
    {
        if (json == null || request == null) return false;
        if (schema == "ids-v3" || schema == "ids-v4") return request.CuponId != null && json == JsonSerializer.Serialize(request);
        if (schema == "ids-v2") return request.CuponId == null && json == JsonSerializer.Serialize(request);
        if (request.CuponId != null) return false;
        if (schema != "ids-v1" || request.ClienteTrajoId != null || request.ClienteRetiraId != null ||
            request.Descuento != 0 || request.Notas != null) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!ExactFields(root, "Id", "ClienteId", "VehiculoId", "Origen", "ClaveIdempotencia", "InicioUtc", "AceptaAdelanto", "Servicios")) return false;
            var services = root.GetProperty("Servicios");
            if (services.ValueKind != JsonValueKind.Array || request.Servicios == null || services.GetArrayLength() != request.Servicios.Count) return false;
            foreach (var item in services.EnumerateArray())
                if (!ExactFields(item, "ServicioId", "PaqueteId")) return false;
            var old = JsonSerializer.Deserialize<SnapshotRequest>(json);
            return old != null && old.Id == request.Id && old.ClienteId == request.ClienteId &&
                old.VehiculoId == request.VehiculoId && old.Origen == request.Origen &&
                old.ClaveIdempotencia == request.ClaveIdempotencia && old.InicioUtc == request.InicioUtc &&
                old.InicioUtc.Kind == DateTimeKind.Utc && old.AceptaAdelanto == request.AceptaAdelanto &&
                old.Servicios != null && old.Servicios.SequenceEqual(request.Servicios);
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool ExactFields(JsonElement element, params string[] fields)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var names = element.EnumerateObject().Select(p => p.Name).ToArray();
        return names.Length == fields.Length && names.Distinct(StringComparer.Ordinal).Count() == fields.Length &&
            fields.All(name => names.Contains(name, StringComparer.Ordinal));
    }

    internal static TurnoOpening[] ObtenerAperturas(Configuracion config, DateTime start, out DateTime anchor)
    {
        var zone = TimeZoneHelper.ArgentinaZone;
        var local = TimeZoneInfo.ConvertTimeFromUtc(start, zone);
        var midnight = DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(midnight) || zone.IsAmbiguousTime(midnight)) throw new InvalidOperationException("Horario ambiguo.");
        anchor = TimeZoneInfo.ConvertTimeToUtc(midnight, zone);
        var day = local.DayOfWeek switch { DayOfWeek.Monday => "Lunes", DayOfWeek.Tuesday => "Martes", DayOfWeek.Wednesday => "Miércoles", DayOfWeek.Thursday => "Jueves", DayOfWeek.Friday => "Viernes", DayOfWeek.Saturday => "Sábado", _ => "Domingo" };
        if (config.HorariosOperacion == null || !config.HorariosOperacion.TryGetValue(day, out var schedule) || string.IsNullOrWhiteSpace(schedule)) throw new InvalidOperationException("Horario inválido.");
        if (string.Equals(schedule.Trim(), "CERRADO", StringComparison.OrdinalIgnoreCase)) return [];
        var openings = new List<TurnoOpening>();
        foreach (var segment in schedule.Split(','))
        {
            var parts = segment.Split('-');
            if (parts.Length != 2 || !TimeSpan.TryParse(parts[0].Trim(), out var from) || !TimeSpan.TryParse(parts[1].Trim(), out var to) || from < TimeSpan.Zero || to >= TimeSpan.FromDays(1) || from >= to)
                throw new InvalidOperationException("Horario inválido.");
            var begin = midnight.Add(from); var end = midnight.Add(to);
            if (zone.IsInvalidTime(begin) || zone.IsAmbiguousTime(begin) || zone.IsInvalidTime(end) || zone.IsAmbiguousTime(end)) throw new InvalidOperationException("Horario ambiguo.");
            openings.Add(new(TimeZoneInfo.ConvertTimeToUtc(begin, zone), TimeZoneInfo.ConvertTimeToUtc(end, zone)));
        }
        return openings.ToArray();
    }

    private static bool ValidDocumentId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && !id.Contains('/') && id is not "." and not ".." &&
        !(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) &&
        Encoding.UTF8.GetByteCount(id) <= 1500;

    // Firestore may abort while executing a transactional query, before the SDK's commit retry.
    // Retry only ABORTED: never retry an ambiguous transport or commit failure here.
    internal static async Task<T> RetryAborted<T>(Func<Task<T>> operation)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await operation(); }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted && attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (1 << attempt)));
            }
        }
    }

    internal static async Task<T> RetryAborted<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return await operation(); }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted && attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (1 << attempt)), cancellationToken);
            }
        }
    }

    /// <summary>Internal deadline transition using the persisted grace, including zero; unconfigured grace fails closed.</summary>
    internal async Task<bool> MarcarAusencia(string id, DateTime now)
    {
        if (!ValidDocumentId(id) || now == default || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Datos de ausencia inválidos.");
        var coordinator = db.Collection("coordinacion").Document("turnos");
        var reference = db.Collection("turnos").Document(id);
        return await RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            // Match conversion: coordinator first, then appointment.
            var capacity = await tx.GetSnapshotAsync(coordinator);
            var appointment = await tx.GetSnapshotAsync(reference);
            if (!appointment.Exists) throw new InvalidOperationException("Turno no encontrado.");
            var state = appointment.GetValue<string>("Estado");
            if (state == "Ausente") return true;
            if (state != "Pendiente") return false;
            if (appointment.ContainsField("LavadoId")) throw new InvalidOperationException("Vínculo de lavado incompatible.");
            if (!appointment.TryGetValue<DateTime>("InicioUtc", out var start) || start == default || start.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException("Intervalo de turno inválido.");
            var config = await tx.GetSnapshotAsync(db.Collection("configuracion").Document("system_config"));
            long grace = 5;
            if (config.Exists && config.ToDictionary().TryGetValue("ToleranciaLlegadaTurnosMinutos", out var raw) && raw is long g && g >= 5)
                grace = g;
            if (grace < 5 || grace > 525600)
                throw new InvalidOperationException("Tolerancia de llegada no disponible.");
            // Release strictly after the configured deadline; no extra grace is added.
            if (now.Ticks - start.Ticks <= grace * TimeSpan.TicksPerMinute) return false;
            var version = capacity.Exists ? capacity.GetValue<long>("Version") : 0;
            tx.Update(reference, "Estado", "Ausente");
            tx.Set(coordinator, new Dictionary<string, object> { ["Version"] = checked(version + 1) });
            return true;
        }));
    }

    internal static string CancellationCascadeId(string id) =>
        "cancel-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));

    internal static string CancellationCouponId(string id) =>
        "cancel-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));

    /// <summary>Internal transition only; the caller must authorize the actor.</summary>
    internal Task Cancelar(string id) => CancelarInterno(id, null);

    internal Task CancelarWeb(string id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("El motivo de cancelación es obligatorio", nameof(motivo));
        return CancelarInterno(id, null, motivo: motivo);
    }

    /// <summary>Caller must supply the authenticated session's client ID, never a request-provided owner.</summary>
    internal Task CancelarDePropietario(string id, string owner)
    {
        if (!ValidDocumentId(owner)) throw new ArgumentException("Propietario inválido.", nameof(owner));
        return CancelarInterno(id, owner);
    }

    internal Task CancelarWhatsApp(string id, string owner, string phone, string incarnation, long revision, string selection)
    {
        if (!ValidDocumentId(owner) || !ValidDocumentId(phone) || string.IsNullOrWhiteSpace(incarnation) ||
            string.IsNullOrWhiteSpace(selection) || revision < 0 || revision == long.MaxValue)
            throw new ArgumentException("Autorización inválida.");
        return CancelarInterno(id, owner, (phone, incarnation, revision, selection));
    }

    private async Task CancelarInterno(string id, string? owner, (string Phone, string Incarnation, long Revision, string Selection)? authorization = null, string? motivo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var turnoRef = db.Collection("turnos").Document(id);
        var lockRef = db.Collection("coordinacion").Document("turnos");
        var cascadeId = CancellationCascadeId(id);
        var cascadeRef = db.Collection("turno_cascadas").Document(cascadeId);
        var couponRef = db.Collection("turno_cupones").Document(CancellationCouponId(id));
        var now = CancellationUtcNow();
        await RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            // Match booking's capacity-before-reservations read order to avoid lock inversion.
            var capacityLock = await tx.GetSnapshotAsync(lockRef);
            DocumentSnapshot? session = null;
            if (authorization is { } auth)
            {
                session = await tx.GetSnapshotAsync(db.Collection("whatsapp_sessions").Document(auth.Phone));
                var data = session.Exists ? session.ToDictionary() : null;
                if (data == null || !data.TryGetValue("Id", out var phone) || !Equals(phone, auth.Phone) ||
                    !data.TryGetValue("ClienteId", out var client) || !Equals(client, owner) ||
                    !data.TryGetValue("CurrentState", out var state) || !Equals(state, "TURNO_CONFIRMAR") ||
                    !data.TryGetValue("IncarnationId", out var incarnation) || !Equals(incarnation, auth.Incarnation) ||
                    !data.TryGetValue("Revision", out var revision) || !Equals(revision, auth.Revision) ||
                    !data.TryGetValue("SelectionId", out var selection) || !Equals(selection, auth.Selection) ||
                    !data.TryGetValue("CancellationAppointmentId", out var appointment) || !Equals(appointment, id))
                    throw new InvalidOperationException("Autorización de sesión inválida.");
            }
            var turno = await tx.GetSnapshotAsync(turnoRef);
            if (!turno.Exists) throw new InvalidOperationException("Turno no encontrado.");
            if (authorization != null && (session == null || !session.TryGetValue<Timestamp>("CancellationAppointmentVersion", out var expectedVersion) || turno.UpdateTime != expectedVersion))
                throw new InvalidOperationException("Turno modificado concurrentemente.");
            if (owner != null && (!turno.TryGetValue<string>("ClienteId", out var persistedOwner) ||
                !string.Equals(owner, persistedOwner, StringComparison.Ordinal)))
                throw new InvalidOperationException("Turno no autorizado.");
            var estado = turno.GetValue<string>("Estado");
            if (estado == "Cancelado" && authorization == null) return true;
            if (estado != "Pendiente") throw new InvalidOperationException("El turno no está pendiente.");
            var cascade = await tx.GetSnapshotAsync(cascadeRef);
            if (cascade.Exists) throw new InvalidOperationException("Cascada de cancelación incompatible.");
            if (!turno.TryGetValue<DateTime>("InicioUtc", out var start) || !turno.TryGetValue<DateTime>("FinUtc", out var end) ||
                start == default || end <= start)
                throw new InvalidOperationException("Intervalo de turno inválido.");
            var configDoc = await tx.GetSnapshotAsync(db.Collection("configuracion").Document("system_config"));
            if (!configDoc.Exists || !configDoc.ContainsField("CancelacionAnticipadaHorasMinimas") ||
                !configDoc.ContainsField("CancelacionAnticipadaDescuento") || !configDoc.ContainsField("CancelacionAnticipadaValidezDias"))
                throw new InvalidOperationException("Configuración de cancelación incompleta.");
            var terms = TurnoCancelacionPolicy.Evaluar(start, now, false, configDoc.ConvertTo<Configuracion>());
            TurnoCupon? coupon = null;
            if (terms.Elegible && terms.Porcentaje > 0)
            {
                var existingCoupon = await tx.GetSnapshotAsync(couponRef);
                if (existingCoupon.Exists) throw new InvalidOperationException("Cupón de cancelación incompatible.");
                coupon = TurnoCuponPolicy.Crear(turno.GetValue<string>("ClienteId"), terms.Porcentaje, now, terms.ValidezDias, false);
            }
            var version = capacityLock.Exists ? capacityLock.GetValue<long>("Version") : 0;
            if (authorization is { } valid && session != null)
                tx.Update(session.Reference, new Dictionary<string, object> {
                    ["Revision"] = checked(valid.Revision + 1), ["CurrentState"] = "MENU_CLIENTE_AUTENTICADO",
                    ["SelectionId"] = FieldValue.Delete, ["CancellationAppointmentId"] = FieldValue.Delete,
                    ["CancellationAppointmentVersion"] = FieldValue.Delete,
                    ["LastInteraction"] = DateTime.UtcNow });
            if (coupon != null) tx.Create(couponRef, new Dictionary<string, object> {
                ["AppointmentId"] = id, ["Owner"] = coupon.Owner, ["Percentage"] = (double)coupon.Percentage,
                ["ExpiresUtc"] = coupon.ExpiresUtc, ["Consumed"] = coupon.Consumed });
            tx.Create(cascadeRef, new Dictionary<string, object> { ["CancelledAppointmentId"] = id,
                ["VacancyStartUtc"] = start, ["VacancyEndUtc"] = end, ["ContinuationState"] = "Pendiente", ["PendingId"] = "" });
            var cancellation = new Dictionary<string, object> { ["Estado"] = "Cancelado" };
            if (motivo != null) cancellation["MotivoCancelacion"] = motivo;
            tx.Update(turnoRef, cancellation);
            tx.Set(lockRef, new Dictionary<string, object> { ["Version"] = checked(version + 1) });
            return true;
        }));
    }

    internal async Task<bool> Crear(Turno turno, int slots, IReadOnlyList<TurnoOpening> openings, DateTime now, DateTime gridAnchor)
    {
        ArgumentNullException.ThrowIfNull(turno);
        if (turno.CuponId != null) throw new ArgumentException("Cupón no admitido.", nameof(turno));
        if (turno.Validar().Count != 0 || turno.Estado != "Pendiente")
            throw new ArgumentException("Turno inválido.", nameof(turno));
        // Freeze the complete request before transaction retries; never retain mutable caller state.
        var json = JsonSerializer.Serialize(turno);
        var candidate = JsonSerializer.Deserialize<Turno>(json)!;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.ClaveIdempotencia)));
        var requestRef = db.Collection("turno_solicitudes").Document(key);
        var turnoRef = db.Collection("turnos").Document(candidate.Id);
        var lockRef = db.Collection("coordinacion").Document("turnos");
        var openingsCopy = openings.ToArray();
        return await RetryAborted(() => db.RunTransactionAsync(async tx =>
        {
            // Both booking and cancellation read capacity before any appointment document.
            var capacityLock = await tx.GetSnapshotAsync(lockRef);
            var request = await tx.GetSnapshotAsync(requestRef);
            var existingId = await tx.GetSnapshotAsync(turnoRef);
            if (request.Exists)
            {
                if (!request.TryGetValue<object>("Solicitud", out var stored) || stored is not string previous || previous != json)
                    throw new InvalidOperationException("Clave de idempotencia reutilizada con otro turno.");
                if (!existingId.Exists)
                    throw new InvalidOperationException("El turno de la solicitud no existe.");
                var identity = existingId.ToDictionary();
                if (!identity.TryGetValue("Id", out var id) || id is not string savedId || savedId != candidate.Id ||
                    !identity.TryGetValue("ClaveIdempotencia", out var savedKey) || savedKey is not string savedKeyText || savedKeyText != candidate.ClaveIdempotencia)
                    throw new InvalidOperationException("Identidad de turno incompatible.");
                if (!ValidReplayAppointment(existingId))
                    throw new InvalidOperationException("Turno persistido inválido.");
                return true;
            }
            if (existingId.Exists) throw new InvalidOperationException("El turno ya existe.");
            throw new InvalidOperationException("La creación directa de turnos no está permitida.");
        }));
    }
}
