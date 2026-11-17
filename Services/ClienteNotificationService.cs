using System.Net;
using System.Text;
using Firebase.Models;

namespace Firebase.Services;

public sealed record NotificationDispatchResult(
    int Accepted,
    int Failed,
    int Indeterminate,
    int InvalidContact,
    int AuditFailures)
{
    public bool HasAttempts => Accepted + Failed + Indeterminate + InvalidContact > 0;

    public string ToEmployeeMessage()
    {
        if (!HasAttempts)
            return "No se encontraron destinatarios con preferencias de notificación.";

        var message = $"Notificaciones: {Accepted} aceptadas por el proveedor, {Failed} fallidas, " +
                      $"{Indeterminate} indeterminadas y {InvalidContact} con datos de contacto inválidos.";
        return AuditFailures == 0
            ? message
            : $"{message} No se pudo registrar {AuditFailures} intento(s) en auditoría.";
    }
}

public sealed class ClienteNotificationService
{
    private readonly ClienteService _clientes;
    private readonly Firebase.Services.LavadoService _lavados;
    private readonly ConfiguracionService _configuracion;
    private readonly IEmailSender _email;
    private readonly MetaWhatsAppService _whatsapp;
    private readonly IAuditWriter _audit;
    private readonly ILogger<ClienteNotificationService> _logger;

    public ClienteNotificationService(
        ClienteService clientes,
        Firebase.Services.LavadoService lavados,
        ConfiguracionService configuracion,
        IEmailSender email,
        MetaWhatsAppService whatsapp,
        IAuditWriter audit,
        ILogger<ClienteNotificationService> logger)
    {
        _clientes = clientes;
        _lavados = lavados;
        _configuracion = configuracion;
        _email = email;
        _whatsapp = whatsapp;
        _audit = audit;
        _logger = logger;
    }

