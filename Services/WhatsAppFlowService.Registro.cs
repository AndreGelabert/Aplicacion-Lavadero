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

        // Validar que solo contenga letras y tenga mínimo 3 caracteres
        if (!EsTextoValido(nombre))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El nombre debe contener al menos 3 letras (solo letras y espacios permitidos). Por favor, inténtalo nuevamente:");
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

        // Validar que solo contenga letras y tenga mínimo 3 caracteres
        if (!EsTextoValido(apellido))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El apellido debe contener al menos 3 letras (solo letras y espacios permitidos). Por favor, inténtalo nuevamente:");
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
                "❌ El formato del email no es válido.\n\n" +
                "El email debe tener:\n" +
                "• Al menos 3 caracteres después del @\n" +
                "• Un dominio válido (ej: .com, .ar)\n\n" +
                "Por favor, inténtalo nuevamente:");
            return;
        }

        // Guardar email en minúsculas (después de validar)
        await _sessionService.SaveTemporaryData(phoneNumber, "Email", email.ToLowerInvariant());
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
            // Marcar datos del cliente como confirmados (NO crear cliente aún)
            await _sessionService.SaveTemporaryData(phoneNumber, "ClienteDatosConfirmados", "true");
            
            // Mostrar opciones de vehículo
            await MostrarOpcionesVehiculoRegistro(phoneNumber, session);
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
    /// Muestra las opciones de vehículo durante el proceso de registro inicial
    /// </summary>
    private async Task MostrarOpcionesVehiculoRegistro(string phoneNumber, WhatsAppSession session)
    {
        var nombre = session.TemporaryData.GetValueOrDefault("Nombre", "");
        
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.REGISTRO_VEHICULO_OPCION);
        
        var options = new List<(string, string, string)>
        {
            ("crear_vehiculo", "🚗 Registrar nuevo vehículo", "Registra un vehículo propio"),
            ("asociar_vehiculo", "🔗 Asociar vehículo existente", "Vincula un vehículo compartido")
        };

        await _whatsAppService.SendListMessage(phoneNumber,
            $"✅ ¡Perfecto {nombre}!\n\n" +
            "Ahora necesitamos registrar al menos un vehículo.\n\n" +
            "¿Qué deseas hacer?",
            "📋 Ver opciones",
            "Opciones de vehículo",
            options);
    }

    /// <summary>
    /// Maneja la selección de opción de vehículo durante el registro inicial
    /// </summary>
    private async Task HandleRegistroVehiculoOpcion(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion == "crear_vehiculo" || opcion.Contains("nuevo") || opcion.Contains("registrar"))
        {
            // Iniciar proceso de registro de vehículo nuevo (flujo de registro inicial)
            await _sessionService.SaveTemporaryData(phoneNumber, "RegistroInicial", "true");
            await IniciarRegistroVehiculo(phoneNumber);
        }
        else if (opcion == "asociar_vehiculo" || opcion.Contains("asociar") || opcion.Contains("existente"))
        {
            // Iniciar proceso de asociación de vehículo existente (flujo de registro inicial)
            await _sessionService.SaveTemporaryData(phoneNumber, "RegistroInicial", "true");
            await IniciarAsociacionVehiculoRegistro(phoneNumber);
        }
        else
        {
            // Volver a mostrar opciones
            await MostrarOpcionesVehiculoRegistro(phoneNumber, session);
        }
    }

    /// <summary>
    /// Inicia el proceso de asociación de vehículo durante el registro inicial (sin cliente creado aún)
    /// </summary>
    private async Task IniciarAsociacionVehiculoRegistro(string phoneNumber)
    {
        await _whatsAppService.SendTextMessage(phoneNumber,
            "🔗 *Asociar vehículo existente*\n\n" +
            "Este proceso te permite vincularte a un vehículo que ya está registrado " +
            "en nuestro sistema (ej: vehículo familiar, de pareja, etc.).\n\n" +
            "Necesitarás:\n" +
            "• La *patente* del vehículo\n" +
            "• La *clave de asociación* que te proporcionó el dueño\n\n" +
            "📝 Por favor, ingresa la *patente* del vehículo:");

        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.ASOCIAR_VEHICULO_PATENTE);
    }

    /// <summary>
    /// Crea el cliente y vehículo juntos al finalizar el proceso de registro inicial.
    /// Esta operación realiza múltiples pasos en secuencia:
    /// 1. Crea el cliente en la base de datos
    /// 2. Asocia el cliente a la sesión de WhatsApp
    /// 3. Crea el vehículo con clave de asociación
    /// 4. Actualiza la lista de vehículos del cliente
    /// 
    /// Nota: En caso de error en cualquier paso, se limpia la sesión pero no se
    /// revierten los datos parciales. En un escenario de producción crítico,
    /// se debería considerar implementar transacciones o compensaciones.
    /// </summary>
    private async Task CrearClienteYVehiculoDesdeSession(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            // phoneNumber viene con código de país (ej: 543751590586)
            // Guardamos SIN código de país en la DB (ej: 3751590586)
            var telefonoParaDB = PhoneNumberHelper.RemoveCountryCode(phoneNumber);
            
            _logger.LogInformation("💾 Creando cliente y vehículo:");
            _logger.LogInformation("   📱 Teléfono WhatsApp: {WhatsAppPhone}", phoneNumber);
            _logger.LogInformation("   📱 Teléfono para DB: {DBPhone}", telefonoParaDB);
            
            // 1. Crear cliente primero
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

            // 2. Asociar cliente a la sesión
            await _sessionService.AssociateClienteToSession(phoneNumber, cliente.Id);

            // 3. Crear vehículo
            var claveAsociacion = VehiculoService.GenerarClaveAsociacion();
            var claveHash = VehiculoService.HashClaveAsociacion(claveAsociacion);

            var vehiculo = new Vehiculo
            {
                Id = "",
                Patente = session.TemporaryData.GetValueOrDefault("VehiculoPatente", "").ToUpperInvariant(),
                TipoVehiculo = session.TemporaryData.GetValueOrDefault("VehiculoTipo", ""),
                Marca = session.TemporaryData.GetValueOrDefault("VehiculoMarca", ""),
                Modelo = session.TemporaryData.GetValueOrDefault("VehiculoModelo", ""),
                Color = session.TemporaryData.GetValueOrDefault("VehiculoColor", ""),
                ClienteId = cliente.Id,
                ClienteNombreCompleto = cliente.NombreCompleto,
                ClientesIds = new List<string> { cliente.Id },
                ClaveAsociacionHash = claveHash,
                Estado = "Activo"
            };

            await _vehiculoService.CrearVehiculo(vehiculo);

            // 4. Actualizar lista de vehículos del cliente
            cliente.VehiculosIds.Add(vehiculo.Id);
            await _clienteService.ActualizarCliente(cliente);

            _logger.LogInformation("✅ Vehículo creado exitosamente: {VehiculoId} para cliente {ClienteId}",
                vehiculo.Id, cliente.Id);

            // 5. Limpiar datos temporales
            LimpiarDatosTemporalesRegistro(session);

            // 6. Enviar mensajes de éxito
            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ ¡Registro completado con éxito, {cliente.Nombre}!\n\n" +
                $"Bienvenido al Lavadero AutoClean 🚗✨");

            await Task.Delay(1000);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"🚗 Vehículo registrado:\n" +
                $"   {vehiculo.Marca} {vehiculo.Modelo}\n" +
                $"   Patente: {vehiculo.Patente}\n\n" +
                $"🔑 *Clave de asociación:* `{claveAsociacion}`\n\n" +
                $"⚠️ *Importante:* Guarda esta clave en un lugar seguro.\n" +
                $"Con ella, otras personas podrán vincularse a este vehículo " +
                $"(ej: familiares, pareja, etc.).\n\n" +
                $"Esta clave se muestra *solo esta vez*.");

            await Task.Delay(1500);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"Ya puedes disfrutar de nuestros servicios 🎉");

            await Task.Delay(1000);

            // 7. Mostrar menú principal
            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
            await ShowClienteMenu(phoneNumber, cliente.Nombre);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando cliente y vehículo desde sesión");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error al crear tu registro. Por favor, intenta nuevamente más tarde.");

            await _sessionService.ClearSession(phoneNumber);
        }
    }

    /// <summary>
    /// Asocia un vehículo existente al nuevo cliente durante el registro inicial.
    /// Esta operación realiza múltiples pasos en secuencia:
    /// 1. Crea el cliente en la base de datos
    /// 2. Asocia el cliente a la sesión de WhatsApp
    /// 3. Asocia el cliente al vehículo existente
    /// 4. Actualiza la lista de vehículos del cliente
    /// 
    /// Nota: En caso de error en cualquier paso, se limpia la sesión pero no se
    /// revierten los datos parciales. En un escenario de producción crítico,
    /// se debería considerar implementar transacciones o compensaciones.
    /// </summary>
    private async Task CrearClienteYAsociarVehiculoDesdeSession(string phoneNumber, WhatsAppSession session, string vehiculoId)
    {
        try
        {
            var telefonoParaDB = PhoneNumberHelper.RemoveCountryCode(phoneNumber);
            
            _logger.LogInformation("💾 Creando cliente y asociando vehículo:");
            
            // 1. Crear cliente primero
            var cliente = new Cliente
            {
                Id = "",
                TipoDocumento = session.TemporaryData.GetValueOrDefault("TipoDocumento", ""),
                NumeroDocumento = session.TemporaryData.GetValueOrDefault("NumeroDocumento", ""),
                Nombre = session.TemporaryData.GetValueOrDefault("Nombre", ""),
                Apellido = session.TemporaryData.GetValueOrDefault("Apellido", ""),
                Telefono = telefonoParaDB,
                Email = session.TemporaryData.GetValueOrDefault("Email", ""),
                VehiculosIds = new List<string>(),
                Estado = "Activo"
            };

            await _clienteService.CrearCliente(cliente);
            _logger.LogInformation("✅ Cliente creado exitosamente: {ClienteId}", cliente.Id);

            // 2. Asociar cliente a la sesión
            await _sessionService.AssociateClienteToSession(phoneNumber, cliente.Id);

            // 3. Asociar cliente al vehículo existente
            var exitoso = await _vehiculoService.AsociarClienteAVehiculo(vehiculoId, cliente.Id);

            if (!exitoso)
            {
                throw new Exception("Error al asociar el vehículo al cliente");
            }

            // 4. Actualizar lista de vehículos del cliente
            cliente.VehiculosIds.Add(vehiculoId);
            await _clienteService.ActualizarCliente(cliente);

            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);
            var vehiculoInfo = vehiculo != null 
                ? $"{vehiculo.Marca} {vehiculo.Modelo} - {vehiculo.Patente}" 
                : "Vehículo asociado";

            _logger.LogInformation("🔗 Cliente {ClienteId} asociado al vehículo {VehiculoId}",
                cliente.Id, vehiculoId);

            // 5. Limpiar datos temporales
            LimpiarDatosTemporalesRegistro(session);

            // 6. Enviar mensajes de éxito
            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ ¡Registro completado con éxito, {cliente.Nombre}!\n\n" +
                $"Bienvenido al Lavadero AutoClean 🚗✨\n\n" +
                $"🔗 Te has vinculado al vehículo:\n" +
                $"   {vehiculoInfo}\n\n" +
                $"Ya puedes disfrutar de nuestros servicios 🎉");

            await Task.Delay(1000);

            // 7. Mostrar menú principal
            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
            await ShowClienteMenu(phoneNumber, cliente.Nombre);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando cliente y asociando vehículo");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error al crear tu registro. Por favor, intenta nuevamente más tarde.");

            await _sessionService.ClearSession(phoneNumber);
        }
    }

    /// <summary>
    /// Limpia los datos temporales del registro
    /// </summary>
    private void LimpiarDatosTemporalesRegistro(WhatsAppSession session)
    {
        // Datos del cliente
        session.TemporaryData.Remove("TipoDocumento");
        session.TemporaryData.Remove("NumeroDocumento");
        session.TemporaryData.Remove("Nombre");
        session.TemporaryData.Remove("Apellido");
        session.TemporaryData.Remove("Email");
        session.TemporaryData.Remove("ClienteDatosConfirmados");
        session.TemporaryData.Remove("RegistroInicial");

        // Datos del vehículo
        session.TemporaryData.Remove("VehiculoPatente");
        session.TemporaryData.Remove("VehiculoTipo");
        session.TemporaryData.Remove("VehiculoMarca");
        session.TemporaryData.Remove("VehiculoMarcaId");
        session.TemporaryData.Remove("VehiculoModelo");
        session.TemporaryData.Remove("VehiculoColor");
        session.TemporaryData.Remove("MarcasDisponibles");
        session.TemporaryData.Remove("ModelosDisponibles");
        session.TemporaryData.Remove("ColoresDisponibles");

        // Datos de asociación
        session.TemporaryData.Remove("AsociarVehiculoId");
        session.TemporaryData.Remove("AsociarVehiculoPatente");
        session.TemporaryData.Remove("AsociarVehiculoInfo");
    }
}
