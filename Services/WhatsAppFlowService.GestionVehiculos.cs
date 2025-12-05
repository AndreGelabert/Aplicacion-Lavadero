using Firebase.Models;
using Firebase.Models.WhatsApp;

namespace Firebase.Services;

/// <summary>
/// Parte del servicio de flujos que maneja la modificación y eliminación de vehículos
/// </summary>
public partial class WhatsAppFlowService
{
    /// <summary>
    /// Muestra la lista de vehículos para seleccionar cuál modificar
    /// </summary>
    private async Task MostrarVehiculosParaModificar(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(session.ClienteId!);
            var vehiculosActivos = vehiculos?.Where(v => v.Estado == "Activo").ToList();

            if (vehiculosActivos == null || !vehiculosActivos.Any())
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "📭 No tienes vehículos para modificar.\n\n" +
                    "Volviendo al menú de vehículos...");

                await Task.Delay(500);
                await MostrarMenuVehiculos(phoneNumber, session);
                return;
            }

            var mensaje = "🚗 *Selecciona el vehículo a modificar:*\n\n";

            var index = 1;
            foreach (var vehiculo in vehiculosActivos)
            {
                mensaje += $"{index}. *{vehiculo.Patente}*\n" +
                          $"   {vehiculo.Marca} {vehiculo.Modelo} - {vehiculo.Color}\n\n";
                
                // Guardar en datos temporales para referencia
                await _sessionService.SaveTemporaryData(phoneNumber, $"vehiculo_{index}_id", vehiculo.Id);
                await _sessionService.SaveTemporaryData(phoneNumber, $"vehiculo_{index}_patente", vehiculo.Patente);
                
                index++;
            }

            mensaje += $"Escribe el *número* del vehículo que deseas modificar (1-{vehiculosActivos.Count}):";

            await _sessionService.SaveTemporaryData(phoneNumber, "total_vehiculos", vehiculosActivos.Count.ToString());
            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.SELECCIONAR_VEHICULO_MODIFICAR);
            
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mostrando vehículos para modificar");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Procesa la selección del vehículo a modificar
    /// </summary>
    private async Task HandleSeleccionVehiculoModificar(string phoneNumber, WhatsAppSession session, string input)
    {
        try
        {
            if (!int.TryParse(input.Trim(), out var numero))
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Por favor, ingresa un número válido.");
                return;
            }

            var totalVehiculos = int.Parse(session.TemporaryData.GetValueOrDefault("total_vehiculos", "0"));

            if (numero < 1 || numero > totalVehiculos)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    $"❌ Número inválido. Por favor, ingresa un número entre 1 y {totalVehiculos}.");
                return;
            }

            var vehiculoId = session.TemporaryData.GetValueOrDefault($"vehiculo_{numero}_id", "");
            var vehiculoPatente = session.TemporaryData.GetValueOrDefault($"vehiculo_{numero}_patente", "");

            if (string.IsNullOrEmpty(vehiculoId))
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener el vehículo. Intenta nuevamente.");
                return;
            }

            // Obtener datos completos del vehículo
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);
            if (vehiculo == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Vehículo no encontrado.");
                return;
            }

            // Guardar ID del vehículo seleccionado
            await _sessionService.SaveTemporaryData(phoneNumber, "vehiculo_modificar_id", vehiculoId);

            // Mostrar menú de opciones para este vehículo
            await MostrarMenuModificarVehiculo(phoneNumber, vehiculo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando selección de vehículo");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Muestra el menú de opciones para modificar un vehículo específico
    /// </summary>
    private async Task MostrarMenuModificarVehiculo(string phoneNumber, Vehiculo vehiculo)
    {
        // Obtener la sesión para verificar si es el dueño principal
        var session = await _sessionService.GetOrCreateSession(phoneNumber);
        var esDuenoPrincipal = vehiculo.ClienteId == session.ClienteId;

        // Contar cuántos dueños tiene el vehículo
        var cantidadDuenos = 1; // Al menos el dueño principal
        if (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Any())
        {
            cantidadDuenos = vehiculo.ClientesIds.Distinct().Count();
        }

        var mensaje = $"🚗 *Gestionar vehículo*\n\n" +
                     $"Patente: *{vehiculo.Patente}*\n" +
                     $"Marca: {vehiculo.Marca}\n" +
                     $"Modelo: {vehiculo.Modelo}\n" +
                     $"Color: {vehiculo.Color}\n" +
                     $"Tipo: {vehiculo.TipoVehiculo}\n";
        
        if (cantidadDuenos > 1)
        {
            mensaje += $"👥 Dueños asociados: {cantidadDuenos}\n";
        }
        
        if (esDuenoPrincipal)
        {
            mensaje += $"👑 Eres el dueño principal\n";
        }
        
        mensaje += $"\n¿Qué deseas hacer?";

        await _whatsAppService.SendTextMessage(phoneNumber, mensaje);

        await Task.Delay(300);

        // Usar lista para poder mostrar más opciones
        var options = new List<(string id, string title, string description)>
        {
            ("modificar_color", "🎨 Editar color", "Cambiar el color del vehículo")
        };

        // Solo el dueño principal puede generar clave de asociación
        if (esDuenoPrincipal)
        {
            options.Add(("generar_clave", "🔑 Generar clave", "Nueva clave de asociación"));
        }

        options.Add(("desvincular_vehiculo", "🔗 Desvincular", "Quitar de mi cuenta"));

        await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU);
        await _whatsAppService.SendListMessage(phoneNumber,
            "Selecciona una opción:",
            "📋 Ver opciones",
            "Opciones del vehículo",
            options);
    }

    /// <summary>
    /// Maneja el menú de modificación de un vehículo específico
    /// </summary>
    private async Task HandleMenuModificarVehiculo(string phoneNumber, WhatsAppSession session, string input)
    {
        var opcion = input.Trim().ToLowerInvariant();

        if (opcion.Contains("color") || opcion == "modificar_color")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "🎨 *Cambiar color*\n\n" +
                "Ingresa el nuevo color del vehículo:");

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.MODIFICAR_VEHICULO_COLOR);
        }
        else if (opcion.Contains("clave") || opcion == "generar_clave")
        {
            // Generar nueva clave de asociación
            await RegenerarClaveAsociacion(phoneNumber, session);
        }
        else if (opcion.Contains("desvincular") || opcion == "desvincular_vehiculo")
        {
            // Verificar y confirmar desvinculación
            await VerificarYConfirmarDesvinculacion(phoneNumber, session);
        }
        else if (opcion.Contains("eliminar") || opcion == "eliminar_vehiculo")
        {
            // Mantener compatibilidad con la opción anterior
            await VerificarYConfirmarDesvinculacion(phoneNumber, session);
        }
        else
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "⚠️ Opción no reconocida. Por favor, selecciona una de las opciones del menú.");
        }
    }

    /// <summary>
    /// Verifica y confirma la desvinculación de un vehículo (reemplaza la eliminación)
    /// </summary>
    private async Task VerificarYConfirmarDesvinculacion(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(session.ClienteId!);
            var vehiculosActivos = vehiculos?.Where(v => v.Estado == "Activo").Count() ?? 0;

            if (vehiculosActivos <= 1)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "⚠️ *No puedes desvincular tu único vehículo.*\n\n" +
                    "Debes tener al menos un vehículo asociado.\n\n" +
                    "Si deseas cambiar de vehículo, primero agrega el nuevo y luego desvincula este.");

                await Task.Delay(500);
                await MostrarMenuVehiculos(phoneNumber, session);
                return;
            }

            // Obtener datos del vehículo
            var vehiculoId = session.TemporaryData.GetValueOrDefault("vehiculo_modificar_id", "");
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);

            if (vehiculo == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener los datos del vehículo.");
                return;
            }

            // Contar cuántos dueños tiene
            var cantidadDuenos = 1;
            if (vehiculo.ClientesIds != null && vehiculo.ClientesIds.Any())
            {
                cantidadDuenos = vehiculo.ClientesIds.Distinct().Count();
            }

            var esDuenoPrincipal = vehiculo.ClienteId == session.ClienteId;

            var mensaje = $"⚠️ *Confirmar desvinculación*\n\n" +
                         $"¿Estás seguro de que deseas desvincular el vehículo?\n\n" +
                         $"Patente: *{vehiculo.Patente}*\n" +
                         $"{vehiculo.Marca} {vehiculo.Modelo} - {vehiculo.Color}\n\n";

            if (cantidadDuenos > 1)
            {
                mensaje += $"👥 Este vehículo tiene *{cantidadDuenos} dueños* asociados.\n" +
                          $"Solo te desvinculará a ti, los demás seguirán teniendo acceso.\n\n";
            }
            else if (esDuenoPrincipal)
            {
                mensaje += $"⚠️ Eres el único dueño. Si te desvinculas, el vehículo quedará sin dueño y se desactivará.\n\n";
            }

            mensaje += $"Responde *SÍ* para confirmar o *NO* para cancelar.";

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.CONFIRMAR_ELIMINAR_VEHICULO);
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando desvinculación de vehículo");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Verifica que el cliente tenga más de un vehículo antes de permitir la eliminación
    /// </summary>
    private async Task VerificarYConfirmarEliminacion(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var vehiculos = await _vehiculoService.ObtenerVehiculosPorCliente(session.ClienteId!);
            var vehiculosActivos = vehiculos?.Where(v => v.Estado == "Activo").Count() ?? 0;

            if (vehiculosActivos <= 1)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "⚠️ *No puedes eliminar tu único vehículo.*\n\n" +
                    "Debes tener al menos un vehículo registrado.\n\n" +
                    "Si deseas cambiar de vehículo, primero agrega el nuevo y luego elimina este.");

                await Task.Delay(500);
                await MostrarMenuVehiculos(phoneNumber, session);
                return;
            }

            // Obtener datos del vehículo a eliminar
            var vehiculoId = session.TemporaryData.GetValueOrDefault("vehiculo_modificar_id", "");
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);

            if (vehiculo == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener los datos del vehículo.");
                return;
            }

            var mensaje = $"⚠️ *Confirmar eliminación*\n\n" +
                         $"¿Estás seguro de que deseas eliminar el vehículo?\n\n" +
                         $"Patente: *{vehiculo.Patente}*\n" +
                         $"{vehiculo.Marca} {vehiculo.Modelo} - {vehiculo.Color}\n\n" +
                         $"Esta acción desvinculará el vehículo de tu cuenta.\n\n" +
                         $"Responde *SÍ* para confirmar o *NO* para cancelar.";

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.CONFIRMAR_ELIMINAR_VEHICULO);
            await _whatsAppService.SendTextMessage(phoneNumber, mensaje);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando eliminación de vehículo");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Procesa la confirmación de eliminación del vehículo
    /// </summary>
    private async Task HandleConfirmarEliminarVehiculo(string phoneNumber, WhatsAppSession session, string input)
    {
        var respuesta = input.Trim().ToUpperInvariant();

        if (respuesta == "SI" || respuesta == "SÍ" || respuesta == "S")
        {
            await DesvincularVehiculo(phoneNumber, session);
        }
        else if (respuesta == "NO" || respuesta == "N")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "✅ Desvinculación cancelada.\n\n" +
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
    /// Desvincula al cliente del vehículo (soporta múltiples dueños)
    /// </summary>
    private async Task DesvincularVehiculo(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            var vehiculoId = session.TemporaryData.GetValueOrDefault("vehiculo_modificar_id", "");
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);

            if (vehiculo == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener el vehículo.");
                return;
            }

            var patente = vehiculo.Patente;

            // Usar el método del servicio que maneja correctamente múltiples dueños
            var exitoso = await _vehiculoService.DesvincularClienteDeVehiculo(vehiculoId, session.ClienteId!);

            if (!exitoso)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al desvincular el vehículo.");
                return;
            }

            // Actualizar lista de vehículos del cliente
            var cliente = await _clienteService.ObtenerCliente(session.ClienteId!);
            if (cliente != null)
            {
                cliente.VehiculosIds.Remove(vehiculoId);
                await _clienteService.ActualizarCliente(cliente);
            }

            _logger.LogInformation("🔗 Cliente {ClienteId} desvinculado del vehículo {Patente}",
                session.ClienteId, patente);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ *Vehículo desvinculado correctamente*\n\n" +
                $"El vehículo {patente} ha sido desvinculado de tu cuenta.\n\n" +
                $"Volviendo al menú de vehículos...");

            await Task.Delay(1000);
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error desvinculando vehículo");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error al desvincular el vehículo. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Elimina (desvincula y desactiva) un vehículo - Método legacy mantenido por compatibilidad
    /// </summary>
    private async Task EliminarVehiculo(string phoneNumber, WhatsAppSession session)
    {
        // Redirigir al nuevo método de desvinculación
        await DesvincularVehiculo(phoneNumber, session);
    }

    /// <summary>
    /// Procesa el cambio de modelo del vehículo
    /// </summary>
    private async Task HandleModificarVehiculoModelo(string phoneNumber, WhatsAppSession session, string input)
    {
        var nuevoModelo = input.Trim();

        if (string.IsNullOrWhiteSpace(nuevoModelo))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El modelo no puede estar vacío. Intenta nuevamente:");
            return;
        }

        try
        {
            var vehiculoId = session.TemporaryData.GetValueOrDefault("vehiculo_modificar_id", "");
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);

            if (vehiculo == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener el vehículo.");
                return;
            }

            var modeloAnterior = vehiculo.Modelo;
            vehiculo.Modelo = nuevoModelo;

            await _vehiculoService.ActualizarVehiculo(vehiculo);

            _logger.LogInformation("✏️ Modelo de vehículo {Patente} actualizado: {Anterior} → {Nuevo}",
                vehiculo.Patente, modeloAnterior, nuevoModelo);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ *Modelo actualizado correctamente*\n\n" +
                $"Vehículo: {vehiculo.Patente}\n" +
                $"Modelo anterior: {modeloAnterior}\n" +
                $"Modelo nuevo: {nuevoModelo}\n\n" +
                $"Volviendo al menú de vehículos...");

            await Task.Delay(1000);
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modificando modelo de vehículo");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }

    /// <summary>
    /// Procesa el cambio de color del vehículo
    /// </summary>
    private async Task HandleModificarVehiculoColor(string phoneNumber, WhatsAppSession session, string input)
    {
        var nuevoColor = input.Trim();

        if (string.IsNullOrWhiteSpace(nuevoColor))
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ El color no puede estar vacío. Intenta nuevamente:");
            return;
        }

        try
        {
            var vehiculoId = session.TemporaryData.GetValueOrDefault("vehiculo_modificar_id", "");
            var vehiculo = await _vehiculoService.ObtenerVehiculo(vehiculoId);

            if (vehiculo == null)
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "❌ Error al obtener el vehículo.");
                return;
            }

            var colorAnterior = vehiculo.Color;
            vehiculo.Color = nuevoColor;

            await _vehiculoService.ActualizarVehiculo(vehiculo);

            _logger.LogInformation("🎨 Color de vehículo {Patente} actualizado: {Anterior} → {Nuevo}",
                vehiculo.Patente, colorAnterior, nuevoColor);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ *Color actualizado correctamente*\n\n" +
                $"Vehículo: {vehiculo.Patente}\n" +
                $"Color anterior: {colorAnterior}\n" +
                $"Color nuevo: {nuevoColor}\n\n" +
                $"Volviendo al menú de vehículos...");

            await Task.Delay(1000);
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modificando color de vehículo");
            await _whatsAppService.SendTextMessage(phoneNumber,
                "❌ Ocurrió un error. Intenta nuevamente.");
        }
    }
}
