using Firebase.Models.WhatsApp;
using System.Text.RegularExpressions;

namespace Firebase.Services;

public partial class WhatsAppFlowService
{
    private async Task IniciarOfertasTurno(string phone, WhatsAppSession session)
    {
        if (_firestore == null || string.IsNullOrWhiteSpace(session.ClienteId) || string.IsNullOrEmpty(session.IncarnationId))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ No se pueden consultar ofertas. Reinicia la conversación.");
            return;
        }
        var offers = await new TurnoOfertaPersistence(_firestore).PendingForOwner(session.ClienteId, DateTime.UtcNow);
        if (offers.Count == 0)
        {
            await _whatsAppService.SendTextMessage(phone, "📭 No tienes ofertas de adelanto pendientes.");
            return;
        }
        if (offers.Count > 10)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Tienes demasiadas ofertas para mostrar. Contacta al lavadero.");
            return;
        }
        var choices = offers.ToDictionary(_ => Guid.NewGuid().ToString("N"), x => x.Id);
        if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_OFERTA_SELECCIONAR", null, null, null, choices))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
            return;
        }
        var rows = choices.Zip(offers, (choice, offer) => (choice.Key, Firebase.Utils.TimeZoneHelper.FormatearFechaHoraApp(offer.Offer.ProposedUtc), "Oferta de adelanto")).ToList();
        await _whatsAppService.SendListMessage(phone, "Selecciona una oferta de adelanto.", "Ver ofertas", "Tus ofertas", rows);
    }

    private async Task HandleSeleccionOferta(string phone, WhatsAppSession session, string input)
    {
        if (_firestore == null || string.IsNullOrWhiteSpace(session.ClienteId) || session.BookingChoices == null ||
            !session.BookingChoices.TryGetValue(input, out var selected))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
            return;
        }
        var offers = await new TurnoOfertaPersistence(_firestore).PendingForOwner(session.ClienteId, DateTime.UtcNow);
        if (!offers.Any(x => x.Id == selected))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La oferta ya no está disponible. Intenta nuevamente.");
            return;
        }
        if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_OFERTA_RESPUESTA", selected, null, null, null))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
            return;
        }
        await _whatsAppService.SendTextMessage(phone, "¿Aceptas adelantar tu turno? Responde SI o NO.");
    }

    private async Task HandleRespuestaOferta(string phone, WhatsAppSession session, string input)
    {
        var answer = input.Trim().ToLowerInvariant();
        if (answer is not ("si" or "sí" or "no"))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Responde SI o NO.");
            return;
        }
        if (_firestore == null || string.IsNullOrWhiteSpace(session.ClienteId) || string.IsNullOrWhiteSpace(session.IncarnationId) ||
            string.IsNullOrWhiteSpace(session.SelectionId) || session.SelectionId.Contains('/'))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La oferta o sesión ya no está disponible. Intenta nuevamente.");
            return;
        }
        var offer = await _firestore.Collection("turno_ofertas").Document(session.SelectionId).GetSnapshotAsync();
        if (!offer.Exists || !offer.TryGetValue<string>("CascadeId", out var cascade) || string.IsNullOrWhiteSpace(cascade) || cascade.Contains('/'))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La oferta o sesión ya no está disponible. Intenta nuevamente.");
            return;
        }
        var capability = new TurnoOfertaPersistence.WhatsAppCapability(phone, session.ClienteId, session.IncarnationId, session.Revision, session.SelectionId);
        try
        {
            var persistence = new TurnoOfertaPersistence(_firestore);
            var now = TurnoOfertaOrquestacion.NormalizeClock(DateTime.UtcNow);
            if (answer == "no") await persistence.RejectWhatsApp(cascade, session.SelectionId, now, capability);
            else await persistence.AcceptWhatsApp(cascade, session.SelectionId, now, capability);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La oferta o sesión ya no está disponible. Intenta nuevamente.");
            return;
        }
        await _whatsAppService.SendTextMessage(phone, answer == "no" ? "La oferta fue rechazada." : "✅ Tu turno fue adelantado.");
    }

    internal async Task HandleRespuestaOfertaBoton(string phone, WhatsAppSession session, string messageBody)
    {
        var isAccept = messageBody.StartsWith("oferta_si_", StringComparison.OrdinalIgnoreCase);
        var prefix = isAccept ? "oferta_si_" : "oferta_no_";
        var offerId = messageBody[prefix.Length..].Trim();

        if (string.IsNullOrWhiteSpace(offerId) || offerId.Contains('/'))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Oferta no válida.");
            return;
        }

        if (string.IsNullOrWhiteSpace(session.ClienteId))
        {
            var cliente = await BuscarClientePorTelefono(phone);
            if (cliente != null)
            {
                await _sessionService.AssociateClienteToSession(phone, cliente.Id);
                session = await _sessionService.GetOrCreateSession(phone);
            }
            else
            {
                await _whatsAppService.SendTextMessage(phone, "⚠️ No se encontró una cuenta de cliente asociada a este número.");
                return;
            }
        }

        if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_OFERTA_RESPUESTA", offerId, null, null, null))
        {
            if (_firestore != null)
            {
                await _firestore.Collection("whatsapp_sessions").Document(phone).UpdateAsync(new Dictionary<string, object>
                {
                    ["CurrentState"] = "TURNO_OFERTA_RESPUESTA",
                    ["SelectionId"] = offerId
                });
            }
            session = await _sessionService.GetOrCreateSession(phone);
        }
        else
        {
            session = await _sessionService.GetOrCreateSession(phone);
        }

        await HandleRespuestaOferta(phone, session, isAccept ? "si" : "no");
    }

    private async Task HandleReservaFecha(string phone, WhatsAppSession session, string input)
    {
        if (!DateTime.TryParseExact(input, "dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var local) ||
            Firebase.Utils.TimeZoneHelper.ArgentinaZone.IsInvalidTime(local) ||
            Firebase.Utils.TimeZoneHelper.ArgentinaZone.IsAmbiguousTime(local))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Indica una fecha y hora válida en formato DD/MM/AAAA HH:MM, horario de Buenos Aires.");
            return;
        }
        try
        {
            using var draft = System.Text.Json.JsonDocument.Parse(session.BookingRequest ?? "");
            var root = draft.RootElement;
            var vehicle = root.GetProperty("VehiculoId").GetString();
            var kind = root.GetProperty("Kind").GetString();
            var id = root.GetProperty("Id").GetString();
            if (string.IsNullOrWhiteSpace(vehicle) || vehicle.Contains('/') || string.IsNullOrWhiteSpace(id) || id.Contains('/') ||
                (kind != "servicio" && kind != "paquete")) throw new InvalidOperationException();
            var next = System.Text.Json.JsonSerializer.Serialize(new { VehiculoId = vehicle, Kind = kind, Id = id, InicioLocal = local });
            if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_RESERVA_ADELANTO", null, next, null, null))
            {
                await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
                return;
            }
            await _whatsAppService.SendTextMessage(phone, "¿Aceptas que te ofrezcamos adelantar el turno? Responde SI o NO. Aún no se verificó la disponibilidad ni se reservó un turno.");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
        }
    }

    private async Task HandleReservaAdelanto(string phone, WhatsAppSession session, string input)
    {
        var answer = input.Trim().ToLowerInvariant();
        if (answer != "si" && answer != "sí" && answer != "no")
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Responde SI o NO.");
            return;
        }
        if (_firestore == null)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La reserva por WhatsApp no está disponible. No se reservó un turno.");
            return;
        }
        var config = await _firestore.Collection("configuracion").Document("system_config").GetSnapshotAsync();
        if (!config.Exists || !config.ToDictionary().TryGetValue("ToleranciaLlegadaTurnosMinutos", out var raw) || raw is not long tolerance || tolerance < 0 || tolerance > 525600)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La reserva por WhatsApp no está disponible: no se pudo consultar la tolerancia. No se reservó un turno.");
            return;
        }
        try
        {
            using var draft = System.Text.Json.JsonDocument.Parse(session.BookingRequest ?? "");
            var root = draft.RootElement;
            var vehicle = root.GetProperty("VehiculoId").GetString();
            var kind = root.GetProperty("Kind").GetString();
            var id = root.GetProperty("Id").GetString();
            var local = root.GetProperty("InicioLocal").GetDateTime();
            if (string.IsNullOrWhiteSpace(vehicle) || vehicle.Contains('/') || string.IsNullOrWhiteSpace(id) || id.Contains('/') ||
                (kind != "servicio" && kind != "paquete") || local.Kind != DateTimeKind.Unspecified || local == default ||
                Firebase.Utils.TimeZoneHelper.ArgentinaZone.IsInvalidTime(local) || Firebase.Utils.TimeZoneHelper.ArgentinaZone.IsAmbiguousTime(local))
                throw new InvalidOperationException();
            var next = System.Text.Json.JsonSerializer.Serialize(new { VehiculoId = vehicle, Kind = kind, Id = id, InicioLocal = local, AceptaAdelanto = answer != "no" });
            if (BookingSchedulerEnabled() && BookingAdapter != null)
            {
                var selections = new List<TurnoService.SnapshotSelection>();
                if (kind == "servicio") selections.Add(new(id, null));
                else
                {
                    var package = await _firestore.Collection("paquetes_servicios").Document(id).GetSnapshotAsync();
                    if (!package.Exists) throw new InvalidOperationException();
                    var ids = package.ConvertTo<Firebase.Models.PaqueteServicio>().ServiciosIds;
                    if (ids == null || ids.Count == 0 || ids.Count > 100 || ids.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException();
                    selections.AddRange(ids.Select(service => new TurnoService.SnapshotSelection(service, id)));
                }
                var identity = Guid.NewGuid().ToString("N");
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, Firebase.Utils.TimeZoneHelper.ArgentinaZone);
                var command = new TurnoService.SnapshotRequest(identity, session.ClienteId!, vehicle, "WhatsApp", identity, utc, answer != "no", selections);
                var serialized = System.Text.Json.JsonSerializer.Serialize(command);
                if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_RESERVA_CONFIRMAR", identity, serialized, (int)tolerance, null))
                {
                    await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
                    return;
                }
                await _whatsAppService.SendTextMessage(phone, $"La tolerancia de llegada configurada es de {tolerance} minutos. Tras vencerla, el turno se libera automáticamente por ausencia; la revisión se realiza periódicamente. ¿Confirmas? Responde SI o NO. La disponibilidad se verificará al confirmar.");
                return;
            }
            if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_RESERVA_BORRADOR", null, next, (int)tolerance, null))
            {
                await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
                return;
            }
            await _whatsAppService.SendTextMessage(phone, $"La tolerancia de llegada configurada es de {tolerance} minutos. La liberación automática por ausencia no está habilitada. La reserva por WhatsApp no está disponible; no se verificó la disponibilidad y no se reservó un turno. Contacta al lavadero.");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
        }
    }

    private async Task HandleReservaConfirmar(string phone, WhatsAppSession session, string input)
    {
        var answer = input.Trim().ToLowerInvariant();
        if (answer == "no")
        {
            if (await _sessionService.TryBookingTransition(phone, session, "MENU_CLIENTE_AUTENTICADO", null, null, null, null))
                await _whatsAppService.SendTextMessage(phone, "No se reservó un turno.");
            else await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
            return;
        }
        if (answer != "si" && answer != "sí")
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Responde SI o NO.");
            return;
        }
        if (!BookingSchedulerEnabled() || BookingAdapter == null)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La liberación automática por ausencia no está habilitada. No se reservó un turno.");
            return;
        }
        try
        {
            var command = System.Text.Json.JsonSerializer.Deserialize<TurnoService.SnapshotRequest>(session.BookingRequest ?? "") ?? throw new InvalidOperationException();
            var local = TimeZoneInfo.ConvertTimeFromUtc(command.InicioUtc, Firebase.Utils.TimeZoneHelper.ArgentinaZone);
            var request = new TurnoWhatsAppBookingRequest(command.Id, command.ClaveIdempotencia, command.VehiculoId, local,
                command.AceptaAdelanto, command.Servicios.Select(s => new TurnoWhatsAppBookingSelection(s.ServicioId, s.PaqueteId)).ToArray());
            var booked = await BookingAdapter.ReservarConDetalle(session, request);
            await _whatsAppService.SendTextMessage(phone, booked.Success
                ? "✅ Tu turno fue reservado."
                : $"⚠️ No se pudo reservar: {booked.ErrorMessage ?? "No hay disponibilidad para ese horario. No se reservó un turno."}");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or ArgumentException or NullReferenceException)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ No se pudo confirmar el turno. Reinicia la conversación.");
        }
    }

    private async Task HandleReservaServicio(string phone, WhatsAppSession session, string input)
    {
        if (session.BookingChoices == null || !session.BookingChoices.TryGetValue(input, out var selected) ||
            string.IsNullOrWhiteSpace(session.BookingRequest))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
            return;
        }
        if (input.StartsWith("page_", StringComparison.Ordinal))
        {
            try
            {
                using var navigation = System.Text.Json.JsonDocument.Parse(selected);
                using var draft = System.Text.Json.JsonDocument.Parse(session.BookingRequest);
                await MostrarPaginaCatalogo(phone, session, draft.RootElement.GetProperty("VehiculoId").GetString()!,
                    draft.RootElement.GetProperty("TipoVehiculo").GetString()!, navigation.RootElement.GetProperty("Kind").GetString()!,
                    navigation.RootElement.GetProperty("Cursor").GetString());
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
            {
                await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
            }
            return;
        }
        string? vehicleId, kind, id;
        try
        {
            using var draft = System.Text.Json.JsonDocument.Parse(session.BookingRequest);
            using var choice = System.Text.Json.JsonDocument.Parse(selected);
            vehicleId = draft.RootElement.GetProperty("VehiculoId").GetString();
            kind = choice.RootElement.GetProperty("Kind").GetString();
            id = choice.RootElement.GetProperty("Id").GetString();
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
            return;
        }
        if (string.IsNullOrWhiteSpace(vehicleId) || vehicleId.Contains('/') ||
            string.IsNullOrWhiteSpace(id) || id.Contains('/') || (kind != "servicio" && kind != "paquete"))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
            return;
        }
        var next = System.Text.Json.JsonSerializer.Serialize(new { VehiculoId = vehicleId, Kind = kind, Id = id });
        if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_RESERVA_FECHA", null, next, null, null))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
            return;
        }
        await _whatsAppService.SendTextMessage(phone, "Indica la fecha y hora deseadas en horario de Buenos Aires (DD/MM/AAAA HH:MM). La disponibilidad se verificará al reservar.");
    }

    private async Task HandleReservaVehiculo(string phone, WhatsAppSession session, string input)
    {
        if (_firestore == null || _turnoSelector == null || string.IsNullOrWhiteSpace(session.ClienteId) ||
            session.BookingChoices == null || !session.BookingChoices.TryGetValue(input, out var vehicleId) ||
            string.IsNullOrWhiteSpace(vehicleId) || vehicleId.Contains('/'))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
            return;
        }
        if (input.StartsWith("page_", StringComparison.Ordinal))
        {
            await MostrarPaginaVehiculos(phone, session, vehicleId);
            return;
        }
        var vehicle = await _firestore.Collection("vehiculos").Document(vehicleId).GetSnapshotAsync();
        if (!vehicle.Exists || !vehicle.TryGetValue<string>("Estado", out var state) || state != "Activo" ||
            !((vehicle.TryGetValue<string>("ClienteId", out var owner) && owner == session.ClienteId) ||
              (vehicle.TryGetValue<List<string>>("ClientesIds", out var owners) && owners.Contains(session.ClienteId))) ||
            !vehicle.TryGetValue<string>("TipoVehiculo", out var type) || string.IsNullOrWhiteSpace(type))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ El vehículo ya no está disponible. Reinicia la conversación.");
            return;
        }
        await MostrarPaginaCatalogo(phone, session, vehicleId, type, "servicio", null);
    }

    private async Task MostrarPaginaCatalogo(string phone, WhatsAppSession session, string vehicleId, string type, string kind, string? cursor)
    {
        if (_firestore == null || _turnoSelector is not TurnoSelectorService selector ||
            (kind != "servicio" && kind != "paquete") || (cursor != null && (cursor.Length == 0 || cursor.Contains('/'))))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación.");
            return;
        }
        var choices = new Dictionary<string, string>();
        var rows = new List<(string id, string title, string description)>();
        string? nextKind = null, nextCursor = null;
        foreach (var phase in kind == "servicio" ? new[] { "servicio", "paquete" } : new[] { "paquete" })
        {
            var collection = _firestore.Collection(phase == "servicio" ? "servicios" : "paquetes_servicios");
            Google.Cloud.Firestore.Query query = collection.OrderBy(Google.Cloud.Firestore.FieldPath.DocumentId);
            if (phase == kind && cursor != null) query = query.StartAfter(collection.Document(cursor));
            var limit = 9 - rows.Count;
            if (limit == 0) { nextKind = phase; break; }
            var snapshot = await query.Limit(limit + 1).GetSnapshotAsync();
            foreach (var doc in snapshot.Documents.Take(limit))
            {
                string? name;
                if (phase == "servicio")
                {
                    var service = doc.ConvertTo<Firebase.Models.Servicio>();
                    name = service.Nombre;
                    if (service.Estado != "Activo" || !string.Equals(service.TipoVehiculo, type, StringComparison.OrdinalIgnoreCase) || service.TiempoEstimado <= 0 || service.Precio < 0) continue;
                }
                else
                {
                    var package = doc.ConvertTo<Firebase.Models.PaqueteServicio>();
                    name = package.Nombre;
                    if (package.Estado != "Activo" || !string.Equals(package.TipoVehiculo, type, StringComparison.OrdinalIgnoreCase) || await selector.ServiciosValidosPaquete(package, type) == null) continue;
                }
                if (string.IsNullOrWhiteSpace(name) || name.Length > 24 || name.Any(char.IsControl)) continue;
                var token = Guid.NewGuid().ToString("N");
                choices.Add(token, System.Text.Json.JsonSerializer.Serialize(new { Kind = phase, Id = doc.Id }));
                rows.Add((token, name, phase == "servicio" ? "Servicio" : "Paquete"));
            }
            if (snapshot.Documents.Count > limit)
            {
                nextKind = phase;
                nextCursor = snapshot.Documents[limit - 1].Id;
                break;
            }
        }
        if (nextKind != null)
        {
            var token = "page_" + Guid.NewGuid().ToString("N");
            choices.Add(token, System.Text.Json.JsonSerializer.Serialize(new { Kind = nextKind, Cursor = nextCursor }));
            rows.Add((token, "Siguiente", "Más opciones"));
        }
        if (rows.Count == 0)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ No hay servicios disponibles para este vehículo.");
            return;
        }
        var draft = System.Text.Json.JsonSerializer.Serialize(new { VehiculoId = vehicleId, TipoVehiculo = type });
        if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_RESERVA_SERVICIO", null, draft, null, choices))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
            return;
        }
        await _whatsAppService.SendListMessage(phone, "Selecciona un servicio o paquete.", "Ver opciones", "Servicios", rows);
    }

    private async Task IniciarReservaTurno(string phone, WhatsAppSession session)
    {
        if (_firestore == null || string.IsNullOrWhiteSpace(session.ClienteId) ||
            session.ClienteId.Contains('/') || string.IsNullOrEmpty(session.IncarnationId))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ No se puede iniciar la reserva. Reinicia la conversación.");
            return;
        }

        await MostrarPaginaVehiculos(phone, session, null);
    }

    private async Task MostrarPaginaVehiculos(string phone, WhatsAppSession session, string? cursor)
    {
        var vehicles = _firestore!.Collection("vehiculos");
        var primary = vehicles.WhereEqualTo("ClienteId", session.ClienteId).OrderBy(Google.Cloud.Firestore.FieldPath.DocumentId);
        var associated = vehicles.WhereArrayContains("ClientesIds", session.ClienteId).OrderBy(Google.Cloud.Firestore.FieldPath.DocumentId);
        if (cursor != null)
        {
            var reference = vehicles.Document(cursor);
            primary = primary.StartAfter(reference);
            associated = associated.StartAfter(reference);
        }
        var p = await primary.Limit(10).GetSnapshotAsync();
        var a = await associated.Limit(10).GetSnapshotAsync();
        var merged = p.Documents.Concat(a.Documents).GroupBy(d => d.Id).Select(g => g.First())
            .OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
        var page = merged.Take(9).ToList();
        var choices = new Dictionary<string, string>();
        var rows = new List<(string id, string title, string description)>();
        foreach (var doc in page)
        {
            if (!doc.TryGetValue<string>("Estado", out var state) || state != "Activo" ||
                !doc.TryGetValue<string>("Patente", out var plate) || !Regex.IsMatch(plate, @"^[a-zA-Z0-9 -]{1,24}$")) continue;
            var token = Guid.NewGuid().ToString("N");
            choices.Add(token, doc.Id);
            rows.Add((token, plate, "Vehículo"));
        }
        if (merged.Count > 9)
        {
            var token = "page_" + Guid.NewGuid().ToString("N");
            choices.Add(token, page[^1].Id);
            rows.Add((token, "Siguiente", "Más vehículos"));
        }
        if (rows.Count == 0)
        {
            await _whatsAppService.SendTextMessage(phone, "📭 No tienes más vehículos activos disponibles para reservar.");
            return;
        }
        if (!await _sessionService.TryBookingTransition(phone, session, "TURNO_RESERVA_VEHICULO", null, null, null, choices))
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente.");
            return;
        }
        await _whatsAppService.SendListMessage(phone, "Selecciona tu vehículo para reservar.", "Ver vehículos", "Tus vehículos", rows);
    }
}
