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
            _logger.LogError("Vehicle operation failed: {ExceptionType}", ex.GetType().Name);
            throw;
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
            _logger.LogError("Vehicle selection failed: {ExceptionType}", ex.GetType().Name);
            throw;
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

        // Contar cuántos dueños tiene el vehículo (evitar duplicados)
        var todosLosDuenos = new HashSet<string>();
        
        // Agregar el dueño principal si existe
        if (!string.IsNullOrEmpty(vehiculo.ClienteId))
        {
            todosLosDuenos.Add(vehiculo.ClienteId);
        }
        
        // Agregar los dueños de la lista ClientesIds
        if (vehiculo.ClientesIds != null)
        {
            foreach (var clienteId in vehiculo.ClientesIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                todosLosDuenos.Add(clienteId);
            }
        }
        
        var cantidadDuenos = todosLosDuenos.Count;

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
            options.Add(("generar_clave", "🔑 Nueva clave", "Reemplazar una clave perdida"));
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
            // Mostrar confirmación antes de regenerar la clave
            await MostrarConfirmacionRegenerarClave(phoneNumber, session);
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

            // Contar cuántos dueños tiene (evitar duplicados)
            var todosLosDuenos = new HashSet<string>();
            
            if (!string.IsNullOrEmpty(vehiculo.ClienteId))
            {
                todosLosDuenos.Add(vehiculo.ClienteId);
            }
            
            if (vehiculo.ClientesIds != null)
            {
                foreach (var clienteId in vehiculo.ClientesIds.Where(id => !string.IsNullOrEmpty(id)))
                {
                    todosLosDuenos.Add(clienteId);
                }
            }
            
            var cantidadDuenos = todosLosDuenos.Count;

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
            _logger.LogError("Vehicle unlink validation failed: {ExceptionType}", ex.GetType().Name);
            throw;
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
            _logger.LogError("Vehicle deletion validation failed: {ExceptionType}", ex.GetType().Name);
            throw;
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

            _logger.LogInformation("Vehicle unlinked");

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"✅ *Vehículo desvinculado correctamente*\n\n" +
                $"El vehículo {patente} ha sido desvinculado de tu cuenta.\n\n" +
                $"Volviendo al menú de vehículos...");

            await Task.Delay(1000);
            await MostrarMenuVehiculos(phoneNumber, session);
        }
        catch (Exception ex)
        {
            _logger.LogError("Vehicle unlink failed: {ExceptionType}", ex.GetType().Name);
            throw;
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

            _logger.LogInformation("Vehicle model updated");

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
            _logger.LogError("Vehicle model update failed: {ExceptionType}", ex.GetType().Name);
            throw;
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

            _logger.LogInformation("Vehicle color updated");

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
            _logger.LogError("Vehicle color update failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Muestra la confirmación antes de regenerar la clave de asociación.
    /// Solo avanza a confirmación si el cliente es el dueño principal.
    /// </summary>
    private async Task MostrarConfirmacionRegenerarClave(string phoneNumber, WhatsAppSession session)
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

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE);

            await _whatsAppService.SendTextMessage(phoneNumber,
                $"🔑 *Generar nueva clave de asociación*\n\n" +
                $"Vehículo: *{vehiculo.Patente}*\n\n" +
                $"⚠️ *Ten en cuenta que:*\n" +
                $"• La clave anterior dejará de funcionar\n" +
                $"• Las personas ya asociadas al vehículo conservan su acceso\n" +
                $"• La clave original no se puede recuperar\n\n" +
                $"¿Deseás continuar?\n\n" +
                $"Responde *SÍ* para generar la nueva clave o *NO* para cancelar.");
        }
        catch (Exception ex)
        {
            _logger.LogError("Vehicle key confirmation failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Maneja la confirmación del usuario para regenerar la clave de asociación.
    /// Revalida que el cliente sea el dueño principal antes de rotar el hash.
    /// </summary>
    private async Task HandleConfirmarRegenerarClave(string phoneNumber, WhatsAppSession session, string input)
    {
        var respuesta = input.Trim().ToUpperInvariant();

        if (respuesta == "SI" || respuesta == "SÍ" || respuesta == "S")
        {
            // Revalidar antes de rotar
            await RegenerarClaveAsociacion(phoneNumber, session);
        }
        else if (respuesta == "NO" || respuesta == "N")
        {
            await _whatsAppService.SendTextMessage(phoneNumber,
                "✅ Generación de clave cancelada.\n\n" +
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
}
