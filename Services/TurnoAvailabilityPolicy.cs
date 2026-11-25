namespace Firebase.Services;

/// <summary>A UTC operating segment; its end is exclusive.</summary>
public readonly record struct TurnoOpening(DateTime Start, DateTime End);

/// <summary>An existing UTC reservation and its required staffing; its end is exclusive.</summary>
public readonly record struct TurnoInterval(DateTime Start, DateTime End, int RequiredStaff);

public enum TurnoAvailabilityFailureReason
{
    None = 0,
    PastTime,
    InvalidGridInterval,
    OutsideBusinessHours,
    ExceedsClosingTime,
    InsufficientActiveStaff,
    ConcurrentSlotsExceeded,
    ConcurrentStaffExceeded
}

public readonly record struct TurnoAvailabilityResult(bool IsAvailable, TurnoAvailabilityFailureReason Reason, string Message);

public readonly record struct TurnoBookingResult(bool Success, string? ErrorMessage = null);

/// <summary>Pure availability calculation. Callers must reserve transactionally after checking.</summary>
public static class TurnoAvailabilityPolicy
{
    public static TurnoAvailabilityResult CheckAvailability(DateTime start, IEnumerable<int> serviceMinutes, int requiredStaff,
        int maxSlots, int activeStaff, IEnumerable<TurnoOpening> openings,
        IEnumerable<TurnoInterval> reservations, DateTime now, DateTime gridAnchor, int gridMinutes = 15)
    {
        RequireUtc(start, nameof(start));
        RequireUtc(now, nameof(now));
        RequireUtc(gridAnchor, nameof(gridAnchor));
        ArgumentNullException.ThrowIfNull(serviceMinutes);
        ArgumentNullException.ThrowIfNull(openings);
        ArgumentNullException.ThrowIfNull(reservations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredStaff);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSlots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(activeStaff);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gridMinutes);

        long duration = 0;
        foreach (var minutes in serviceMinutes)
        {
            if (minutes <= 0) throw new ArgumentException("Service durations must be positive.", nameof(serviceMinutes));
            duration = checked(duration + minutes);
        }
        if (duration == 0) throw new ArgumentException("At least one service is required.", nameof(serviceMinutes));
        var end = start.AddTicks(checked(duration * TimeSpan.TicksPerMinute));

        var openingList = openings.ToArray();
        foreach (var opening in openingList)
            ValidateInterval(opening.Start, opening.End, nameof(openings));
        var reservationList = reservations.ToArray();
        foreach (var reservation in reservationList)
        {
            ValidateInterval(reservation.Start, reservation.End, nameof(reservations));
            if (reservation.RequiredStaff <= 0)
                throw new ArgumentException("Reservation staffing must be positive.", nameof(reservations));
        }

        if (start < now)
            return new(false, TurnoAvailabilityFailureReason.PastTime, "La fecha y hora del turno ya ha pasado. Seleccione un horario futuro.");

        if ((start.Ticks - gridAnchor.Ticks) % ((long)gridMinutes * TimeSpan.TicksPerMinute) != 0)
            return new(false, TurnoAvailabilityFailureReason.InvalidGridInterval, $"El horario de inicio del turno debe coincidir con un intervalo de {gridMinutes} minutos.");

        var matchingOpening = openingList.FirstOrDefault(opening => opening.Start <= start && start < opening.End);
        if (matchingOpening == default)
            return new(false, TurnoAvailabilityFailureReason.OutsideBusinessHours, "El lavadero se encuentra cerrado en el horario seleccionado.");

        if (end > matchingOpening.End)
            return new(false, TurnoAvailabilityFailureReason.ExceedsClosingTime, $"La duración estimada de los servicios ({duration} min) supera el horario de cierre del lavadero.");

        if (requiredStaff > activeStaff)
            return new(false, TurnoAvailabilityFailureReason.InsufficientActiveStaff, $"El tipo de vehículo requiere {requiredStaff} empleados, pero actualmente solo hay {activeStaff} empleados activos.");

        // Demand can increase only at the proposed start or an existing reservation start.
        var overlaps = reservationList.Where(r => r.Start < end && start < r.End).ToArray();
        foreach (var boundary in overlaps.Select(r => r.Start > start ? r.Start : start).Append(start).Distinct())
        {
            long staff = requiredStaff;
            long slots = 1;
            foreach (var reservation in overlaps)
            {
                if (reservation.Start <= boundary && boundary < reservation.End)
                {
                    slots++;
                    staff += reservation.RequiredStaff;
                }
            }
            if (slots > maxSlots)
                return new(false, TurnoAvailabilityFailureReason.ConcurrentSlotsExceeded, $"Se alcanzó la capacidad máxima concurrente de {maxSlots} vehículos simultáneos para ese horario.");

            if (staff > activeStaff)
                return new(false, TurnoAvailabilityFailureReason.ConcurrentStaffExceeded, $"No hay suficiente personal disponible para atender turnos concurrentes en ese horario ({staff} requeridos vs {activeStaff} activos).");
        }
        return new(true, TurnoAvailabilityFailureReason.None, string.Empty);
    }

    public static bool IsAvailable(DateTime start, IEnumerable<int> serviceMinutes, int requiredStaff,
        int maxSlots, int activeStaff, IEnumerable<TurnoOpening> openings,
        IEnumerable<TurnoInterval> reservations, DateTime now, DateTime gridAnchor, int gridMinutes = 15) =>
        CheckAvailability(start, serviceMinutes, requiredStaff, maxSlots, activeStaff, openings, reservations, now, gridAnchor, gridMinutes).IsAvailable;

    private static void RequireUtc(DateTime value, string parameter)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Instants must be UTC.", parameter);
    }

    private static void ValidateInterval(DateTime start, DateTime end, string parameter)
    {
        RequireUtc(start, parameter);
        RequireUtc(end, parameter);
        if (end <= start) throw new ArgumentException("Interval end must be after start.", parameter);
    }
}
