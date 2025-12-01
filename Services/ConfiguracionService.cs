using Firebase.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Servicio para la gestión de la configuración del sistema en Firestore.
/// Proporciona operaciones para obtener y actualizar la configuración global del sistema.
/// Incluye caché en memoria para mejorar el rendimiento.
/// </summary>
public class ConfiguracionService
{
    #region Constantes
    private const string COLLECTION_NAME = "configuracion";
    private const string CONFIG_ID = "system_config";
    private const string CACHE_KEY = "system_configuration";
    private const int CACHE_DURATION_MINUTES = 30; // Caché por 30 minutos
    #endregion

    #region Dependencias
    private readonly FirestoreDb _firestore;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Inicializa una nueva instancia del servicio de configuración.
    /// </summary>
    /// <param name="firestore">Instancia de la base de datos Firestore.</param>
    /// <param name="cache">Instancia de caché en memoria.</param>
    public ConfiguracionService(FirestoreDb firestore, IMemoryCache cache)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
    #endregion

    #region Operaciones de Consulta

    /// <summary>
    /// Obtiene la configuración del sistema. Si no existe, crea una con valores por defecto.
    /// Utiliza caché para mejorar el rendimiento.
    /// </summary>
    /// <returns>La configuración del sistema.</returns>
    public async Task<Configuracion> ObtenerConfiguracion()
    {
        // Intentar obtener desde caché
        if (_cache.TryGetValue(CACHE_KEY, out Configuracion? cachedConfig) && cachedConfig != null)
        {
            return cachedConfig;
        }

        // Si no está en caché, obtener de Firestore
        var docRef = _firestore.Collection(COLLECTION_NAME).Document(CONFIG_ID);
        var snapshot = await docRef.GetSnapshotAsync();

        Configuracion config;
        if (snapshot.Exists)
        {
            config = snapshot.ConvertTo<Configuracion>();
        }
        else
        {
            // Si no existe configuración, crear una por defecto
            config = CrearConfiguracionPorDefecto();
            await CrearConfiguracion(config);
        }

        // Guardar en caché
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
        _cache.Set(CACHE_KEY, config, cacheOptions);

        return config;
    }

    /// <summary>
    /// Obtiene un valor específico de configuración de forma eficiente.
    /// </summary>
    /// <param name="propertyName">Nombre de la propiedad a obtener.</param>
    /// <returns>El valor de la configuración solicitada.</returns>
    public async Task<object?> ObtenerValorConfiguracion(string propertyName)
    {
        var config = await ObtenerConfiguracion();
        var property = typeof(Configuracion).GetProperty(propertyName);
        return property?.GetValue(config);
    }

    /// <summary>
    /// Obtiene el paso de descuento para paquetes de servicios.
    /// </summary>
    /// <returns>El paso de descuento.</returns>
    public async Task<int> ObtenerPaquetesDescuentoStep()
    {
        var config = await ObtenerConfiguracion();
        return config.PaquetesDescuentoStep;
    }

    /// <summary>
    /// Obtiene los horarios de operación del lavadero.
    /// </summary>
    /// <returns>Diccionario con los horarios de operación.</returns>
    public async Task<Dictionary<string, string>> ObtenerHorariosOperacion()
    {
        var config = await ObtenerConfiguracion();
        return config.HorariosOperacion;
    }

    /// <summary>
    /// Obtiene la capacidad máxima concurrente del lavadero.
    /// </summary>
    /// <returns>La capacidad máxima.</returns>
    public async Task<int> ObtenerCapacidadMaxima()
    {
        var config = await ObtenerConfiguracion();
        return config.CapacidadMaximaConcurrente;
    }

    /// <summary>
    /// Obtiene la configuración de cancelación anticipada.
    /// </summary>
    /// <returns>Tupla con descuento, horas mínimas y validez en días.</returns>
    public async Task<(decimal descuento, int horasMinimas, int validezDias)> ObtenerConfiguracionCancelacionAnticipada()
    {
        var config = await ObtenerConfiguracion();
        return (config.CancelacionAnticipadaDescuento, 
                config.CancelacionAnticipadaHorasMinimas, 
                config.CancelacionAnticipadaValidezDias);
    }

    /// <summary>
    /// Obtiene la duración máxima de la sesión en minutos.
    /// Convierte las horas configuradas a minutos.
    /// </summary>
    /// <returns>Duración en minutos.</returns>
    public async Task<int> ObtenerSesionDuracionMinutos()
    {
        var config = await ObtenerConfiguracion();
        return config.SesionDuracionHoras * 60; // Convertir horas a minutos
    }

    /// <summary>
    /// Obtiene el tiempo de inactividad antes del cierre automático en minutos.
    /// </summary>
    /// <returns>Tiempo de inactividad en minutos.</returns>
    public async Task<int> ObtenerSesionInactividadMinutos()
    {
        var config = await ObtenerConfiguracion();
        return config.SesionInactividadMinutos;
    }

    /// <summary>
    /// Obtiene el número máximo de empleados que se pueden asignar a un lavado.
    /// </summary>
    /// <returns>Número máximo de empleados por lavado.</returns>
    public async Task<int> ObtenerEmpleadosMaximosPorLavado()
    {
        var config = await ObtenerConfiguracion();
        return config.EmpleadosMaximosPorLavado;
    }
    #endregion

