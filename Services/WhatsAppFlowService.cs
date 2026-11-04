using Firebase.Models;
using Firebase.Models.WhatsApp;
using System.Text.RegularExpressions;

namespace Firebase.Services;

/// <summary>
/// Servicio que maneja la lógica de flujos conversacionales de WhatsApp
/// </summary>
public partial class WhatsAppFlowService
{
    private readonly WhatsAppSessionService _sessionService;
    private readonly MetaWhatsAppService _whatsAppService;
    private readonly ClienteService _clienteService;
    private readonly VehiculoService _vehiculoService;
    private readonly TipoDocumentoService _tipoDocumentoService;
    private readonly TipoVehiculoService _tipoVehiculoService;
    private readonly LavaderoInfoService _lavaderoInfoService;
    private readonly ICarQueryService _carQueryService;
    private readonly ILogger<WhatsAppFlowService> _logger;

    public WhatsAppFlowService(
        WhatsAppSessionService sessionService,
        MetaWhatsAppService whatsAppService,
        ClienteService clienteService,
        VehiculoService vehiculoService,
        TipoDocumentoService tipoDocumentoService,
        TipoVehiculoService tipoVehiculoService,
        LavaderoInfoService lavaderoInfoService,
        ICarQueryService carQueryService,
        ILogger<WhatsAppFlowService> logger)
    {
        _sessionService = sessionService;
        _whatsAppService = whatsAppService;
        _clienteService = clienteService;
        _vehiculoService = vehiculoService;
        _tipoDocumentoService = tipoDocumentoService;
        _tipoVehiculoService = tipoVehiculoService;
        _lavaderoInfoService = lavaderoInfoService;
        _carQueryService = carQueryService;
        _logger = logger;
    }

