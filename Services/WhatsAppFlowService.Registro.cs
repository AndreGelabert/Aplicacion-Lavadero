using Firebase.Models;
using Firebase.Models.WhatsApp;

namespace Firebase.Services;

/// <summary>
/// Parte del servicio de flujos que maneja el registro de clientes
/// </summary>
public partial class WhatsAppFlowService
{
    /// <summary>
    /// Maneja la selección del tipo de documento
    /// </summary>
    private async Task HandleRegistroTipoDocumento(string phoneNumber, WhatsAppSession session, string input)
    {
        var tipoDocumento = input.Trim();

        // Validar que el tipo de documento exists
        var tiposDocumento = await _tipoDocumentoService.ObtenerTiposDocumento();
        if (!tiposDocumento.Any(t => t.Equals(tipoDocumento, StringComparison.OrdinalIgnoreCase)))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Tipo de documento no válido. Por favor, selecciona uno de los botones.");
            return;
        }

        // Guardar tipo de documento
        await _sessionService.SaveTemporaryData(phoneNumber, "TipoDocumento", tipoDocumento);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.REGISTRO_NUM_DOCUMENTO);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ Tipo de documento: {tipoDocumento}\n\n" +
            $"📝 Ahora, ingresa tu número de documento (solo números):");
    }

    /// <summary>
    /// Maneja el ingreso del número de documento
    /// </summary>
    private async Task HandleRegistroNumDocumento(string phoneNumber, WhatsAppSession session, string input)
    {
        var numeroDocumento = input.Trim();

        // Validar que solo contenga números
        if (!EsNumeroValido(numeroDocumento))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El número de documento solo debe contener números. Por favor, inténtalo nuevamente:");
            return;
        }

        // Verificar si ya existe un cliente con este documento
        var tipoDoc = session.TemporaryData.GetValueOrDefault("TipoDocumento", "");
        var clienteExistente = await _clienteService.ObtenerClientePorDocumento(tipoDoc, numeroDocumento);

        if (clienteExistente != null)
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Ya existe un cliente registrado con este documento.\n\n" +
                "Por favor, contacta al lavadero si crees que esto es un error.");

            // Reiniciar sesión
            await _sessionService.ClearSession(phoneNumber);
            return;
        }

        // Guardar número de documento
        await _sessionService.SaveTemporaryData(phoneNumber, "NumeroDocumento", numeroDocumento);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.REGISTRO_NOMBRE);

        await _whatsAppService.SendTextMessage(phoneNumber,
            "✅ Perfecto!\n\n" +
            "👤 ¿Cuál es tu nombre? (solo letras y espacios):");
    }

    /// <summary>
    /// Maneja el ingreso del nombre
    /// </summary>
    private async Task HandleRegistroNombre(string phoneNumber, WhatsAppSession session, string input)
    {
        var nombre = input.Trim();

        // Validar que solo contenga letras
        if (!EsTextoValido(nombre))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El nombre solo debe contener letras y espacios. Por favor, inténtalo nuevamente:");
            return;
        }

        // Guardar nombre
        await _sessionService.SaveTemporaryData(phoneNumber, "Nombre", nombre);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.REGISTRO_APELLIDO);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ Hola {nombre}! 👋\n\n" +
            $"👤 Ahora, ¿cuál es tu apellido?");
    }

    /// <summary>
    /// Maneja el ingreso del apellido
    /// </summary>
    private async Task HandleRegistroApellido(string phoneNumber, WhatsAppSession session, string input)
    {
        var apellido = input.Trim();

        // Validar que solo contenga letras
        if (!EsTextoValido(apellido))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El apellido solo debe contener letras y espacios. Por favor, inténtalo nuevamente:");
            return;
        }

        // Guardar apellido
        await _sessionService.SaveTemporaryData(phoneNumber, "Apellido", apellido);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.REGISTRO_EMAIL);

        await _whatsAppService.SendTextMessage(phoneNumber,
            "✅ Genial!\n\n" +
            "📧 Por último, ingresa tu correo electrónico:");
    }

    /// <summary>
    /// Maneja el ingreso del email
    /// </summary>
    private async Task HandleRegistroEmail(string phoneNumber, WhatsAppSession session, string input)
    {
        var email = input.Trim();

        // Validar formato de email
        if (!EsEmailValido(email))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El formato del email no es válido. Por favor, inténtalo nuevamente:");
            return;
        }

        // Guardar email
        await _sessionService.SaveTemporaryData(phoneNumber, "Email", email);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.REGISTRO_CONFIRMACION);

        // Mostrar resumen para confirmación
        var tipoDoc = session.TemporaryData.GetValueOrDefault("TipoDocumento", "");
        var numDoc = session.TemporaryData.GetValueOrDefault("NumeroDocumento", "");
        var nombre = session.TemporaryData.GetValueOrDefault("Nombre", "");
        var apellido = session.TemporaryData.GetValueOrDefault("Apellido", "");

        var resumen = $"📋 *Resumen de tus datos:*\n\n" +
                      $"• Tipo de documento: {tipoDoc}\n" +
                      $"• Número: {numDoc}\n" +
                      $"• Nombre: {nombre} {apellido}\n" +
                      $"• Teléfono: {phoneNumber}\n" +
                      $"• Email: {email}\n\n" +
                      $"¿Los datos son correctos?\n\n" +
                      $"Responde *SÍ* para confirmar o *NO* para cancelar.";

        await _whatsAppService.SendTextMessage(phoneNumber, resumen);
    }

    /// <summary>
    /// Maneja la confirmación del registro
    /// </summary>
    private async Task HandleRegistroConfirmacion(string phoneNumber, WhatsAppSession session, string input)
    {
        var respuesta = input.Trim().ToUpperInvariant();

        if (respuesta == "SI" || respuesta == "SÍ" || respuesta == "S")
        {
            // Confirmar registro
            await CrearClienteDesdeSession(phoneNumber, session);
        }
        else if (respuesta == "NO" || respuesta == "N")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Registro cancelado.\n\n" +
                "Si deseas registrarte nuevamente, envía cualquier mensaje.");

            await _sessionService.ClearSession(phoneNumber);
        }
        else
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Por favor, responde *SÍ* para confirmar o *NO* para cancelar.");
        }
    }

    /// <summary>
    /// Crea el cliente en la base de datos a partir de la sesión
    /// </summary>
    private async Task CrearClienteDesdeSession(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            // phoneNumber viene con código de país (ej: 543751590586)
            // Guardamos SIN código de país en la DB (ej: 3751590586)
            var telefonoParaDB = PhoneNumberHelper.RemoveCountryCode(phoneNumber);
            
            _logger.LogInformation("💾 Creando cliente:");
            _logger.LogInformation("   📱 Teléfono WhatsApp: {WhatsAppPhone}", phoneNumber);
            _logger.LogInformation("   📱 Teléfono para DB: {DBPhone}", telefonoParaDB);
            
            var cliente = new Cliente
            {
                Id = "",
                TipoDocumento = session.TemporaryData.GetValueOrDefault("TipoDocumento", ""),
                NumeroDocumento = session.TemporaryData.GetValueOrDefault("NumeroDocumento", ""),
                Nombre = session.TemporaryData.GetValueOrDefault("Nombre", ""),
                Apellido = session.TemporaryData.GetValueOrDefault("Apellido", ""),
                Telefono = telefonoParaDB, // Guardar SIN código de país
                Email = session.TemporaryData.GetValueOrDefault("Email", ""),
                VehiculosIds = new List<string>(),
                Estado = "Activo"
            };

            await _clienteService.CrearCliente(cliente);

            _logger.LogInformation("✅ Cliente creado exitosamente: {ClienteId}", cliente.Id);

            // Asociar cliente a la sesión
            await _sessionService.AssociateClienteToSession(phoneNumber, cliente.Id);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ ¡Registro completado con éxito, {cliente.Nombre}!\n\n" +
                $"Bienvenido al Lavadero AutoClean 🚗✨\n\n" +
                $"Ahora, para brindarte un mejor servicio, necesitamos registrar tu primer vehículo.");

            await Task.Delay(1000);

            // Iniciar proceso de registro de vehículo
            await IniciarRegistroVehiculo(phoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando cliente desde sesión");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error al crear tu registro. Por favor, intenta nuevamente más tarde.");

            await _sessionService.ClearSession(phoneNumber);
        }
    }
}