    #region Operaciones de Escritura

    /// <summary>
    /// Crea la configuración inicial del sistema con valores por defecto.
    /// </summary>
    /// <param name="config">Configuración a crear.</param>
    private async Task CrearConfiguracion(Configuracion config)
    {
        config.Id = CONFIG_ID;
        config.FechaActualizacion = DateTime.UtcNow;

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(CONFIG_ID);
        await docRef.SetAsync(config);

        // Invalidar caché
        _cache.Remove(CACHE_KEY);
    }

    /// <summary>
    /// Actualiza la configuración del sistema.
    /// </summary>
    /// <param name="config">Configuración actualizada.</param>
    /// <param name="userEmail">Email del usuario que realiza la actualización.</param>
    public async Task ActualizarConfiguracion(Configuracion config, string userEmail)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        config.Id = CONFIG_ID;
        config.FechaActualizacion = DateTime.UtcNow;
        config.ActualizadoPor = userEmail;

        // Validar horarios de operación
        ValidarHorariosOperacion(config.HorariosOperacion);

        var docRef = _firestore.Collection(COLLECTION_NAME).Document(CONFIG_ID);
        await docRef.SetAsync(config, SetOptions.Overwrite);

        // Invalidar caché
        _cache.Remove(CACHE_KEY);
    }
    #endregion

    #region Métodos Privados de Validación

    /// <summary>
    /// Crea una configuración con valores por defecto.
    /// </summary>
    /// <returns>Configuración por defecto.</returns>
    private Configuracion CrearConfiguracionPorDefecto()
    {
        return new Configuracion
        {
            Id = CONFIG_ID,
            CancelacionAnticipadaDescuento = 10,
            CancelacionAnticipadaHorasMinimas = 24,
            CancelacionAnticipadaValidezDias = 30,
            PaquetesDescuentoStep = 5,
            HorariosOperacion = new Dictionary<string, string>
            {
                { "Lunes", "09:00-18:00" },
                { "Martes", "09:00-18:00" },
                { "Miércoles", "09:00-18:00" },
                { "Jueves", "09:00-18:00" },
                { "Viernes", "09:00-18:00" },
                { "Sábado", "09:00-13:00" },
                { "Domingo", "CERRADO" }
            },
            CapacidadMaximaConcurrente = 5,
            ConsiderarEmpleadosActivos = true,
            EmpleadosMaximosPorLavado = 3,
            SesionDuracionHoras = 8, // 8 horas
            SesionInactividadMinutos = 15, // 15 minutos
            FechaActualizacion = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Valida que los horarios de operación tengan el formato correcto.
    /// Formato válido: "HH:MM-HH:MM" o "HH:MM-HH:MM,HH:MM-HH:MM" o "CERRADO".
    /// </summary>
    /// <param name="horarios">Horarios a validar.</param>
    private void ValidarHorariosOperacion(Dictionary<string, string> horarios)
    {
        if (horarios == null || horarios.Count == 0)
            throw new ArgumentException("Los horarios de operación no pueden estar vacíos");

        var diasSemana = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        
        foreach (var dia in diasSemana)
        {
            if (!horarios.ContainsKey(dia))
                throw new ArgumentException($"Falta configurar el horario para {dia}");

            var horario = horarios[dia];
            if (string.IsNullOrWhiteSpace(horario))
                throw new ArgumentException($"El horario para {dia} no puede estar vacío");

            // Si no es CERRADO, validar formato de horario
            if (horario.ToUpper() != "CERRADO")
            {
                var segmentos = horario.Split(',');
                foreach (var segmento in segmentos)
                {
                    if (!ValidarFormatoHorario(segmento.Trim()))
                        throw new ArgumentException($"Formato de horario inválido para {dia}: {segmento}");
                }
            }
        }
    }

    /// <summary>
    /// Valida que un segmento de horario tenga el formato "HH:MM-HH:MM".
    /// </summary>
    /// <param name="segmento">Segmento a validar.</param>
    /// <returns>True si es válido.</returns>
    private bool ValidarFormatoHorario(string segmento)
    {
        if (string.IsNullOrWhiteSpace(segmento))
            return false;

        var partes = segmento.Split('-');
        if (partes.Length != 2)
            return false;

        return ValidarHora(partes[0].Trim()) && ValidarHora(partes[1].Trim());
    }

    /// <summary>
    /// Valida que una hora tenga el formato "HH:MM" y sea válida.
    /// </summary>
    /// <param name="hora">Hora a validar.</param>
    /// <returns>True si es válida.</returns>
    private bool ValidarHora(string hora)
    {
        if (string.IsNullOrWhiteSpace(hora))
            return false;

        var partes = hora.Split(':');
        if (partes.Length != 2)
            return false;

        if (!int.TryParse(partes[0], out int horas) || horas < 0 || horas > 23)
            return false;

        if (!int.TryParse(partes[1], out int minutos) || minutos < 0 || minutos > 59)
            return false;

        return true;
    }
    #endregion
}
