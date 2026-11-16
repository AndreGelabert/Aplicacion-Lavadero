using System.ComponentModel.DataAnnotations;
using Firebase.Converters;
using Google.Cloud.Firestore;

namespace Firebase.Models;

[FirestoreData]
public sealed class Insumo
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty, Required, StringLength(120)] public string Nombre { get; set; } = string.Empty;
    [FirestoreProperty] public string TipoId { get; set; } = string.Empty;
    [FirestoreProperty, Required] public string Tipo { get; set; } = string.Empty;
    [FirestoreProperty, Required] public string UnidadMedida { get; set; } = "unidad";
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal StockActual { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal StockMinimo { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal CantidadReabastecimiento { get; set; }
    [FirestoreProperty] public bool ReabastecimientoAutomatico { get; set; }
    [FirestoreProperty] public List<string> ProveedoresIds { get; set; } = new();
    [FirestoreProperty] public string Estado { get; set; } = "Activo";
}

[FirestoreData]
public sealed class Proveedor
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty, Required, StringLength(20)] public string IdentificacionTributaria { get; set; } = string.Empty;
    [FirestoreProperty, Required, StringLength(160)] public string RazonSocial { get; set; } = string.Empty;
    [FirestoreProperty] public string Contacto { get; set; } = string.Empty;
    [FirestoreProperty, EmailAddress] public string EmailOrdenes { get; set; } = string.Empty;
    [FirestoreProperty] public string Estado { get; set; } = "Activo";
    public bool EmailOrdenesBloqueado { get; set; }
}

public enum TipoMovimientoStock
{
    IngresoManual,
    EgresoManual,
    ConsumoEtapa,
    RecepcionOrden,
    MermaCancelacion
}

[FirestoreData]
public sealed class MovimientoStock
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty] public string InsumoId { get; set; } = string.Empty;
    [FirestoreProperty] public DateTime Fecha { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public string Tipo { get; set; } = string.Empty;
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal Cantidad { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal SaldoResultante { get; set; }
    [FirestoreProperty] public string ReferenciaId { get; set; } = string.Empty;
    [FirestoreProperty] public string UsuarioId { get; set; } = string.Empty;
    [FirestoreProperty] public string Justificacion { get; set; } = string.Empty;
}

[FirestoreData]
public sealed class OrdenCompraItem
{
    [FirestoreProperty] public string InsumoId { get; set; } = string.Empty;
    [FirestoreProperty] public string NombreInsumo { get; set; } = string.Empty;
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal Cantidad { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal CantidadRecibida { get; set; }
    [FirestoreProperty(ConverterType = typeof(DecimalConverter))] public decimal PrecioUnitario { get; set; }
    [FirestoreProperty] public bool Disponible { get; set; } = true;
}

[FirestoreData]
public sealed class OrdenCompra
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty] public string ProveedorId { get; set; } = string.Empty;
    [FirestoreProperty] public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public string Estado { get; set; } = "Enviada";
    [FirestoreProperty] public string GrupoCotizacionId { get; set; } = string.Empty;
    [FirestoreProperty] public List<OrdenCompraItem> Items { get; set; } = new();
    [FirestoreProperty] public string Notas { get; set; } = string.Empty;
    [FirestoreProperty] public string MotivoCancelacion { get; set; } = string.Empty;
}

public sealed class RecepcionOrdenItem
{
    public string InsumoId { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
}

[FirestoreData]
public sealed class SolicitudCotizacion
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty] public string GrupoId { get; set; } = string.Empty;
    [FirestoreProperty] public string ProveedorId { get; set; } = string.Empty;
    [FirestoreProperty] public string Estado { get; set; } = "Pendiente";
    [FirestoreProperty] public List<OrdenCompraItem> Items { get; set; } = new();
    [FirestoreProperty] public DateTime Fecha { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public string Notas { get; set; } = string.Empty;
}