    /// <summary>
    /// Procesa un mensaje entrante y devuelve la respuesta
    /// </summary>
    public async Task ProcessMessage(string phoneNumber, string messageBody)
    {
        try
        {
            _logger.LogInformation("🔄 Procesando mensaje entrante (longitud: {MessageLength})", messageBody.Length);

            // Obtener o crear sesión
            var session = await _sessionService.GetOrCreateSession(phoneNumber);

            _logger.LogInformation("WhatsApp session loaded");

            // Si es el PRIMER mensaje (estado INICIO), verificar si el cliente ya existe
            if (session.CurrentState == WhatsAppFlowStates.INICIO)
            {
                await HandleInitialContact(phoneNumber, session);
                return;
            }

            // COMANDO ESPECIAL: Si el usuario escribe "REINICIAR" o "INICIO", reiniciar la sesión
            if (messageBody.Trim().ToUpperInvariant() == "REINICIAR" || 
                messageBody.Trim().ToUpperInvariant() == "INICIO" ||
                messageBody.Trim().ToUpperInvariant() == "MENU")
            {
                _logger.LogInformation("🔄 Usuario solicitó reinicio de sesión");
                
                // Reiniciar sesión
                await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.INICIO);
                
                // Procesar como primer contacto
                var freshSession = await _sessionService.GetOrCreateSession(phoneNumber);
                await HandleInitialContact(phoneNumber, freshSession);
                return;
            }

            // Procesar según el estado actual
            await ProcessByState(phoneNumber, session, messageBody);
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("WhatsApp flow failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Maneja el primer contacto del usuario
    /// </summary>
    private async Task HandleInitialContact(string phoneNumber, WhatsAppSession session)
    {
        try
        {
            _logger.LogInformation("WhatsApp initial contact");
            
            // Buscar si ya existe un cliente con este teléfono
            var clienteExistente = await BuscarClientePorTelefono(phoneNumber);

            if (clienteExistente != null)
            {
                // ✅ CLIENTE REGISTRADO → Iniciar sesión automáticamente
                _logger.LogInformation("Existing client found");

                await _sessionService.AssociateClienteToSession(phoneNumber, clienteExistente.Id);

                var nombreLavadero = await _lavaderoInfoService.ObtenerNombreLavadero();
                
                await _whatsAppService.SendTextMessage(phoneNumber,
                    $"¡Hola {clienteExistente.Nombre}! 👋\n\n" +
                    $"Bienvenido de vuelta a {nombreLavadero} 🚗✨");

                await Task.Delay(500); // Pequeña pausa para mejor UX

                await ShowClienteMenu(phoneNumber, clienteExistente.Nombre);
            }
            else
            {
                // ❌ CLIENTE NO REGISTRADO → Iniciar proceso de registro
                _logger.LogInformation("📝 Cliente nuevo detectado, iniciando registro");

                var nombreLavadero = await _lavaderoInfoService.ObtenerNombreLavadero();

                await _whatsAppService.SendTextMessage(phoneNumber,
                    $"¡Hola! 👋 Bienvenido a {nombreLavadero} 🚗✨\n\n" +
                    "Veo que es tu primer contacto con nosotros.\n\n" +
                    "Para brindarte un mejor servicio, necesito registrarte. " +
                    "El proceso es rápido y sencillo. ¿Empezamos? 😊");

                await Task.Delay(800);

                // Iniciar proceso de registro
                await IniciarRegistroCliente(phoneNumber);
            }
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("Initial contact failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Busca un cliente por número de teléfono
    /// </summary>
    private async Task<Cliente?> BuscarClientePorTelefono(string phoneNumber)
    {
        try
        {
            // phoneNumber viene de WhatsApp con código de país (ej: 5493751590586)
            // En la DB está sin código de país ni 9 (ej: 3751590586)
            
            var telefonoNormalizado = PhoneNumberHelper.NormalizePhoneNumber(phoneNumber);
            var telefonoSinCodigo = PhoneNumberHelper.RemoveCountryCode(telefonoNormalizado);
            
            _logger.LogInformation("Looking up WhatsApp client");

            // Obtener todos los clientes activos
            var clientes = await _clienteService.ObtenerClientes("", 1, 1000, "Nombre", "asc", new List<string> { "Activo" });

            // Buscar por teléfono usando el comparador mejorado
            var cliente = clientes.FirstOrDefault(c =>
            {
                var esIgual = PhoneNumberHelper.AreEqual(c.Telefono, phoneNumber);
                
                if (esIgual)
                {
                    _logger.LogInformation("WhatsApp client found");
                }
                
                return esIgual;
            });

            if (cliente == null)
            {
                _logger.LogInformation("WhatsApp client not found");
            }

            return cliente;
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("Client lookup failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Muestra el menú principal para clientes autenticados
    /// </summary>
    private async Task ShowClienteMenu(string phoneNumber, string nombreCliente)
    {
        // Como tenemos 4 opciones, usamos lista desplegable (WhatsApp permite máximo 3 botones)
        var options = new List<(string id, string title, string description)>
        {
            ("vehiculos", "🚗 Gestionar vehículos", "Agregar o modificar vehículos"),
            ("datos", "👤 Mis datos", "Ver o editar mi información"),
            ("sobre_nosotros", "ℹ️ Sobre nosotros", "Información del lavadero"),
            ("ayuda", "❓ Ayuda", "Comandos y contacto")
        };

        await _whatsAppService.SendListMessage(phoneNumber,
            $"¡Hola {nombreCliente}! 👋\n\n¿Qué deseas hacer hoy?",
            "📋 Ver menú",
            "Opciones disponibles",
            options);
    }

    /// <summary>
    /// Inicia el proceso de registro de cliente
    /// </summary>
    private async Task IniciarRegistroCliente(string phoneNumber)
    {
        try
        {
            // Obtener tipos de documento disponibles
            var tiposDocumento = await _tipoDocumentoService.ObtenerTiposDocumento();

            if (tiposDocumento == null || !tiposDocumento.Any())
            {
                await _whatsAppService.SendTextMessage(phoneNumber,
                    "⚠️ Lo siento, hubo un problema al cargar los tipos de documento. " +
                    "Por favor, contacta al lavadero directamente.");
                return;
            }

            await _sessionService.UpdateSessionState(phoneNumber, WhatsAppFlowStates.REGISTRO_TIPO_DOCUMENTO);

            // Crear lista con tipos de documento
            var options = tiposDocumento.Select(tipo => (
                tipo,
                tipo,
                "Documento de identidad"
            )).ToList();

            if (options.Count <= 3)
            {
                // Usar botones si son 3 o menos
                var buttons = options.Select(o => (o.Item1, o.Item2)).ToList();
                await _whatsAppService.SendButtonMessage(phoneNumber,
                    "📄 Primero, ¿qué tipo de documento tienes?",
                    buttons);
            }
            else
            {
                // Usar lista si son más de 3
                await _whatsAppService.SendListMessage(phoneNumber,
                    "📄 Primero, ¿qué tipo de documento tienes?",
                    "Ver opciones",
                    "Tipos de documento",
                    options);
            }
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("Client registration failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Procesa el mensaje según el estado actual de la sesión
    /// </summary>
    private async Task ProcessByState(string phoneNumber, WhatsAppSession session, string messageBody)
    {
        var state = session.CurrentState;

        try
        {
            switch (state)
            {
                // ========== FLUJO DE REGISTRO DE CLIENTE ==========
                case WhatsAppFlowStates.REGISTRO_TIPO_DOCUMENTO:
                    await HandleRegistroTipoDocumento(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.REGISTRO_NUM_DOCUMENTO:
                    await HandleRegistroNumDocumento(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.REGISTRO_NOMBRE:
                    await HandleRegistroNombre(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.REGISTRO_APELLIDO:
                    await HandleRegistroApellido(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.REGISTRO_EMAIL:
                    await HandleRegistroEmail(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.REGISTRO_CONFIRMACION:
                    await HandleRegistroConfirmacion(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.REGISTRO_VEHICULO_OPCION:
                    await HandleRegistroVehiculoOpcion(phoneNumber, session, messageBody);
                    break;

                // ========== MENÚ CLIENTE AUTENTICADO ==========
                case WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO:
                    await HandleMenuClienteAutenticado(phoneNumber, session, messageBody);
                    break;

                // ========== FLUJO DE AGREGAR VEHÍCULO ==========
                case WhatsAppFlowStates.VEHICULO_TIPO:
                    await HandleVehiculoTipo(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.VEHICULO_PATENTE:
                    await HandleVehiculoPatente(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.VEHICULO_MARCA:
                    await HandleVehiculoMarca(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.VEHICULO_MODELO:
                    await HandleVehiculoModelo(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.VEHICULO_MARCA_MANUAL:
                case WhatsAppFlowStates.VEHICULO_MODELO_MANUAL:
                    await HandleVehiculoManualConfirmation(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.VEHICULO_COLOR:
                    await HandleVehiculoColor(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.VEHICULO_CONFIRMACION:
                    await HandleVehiculoConfirmacion(phoneNumber, session, messageBody);
                    break;

                // ========== CONSULTAS Y GESTIÓN ==========
                case WhatsAppFlowStates.MOSTRAR_DATOS:
                    await HandleMostrarDatos(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.MENU_VEHICULOS:
                    await HandleMenuVehiculos(phoneNumber, session, messageBody);
                    break;

                // ========== EDICIÓN DE DATOS DEL CLIENTE ==========
                case WhatsAppFlowStates.EDITAR_DATOS_MENU:
                    await HandleMenuEdicionDatos(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.EDITAR_NOMBRE:
                    await HandleEditarNombre(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.EDITAR_APELLIDO:
                    await HandleEditarApellido(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.EDITAR_EMAIL:
                    await HandleEditarEmail(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.CONFIRMAR_EDICION:
                    await HandleConfirmarEdicion(phoneNumber, session, messageBody);
                    break;

                // ========== GESTIÓN DE VEHÍCULOS ==========
                case WhatsAppFlowStates.SELECCIONAR_VEHICULO_MODIFICAR:
                    await HandleSeleccionVehiculoModificar(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU:
                    await HandleMenuModificarVehiculo(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.MODIFICAR_VEHICULO_MODELO:
                    await HandleModificarVehiculoModelo(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.MODIFICAR_VEHICULO_COLOR:
                    await HandleModificarVehiculoColor(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.CONFIRMAR_ELIMINAR_VEHICULO:
                    await HandleConfirmarEliminarVehiculo(phoneNumber, session, messageBody);
                    break;

                // ========== ASOCIACIÓN DE VEHÍCULOS (MÚLTIPLES DUEÑOS) ==========
                case WhatsAppFlowStates.ASOCIAR_VEHICULO_PATENTE:
                    await HandleAsociarVehiculoPatente(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.ASOCIAR_VEHICULO_CLAVE:
                    await HandleAsociarVehiculoClave(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION:
                    await HandleAsociarVehiculoConfirmacion(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.MOSTRAR_CLAVE_VEHICULO:
                    await HandleMostrarClaveVehiculo(phoneNumber, session, messageBody);
                    break;

                case WhatsAppFlowStates.CONFIRMAR_REGENERAR_CLAVE:
                    await HandleConfirmarRegenerarClave(phoneNumber, session, messageBody);
                    break;

                default:
                    _logger.LogWarning("Unknown WhatsApp session state");
                    await _whatsAppService.SendTextMessage(phoneNumber,
                        "⚠️ Parece que algo salió mal.\n\n" +
                        "Escribe *REINICIAR* para volver al inicio.");
                    break;
            }
        }
        catch (Exception ex) when (ex is not WhatsAppOperationException)
        {
            _logger.LogError("WhatsApp state processing failed: {ExceptionType}", ex.GetType().Name);
            throw;
        }
    }

    // ========================================================================
    // MÉTODOS AUXILIARES DE VALIDACIÓN
    // ========================================================================

    /// <summary>
    /// Valida que un texto solo contenga letras y espacios, con mínimo 3 caracteres
    /// Coincide con la validación del modelo: ^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$
    /// </summary>
    private bool EsTextoValido(string texto)
    {
        return Regex.IsMatch(texto, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$");
    }

    /// <summary>
    /// Valida formato de email según el modelo:
    /// ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]{3,}\.[a-zA-Z]{2,}$
    /// </summary>
    private bool EsEmailValido(string email)
    {
        // Usar solo regex para validar, coincide con la validación del modelo
        return Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]{3,}\.[a-zA-Z]{2,}$");
    }

    /// <summary>
    /// Valida que un texto solo contenga números
    /// </summary>
    private bool EsNumeroValido(string texto)
    {
        return Regex.IsMatch(texto, @"^\d+$");
    }

    /// <summary>
    /// Valida formato de patente según el modelo.
    /// Debe contener letras y/o números, puede tener espacios y guiones.
    /// Mínimo 5 caracteres (ej: "ABC12" o "AB 123 CD")
    /// </summary>
    private bool EsPatenteValida(string patente)
    {
        // Debe cumplir el formato básico
        if (!Regex.IsMatch(patente, @"^[a-zA-Z0-9\s-]+$"))
            return false;
        
        // Remover espacios y guiones para contar caracteres alfanuméricos
        var soloAlfanumerico = Regex.Replace(patente, @"[\s-]", "");
        
        // Mínimo 5 caracteres alfanuméricos (patentes argentinas: viejas=6, nuevas=7)
        if (soloAlfanumerico.Length < 5)
            return false;
        
        // Debe contener al menos una letra Y al menos un número
        return Regex.IsMatch(soloAlfanumerico, @"[a-zA-Z]") && 
               Regex.IsMatch(soloAlfanumerico, @"\d");
    }
}
