using Firebase.Models;
using Firebase.Models.WhatsApp;

namespace Firebase.Services;

/// <summary>
/// Parte del servicio de flujos que maneja la edición de datos del cliente
/// </summary>
public partial class WhatsAppFlowService
{
    /// <summary>
    /// Maneja el menú de edición de datos
    /// </summary>
    private async Task HandleMenuEdicionDatos(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion.Contains("nombre") || opcion == "editar_nombre")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "👤 *Cambiar nombre*\n\n" +
                "Ingresa tu nuevo nombre (solo letras):");

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.EDITAR_NOMBRE);
        }
        else if (opcion.Contains("apellido") || opcion == "editar_apellido")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "👤 *Cambiar apellido*\n\n" +
                "Ingresa tu nuevo apellido (solo letras):");

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.EDITAR_APELLIDO);
        }
        else if (opcion.Contains("email") || opcion == "editar_email")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "📧 *Cambiar email*\n\n" +
                "Ingresa tu nuevo correo electrónico:");

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.EDITAR_EMAIL);
        }
        else
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Opción no reconocida. Por favor, selecciona una de las opciones del menú.");
        }
    }

    /// <summary>
    /// Maneja la edición del nombre
    /// </summary>
    private async Task HandleEditarNombre(string phoneNumber, WhatsAppSession session, string input)
    {
        var nuevoNombre = input.Trim();

        // Validar que solo contenga letras
        if (!EsTextoValido(nuevoNombre))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El nombre solo debe contener letras. Por favor, inténtalo nuevamente:");
            return;
        }

        try
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener tus datos.");
                return;
            }

            // Guardar cambios temporales para confirmación
            await _sessionService.SaveTemporaryData(phoneNumber, "nuevo_nombre", nuevoNombre);
            await _sessionService.SaveTemporaryData(phoneNumber, "nombre_anterior", cliente.Nombre);
            await _sessionService.SaveTemporaryData(phoneNumber, "campo_editado", "Nombre");

            var mensaje = $"✅ *Confirmar cambio*\n\n" +
                         $"Nombre actual: {cliente.Nombre}\n" +
                         $"Nombre nuevo: {nuevoNombre}\n\n" +
                         $"¿Confirmas el cambio?\n\n" +
                         $"Responde *SÍ* para confirmar o *NO* para cancelar.";

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.CONFIRMAR_EDICION);
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editando nombre");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Maneja la edición del apellido
    /// </summary>
    private async Task HandleEditarApellido(string phoneNumber, WhatsAppSession session, string input)
    {
        var nuevoApellido = input.Trim();

        // Validar que solo contenga letras
        if (!EsTextoValido(nuevoApellido))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El apellido solo debe contener letras. Por favor, inténtalo nuevamente:");
            return;
        }

        try
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener tus datos.");
                return;
            }

            // Guardar cambios temporales para confirmación
            await _sessionService.SaveTemporaryData(phoneNumber, "nuevo_apellido", nuevoApellido);
            await _sessionService.SaveTemporaryData(phoneNumber, "apellido_anterior", cliente.Apellido);
            await _sessionService.SaveTemporaryData(phoneNumber, "campo_editado", "Apellido");

            var mensaje = $"✅ *Confirmar cambio*\n\n" +
                         $"Apellido actual: {cliente.Apellido}\n" +
                         $"Apellido nuevo: {nuevoApellido}\n\n" +
                         $"¿Confirmas el cambio?\n\n" +
                         $"Responde *SÍ* para confirmar o *NO* para cancelar.";

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.CONFIRMAR_EDICION);
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editando apellido");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Maneja la edición del email
    /// </summary>
    private async Task HandleEditarEmail(string phoneNumber, WhatsAppSession session, string input)
    {
        var nuevoEmail = input.Trim();

        // Validar formato de email
        if (!EsEmailValido(nuevoEmail))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El formato del email no es válido. Por favor, inténtalo nuevamente:");
            return;
        }

        try
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener tus datos.");
                return;
            }

            // Guardar cambios temporales para confirmación
            await _sessionService.SaveTemporaryData(phoneNumber, "nuevo_email", nuevoEmail);
            await _sessionService.SaveTemporaryData(phoneNumber, "email_anterior", cliente.Email);
            await _sessionService.SaveTemporaryData(phoneNumber, "campo_editado", "Email");

            var mensaje = $"✅ *Confirmar cambio*\n\n" +
                         $"Email actual: {cliente.Email}\n" +
                         $"Email nuevo: {nuevoEmail}\n\n" +
                         $"¿Confirmas el cambio?\n\n" +
                         $"Responde *SÍ* para confirmar o *NO* para cancelar.";

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.CONFIRMAR_EDICION);
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editando email");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Confirma y aplica los cambios en los datos del cliente
    /// </summary>
    private async Task HandleConfirmarEdicion(string phoneNumber, WhatsAppSession session, string input)
    {
        var respuesta = input.Trim().ToUpperInvariant();

        if (respuesta == "SI" || respuesta == "SÍ" || respuesta == "S")
        {
            await AplicarCambiosCliente(phoneNumber, session);
        }
        else if (respuesta == "NO" || respuesta == "N")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Cambios cancelados.\n\n" +
                "Tus datos no fueron modificados.\n\n" +
                "Volviendo al menú principal...");

            await Task.Delay(500);

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
                "⚠️ Por favor, responde *SÍ* para confirmar o *NO* para cancelar.");
        }
    }

    /// <summary>
    /// Aplica los cambios confirmados en los datos del cliente
    /// </summary>
    private async Task AplicarCambiosCliente(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener tus datos.");
                return;
            }

            var campoEditado = session.TemporaryData.GetValueOrDefault("campo_editado", "");
            string valorAnterior = "";
            string valorNuevo = "";

            // Aplicar cambios según el campo editado
            switch (campoEditado)
            {
                case "Nombre":
                    valorAnterior = cliente.Nombre;
                    valorNuevo = session.TemporaryData.GetValueOrDefault("nuevo_nombre", "");
                    cliente.Nombre = valorNuevo;
                    break;

                case "Apellido":
                    valorAnterior = cliente.Apellido;
                    valorNuevo = session.TemporaryData.GetValueOrDefault("nuevo_apellido", "");
                    cliente.Apellido = valorNuevo;
                    break;

                case "Email":
                    valorAnterior = cliente.Email;
                    valorNuevo = session.TemporaryData.GetValueOrDefault("nuevo_email", "");
                    cliente.Email = valorNuevo;
                    break;

                default:
                    await _whatsAppService.SendTextMessage(phoneNumber,
                        "❌ Error: Campo de edición no reconocido.");
                    return;
            }

            // Guardar cambios en la base de datos
            await _clienteService.ActualizarCliente(cliente);

            _logger.LogInformation("✏️ {Campo} de cliente {ClienteId} actualizado: {Anterior} → {Nuevo}",
                campoEditado, session.ClienteId, valorAnterior, valorNuevo);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ *{campoEditado} actualizado correctamente*\n\n" +
                $"{campoEditado} anterior: {valorAnterior}\n" +
                $"{campoEditado} nuevo: {valorNuevo}\n\n" +
                $"Tus datos han sido actualizados exitosamente.\n\n" +
                $"Volviendo al menú principal...");

            // Limpiar datos temporales
            session.TemporaryData.Remove("nuevo_nombre");
            session.TemporaryData.Remove("nuevo_apellido");
            session.TemporaryData.Remove("nuevo_email");
            session.TemporaryData.Remove("nombre_anterior");
            session.TemporaryData.Remove("apellido_anterior");
            session.TemporaryData.Remove("email_anterior");
            session.TemporaryData.Remove("campo_editado");

            await Task.Delay(1000);

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
            await ShowClienteMenu(phoneNumber, cliente.Nombre);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aplicando cambios al cliente");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error al guardar los cambios. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Maneja la opción desde el estado MOSTRAR_DATOS
    /// </summary>
    private async Task HandleMostrarDatos(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion.Contains("editar") || opcion == "editar_datos")
        {
            await MostrarMenuEdicionDatos(phoneNumber, session);
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
                "⚠️ Opción no reconocida. Por favor, selecciona una de las opciones del menú.");
        }
    }
}