    public async Task<NotificationDispatchResult> NotifyAsync(
        string lavadoId,
        string eventKind,
        string? servicioId = null,
        string? etapaId = null,
        string? motivo = null,
        string? actorId = null,
        string? actorEmail = null)
    {
        var after = await _lavados.ObtenerLavado(lavadoId);
        if (after == null)
            return new(0, 1, 0, 0, 0);

        var recipients = new[] { after.ClienteTrajoId, after.ClienteRetiraId, after.ClienteId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var result = new NotificationDispatchResult(0, 0, 0, 0, 0);
        var config = await _configuracion.ObtenerConfiguracion();
        var message = BuildMessage(after, null, eventKind, servicioId, etapaId, motivo, config.NombreLavadero);
        var subject = $"Actualización del lavado {after.VehiculoPatente ?? after.Id}";

        foreach (var recipientId in recipients)
        {
            var cliente = await _clientes.ObtenerCliente(recipientId!);
            if (cliente == null) continue;

            cliente.NormalizarPreferenciasNotificacion();
            if (cliente.PrefiereEmail)
                result = await SendEmail(result, cliente, subject, message, lavadoId, eventKind, actorId, actorEmail);
            if (cliente.PrefiereWhatsApp)
                result = await SendWhatsApp(result, cliente, message, lavadoId, eventKind, actorId, actorEmail);
        }

        return result;
    }

    private async Task<NotificationDispatchResult> SendEmail(
        NotificationDispatchResult current,
        Cliente cliente,
        string subject,
        string text,
        string lavadoId,
        string eventKind,
        string? actorId,
        string? actorEmail)
    {
        if (string.IsNullOrWhiteSpace(cliente.Email))
            return await Record(current, lavadoId, eventKind, cliente, "Email", "contacto_invalido", actorId, actorEmail);

        try
        {
            var response = await _email.EnviarAsync(new EmailMessage(
                cliente.Email,
                subject,
                $"<p>{WebUtility.HtmlEncode(text).Replace("\n", "<br>")}</p>"));
            return await Record(current, lavadoId, eventKind, cliente, "Email",
                response.Enviado ? "aceptado_por_proveedor" : "fallido", actorId, actorEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el envío de email del lavado {LavadoId}", lavadoId);
            return await Record(current, lavadoId, eventKind, cliente, "Email", "fallido", actorId, actorEmail);
        }
    }

    private async Task<NotificationDispatchResult> SendWhatsApp(
        NotificationDispatchResult current,
        Cliente cliente,
        string text,
        string lavadoId,
        string eventKind,
        string? actorId,
        string? actorEmail)
    {
        if (!PhoneNumberHelper.IsValidPhoneNumber(cliente.Telefono))
            return await Record(current, lavadoId, eventKind, cliente, "WhatsApp", "contacto_invalido", actorId, actorEmail);

        try
        {
            await _whatsapp.SendTextMessage(PhoneNumberHelper.ToWhatsAppFormat(cliente.Telefono), text);
            return await Record(current, lavadoId, eventKind, cliente, "WhatsApp",
                "aceptado_por_proveedor", actorId, actorEmail);
        }
        catch (WhatsAppOperationException ex) when (ex.Classification == "uncertain transport")
        {
            _logger.LogWarning("Resultado incierto al notificar por WhatsApp el lavado {LavadoId}", lavadoId);
            return await Record(current, lavadoId, eventKind, cliente, "WhatsApp", "indeterminado", actorId, actorEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el envío de WhatsApp del lavado {LavadoId}", lavadoId);
            return await Record(current, lavadoId, eventKind, cliente, "WhatsApp", "fallido", actorId, actorEmail);
        }
    }

    private async Task<NotificationDispatchResult> Record(
        NotificationDispatchResult current,
        string lavadoId,
        string eventKind,
        Cliente cliente,
        string channel,
        string outcome,
        string? actorId,
        string? actorEmail)
    {
        try
        {
            await _audit.LogEvent(
                actorId ?? "sistema",
                actorEmail ?? string.Empty,
                $"Intento de notificación {channel}: {outcome}",
                lavadoId,
                "Notificacion",
                new(),
                new Dictionary<string, string>
                {
                    ["LavadoId"] = lavadoId,
                    ["Evento"] = eventKind,
                    ["ClienteId"] = cliente.Id,
                    ["Canal"] = channel,
                    ["Resultado"] = outcome,
                    ["FechaHora"] = DateTime.UtcNow.ToString("O")
                },
                cliente.NombreCompleto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo auditar el intento de notificación del lavado {LavadoId}", lavadoId);
            current = current with { AuditFailures = current.AuditFailures + 1 };
        }

        return outcome switch
        {
            "aceptado_por_proveedor" => current with { Accepted = current.Accepted + 1 },
            "contacto_invalido" => current with { InvalidContact = current.InvalidContact + 1 },
            "indeterminado" => current with { Indeterminate = current.Indeterminate + 1 },
            _ => current with { Failed = current.Failed + 1 }
        };
    }

    public static string BuildMessage(
        Lavado lavado,
        Lavado? before,
        string eventKind,
        string? servicioId,
        string? etapaId,
        string? motivo,
        string? nombreLavadero = null)
    {
        var service = lavado.Servicios.FirstOrDefault(x => x.ServicioId == servicioId);
        var stage = service?.Etapas.FirstOrDefault(x => x.EtapaId == etapaId);
        var eventTime = eventKind.EndsWith("_etapa", StringComparison.Ordinal) ? stage?.TiempoFinalizacion :
            eventKind.EndsWith("_servicio", StringComparison.Ordinal) ? service?.TiempoFinalizacion : lavado.TiempoFinalizacion;
        var when = (eventTime?.ToLocalTime() ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm");
        var name = stage?.Nombre ?? service?.ServicioNombre;
        var builder = new StringBuilder();

        var business = string.IsNullOrWhiteSpace(nombreLavadero) ? "el lavadero" : nombreLavadero.Trim();
        var description = eventKind switch
        {
            "finalizacion_etapa" => $"la etapa \"{name ?? "seleccionada"}\" finalizó correctamente",
            "finalizacion_servicio" => $"el servicio \"{name ?? "seleccionado"}\" {(service?.Estado == "RealizadoParcialmente" ? "finalizó parcialmente" : "finalizó correctamente")}",
            "finalizacion_lavado" => lavado.Estado == "RealizadoParcialmente" ? "el lavado finalizó parcialmente" : "el lavado finalizó correctamente",
            "cancelacion_etapa" => $"la etapa \"{name ?? "seleccionada"}\" fue cancelada",
            "cancelacion_servicio" => $"el servicio \"{name ?? "seleccionado"}\" fue cancelado",
            "cancelacion_lavado" => "el lavado fue cancelado",
            _ => "el estado del lavado fue actualizado"
        };
        builder.AppendLine($"Desde {business} queremos avisarte que {description}.");
        builder.AppendLine($"Fecha y hora: {when}.");
        if (eventKind.StartsWith("cancelacion", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(motivo))
            builder.AppendLine($"Motivo: {motivo}.");
        if (eventKind == "finalizacion_etapa" &&
            (service?.Estado is "Realizado" or "RealizadoParcialmente"))
            builder.AppendLine($"El servicio \"{service.ServicioNombre ?? "seleccionado"}\" también finalizó.");

        var next = lavado.Servicios.Where(s => s.Estado is "Pendiente" or "EnProceso")
            .OrderBy(s => s.Orden).FirstOrDefault();
        if (next != null)
        {
            var nextStage = next.Etapas.FirstOrDefault(e => e.Estado is "Pendiente" or "EnProceso");
            builder.AppendLine($"Sigue: {nextStage?.Nombre ?? next.ServicioNombre ?? "el próximo servicio"}.");
        }

        if (lavado.Estado is "Realizado" or "RealizadoParcialmente")
        {
            builder.AppendLine(lavado.Estado == "Realizado"
                ? "El vehículo está listo para retirar."
                : "El lavado terminó parcialmente; el vehículo no se informa como lavado completo.");
        }
        else if (lavado.Estado == "Cancelado")
        {
            builder.AppendLine("El lavado no se informa como finalizado.");
        }

        return builder.ToString().Trim();
    }
}
