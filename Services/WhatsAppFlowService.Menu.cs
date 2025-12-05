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

        if (opcion.Contains("vehiculo") || opcion == "vehiculos")
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mostrando menú de vehículos");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Por favor, intenta nuevamente.");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mostrando datos del cliente");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error al cargar tus datos. Por favor, intenta nuevamente.");
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
                         $"• Email: {cliente.Email}\n\n" +
                         $"¿Qué dato deseas modificar?";

            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);

            await Task.Delay(300);

            var buttons = new List<(string id, string title)>
            {
                ("editar_nombre", "👤 Nombre"),
                ("editar_apellido", "👤 Apellido"),
                ("editar_email", "📧 Email")
            };

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.EDITAR_DATOS_MENU);
            await _whatsAppService.SendButtonMessage(phoneNumber,
                "Selecciona una opción:",
                buttons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mostrando menú de edición");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
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
