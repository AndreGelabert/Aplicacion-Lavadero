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
}
