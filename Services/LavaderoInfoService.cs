using Firebase.Models;

namespace Firebase.Services;

/// <summary>
/// Servicio para obtener información del lavadero desde la configuración
/// </summary>
public class LavaderoInfoService
{
    private readonly ConfiguracionService _configuracionService;
    private readonly TipoServicioService _tipoServicioService;
    private readonly ILogger<LavaderoInfoService> _logger;

    public LavaderoInfoService(
        ConfiguracionService configuracionService,
        TipoServicioService tipoServicioService,
        ILogger<LavaderoInfoService> logger)
    {
        _configuracionService = configuracionService;
        _tipoServicioService = tipoServicioService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el mensaje "Sobre nosotros" con información del lavadero
    /// </summary>
    public async Task<string> ObtenerMensajeSobreNosotros()
    {
        try
        {
            var config = await _configuracionService.ObtenerConfiguracion();
            if (config == null)
            {
                return "⚠️ No se pudo cargar la información del lavadero.";
            }

            var tiposServicio = await _tipoServicioService.ObtenerTiposServicio();

            var mensaje = $"ℹ️ *Sobre Nosotros*\n\n";
            mensaje += $"🏢 *{config.NombreLavadero}*\n\n";

            // Ubicación
            if (!string.IsNullOrWhiteSpace(config.Ubicacion))
            {
                mensaje += $"📍 *Ubicación:*\n{config.Ubicacion}\n\n";
            }

            // Horarios
            mensaje += "🕐 *Horarios de atención:*\n";
            foreach (var horario in config.HorariosOperacion.OrderBy(h => GetDiaOrden(h.Key)))
            {
                mensaje += $"• {horario.Key}: {horario.Value}\n";
            }
            mensaje += "\n";

            // Servicios disponibles
            if (tiposServicio != null && tiposServicio.Any())
            {
                mensaje += "🚗 *Servicios disponibles:*\n";
                foreach (var servicio in tiposServicio)
                {
                    mensaje += $"• {servicio}\n";
                }
                mensaje += "\n";
            }

            mensaje += "📞 *Contacto:*\n" +
                      "Para consultas o reservas, contáctanos a través de este WhatsApp o visítanos en nuestra ubicación.\n\n" +
                      "¡Esperamos poder atenderte pronto! 😊";

            return mensaje;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo información del lavadero");
            return "❌ Ocurrió un error al cargar la información.";
        }
    }

    /// <summary>
    /// Obtiene el nombre del lavadero desde la configuración
    /// </summary>
    public async Task<string> ObtenerNombreLavadero()
    {
        try
        {
            var config = await _configuracionService.ObtenerConfiguracion();
            return config?.NombreLavadero ?? "Lavadero AutoClean";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo nombre del lavadero");
            return "Lavadero AutoClean";
        }
    }

    /// <summary>
    /// Obtiene el orden del día para ordenar correctamente
    /// </summary>
    private int GetDiaOrden(string dia)
    {
        return dia.ToLower() switch
        {
            "lunes" => 1,
            "martes" => 2,
            "miércoles" or "miercoles" => 3,
            "jueves" => 4,
            "viernes" => 5,
            "sábado" or "sabado" => 6,
            "domingo" => 7,
            _ => 8
        };
    }
}
