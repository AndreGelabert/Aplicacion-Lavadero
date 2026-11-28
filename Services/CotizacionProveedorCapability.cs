using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Firebase.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;

namespace Firebase.Services;

public sealed class CotizacionProveedorInvalidaException() : InvalidOperationException("Enlace de cotización inválido o expirado.");

public sealed class CotizacionProveedorDatosInvalidosException(string message) : InvalidOperationException(message);

public sealed class CotizacionProveedorTokenRequest
{
    public string? SolicitudId { get; set; }
    public string? Token { get; set; }
}

public sealed class DetalleCotizacionProveedorItem
{
    public string InsumoId { get; set; } = string.Empty;
    public string NombreInsumo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string UnidadMedida { get; set; } = string.Empty;
}

public sealed class DetalleCotizacionProveedor
{
    public string NombreLavadero { get; set; } = string.Empty;
    public string? FechaEntregaPreferida { get; set; }
    public string NotasSolicitud { get; set; } = string.Empty;
    public List<DetalleCotizacionProveedorItem> Items { get; set; } = new();
}

public sealed class RespuestaCotizacionProveedorItem
{
    public string InsumoId { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public string TipoPrecio { get; set; } = "Unitario";
    public bool Disponible { get; set; } = true;
}

public sealed class RespuestaCotizacionProveedorRequest
{
    public string? SolicitudId { get; set; }
    public string? Token { get; set; }
    public string? PlazoEntrega { get; set; }
    public string? Notas { get; set; }
    public List<RespuestaCotizacionProveedorItem>? Items { get; set; }
}

public sealed class CotizacionProveedorCapability(FirestoreDb db, TimeProvider? reloj = null)
{
    private const string ColeccionCapabilities = "cotizaciones_proveedor_capabilities";
    private const int VigenciaDefaultDias = 7;
    private const int VigenciaMaximaDias = 30;
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    private DateTime AhoraUtc => (reloj ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public static string GenerateSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool HashesIguales(string expectedHex, string actualHex)
    {
        if (string.IsNullOrEmpty(expectedHex) || expectedHex.Length != actualHex.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expectedHex), Encoding.ASCII.GetBytes(actualHex));
    }

    private static bool ValidarId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        !id.Contains('/') &&
        id is not "." and not ".." &&
        !(id.StartsWith("__", StringComparison.Ordinal) && id.EndsWith("__", StringComparison.Ordinal)) &&
        Encoding.UTF8.GetByteCount(id) <= 1500;

    private static bool ValidarTokenFormato(string? token) =>
        !string.IsNullOrEmpty(token) && token.Length == 64 && token.All(Uri.IsHexDigit);

    public async Task<string?> EmitirAsync(string solicitudId, CancellationToken cancellationToken = default)
    {
        if (!ValidarId(solicitudId)) return null;
        var token = GenerateSecret();
        var hash = Hash(token);
        var capRef = db.Collection(ColeccionCapabilities).Document(solicitudId);
        var solRef = db.Collection("solicitudesCotizacion").Document(solicitudId);

        var emitido = await InsumosTransacciones.EjecutarAsync(db, async transaction =>
        {
            var solSnap = await transaction.GetSnapshotAsync(solRef, cancellationToken);
            if (!solSnap.Exists) return false;
            var solicitud = solSnap.ConvertTo<SolicitudCotizacion>();
            if (solicitud.Estado != "Pendiente" || !ValidarId(solicitud.ProveedorId))
                return false;

            var now = AhoraUtc;
            var defaultExpiry = now.AddDays(VigenciaDefaultDias);
            var maxExpiry = now.AddDays(VigenciaMaximaDias);
            var expiresUtc = solicitud.FechaEntregaPreferidaUtc is DateTime pref && pref > defaultExpiry
                ? (pref <= maxExpiry ? pref : maxExpiry)
                : defaultExpiry;

            transaction.Set(capRef, new Dictionary<string, object>
            {
                ["Hash"] = hash,
                ["SolicitudId"] = solicitudId,
                ["ProveedorId"] = solicitud.ProveedorId,
                ["CreatedUtc"] = now,
                ["ExpiresUtc"] = expiresUtc,
                ["Consumed"] = false
            });
            return true;
        });

        return emitido ? token : null;
    }

