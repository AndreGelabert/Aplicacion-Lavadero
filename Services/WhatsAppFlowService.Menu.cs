using Firebase.Models;
using Firebase.Models.WhatsApp;

namespace Firebase.Services;

/// <summary>
/// Parte del servicio de flujos que maneja el menú y consultas
/// </summary>
public partial class WhatsAppFlowService
{
    /// <summary>
    /// Maneja las opciones del menú de cliente autenticado
    /// </summary>
    private async Task HandleMenuClienteAutenticado(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion == "turnos" || opcion == "gestionar_turnos" || opcion == "gestionar turnos")
        {
            await MostrarMenuTurnos(phoneNumber, session);
        }
        else if (opcion == "ofertas_turno" || opcion == "ofertas turno")
        {
            await IniciarOfertasTurno(phoneNumber, session);
        }
        else if (opcion == "reservar_turno" || opcion == "reservar turno")
        {
            await IniciarReservaTurno(phoneNumber, session);
        }
        else if (opcion == "mis_turnos" || opcion == "mis turnos")
        {
            if (string.IsNullOrWhiteSpace(session.ClienteId) || session.ClienteId.Contains('/'))
            {
                await _whatsAppService.SendTextMessage(phoneNumber, "❌ Error: No se pudo identificar tu usuario.");
                return;
            }
            var turnos = _testTurnos != null
                ? await _testTurnos(session.ClienteId, DateTime.UtcNow)
                : await (_turnoConsulta ?? throw new InvalidOperationException("Consulta de turnos no disponible")).ProximosDeCliente(session.ClienteId, DateTime.UtcNow);
            var mensaje = turnos.Count == 0 ? "📭 No tienes próximos turnos." :
                "📅 *Tus próximos turnos:*\n\n" + string.Join("\n", turnos.Take(10).Select(t => "• " + Firebase.Utils.TimeZoneHelper.FormatearFechaHoraApp(t.InicioUtc))) +
                (turnos.Count > 10 ? "\nMostrando los primeros 10 turnos." : "");
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        else if (opcion == "cancelar_turno" || opcion == "cancelar turno")
        {
            await IniciarCancelacionTurno(phoneNumber, session);
        }
        else if (opcion.Contains("vehiculo") || opcion == "vehiculos")
        {
            // Opción: Gestionar vehículos
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        else if (opcion.Contains("datos") || opcion == "datos")
        {
            // Opción: Mis datos
            await MostrarDatosCliente(phoneNumber, session);
        }
        else if (opcion.Contains("sobre") || opcion == "sobre_nosotros")
        {
            // Opción: Sobre nosotros
            await MostrarSobreNosotros(phoneNumber);
        }
        else if (opcion.Contains("ayuda") || opcion == "ayuda")
        {
            // Opción: Ayuda
            await MostrarAyuda(phoneNumber);
        }
        else if (opcion == "menu" || opcion == "menú")
        {
            // Volver a mostrar el menú
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente != null)
            {
                await ShowClienteMenu(phoneNumber, cliente.Nombre);
            }
        }
        else
        {
            // Opción no reconocida
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Opción no reconocida.\n\n" +
                "Por favor, selecciona una de las opciones del menú o escribe *MENÚ* para verlas nuevamente.");
        }
    }

    private async Task IniciarCancelacionTurno(string phone, WhatsAppSession session)
    {
        if (string.IsNullOrWhiteSpace(session.ClienteId) || session.ClienteId.Contains('/') ||
            string.IsNullOrEmpty(session.IncarnationId) || _turnoConsulta == null || _firestore == null)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ No se puede iniciar la cancelación. Reinicia la conversación.");
            return;
        }
        var turnos = (await _turnoConsulta.ProximosDeCliente(session.ClienteId, DateTime.UtcNow))
            .Where(t => t.ClienteId == session.ClienteId && !string.IsNullOrWhiteSpace(t.Id) && !t.Id.Contains('/'))
            .Take(10).ToList();
        if (turnos.Count == 0) { await _whatsAppService.SendTextMessage(phone, "📭 No tienes próximos turnos."); return; }
        var identificados = new List<(Turno Turno, string Identificador)>();
        foreach (var turno in turnos)
        {
            var identificador = await IdentificadorCancelacion(turno, session.ClienteId);
            if (identificador != null) identificados.Add((turno, identificador));
        }
        if (identificados.Count != turnos.Count || identificados.Select(x => x.Identificador).Distinct().Count() != identificados.Count)
        {
            await _whatsAppService.SendTextMessage(phone, "⚠️ No se pueden distinguir tus turnos. Contacta al lavadero para cancelarlos.");
            return;
        }
        var choices = identificados.ToDictionary(_ => Guid.NewGuid().ToString("N"), x => x.Turno.Id);
        if (!await _sessionService.TryCancellationTransition(phone, session, "TURNO_SELECCIONAR", null, null, choices))
        { await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente."); return; }
        var rows = choices.Zip(identificados, (choice, item) => (choice.Key, Firebase.Utils.TimeZoneHelper.FormatearFechaHoraApp(item.Turno.InicioUtc), item.Identificador)).ToList();
        await _whatsAppService.SendListMessage(phone, "Selecciona el turno que deseas cancelar:", "Ver turnos", "Tus turnos", rows);
    }

    private async Task HandleSeleccionCancelacion(string phone, WhatsAppSession session, string input)
    {
        if (session.CancellationChoices == null || !session.CancellationChoices.TryGetValue(input, out var id) ||
            string.IsNullOrWhiteSpace(id) || id.Contains('/'))
        { await _whatsAppService.SendTextMessage(phone, "⚠️ Selección inválida. Reinicia la conversación."); return; }
        if (_firestore == null || string.IsNullOrWhiteSpace(session.ClienteId)) return;
        var appointment = await _firestore.Collection("turnos").Document(id).GetSnapshotAsync();
        if (!appointment.Exists || !appointment.UpdateTime.HasValue ||
            !appointment.TryGetValue<string>("ClienteId", out var owner) || owner != session.ClienteId ||
            !appointment.TryGetValue<string>("Estado", out var state) || state != "Pendiente" ||
            !appointment.TryGetValue<DateTime>("InicioUtc", out var start) ||
            !appointment.TryGetValue<DateTime>("FinUtc", out var end) || end <= start)
        { await _whatsAppService.SendTextMessage(phone, "⚠️ El turno ya no está disponible. Intenta nuevamente."); return; }
        var identificador = await IdentificadorCancelacion(appointment.ConvertTo<Turno>(), session.ClienteId);
        if (identificador == null)
        { await _whatsAppService.SendTextMessage(phone, "⚠️ No se puede identificar el turno. Contacta al lavadero."); return; }
        var token = Guid.NewGuid().ToString("N");
        if (!await _sessionService.TryCancellationTransition(phone, session, "TURNO_CONFIRMAR", token, id, null, appointment.UpdateTime))
        { await _whatsAppService.SendTextMessage(phone, "⚠️ La sesión cambió. Intenta nuevamente."); return; }
        await _whatsAppService.SendButtonMessage(phone, $"¿Confirmas la cancelación del turno del {Firebase.Utils.TimeZoneHelper.FormatearFechaHoraApp(start)} hasta {Firebase.Utils.TimeZoneHelper.FormatearFechaHoraApp(end)} ({identificador})?",

            new List<(string id, string title)> { ("confirmar_" + token, "Sí, cancelar"), ("rechazar_" + token, "No") });
    }

    private async Task<string?> IdentificadorCancelacion(Turno turno, string owner)
    {
        var id = turno.Vehiculos?.Count == 1 ? turno.Vehiculos[0]?.VehiculoId : null;
        if (!string.IsNullOrWhiteSpace(id) && !id.Contains('/'))
        {
            var vehicle = await _firestore!.Collection("vehiculos").Document(id).GetSnapshotAsync();
            if (vehicle.Exists &&
                ((vehicle.TryGetValue<string>("ClienteId", out var principal) && principal == owner) ||
                 (vehicle.TryGetValue<List<string>>("ClientesIds", out var owners) && owners.Contains(owner))) &&
                vehicle.TryGetValue<string>("Patente", out var plate) &&
                System.Text.RegularExpressions.Regex.IsMatch(plate, @"^[a-zA-Z0-9 -]{1,60}$"))
                return "Patente: " + plate;
        }
        // Only expose a bounded, persisted appointment identifier belonging to the session owner.
        return turno.ClienteId == owner && System.Text.RegularExpressions.Regex.IsMatch(turno.Id ?? "", @"^[a-zA-Z0-9_-]{1,60}$")
            ? "Turno: " + turno.Id : null;
    }

    private async Task HandleConfirmacionCancelacion(string phone, WhatsAppSession session, string input)
    {
        if (string.IsNullOrEmpty(session.SelectionId) || string.IsNullOrEmpty(session.IncarnationId) ||
            string.IsNullOrEmpty(session.ClienteId) || string.IsNullOrEmpty(session.CancellationAppointmentId))
        { await _whatsAppService.SendTextMessage(phone, "⚠️ Confirmación inválida. Reinicia la conversación."); return; }
        if (input == "rechazar_" + session.SelectionId)
        {
            if (await _sessionService.TryCancellationTransition(phone, session, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, null, null, null))
                await _whatsAppService.SendTextMessage(phone, "No se canceló el turno.");
            return;
        }
        if (input != "confirmar_" + session.SelectionId || _firestore == null)
        { await _whatsAppService.SendTextMessage(phone, "⚠️ Confirmación inválida. Reinicia la conversación."); return; }
        try
        {
            await new TurnoService(_firestore).CancelarWhatsApp(session.CancellationAppointmentId, session.ClienteId, phone,
                session.IncarnationId, session.Revision, session.SelectionId);
            await _whatsAppService.SendTextMessage(phone, "✅ Tu turno fue cancelado.");
        }
        catch (InvalidOperationException)
        { await _whatsAppService.SendTextMessage(phone, "⚠️ No se pudo cancelar el turno. Intenta nuevamente."); }
    }

    /// <summary>
    /// Muestra el menú de gestión de vehículos
    /// </summary>
    private async Task MostrarMenuVehiculos(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            if (string.IsNullOrEmpty(session.ClienteId))
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: No se pudo identificar tu usuario.");
                return;
            }

            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(session.ClienteId);
            var vehiculosActivos = vehiculos?.Where(v => v.Estado == "Activo").ToList();
            
            if (vehiculosActivos == null)
                vehiculosActivos = new List<Vehiculo>();

            // Mostrar lista de vehículos
            if (vehiculosActivos.Any())
            {
                var mensaje = "🚗 *Tus vehículos registrados:*\n\n";

                var index = 1;
                foreach (var vehiculo in vehiculosActivos)
                {
                    mensaje += $"{index}. *{vehiculo.Patente}*\n" +
                              $"   {vehiculo.Marca} {vehiculo.Modelo}\n" +
                              $"   {vehiculo.TipoVehiculo} - {vehiculo.Color}\n\n";
                    index++;
                }

                await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
            }
            else
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "📭 No tienes vehículos registrados actualmente.");
            }

            await Task.Delay(500);

            // Mostrar opciones del submenú (usando lista para más opciones)
            var options = new List<(string id, string title, string description)>
            {
                ("agregar_vehiculo", "➕ Agregar nuevo", "Registrar un vehículo nuevo"),
                ("asociar_vehiculo", "🔗 Asociar existente", "Vincular vehículo con clave"),
                ("modificar_vehiculo", "✏️ Editar vehículo", "Modificar o eliminar"),
                ("menu_principal", "⬅️ Menú principal", "Volver al inicio")
            };

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_VEHICULOS);
            await _whatsAppService.SendListMessage(phoneNumber,
                "¿Qué deseas hacer?",
                "📋 Ver opciones",
                "Gestión de vehículos",
                options);
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("Vehicle menu failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Maneja las opciones del submenú de vehículos
    /// </summary>
    private async Task HandleMenuVehiculos(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion.Contains("agregar") || opcion == "agregar_vehiculo")
        {
            // Iniciar proceso de agregar vehículo nuevo
            await IniciarRegistroVehiculo(phoneNumber);
        }
        else if (opcion.Contains("asociar") || opcion == "asociar_vehiculo")
        {
            // Iniciar proceso de asociar vehículo existente
            await IniciarAsociacionVehiculo(phoneNumber);
        }
        else if (opcion.Contains("modificar") || opcion.Contains("editar") || opcion == "modificar_vehiculo")
        {
            // Mostrar lista de vehículos para modificar
            await MostrarVehiculosParaModificar(phoneNumber, session);
        }
        else if (opcion.Contains("menu") || opcion.Contains("principal") || opcion == "menu_principal")
        {
            // Volver al menú principal
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente != null)
            {
                await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
                await ShowClienteMenu(phoneNumber, cliente.Nombre);
            }
        }
        else
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Opción no reconocida. Por favor, selecciona una de las opciones del menú.");
        }
    }

    /// <summary>
    /// Muestra el submenú de gestión de turnos
    /// </summary>
    private async Task MostrarMenuTurnos(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_TURNOS);

            var options = new List<(string id, string title, string description)>
            {
                ("reservar_turno", "📅 Pedir turno", "Reservar un nuevo turno"),
                ("mis_turnos", "📋 Mis turnos", "Consultar próximos turnos agendados"),
                ("cancelar_turno", "❌ Cancelar turno", "Cancelar un turno pendiente"),
                ("ofertas_turno", "⚡ Ofertas de adelanto", "Consultar turnos más tempranos"),
                ("menu_principal", "🏠 Menú principal", "Volver al menú anterior")
            };

            await _whatsAppService.SendListMessage(phoneNumber,
                "📅 *Gestión de Turnos*\n\n¿Qué deseas hacer?",
                "📋 Ver opciones",
                "Opciones de turnos",
                options);
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("Turnos menu failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Maneja las opciones del submenú de turnos
    /// </summary>
    private async Task HandleMenuTurnos(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion == "reservar_turno" || opcion == "reservar turno" || opcion.Contains("pedir") || opcion.Contains("reservar"))
        {
            await IniciarReservaTurno(phoneNumber, session);
        }
        else if (opcion == "mis_turnos" || opcion == "mis turnos" || opcion == "turnos" || opcion.Contains("proximos") || opcion.Contains("próximos"))
        {
            if (string.IsNullOrWhiteSpace(session.ClienteId) || session.ClienteId.Contains('/'))
            {
                await _whatsAppService.SendTextMessage(phoneNumber, "❌ Error: No se pudo identificar tu usuario.");
                return;
            }
            var turnos = _testTurnos != null
                ? await _testTurnos(session.ClienteId, DateTime.UtcNow)
                : await (_turnoConsulta ?? throw new InvalidOperationException("Consulta de turnos no disponible")).ProximosDeCliente(session.ClienteId, DateTime.UtcNow);
            var mensaje = turnos.Count == 0 ? "📭 No tienes próximos turnos." :
                "📅 *Tus próximos turnos:*\n\n" + string.Join("\n", turnos.Take(10).Select(t => "• " + Firebase.Utils.TimeZoneHelper.FormatearFechaHoraApp(t.InicioUtc))) +
                (turnos.Count > 10 ? "\nMostrando los primeros 10 turnos." : "");
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        else if (opcion == "cancelar_turno" || opcion == "cancelar turno" || opcion.Contains("cancelar"))
        {
            await IniciarCancelacionTurno(phoneNumber, session);
        }
        else if (opcion == "ofertas_turno" || opcion == "ofertas_adelanto" || opcion.Contains("oferta") || opcion.Contains("adelanto"))
        {
            await IniciarOfertasTurno(phoneNumber, session);
        }
        else if (opcion.Contains("menu") || opcion.Contains("principal") || opcion == "menu_principal")
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente != null)
            {
                await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
                await ShowClienteMenu(phoneNumber, cliente.Nombre);
            }
        }
        else
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Opción no reconocida. Por favor, selecciona una de las opciones del menú de turnos o escribe *MENÚ* para volver.");
        }
    }

    /// <summary>
    /// Muestra los datos del cliente con opción de editar
    /// </summary>
    private async Task MostrarDatosCliente(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            if (string.IsNullOrEmpty(session.ClienteId))
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: No se pudo identificar tu usuario.");
                return;
            }

            var cliente = await _clienteService.ObtenerCliente(session.ClienteId);

            if (cliente == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: No se pudieron cargar tus datos.");
                return;
            }

            // Construir mensaje con los datos del cliente
            var mensaje = "👤 *Tus datos registrados:*\n\n" +
                         $"• Nombre: {cliente.Nombre}\n" +
                         $"• Apellido: {cliente.Apellido}\n" +
                         $"• Documento: {cliente.TipoDocumento} {cliente.NumeroDocumento}\n" +
                         $"• Teléfono: {cliente.Telefono}\n" +
                         $"• Email: {cliente.Email}\n" +
                         $"• Notificaciones: {DescribeNotificationPreference(cliente.PrefiereWhatsApp, cliente.PrefiereEmail)}\n" +
                         $"• Vehículos: {cliente.VehiculosIds.Count}\n" +
                         $"• Estado: {cliente.Estado}\n\n" +
                         $"📝 Puedes editar tu nombre, apellido y email.";

            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);

            await Task.Delay(500);

            // Mostrar opciones
            var buttons = new List<(string id, string title)>
            {
                ("editar_datos", "✏️ Editar datos"),
                ("menu_principal", "⬅️ Menú principal")
            };

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MOSTRAR_DATOS);
            await _whatsAppService.SendButtonMessage(phoneNumber,
                "¿Qué deseas hacer?",
                buttons);
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("Client data display failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Muestra opciones para editar datos del cliente
    /// </summary>
    private async Task MostrarMenuEdicionDatos(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente == null) return;

            var mensaje = "✏️ *Editar mis datos*\n\n" +
                         $"Datos actuales:\n" +
                         $"• Nombre: {cliente.Nombre}\n" +
                         $"• Apellido: {cliente.Apellido}\n" +
                         $"• Email: {cliente.Email}\n" +
                         $"• Notificaciones: {DescribeNotificationPreference(cliente.PrefiereWhatsApp, cliente.PrefiereEmail)}\n\n" +
                         $"¿Qué dato deseas modificar?";

            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);

            await Task.Delay(300);

            var options = new List<(string id, string title, string description)>
            {
                ("editar_nombre", "👤 Nombre", "Cambiar nombre"),
                ("editar_apellido", "👤 Apellido", "Cambiar apellido"),
                ("editar_email", "📧 Email", "Cambiar correo"),
                ("editar_preferencias", "📬 Notificaciones", "Elegir email, WhatsApp o ambos")
            };

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.EDITAR_DATOS_MENU);
            await _whatsAppService.SendListMessage(phoneNumber,
                "Selecciona una opción:",
                "📋 Ver opciones", "Editar datos", options);
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("Client edit menu failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Muestra información "Sobre nosotros" del lavadero
    /// </summary>
    private async Task MostrarSobreNosotros(string phoneNumber)
    {
        var mensaje = await _lavaderoInfoService.ObtenerMensajeSobreNosotros();
        await _whatsAppService.SendTextMessage(phoneNumber, mensaje);

        await Task.Delay(1000);

        // Volver al menú principal
        var session = await _sessionService.GetOrCreateSession(phoneNumber);
        if (!string.IsNullOrEmpty(session.ClienteId))
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId);
            if (cliente != null)
            {
                await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
                await ShowClienteMenu(phoneNumber, cliente.Nombre);
            }
        }
    }

    /// <summary>
    /// Muestra ayuda al usuario con opción de hablar con personal
    /// </summary>
    private async Task MostrarAyuda(string phoneNumber)
    {
        var mensaje = "❓ *Ayuda - Comandos disponibles:*\n\n" +
                     "• *MENÚ* - Volver al menú principal\n" +
                     "• *REINICIAR* - Reiniciar la conversación\n\n" +
                     "¿Necesitas ayuda adicional?";

        await _whatsAppService.SendTextMessage(phoneNumber, mensaje);

        await Task.Delay(300);

        var buttons = new List<(string id, string title)>
        {
            ("hablar_personal", "👤 Hablar con personal"),
            ("menu_principal", "⬅️ Volver al menú")
        };

        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MOSTRAR_DATOS);
        await _whatsAppService.SendButtonMessage(phoneNumber,
            "Selecciona una opción:",
            buttons);
    }

    /// <summary>
    /// Maneja la opción de hablar con el personal
    /// </summary>
    private async Task HablarConPersonal(string phoneNumber)
    {
        var nombreLavadero = await _lavaderoInfoService.ObtenerNombreLavadero();
        
        var mensaje = $"👤 *Solicitud de atención personal*\n\n" +
                     $"Hemos registrado tu solicitud de hablar con nuestro personal.\n\n" +
                     $"Un miembro del equipo de {nombreLavadero} se pondrá en contacto contigo a través de este WhatsApp lo antes posible.\n\n" +
                     $"Horario de atención: Lunes a Sábado\n\n" +
                     $"Mientras tanto, puedes seguir usando el menú automático. 😊";

        await _whatsAppService.SendTextMessage(phoneNumber, mensaje);

        await Task.Delay(1000);

        // Volver al menú principal
        var session = await _sessionService.GetOrCreateSession(phoneNumber);
        if (!string.IsNullOrEmpty(session.ClienteId))
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId);
            if (cliente != null)
            {
                await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
                await ShowClienteMenu(phoneNumber, cliente.Nombre);
            }
        }
    }
}
