using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Firebase.Models;

namespace Firebase.Services;

public static class InsumosEmailFormatter
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    private static readonly string[] Campos = { "proveedor", "contacto_proveedor", "comprador", "ubicacion", "notas", "items", "total", "plazo_entrega", "fecha_entrega", "hora_entrega" };
    public const string CuerpoCotizacionDefault = "Solicitamos cotización para los siguientes insumos:\n{items}\nNotas: {notas}\nProveedor: {proveedor}\nContacto del proveedor: {contacto_proveedor}\nComprador: {comprador}\nUbicación: {ubicacion}\nFecha de entrega: {fecha_entrega}\nHora de entrega: {hora_entrega}\nPlazo de entrega: {plazo_entrega}";
    public const string CuerpoOrdenDefault = "Orden de compra:\n{items}\nTotal: {total}\nNotas: {notas}\nProveedor: {proveedor}\nContacto del proveedor: {contacto_proveedor}\nComprador: {comprador}\nUbicación: {ubicacion}\nFecha de entrega: {fecha_entrega}\nHora de entrega: {hora_entrega}\nPlazo de entrega: {plazo_entrega}";

    public static bool PlantillaValida(string? valor, bool asunto)
    {
        if (string.IsNullOrEmpty(valor)) return true;
        if (valor.Length > (asunto ? 200 : 10000) || (asunto && (valor.Contains('\r') || valor.Contains('\n')))) return false;
        var resto = Regex.Replace(valor, @"\{([^{}]+)\}", m => Campos.Contains(m.Groups[1].Value, StringComparer.Ordinal) && (!asunto || m.Groups[1].Value != "items") ? "" : m.Value);
        return !resto.Contains('{') && !resto.Contains('}');
    }

    public static EmailMessage FormatearSolicitud(DirectivaReabastecimiento directiva, SolicitudCotizacion solicitud, Proveedor proveedor, Configuracion? comprador, IReadOnlyDictionary<string, string>? unidades = null)
    {
        var datos = Datos(proveedor, comprador, solicitud.Notas, solicitud.Items, unidades, false, null);
        var asunto = string.IsNullOrWhiteSpace(directiva.AsuntoCotizacion) ? directiva.PlantillaEmail : directiva.AsuntoCotizacion;
        if (string.IsNullOrWhiteSpace(asunto)) asunto = "Solicitud de cotización de insumos";
        return Crear(proveedor.EmailOrdenes, asunto, directiva.CuerpoCotizacion, CuerpoCotizacionDefault, datos);
    }

    public static EmailMessage FormatearOrden(DirectivaReabastecimiento directiva, OrdenCompra orden, Proveedor proveedor, Configuracion? comprador, string? plazoEntrega, IReadOnlyDictionary<string, string>? unidades = null)
    {
        var datos = Datos(proveedor, comprador, orden.Notas, orden.Items, unidades, true, plazoEntrega);
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
        var html = Sustituir(WebUtility.HtmlEncode(cuerpo), datos, true).Replace("\r\n", "\n").Replace("\n", "<br>");
        return new EmailMessage(email, subject, "<p>" + html + "</p>");
    }

    private static string Sustituir(string plantilla, Dictionary<string, string> datos, bool html)
    {
        return Regex.Replace(plantilla, @"\{([^{}]+)\}", m => datos.TryGetValue(m.Groups[1].Value, out var valor)
            ? (html && m.Groups[1].Value != "items" ? WebUtility.HtmlEncode(valor) : valor)
            : (html ? WebUtility.HtmlEncode(m.Value) : m.Value));
    }

    private static Dictionary<string, string> Datos(Proveedor proveedor, Configuracion? comprador, string? notas, IEnumerable<OrdenCompraItem> items, IReadOnlyDictionary<string, string>? unidades, bool precios, string? plazo)
    {
        var lista = items.ToList();
        string Texto(string? s) => string.IsNullOrWhiteSpace(s) ? "No informado" : s;
        var detalle = string.Join("<br>", lista.Select(i =>
        {
            var unidad = unidades != null && unidades.TryGetValue(i.InsumoId, out var u) ? Texto(u) : "No informado";
            var linea = $"{WebUtility.HtmlEncode(i.NombreInsumo)} — Cantidad: {i.Cantidad.ToString("N2", Cultura)}; Unidad: {WebUtility.HtmlEncode(unidad)}";
            return precios ? linea + $"; Precio unitario: $ {i.PrecioUnitario.ToString("N2", Cultura)}; Importe: $ {(i.Cantidad * i.PrecioUnitario).ToString("N2", Cultura)}" : linea;
        }));
        return new() { ["proveedor"] = Texto(proveedor.RazonSocial), ["contacto_proveedor"] = Texto(proveedor.Contacto),
            ["comprador"] = Texto(comprador?.NombreLavadero), ["ubicacion"] = Texto(comprador?.Ubicacion), ["notas"] = Texto(notas),
            ["items"] = detalle, ["total"] = precios ? "$ " + lista.Sum(i => i.Cantidad * i.PrecioUnitario).ToString("N2", Cultura) : "No informado",
            ["plazo_entrega"] = Texto(plazo), ["fecha_entrega"] = "No informado", ["hora_entrega"] = "No informado" };
    }
}