    public async Task<DetalleCotizacionProveedor> ObtenerDetalleAsync(string solicitudId, string token, CancellationToken cancellationToken = default)
    {
        if (!ValidarId(solicitudId) || !ValidarTokenFormato(token))
            throw new CotizacionProveedorInvalidaException();

        var tokenHash = Hash(token);
        var capSnap = await db.Collection(ColeccionCapabilities).Document(solicitudId).GetSnapshotAsync(cancellationToken);
        var solSnap = await db.Collection("solicitudesCotizacion").Document(solicitudId).GetSnapshotAsync(cancellationToken);
        var anteriores = await db.Collection("cotizacionesRespuestas").WhereEqualTo("SolicitudId", solicitudId).GetSnapshotAsync(cancellationToken);

        if (!capSnap.Exists || !solSnap.Exists || anteriores.Count > 0)
            throw new CotizacionProveedorInvalidaException();

        var solicitud = solSnap.ConvertTo<SolicitudCotizacion>();
        if (!CapacidadValida(capSnap, solicitud, tokenHash, AhoraUtc))
            throw new CotizacionProveedorInvalidaException();

        var configSnap = await db.Collection("configuracion").Document("system_config").GetSnapshotAsync(cancellationToken);
        var nombreLavadero = configSnap.Exists && configSnap.TryGetValue<string>("NombreLavadero", out var nl) && !string.IsNullOrWhiteSpace(nl)
            ? nl
            : "Lavadero";

        var unidades = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var insumoId in solicitud.Items.Select(i => i.InsumoId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            var insumoSnap = await db.Collection("insumos").Document(insumoId).GetSnapshotAsync(cancellationToken);
            if (insumoSnap.Exists)
                unidades[insumoId] = insumoSnap.ConvertTo<Insumo>().UnidadMedida;
        }

        string? fechaPreferida = null;
        if (solicitud.FechaEntregaPreferidaUtc is DateTime pref)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(pref, ComprasService.ZonaEntrega);
            fechaPreferida = local.ToString("dd/MM/yyyy HH:mm", Cultura);
        }

        return new DetalleCotizacionProveedor
        {
            NombreLavadero = nombreLavadero,
            FechaEntregaPreferida = fechaPreferida,
            NotasSolicitud = solicitud.Notas ?? string.Empty,
            Items = solicitud.Items.Select(i => new DetalleCotizacionProveedorItem
            {
                InsumoId = i.InsumoId,
                NombreInsumo = i.NombreInsumo,
                Cantidad = i.Cantidad,
                UnidadMedida = unidades.TryGetValue(i.InsumoId, out var u) && !string.IsNullOrWhiteSpace(u) ? u : "unidad"
            }).ToList()
        };
    }

