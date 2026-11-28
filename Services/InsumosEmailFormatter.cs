using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Firebase.Models;

namespace Firebase.Services;

public static class InsumosEmailFormatter
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    private static readonly string[] Campos = { "proveedor", "contacto_proveedor", "comprador", "ubicacion", "notas", "items", "total", "plazo_entrega", "fecha_entrega", "hora_entrega", "link_respuesta" };
    public const string CuerpoCotizacionDefault = "Hola,\n\nDesde {comprador}, ubicado en {ubicacion}, nos contactamos para solicitarte una cotización de los siguientes insumos:\n\n{items}\n\nPor favor, indícanos precio, disponibilidad y plazo de entrega.\n\nFecha y hora preferida de entrega: {fecha_entrega} a las {hora_entrega} hs (sujetas a confirmación).\nObservaciones: {notas}\n\n¡Muchas gracias!\n{comprador}";
    public const string CuerpoOrdenDefault = "Hola,\n\nDesde {comprador}, ubicado en {ubicacion}, te confirmamos la siguiente orden de compra:\n\n{items}\n\nTotal estimado: {total}\nPlazo de entrega acordado: {plazo_entrega}\nObservaciones: {notas}\n\n¡Muchas gracias!\n{comprador}";

    public static bool PlantillaValida(string? valor, bool asunto)
    {
        if (string.IsNullOrEmpty(valor)) return true;
        if (valor.Length > (asunto ? 200 : 10000) || (asunto && (valor.Contains('\r') || valor.Contains('\n')))) return false;
        var resto = Regex.Replace(valor, @"\{([^{}]+)\}", m => Campos.Contains(m.Groups[1].Value, StringComparer.Ordinal) && (!asunto || m.Groups[1].Value is not ("items" or "link_respuesta")) ? "" : m.Value);
        return !resto.Contains('{') && !resto.Contains('}');
    }

    public static void ValidarAsuntoSolicitud(DirectivaReabastecimiento directiva, Proveedor proveedor, Configuracion? comprador)
    {
        var datos = Datos(proveedor, comprador, string.Empty, Array.Empty<OrdenCompraItem>(), null, false, null, null);
        var asunto = string.IsNullOrWhiteSpace(directiva.AsuntoCotizacion) ? directiva.PlantillaEmail : directiva.AsuntoCotizacion;
        if (string.IsNullOrWhiteSpace(asunto)) asunto = "Solicitud de cotización de insumos";
        var subject = PlantillaValida(asunto, true) ? Sustituir(asunto, datos, false) : asunto.Replace('\r', ' ').Replace('\n', ' ');
        if (subject.Contains('\r') || subject.Contains('\n'))
            throw new InvalidOperationException("El asunto generado no puede contener saltos de línea.");
    }

    public static EmailMessage FormatearSolicitud(DirectivaReabastecimiento directiva, SolicitudCotizacion solicitud, Proveedor proveedor, Configuracion? comprador, IReadOnlyDictionary<string, string>? unidades = null, string? linkRespuesta = null)
    {
        var enlaceHtml = string.IsNullOrWhiteSpace(linkRespuesta)
            ? null
            : $"<a href=\"{WebUtility.HtmlEncode(linkRespuesta)}\">Cargar cotización</a>";
        var datos = Datos(proveedor, comprador, solicitud.Notas, solicitud.Items, unidades, false, null, enlaceHtml);
        if (solicitud.FechaEntregaPreferidaUtc is DateTime fecha)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(fecha, ComprasService.ZonaEntrega);
            datos["fecha_entrega"] = local.ToString("dd/MM/yyyy", Cultura);
            datos["hora_entrega"] = local.ToString("HH:mm", Cultura);
        }
        var asunto = string.IsNullOrWhiteSpace(directiva.AsuntoCotizacion) ? directiva.PlantillaEmail : directiva.AsuntoCotizacion;
        if (string.IsNullOrWhiteSpace(asunto)) asunto = "Solicitud de cotización de insumos";
        var mensaje = Crear(proveedor.EmailOrdenes, asunto, directiva.CuerpoCotizacion, CuerpoCotizacionDefault, datos);
        if (enlaceHtml != null)
        {
            var cuerpoEfectivo = string.IsNullOrWhiteSpace(directiva.CuerpoCotizacion) ? CuerpoCotizacionDefault : directiva.CuerpoCotizacion;
            if (!cuerpoEfectivo.Contains("{link_respuesta}", StringComparison.Ordinal))
                mensaje = mensaje with { Html = mensaje.Html + $"<p style=\"margin:16px 0 0 0;\">Podés cargar tu cotización directamente desde el siguiente enlace:<br><strong>{enlaceHtml}</strong></p>" };
        }
        return mensaje;
    }

    public static EmailMessage FormatearOrden(DirectivaReabastecimiento directiva, OrdenCompra orden, Proveedor proveedor, Configuracion? comprador, string? plazoEntrega, IReadOnlyDictionary<string, string>? unidades = null)
    {
        var datos = Datos(proveedor, comprador, orden.Notas, orden.Items, unidades, true, plazoEntrega, null);
        return Crear(proveedor.EmailOrdenes, directiva.AsuntoOrden, directiva.CuerpoOrden, CuerpoOrdenDefault, datos, "Orden de compra de insumos");
    }

    private static EmailMessage Crear(string email, string? asunto, string? cuerpo, string cuerpoDefault, Dictionary<string, string> datos, string asuntoDefault = "Solicitud de cotización de insumos")
    {
        asunto = string.IsNullOrWhiteSpace(asunto) ? asuntoDefault : asunto;
        cuerpo = string.IsNullOrWhiteSpace(cuerpo) ? cuerpoDefault : cuerpo;
        // Legacy values were not constrained; treat invalid legacy subjects as literal text.
        var subject = PlantillaValida(asunto, true) ? Sustituir(asunto, datos, false) : asunto.Replace('\r', ' ').Replace('\n', ' ');
        if (subject.Contains('\r') || subject.Contains('\n'))
            throw new InvalidOperationException("El asunto generado no puede contener saltos de línea.");
        var cuerpoNormalizado = WebUtility.HtmlEncode(cuerpo).Replace("\r\n", "\n");
        var parrafos = cuerpoNormalizado.Split("\n\n", StringSplitOptions.None)
            .Select(p => "<p style=\"margin:0 0 12px 0;\">" + Sustituir(p, datos, true).Replace("\n", "<br>") + "</p>");
        var html = "<div style=\"font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;color:#1e293b;line-height:1.5;\">" + string.Join("", parrafos) + "</div>";
        return new EmailMessage(email, subject, html);
    }

    private static string Sustituir(string plantilla, Dictionary<string, string> datos, bool html)
    {
        return Regex.Replace(plantilla, @"\{([^{}]+)\}", m => datos.TryGetValue(m.Groups[1].Value, out var valor)
            ? (html && m.Groups[1].Value is not ("items" or "link_respuesta") ? WebUtility.HtmlEncode(valor) : valor)
            : (html ? WebUtility.HtmlEncode(m.Value) : m.Value));
    }

    private static Dictionary<string, string> Datos(Proveedor proveedor, Configuracion? comprador, string? notas, IEnumerable<OrdenCompraItem> items, IReadOnlyDictionary<string, string>? unidades, bool precios, string? plazo, string? enlaceHtml)
    {
        var lista = items.ToList();
        string Texto(string? s) => string.IsNullOrWhiteSpace(s) ? "No informado" : s;
        var detalleItems = string.Join("", lista.Select(i =>
        {
            var unidad = unidades != null && unidades.TryGetValue(i.InsumoId, out var u) ? Texto(u) : "No informado";
            var linea = $"<strong>{WebUtility.HtmlEncode(i.NombreInsumo)}</strong> — Cantidad: {i.Cantidad.ToString("N2", Cultura)} ({WebUtility.HtmlEncode(unidad)})";
            var texto = precios ? linea + $" — Precio unitario: $ {i.PrecioUnitario.ToString("N2", Cultura)} — Importe: <strong>$ {(i.Cantidad * i.PrecioUnitario).ToString("N2", Cultura)}</strong>" : linea;
            return $"<li style=\"margin-bottom:4px;\">{texto}</li>";
        }));
        var detalle = lista.Count == 0
            ? "No informado"
            : $"<ul style=\"margin:8px 0;padding-left:20px;\">{detalleItems}</ul>";
        return new() { ["proveedor"] = Texto(proveedor.RazonSocial), ["contacto_proveedor"] = Texto(proveedor.Contacto),
            ["comprador"] = Texto(comprador?.NombreLavadero), ["ubicacion"] = Texto(comprador?.Ubicacion), ["notas"] = Texto(notas),
            ["items"] = detalle, ["total"] = precios ? "$ " + lista.Sum(i => i.Cantidad * i.PrecioUnitario).ToString("N2", Cultura) : "No informado",
            ["plazo_entrega"] = Texto(plazo), ["fecha_entrega"] = "No informado", ["hora_entrega"] = "No informado",
            ["link_respuesta"] = Texto(enlaceHtml) };
    }
}