[FirestoreData]
public sealed class CotizacionRespuesta
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty] public string SolicitudId { get; set; } = string.Empty;
    [FirestoreProperty] public string ProveedorId { get; set; } = string.Empty;
    [FirestoreProperty] public string Estado { get; set; } = "Recibida";
    [FirestoreProperty] public List<OrdenCompraItem> Items { get; set; } = new();
    [FirestoreProperty] public string PlazoEntrega { get; set; } = string.Empty;
    [FirestoreProperty] public DateTime Fecha { get; set; } = DateTime.UtcNow;
    [FirestoreProperty] public string Notas { get; set; } = string.Empty;
}

public sealed class ComparacionCotizacion
{
    public required SolicitudCotizacion Solicitud { get; init; }
    public CotizacionRespuesta? Respuesta { get; init; }
    public decimal Total { get; init; }
    public string ProveedorNombre { get; init; } = string.Empty;
}

[FirestoreData]
public sealed class DirectivaReabastecimiento
{
    [FirestoreProperty] public string Id { get; set; } = "principal";
    [FirestoreProperty] public string ModoEmision { get; set; } = "RequiereAprobacion";
    [FirestoreProperty] public string PlantillaEmail { get; set; } = "Solicitud de cotización de insumos";
    [FirestoreProperty] public string AsuntoCotizacion { get; set; } = "";
    [FirestoreProperty] public string CuerpoCotizacion { get; set; } = "";
    [FirestoreProperty] public string AsuntoOrden { get; set; } = "";
    [FirestoreProperty] public string CuerpoOrden { get; set; } = "";
}

public sealed class KardexCursor
{
    public string Direccion { get; }
    public DateTime FechaUtc { get; }
    public string DocumentId { get; }

    public KardexCursor(string direccion, DateTime fechaUtc, string documentId)
    {
        Direccion = direccion.Trim().ToLowerInvariant();
        FechaUtc = fechaUtc.Kind == DateTimeKind.Utc ? fechaUtc : fechaUtc.ToUniversalTime();
        DocumentId = documentId.Trim();
    }

    public string Serializar()
    {
        var raw = $"{Direccion}|{FechaUtc:O}|{DocumentId}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Crear(string direccion, DateTime fechaUtc, string documentId)
    {
        return new KardexCursor(direccion, fechaUtc, documentId).Serializar();
    }

    public static bool TratarDeDecodificar(string? token, out KardexCursor? cursor, out string? error)
    {
        cursor = null;
        error = null;

        if (string.IsNullOrWhiteSpace(token))
            return true;

        if (token.Length > 512)
        {
            error = "Token de cursor excede la longitud permitida.";
            return false;
        }

        byte[] bytes;
        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            bytes = Convert.FromBase64String(base64);
        }
        catch
        {
            error = "Token de cursor con formato Base64 no válido.";
            return false;
        }

        string raw;
        try
        {
            raw = System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            error = "Token de cursor no contiene texto UTF-8 válido.";
            return false;
        }

        var parts = raw.Split('|');
        if (parts.Length != 3)
        {
            error = "Estructura de cursor no válida.";
            return false;
        }

        var direccion = parts[0].Trim().ToLowerInvariant();
        if (direccion != "next" && direccion != "prev")
        {
            error = "Dirección de cursor no válida.";
            return false;
        }

        if (!DateTime.TryParse(parts[1].Trim(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var fechaUtc))
        {
            error = "Fecha de cursor no válida.";
            return false;
        }

        var docId = parts[2].Trim();
        if (string.IsNullOrEmpty(docId) || docId.Contains('/'))
        {
            error = "Identificador de documento no válido en cursor.";
            return false;
        }

        cursor = new KardexCursor(direccion, fechaUtc, docId);
        return true;
    }
}

public sealed class ResultadoKardexPaginado
{
    public List<MovimientoStock> Movimientos { get; set; } = new();
    public bool TienePaginaSiguiente { get; set; }
    public bool TienePaginaAnterior { get; set; }
    public string? CursorSiguiente { get; set; }
    public string? CursorAnterior { get; set; }

    // Propiedades de compatibilidad para evitar regresiones en consumidores existentes
    public int PaginaActual { get; set; } = 1;
    public int TotalPaginas { get; set; } = 1;
    public long TotalRegistros { get; set; }
}