    public async Task ResponderAsync(string solicitudId, string token, RespuestaCotizacionProveedorRequest request, CancellationToken cancellationToken = default)
    {
        if (!ValidarId(solicitudId) || !ValidarTokenFormato(token) || request == null || request.Items == null)
            throw new CotizacionProveedorInvalidaException();

        var tokenHash = Hash(token);
        var capRef = db.Collection(ColeccionCapabilities).Document(solicitudId);
        var solRef = db.Collection("solicitudesCotizacion").Document(solicitudId);
        var responseRef = db.Collection("cotizacionesRespuestas").Document();
        var auditRef = db.Collection("registros_auditoria").Document();

        await InsumosTransacciones.EjecutarAsync(db, async transaction =>
        {
            var capSnap = await transaction.GetSnapshotAsync(capRef, cancellationToken);
            var solSnap = await transaction.GetSnapshotAsync(solRef, cancellationToken);
            var anteriores = await transaction.GetSnapshotAsync(db.Collection("cotizacionesRespuestas").WhereEqualTo("SolicitudId", solicitudId), cancellationToken);

            var sol = solSnap.Exists ? solSnap.ConvertTo<SolicitudCotizacion>() : null;
            DocumentSnapshot? provSnap = sol != null && ValidarId(sol.ProveedorId)
                ? await transaction.GetSnapshotAsync(db.Collection("proveedores").Document(sol.ProveedorId), cancellationToken)
                : null;

            if (!capSnap.Exists || sol == null || anteriores.Count > 0 || !CapacidadValida(capSnap, sol, tokenHash, AhoraUtc))
                throw new CotizacionProveedorInvalidaException();

            var respuesta = new CotizacionRespuesta
            {
                Id = responseRef.Id,
                SolicitudId = solicitudId,
                ProveedorId = sol.ProveedorId,
                Estado = "Recibida",
                PlazoEntrega = request.PlazoEntrega?.Trim() ?? string.Empty,
                Notas = request.Notas?.Trim() ?? string.Empty,
                Fecha = AhoraUtc,
                Items = request.Items.Select(i => new OrdenCompraItem
                {
                    InsumoId = i.InsumoId ?? string.Empty,
                    PrecioUnitario = i.Precio,
                    TipoPrecio = i.TipoPrecio ?? "Unitario",
                    Disponible = i.Disponible
                }).ToList()
            };

            var validacion = ComprasService.ValidarYNormalizarRespuesta(sol, respuesta);
            if (!validacion.EsExitoso)
                throw new CotizacionProveedorDatosInvalidosException(validacion.MensajeError);

            var proveedorEmail = provSnap != null && provSnap.Exists && provSnap.TryGetValue<string>("EmailOrdenes", out var email)
                ? email ?? string.Empty
                : string.Empty;

            transaction.Create(responseRef, respuesta);
            transaction.Update(solRef, "Estado", "Respondida");
            transaction.Update(capRef, new Dictionary<string, object>
            {
                ["Consumed"] = true,
                ["ConsumedAtUtc"] = AhoraUtc
            });
            transaction.Create(auditRef, new AuditLog
            {
                DocId = auditRef.Id,
                UserId = "proveedor:" + sol.ProveedorId,
                UserEmail = proveedorEmail,
                Action = "Registro Respuesta Cotizacion (Proveedor)",
                TargetType = "SolicitudCotizacion",
                TargetId = solicitudId,
                NombreHistorico = $"Solicitud {solicitudId}",
                Antes = new Dictionary<string, string> { ["Estado"] = sol.Estado },
                Despues = new Dictionary<string, string> { ["Estado"] = "Respondida", ["Origen"] = "LinkProveedor" },
                Timestamp = AhoraUtc
            });

            return true;
        });
    }

    private static bool CapacidadValida(DocumentSnapshot capSnap, SolicitudCotizacion solicitud, string tokenHash, DateTime now)
    {
        if (solicitud.Estado != "Pendiente") return false;
        if (!capSnap.TryGetValue<string>("Hash", out var storedHash) ||
            !capSnap.TryGetValue<string>("SolicitudId", out var storedSolId) ||
            !capSnap.TryGetValue<string>("ProveedorId", out var storedProvId) ||
            !capSnap.TryGetValue<DateTime>("ExpiresUtc", out var expiresUtc) ||
            !capSnap.TryGetValue<bool>("Consumed", out var consumed))
            return false;

        return !consumed &&
               storedSolId == solicitud.Id &&
               storedProvId == solicitud.ProveedorId &&
               now < expiresUtc &&
               HashesIguales(storedHash, tokenHash);
    }
}

public sealed class CotizacionProveedorLinkService
{
    private readonly CotizacionProveedorCapability? capability;
    private readonly string? origin;

    public CotizacionProveedorLinkService(CotizacionProveedorCapability capability, IConfiguration configuration)
        : this(capability, configuration["Insumos:PublicOrigin"] ?? configuration["TurnoOffer:Origin"])
    {
    }

    public CotizacionProveedorLinkService(CotizacionProveedorCapability? capability, string? origin)
    {
        this.capability = capability;
        this.origin = ValidarOrigen(origin);
    }

    public bool TieneOrigen => origin != null;

    public static string? ValidarOrigen(string? origin)
    {
        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host == "localhost"))) &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment))
        {
            return uri.GetLeftPart(UriPartial.Authority);
        }
        return null;
    }

    public string ConstruirUrl(string solicitudId, string token)
    {
        if (origin == null) throw new InvalidOperationException("Origen público de cotizaciones inválido.");
        return origin + "/cotizacion-proveedor/responder#SolicitudId=" + Uri.EscapeDataString(solicitudId) + "&Token=" + token;
    }

    public async Task<string?> CrearLinkAsync(string solicitudId, CancellationToken cancellationToken = default)
    {
        if (origin == null || capability == null) return null;
        var token = await capability.EmitirAsync(solicitudId, cancellationToken);
        return token == null ? null : ConstruirUrl(solicitudId, token);
    }
}
