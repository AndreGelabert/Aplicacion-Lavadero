using System;
using Firebase.Models;
using Firebase.Utils;
using Xunit;

namespace Firebase.Tests.Services;

public class TimeZoneHelperTests
{
    [Fact]
    public void ConvertirUtcAZonaApp_RestaTresHorasEnHorarioNormal()
    {
        // 19 de Noviembre de 2026 a las 22:30 UTC -> 19:30 Argentina (UTC-3)
        var utc = new DateTime(2026, 11, 19, 22, 30, 0, DateTimeKind.Utc);
        var local = TimeZoneHelper.ConvertirUtcAZonaApp(utc);

        Assert.Equal(new DateTime(2026, 11, 19, 19, 30, 0), local);
        Assert.Equal("19/11/2026 19:30", TimeZoneHelper.FormatearFechaHoraApp(utc));
    }

    [Fact]
    public void ConvertirUtcAZonaApp_CambioDeDiaPorCruceDeMedianoche()
    {
        // 20 de Noviembre a las 01:15 UTC -> 19 de Noviembre a las 22:15 Argentina
        // Sin conversión, la pantalla mostraría erróneamente el día de mañana (20/11)
        var utc = new DateTime(2026, 11, 20, 1, 15, 0, DateTimeKind.Utc);
        var local = TimeZoneHelper.ConvertirUtcAZonaApp(utc);

        Assert.Equal(new DateTime(2026, 11, 19, 22, 15, 0), local);
        Assert.Equal(19, local.Day);
        Assert.Equal(22, local.Hour);
        Assert.Equal("19/11/2026 22:15", TimeZoneHelper.FormatearFechaHoraApp(utc));
    }

    [Fact]
    public void ConvertirUtcAZonaApp_CambioDeMesYAno()
    {
        // 1 de Enero de 2026 a las 02:00 UTC -> 31 de Diciembre de 2025 a las 23:00 Argentina
        var utc = new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc);
        var local = TimeZoneHelper.ConvertirUtcAZonaApp(utc);

        Assert.Equal(new DateTime(2025, 12, 31, 23, 0, 0), local);
        Assert.Equal(2025, local.Year);
        Assert.Equal(12, local.Month);
        Assert.Equal(31, local.Day);
        Assert.Equal(23, local.Hour);
        Assert.Equal("31/12/2025 23:00", TimeZoneHelper.FormatearFechaHoraApp(utc));
    }

    [Fact]
    public void ConvertirUtcAZonaApp_ManejaDateTimeKindUnspecifiedYLocal()
    {
        var unspecified = new DateTime(2026, 11, 19, 21, 0, 0, DateTimeKind.Unspecified);
        var localUnspecified = TimeZoneHelper.ConvertirUtcAZonaApp(unspecified);
        Assert.Equal(new DateTime(2026, 11, 19, 18, 0, 0), localUnspecified);

        var utc = new DateTime(2026, 11, 19, 21, 0, 0, DateTimeKind.Utc);
        var localDateTime = utc.ToLocalTime();
        var convertedFromLocal = TimeZoneHelper.ConvertirUtcAZonaApp(localDateTime);
        Assert.Equal(new DateTime(2026, 11, 19, 18, 0, 0), convertedFromLocal);
    }

    [Fact]
    public void FormatearFechaHoraApp_ManejaNull()
    {
        Assert.Equal(string.Empty, TimeZoneHelper.FormatearFechaHoraApp(null));
    }

    [Fact]
    public void Configuracion_FechaActualizacionLocal_CalculaCorrectamente()
    {
        var config = new Configuracion
        {
            FechaActualizacion = new DateTime(2026, 11, 20, 1, 45, 0, DateTimeKind.Utc)
        };

        Assert.NotNull(config.FechaActualizacionLocal);
        Assert.Equal(new DateTime(2026, 11, 19, 22, 45, 0), config.FechaActualizacionLocal.Value);
        Assert.Equal("19/11/2026 22:45", config.FechaActualizacionLocal.Value.ToString("dd/MM/yyyy HH:mm"));
    }
}
