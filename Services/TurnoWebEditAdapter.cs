using Firebase.Models;
using Firebase.Utils;
using Google.Cloud.Firestore;

namespace Firebase.Services;

internal sealed class TurnoWebEditAdapter(FirestoreDb db, TurnoService turnos)
{
    internal async Task<bool> Editar(TurnoEditWebForm form, string key)
    {
        if (form.Fecha == null || form.Fecha == DateOnly.MinValue || form.Hora == null ||
            !DateTime.TryParseExact(form.VersionEsperada, "O", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var version) || version.Kind != DateTimeKind.Utc)
            throw new ArgumentException();
        var local = form.Fecha.Value.ToDateTime(form.Hora.Value);
        var zone = TimeZoneHelper.ArgentinaZone;
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) throw new ArgumentException();
        var doc = await db.Collection("turnos").Document(form.Id).GetSnapshotAsync();
        if (!doc.Exists) throw new InvalidOperationException();
        var current = doc.ConvertTo<Turno>();
        if (current.Validar().Count != 0 || current.Id != form.Id || doc.UpdateTime == null) throw new InvalidOperationException();
        if (form.CuponIdEnviado && form.CuponId != current.CuponId) throw new InvalidOperationException();
        if ((form.Servicios != null && form.Servicios.Any(s => s == null)) || form.Notas?.Length > 2000) throw new ArgumentException();
        var selections = form.Servicios == null
            ? current.Vehiculos[0].Servicios.Select(s => new TurnoService.SnapshotSelection(s.ServicioId, s.PaqueteId)).ToArray()
            : form.Servicios.Select(s => new TurnoService.SnapshotSelection(s.ServicioId, s.PaqueteId)).ToArray();
        var request = new TurnoService.SnapshotRequest(current.Id, current.ClienteId, form.VehiculoId ?? current.Vehiculos[0].VehiculoId,
            current.Origen, current.ClaveIdempotencia, TimeZoneInfo.ConvertTimeToUtc(local, zone), form.AceptaAdelanto,
            selections,
            form.ClienteTrajoId ?? current.ClienteTrajoId, form.ClienteRetiraId ?? current.ClienteRetiraId,
            form.Descuento ?? (current.CuponId == null ? current.Descuento : 0), form.NotasEnviadas ? form.Notas ?? string.Empty : current.Notas, current.CuponId);
        return await turnos.Editar(new(request, key, Timestamp.FromDateTime(version)));
    }
}
