using Firebase.Services;

namespace Firebase.Tests.Services;

public class TurnoOfertaMessageTimeTests
{
    [Theory]
    [InlineData(2030, 1, 2, 2, 30, 2030, 1, 2, 3, 15, "Hay una oferta para adelantar su turno al 2030-01-01 23:30 (hora de Buenos Aires). Responda antes de 2030-01-02 00:15 (hora de Buenos Aires).")]
    [InlineData(2030, 7, 1, 15, 0, 2030, 7, 1, 14, 45, "Hay una oferta para adelantar su turno al 2030-07-01 12:00 (hora de Buenos Aires). Responda antes de 2030-07-01 11:45 (hora de Buenos Aires).")]
    public void MessageUsesBuenosAiresForBothTimes(int py, int pm, int pd, int ph, int pmin, int ey, int em, int ed, int eh, int emin, string expected)
    {
        var proposed = new DateTime(py, pm, pd, ph, pmin, 0, DateTimeKind.Utc);
        var expires = new DateTime(ey, em, ed, eh, emin, 0, DateTimeKind.Utc);
        Assert.Equal(expected, TurnoOfertaNotificationResolver.FormatMessage(proposed, expires));
    }
}
