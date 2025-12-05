using Firebase.Models;
using Firebase.Models.WhatsApp;

namespace Firebase.Services;

/// <summary>
/// Parte del servicio de flujos que maneja la gestión de vehículos
/// </summary>
public partial class WhatsAppFlowService
{
    /// <summary>
    /// Inicia el proceso de registro de vehículo
    /// </summary>
    private async Task IniciarRegistroVehiculo(string phoneNumber)
    {
        try
        {
            // Obtener tipos de vehículo disponibles
            var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos();

            if (tiposVehiculo == null || !tiposVehiculo.Any())
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "⚠️ Lo siento, hubo un problema al cargar los tipos de vehículo. " +
                    "Por favor, contacta al lavadero directamente.");
                return;
            }

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.VEHICULO_TIPO);

            // Crear lista con tipos de vehículo
            var options = tiposVehiculo.Select(tipo => (
                tipo,
                tipo,
                "Disponible"
            )).ToList();

            if (options.Count <= 3)
            {
                // Usar botones si son 3 o menos
                var buttons = options.Select(o => (o.Item1, o.Item2)).ToList();
                await _whatsAppService.SendButtonMessage(phoneNumber,
                    "🚗 ¿Qué tipo de vehículo deseas registrar?",
                    buttons);
            }
            else
            {
                // Usar lista si son más de 3
                await _whatsAppService.SendListMessage(phoneNumber,
                    "🚗 ¿Qué tipo de vehículo deseas registrar?",
                    "Ver opciones",
                    "Tipos de vehículo",
                    options);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error iniciando registro de vehículo");
            throw;
        }
    }

    /// <summary>
    /// Maneja la selección del tipo de vehículo
    /// </summary>
    private async Task HandleVehiculoTipo(string phoneNumber, WhatsAppSession session, string input)
    {
        var tipoVehiculo = input.Trim();

        // Validar que el tipo de vehículo existe
        var tiposVehiculo = await _tipoVehiculoService.ObtenerTiposVehiculos();
        if (!tiposVehiculo.Any(t => t.Equals(tipoVehiculo, StringComparison.OrdinalIgnoreCase)))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Tipo de vehículo no válido. Por favor, selecciona uno de las opciones.");
            return;
        }

        // Guardar tipo de vehículo
        await _sessionService.SaveTemporaryData(phoneNumber, "VehiculoTipo", tipoVehiculo);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.VEHICULO_PATENTE);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ Tipo: {tipoVehiculo}\n\n" +
            $"🔢 Ahora, ingresa la *patente* del vehículo:");
    }

    /// <summary>
    /// Maneja el ingreso de la patente
    /// </summary>
    private async Task HandleVehiculoPatente(string phoneNumber, WhatsAppSession session, string input)
    {
        var patente = input.Trim().ToUpperInvariant();

        // Validar formato de patente
        if (!EsPatenteValida(patente))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ La patente solo puede contener letras, números, espacios y guiones. Por favor, inténtalo nuevamente:");
            return;
        }

        // Verificar si ya existe un vehículo con esta patente
        var vehiculoExistente = await _vehiculoService.ObtenerVehiculoPorPatente(patente);

        if (vehiculoExistente != null && vehiculoExistente.Estado == "Activo")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                $"⚠️ Ya existe un vehículo activo con la patente *{patente}* " +
                $"({vehiculoExistente.Marca} {vehiculoExistente.Modelo}).\n\n" +
                $"Por favor, ingresa otra patente:");
            return;
        }

        // Guardar patente
        await _sessionService.SaveTemporaryData(phoneNumber, "VehiculoPatente", patente);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.VEHICULO_MARCA);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ Patente: {patente}\n\n" +
            $"🏷️ ¿Cuál es la *marca* del vehículo? (ej: Toyota, Ford, etc.)");
    }

    /// <summary>
    /// Maneja el ingreso de la marca
    /// </summary>
    private async Task HandleVehiculoMarca(string phoneNumber, WhatsAppSession session, string input)
    {
        var marca = input.Trim();

        // Validar que no esté vacío
        if (string.IsNullOrWhiteSpace(marca))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Por favor, ingresa una marca válida:");
            return;
        }

        // Guardar marca
        await _sessionService.SaveTemporaryData(phoneNumber, "VehiculoMarca", marca);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.VEHICULO_MODELO);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ Marca: {marca}\n\n" +
            $"🚙 ¿Cuál es el *modelo*? (ej: Corolla, Fiesta, etc.)");
    }

    /// <summary>
    /// Maneja el ingreso del modelo
    /// </summary>
    private async Task HandleVehiculoModelo(string phoneNumber, WhatsAppSession session, string input)
    {
        var modelo = input.Trim();

        // Validar que no esté vacío
        if (string.IsNullOrWhiteSpace(modelo))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Por favor, ingresa un modelo válido:");
            return;
        }

        // Guardar modelo
        await _sessionService.SaveTemporaryData(phoneNumber, "VehiculoModelo", modelo);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.VEHICULO_COLOR);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ Modelo: {modelo}\n\n" +
            $"🎨 ¿De qué *color* es tu vehículo?");
    }

    /// <summary>
    /// Maneja el ingreso del color
    /// </summary>
    private async Task HandleVehiculoColor(string phoneNumber, WhatsAppSession session, string input)
    {
        var color = input.Trim();

        // Validar que no esté vacío
        if (string.IsNullOrWhiteSpace(color))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Por favor, ingresa un color válido:");
            return;
        }

        // Guardar color
        await _sessionService.SaveTemporaryData(phoneNumber, "VehiculoColor", color);
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.VEHICULO_CONFIRMACION);

        // Mostrar resumen para confirmación
        var patente = session.TemporaryData.GetValueOrDefault("VehiculoPatente", "");
        var tipo = session.TemporaryData.GetValueOrDefault("VehiculoTipo", "");
        var marca = session.TemporaryData.GetValueOrDefault("VehiculoMarca", "");
        var modelo = session.TemporaryData.GetValueOrDefault("VehiculoModelo", "");

        var resumen = $"🚗 *Resumen del vehículo:*\n\n" +
                      $"• Tipo: {tipo}\n" +
                      $"• Patente: {patente}\n" +
                      $"• Marca: {marca}\n" +
                      $"• Modelo: {modelo}\n" +
                      $"• Color: {color}\n\n" +
                      $"¿Los datos son correctos?\n\n" +
                      $"Responde *SÍ* para confirmar o *NO* para cancelar.";

        await _whatsAppService.SendTextMessage(phoneNumber, resumen);
    }

    /// <summary>
    /// Maneja la confirmación del registro del vehículo
    /// </summary>
    private async Task HandleVehiculoConfirmacion(string phoneNumber, WhatsAppSession session, string input)
    {
        var respuesta = input.Trim().ToUpperInvariant();

        if (respuesta == "SI" || respuesta == "SÍ" || respuesta == "S")
        {
            // Confirmar registro del vehículo
            await CrearVehiculoDesdeSession(phoneNumber, session);
        }
        else if (respuesta == "NO" || respuesta == "N")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Registro de vehículo cancelado.");

            await Task.Delay(500);

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
                "⚠️ Por favor, responde *SÍ* para confirmar o *NO* para cancelar.");
        }
    }

    /// <summary>
    /// Crea el vehículo en la base de datos a partir de la sesión
    /// </summary>
    private async Task CrearVehiculoDesdeSession(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            if (string.IsNullOrEmpty(session.ClienteId))
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: No se pudo identificar tu usuario. Por favor, reinicia la conversación.");
                return;
            }

            var cliente = await _clienteService.ObtenerCliente(session.ClienteId);
            if (cliente == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: Cliente no encontrado.");
                return;
            }

            // Generar clave de asociación para permitir que otros usuarios se asocien al vehículo
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
                ClienteId = session.ClienteId,
                ClienteNombreCompleto = cliente.NombreCompleto,
                ClientesIds = new List<string> { session.ClienteId },
                ClaveAsociacionHash = claveHash,
                Estado = "Activo"
            };

            await _vehiculoService.CrearVehiculo(vehiculo);

            // Actualizar lista de vehículos del cliente
            cliente.VehiculosIds.Add(vehiculo.Id);
            await _clienteService.ActualizarCliente(cliente);

            _logger.LogInformation("✅ Vehículo creado exitosamente: {VehiculoId} para cliente {ClienteId}",
                vehiculo.Id, cliente.Id);

            // Limpiar datos temporales del vehículo
            session.TemporaryData.Remove("VehiculoPatente");
            session.TemporaryData.Remove("VehiculoTipo");
            session.TemporaryData.Remove("VehiculoMarca");
            session.TemporaryData.Remove("VehiculoModelo");
            session.TemporaryData.Remove("VehiculoColor");

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ ¡Vehículo registrado con éxito!\n\n" +
                $"🚗 {vehiculo.Marca} {vehiculo.Modelo}\n" +
                $"🔢 Patente: {vehiculo.Patente}\n\n" +
                $"🔑 *Clave de asociación:* `{claveAsociacion}`\n\n" +
                $"⚠️ *Importante:* Guarda esta clave en un lugar seguro.\n" +
                $"Con ella, otras personas podrán vincularse a este vehículo " +
                $"(ej: familiares, pareja, etc.).\n\n" +
                $"Esta clave se muestra *solo esta vez*.");

            await Task.Delay(1500);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"Ya puedes disfrutar de nuestros servicios 🎉");

            await Task.Delay(1000);

            // Volver al menú principal
            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
            await ShowClienteMenu(phoneNumber, cliente.Nombre);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando vehículo desde sesión");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error al registrar el vehículo. Por favor, intenta nuevamente más tarde.");
        }
    }

    // ========================================================================
    // MÉTODOS PARA ASOCIACIÓN DE VEHÍCULOS (MÚLTIPLES DUEÑOS)
    // ========================================================================

    /// <summary>
    /// Inicia el proceso de asociación a un vehículo existente
    /// </summary>
    private async Task IniciarAsociacionVehiculo(string phoneNumber)
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
    /// Maneja el ingreso de la patente para asociación
    /// </summary>
    private async Task HandleAsociarVehiculoPatente(string phoneNumber, WhatsAppSession session, string input)
    {
        var patente = input.Trim().ToUpperInvariant();

        // Validar formato de patente
        if (!EsPatenteValida(patente))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ La patente solo puede contener letras, números, espacios y guiones. Por favor, inténtalo nuevamente:");
            return;
        }

        // Buscar el vehículo
        var vehiculo = await _vehiculoService.ObtenerVehiculoPorPatente(patente);

        if (vehiculo == null || vehiculo.Estado != "Activo")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                $"❌ No se encontró un vehículo activo con la patente *{patente}*.\n\n" +
                $"Verifica la patente e intenta nuevamente, o escribe *MENU* para volver al menú.");
            return;
        }

        // Verificar que tenga clave de asociación
        if (string.IsNullOrEmpty(vehiculo.ClaveAsociacionHash))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                $"⚠️ El vehículo con patente *{patente}* no tiene habilitada la asociación.\n\n" +
                $"El dueño del vehículo debe solicitar una nueva clave de asociación.\n\n" +
                $"Escribe *MENU* para volver al menú.");
            return;
        }

        // Verificar que el cliente no esté ya asociado
        var clienteYaAsociado = vehiculo.ClienteId == session.ClienteId ||
                                 (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Contains(session.ClienteId!));

        if (clienteYaAsociado)
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                $"⚠️ Ya estás asociado al vehículo *{patente}*.\n\n" +
                $"Escribe *MENU* para volver al menú.");
            return;
        }

        // Guardar datos temporales
        await _sessionService.SaveTemporaryData(phoneNumber, "AsociarVehiculoId", vehiculo.Id);
        await _sessionService.SaveTemporaryData(phoneNumber, "AsociarVehiculoPatente", patente);
        await _sessionService.SaveTemporaryData(phoneNumber, "AsociarVehiculoInfo", $"{vehiculo.Marca} {vehiculo.Modelo} - {vehiculo.Color}");

        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.ASOCIAR_VEHICULO_CLAVE);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ Vehículo encontrado:\n\n" +
            $"🚗 *{patente}*\n" +
            $"   {vehiculo.Marca} {vehiculo.Modelo} - {vehiculo.Color}\n\n" +
            $"🔑 Ahora, ingresa la *clave de asociación* (formato: XXXX-XXXX):");
    }

    /// <summary>
    /// Maneja el ingreso de la clave de asociación
    /// </summary>
    private async Task HandleAsociarVehiculoClave(string phoneNumber, WhatsAppSession session, string input)
    {
        var claveIngresada = input.Trim().ToUpperInvariant();

        var vehiculoId = session.TemporaryData.GetValueOrDefault("AsociarVehiculoId", "");
        var vehiculoPatente = session.TemporaryData.GetValueOrDefault("AsociarVehiculoPatente", "");
        var vehiculoInfo = session.TemporaryData.GetValueOrDefault("AsociarVehiculoInfo", "");

        if (string.IsNullOrEmpty(vehiculoId))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Error: No se encontró información del vehículo. Por favor, reinicia el proceso.");
            await MostrarMenuVehiculos(phoneNumber, session);
            return;
        }

        var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);
        if (vehiculo == null)
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Error: Vehículo no encontrado.");
            await MostrarMenuVehiculos(phoneNumber, session);
            return;
        }

        // Validar la clave
        if (!VehiculoService.ValidarClaveAsociacion(claveIngresada, vehiculo.ClaveAsociacionHash ?? ""))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ La clave de asociación es incorrecta.\n\n" +
                "Verifica la clave e intenta nuevamente, o escribe *MENU* para cancelar:");
            return;
        }

        // Clave correcta - pedir confirmación
        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION);

        await _whatsAppService.SendTextMessage(phoneNumber,
            $"✅ *Clave correcta*\n\n" +
            $"Estás a punto de asociarte al siguiente vehículo:\n\n" +
            $"🚗 *{vehiculoPatente}*\n" +
            $"   {vehiculoInfo}\n\n" +
            $"Una vez asociado, podrás gestionar este vehículo desde tu cuenta.\n\n" +
            $"¿Confirmas la asociación?\n\n" +
            $"Responde *SÍ* para confirmar o *NO* para cancelar.");
    }

    /// <summary>
    /// Maneja la confirmación de asociación del vehículo
    /// </summary>
    private async Task HandleAsociarVehiculoConfirmacion(string phoneNumber, WhatsAppSession session, string input)
    {
        var respuesta = input.Trim().ToUpperInvariant();

        if (respuesta == "SI" || respuesta == "SÍ" || respuesta == "S")
        {
            await AsociarVehiculoACliente(phoneNumber, session);
        }
        else if (respuesta == "NO" || respuesta == "N")
        {
            // Limpiar datos temporales
            session.TemporaryData.Remove("AsociarVehiculoId");
            session.TemporaryData.Remove("AsociarVehiculoPatente");
            session.TemporaryData.Remove("AsociarVehiculoInfo");

            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Asociación cancelada.\n\n" +
                "Volviendo al menú de vehículos...");

            await Task.Delay(500);
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        else
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Por favor, responde *SÍ* para confirmar o *NO* para cancelar.");
        }
    }

    /// <summary>
    /// Asocia el vehículo al cliente actual
    /// </summary>
    private async Task AsociarVehiculoACliente(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var vehiculoId = session.TemporaryData.GetValueOrDefault("AsociarVehiculoId", "");
            var vehiculoPatente = session.TemporaryData.GetValueOrDefault("AsociarVehiculoPatente", "");
            var vehiculoInfo = session.TemporaryData.GetValueOrDefault("AsociarVehiculoInfo", "");

            if (string.IsNullOrEmpty(session.ClienteId) || string.IsNullOrEmpty(vehiculoId))
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: Datos incompletos para la asociación.");
                return;
            }

            // Asociar cliente al vehículo
            var exitoso = await _vehiculoService.AsociarClienteAVehiculo(vehiculoId, session.ClienteId);

            if (!exitoso)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al asociar el vehículo. Por favor, intenta nuevamente.");
                return;
            }

            // Actualizar lista de vehículos del cliente
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId);
            if (cliente != null && !cliente.VehiculosIds.Contains(vehiculoId))
            {
                cliente.VehiculosIds.Add(vehiculoId);
                await _clienteService.ActualizarCliente(cliente);
            }

            _logger.LogInformation("🔗 Cliente {ClienteId} asociado al vehículo {VehiculoId} ({Patente})",
                session.ClienteId, vehiculoId, vehiculoPatente);

            // Limpiar datos temporales
            session.TemporaryData.Remove("AsociarVehiculoId");
            session.TemporaryData.Remove("AsociarVehiculoPatente");
            session.TemporaryData.Remove("AsociarVehiculoInfo");

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ *¡Asociación exitosa!*\n\n" +
                $"Ahora estás vinculado al vehículo:\n\n" +
                $"🚗 *{vehiculoPatente}*\n" +
                $"   {vehiculoInfo}\n\n" +
                $"Ya puedes gestionar este vehículo desde tu cuenta 🎉");

            await Task.Delay(1000);

            // Volver al menú principal
            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
            if (cliente != null)
            {
                await ShowClienteMenu(phoneNumber, cliente.Nombre);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error asociando vehículo a cliente");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Por favor, intenta nuevamente.");
        }
    }

    /// <summary>
    /// Maneja la visualización de la clave de asociación de un vehículo
    /// </summary>
    private async Task HandleMostrarClaveVehiculo(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion.Contains("generar") || opcion == "generar_clave")
        {
            await RegenerarClaveAsociacion(phoneNumber, session);
        }
        else if (opcion.Contains("menu") || opcion.Contains("volver") || opcion == "menu_vehiculos")
        {
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        else
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Opción no reconocida. Por favor, selecciona una de las opciones del menú.");
        }
    }

    /// <summary>
    /// Regenera la clave de asociación de un vehículo
    /// </summary>
    private async Task RegenerarClaveAsociacion(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var vehiculoId = session.TemporaryData.GetValueOrDefault("vehiculo_modificar_id", "");

            if (string.IsNullOrEmpty(vehiculoId))
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: No se encontró información del vehículo.");
                return;
            }

            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);
            if (vehiculo == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error: Vehículo no encontrado.");
                return;
            }

            // Verificar que el cliente sea el dueño principal
            if (vehiculo.ClienteId != session.ClienteId)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Solo el dueño principal puede generar una nueva clave de asociación.");
                await MostrarMenuVehiculos(phoneNumber, session);
                return;
            }

            // Generar nueva clave
            var nuevaClave = VehiculoService.GenerarClaveAsociacion();
            var nuevoHash = VehiculoService.HashClaveAsociacion(nuevaClave);

            vehiculo.ClaveAsociacionHash = nuevoHash;
            await _vehiculoService.ActualizarVehiculo(vehiculo);

            _logger.LogInformation("🔑 Nueva clave de asociación generada para vehículo {Patente} por cliente {ClienteId}",
                vehiculo.Patente, session.ClienteId);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ *Nueva clave de asociación generada*\n\n" +
                $"🚗 Vehículo: *{vehiculo.Patente}*\n\n" +
                $"🔑 *Nueva clave:* `{nuevaClave}`\n\n" +
                $"⚠️ *Importante:*\n" +
                $"• La clave anterior ya no funcionará\n" +
                $"• Guarda esta clave en un lugar seguro\n" +
                $"• Compártela solo con personas de confianza\n" +
                $"• Esta clave se muestra *solo esta vez*");

            await Task.Delay(1000);
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerando clave de asociación");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Por favor, intenta nuevamente.");
        }
    }
}
