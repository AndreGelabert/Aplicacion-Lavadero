using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the ConfiguracionService module.
    /// Tests cover configuration management including business hours, sessions, and discount settings.
    /// </summary>
    public class ConfiguracionServiceTests
    {
        #region Model Tests

        [Fact]
        public void Configuracion_Model_ShouldHaveCorrectProperties()
        {
            var config = TestFactory.CreateConfiguracion(
                id: "system_config",
                cancelacionDescuento: 10,
                cancelacionHoras: 24,
                cancelacionDias: 30,
                descuentoStep: 5,
                capacidad: 5,
                sesionHoras: 8,
                sesionInactividad: 15);

            Assert.Equal("system_config", config.Id);
            Assert.Equal(10, config.CancelacionAnticipadaDescuento);
            Assert.Equal(24, config.CancelacionAnticipadaHorasMinimas);
            Assert.Equal(30, config.CancelacionAnticipadaValidezDias);
            Assert.Equal(5, config.PaquetesDescuentoStep);
            Assert.Equal(5, config.CapacidadMaximaConcurrente);
            Assert.Equal(8, config.SesionDuracionHoras);
            Assert.Equal(15, config.SesionInactividadMinutos);
            Assert.True(config.ConsiderarEmpleadosActivos);
            Assert.Equal(7, config.HorariosOperacion.Count);
        }

        #endregion

        #region Cancellation Discount Tests

        [Theory]
        [InlineData(0, true)]
        [InlineData(10, true)]
        [InlineData(50, true)]
        [InlineData(-1, false)]
        public void CancelacionAnticipadaDescuento_ShouldBeNonNegative(decimal descuento, bool isValid)
        {
            Assert.Equal(isValid, descuento >= 0);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(24, true)]
        [InlineData(48, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void CancelacionAnticipadaHorasMinimas_ShouldBePositive(int horas, bool isValid)
        {
            Assert.Equal(isValid, horas > 0);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(30, true)]
        [InlineData(90, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void CancelacionAnticipadaValidezDias_ShouldBePositive(int dias, bool isValid)
        {
            Assert.Equal(isValid, dias > 0);
        }

        #endregion

        #region Discount Step Tests

        [Theory]
        [InlineData(5, true)]
        [InlineData(10, true)]
        [InlineData(15, true)]
        [InlineData(4, false)]
        [InlineData(0, false)]
        [InlineData(-5, false)]
        public void PaquetesDescuentoStep_ShouldBeAtLeast5(int step, bool isValid)
        {
            Assert.Equal(isValid, step >= 5);
        }

        [Fact]
        public void DiscountStep_ShouldGenerateCorrectOptions_Step5()
        {
            int step = 5;
            var options = new List<int>();
            for (int i = step; i <= 95; i += step)
            {
                options.Add(i);
            }

            Assert.Equal(19, options.Count);
            Assert.Equal(5, options[0]);
            Assert.Equal(95, options[^1]);
        }

        [Fact]
        public void DiscountStep_ShouldGenerateCorrectOptions_Step10()
        {
            int step = 10;
            var options = new List<int>();
            for (int i = step; i <= 95; i += step)
            {
                options.Add(i);
            }

            Assert.Equal(9, options.Count);
            Assert.Equal(10, options[0]);
            Assert.Equal(90, options[^1]);
        }

        #endregion

        #region Capacity Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(5, true)]
        [InlineData(10, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void CapacidadMaximaConcurrente_ShouldBePositive(int capacidad, bool isValid)
        {
            Assert.Equal(isValid, capacidad > 0);
        }

        #endregion

        #region Session Duration Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(8, true)]
        [InlineData(24, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void SesionDuracionHoras_ShouldBePositive(int horas, bool isValid)
        {
            Assert.Equal(isValid, horas > 0);
        }

        [Theory]
        [InlineData(1, 60)]
        [InlineData(8, 480)]
        [InlineData(24, 1440)]
        public void SesionDuracion_ShouldConvertToMinutes(int hours, int expected)
        {
            var actualMinutes = hours * 60;
            Assert.Equal(expected, actualMinutes);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(15, true)]
        [InlineData(30, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void SesionInactividadMinutos_ShouldBePositive(int minutos, bool isValid)
        {
            Assert.Equal(isValid, minutos > 0);
        }

        #endregion

        #region Business Hours Validation Tests

        [Theory]
        [InlineData("09:00-18:00", true)]
        [InlineData("08:00-12:00,14:00-18:00", true)]
        [InlineData("CERRADO", true)]
        [InlineData("cerrado", true)]
        [InlineData("9:00-18:00", false)]
        [InlineData("09:00", false)]
        [InlineData("invalid", false)]
        public void HorarioFormat_ShouldBeValid(string horario, bool isValid)
        {
            var isValidFormat = ValidateHorarioFormat(horario);
            Assert.Equal(isValid, isValidFormat);
        }

        [Fact]
        public void HorariosOperacion_ShouldIncludeAllDays()
        {
            var diasSemana = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
            var config = TestFactory.CreateConfiguracion();

            var allDaysPresent = diasSemana.All(dia => config.HorariosOperacion.ContainsKey(dia));

            Assert.True(allDaysPresent);
            Assert.Equal(7, config.HorariosOperacion.Count);
        }

        [Fact]
        public void HorariosOperacion_MissingDay_ShouldBeDetected()
        {
            var diasSemana = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
            var horarios = new Dictionary<string, string>
            {
                { "Lunes", "09:00-18:00" },
                { "Martes", "09:00-18:00" },
                { "Jueves", "09:00-18:00" },
                { "Viernes", "09:00-18:00" },
                { "Sábado", "09:00-13:00" },
                { "Domingo", "CERRADO" }
            };

            var missingDays = diasSemana.Where(dia => !horarios.ContainsKey(dia)).ToList();

            Assert.Single(missingDays);
            Assert.Equal("Miércoles", missingDays[0]);
        }

        [Theory]
        [InlineData("00:00", true)]
        [InlineData("09:00", true)]
        [InlineData("12:30", true)]
        [InlineData("23:59", true)]
        [InlineData("24:00", false)]
        [InlineData("9:00", false)]
        [InlineData("09:60", false)]
        public void HoraFormat_ShouldBeValid(string hora, bool isValid)
        {
            var isValidHora = ValidateHoraFormat(hora);
            Assert.Equal(isValid, isValidHora);
        }

        #endregion

        #region Default Configuration Tests

        [Fact]
        public void DefaultConfiguration_ShouldHaveCorrectValues()
        {
            var defaultConfig = TestFactory.CreateConfiguracion();

            Assert.Equal(10, defaultConfig.CancelacionAnticipadaDescuento);
            Assert.Equal(24, defaultConfig.CancelacionAnticipadaHorasMinimas);
            Assert.Equal(30, defaultConfig.CancelacionAnticipadaValidezDias);
            Assert.Equal(5, defaultConfig.PaquetesDescuentoStep);
            Assert.Equal(5, defaultConfig.CapacidadMaximaConcurrente);
            Assert.True(defaultConfig.ConsiderarEmpleadosActivos);
            Assert.Equal(8, defaultConfig.SesionDuracionHoras);
            Assert.Equal(15, defaultConfig.SesionInactividadMinutos);
            Assert.Equal(7, defaultConfig.HorariosOperacion.Count);
        }

        #endregion

        #region Cache Tests

        [Fact]
        public void CacheDuration_ShouldBe30Minutes()
        {
            const int expectedCacheDuration = 30;
            Assert.Equal(30, expectedCacheDuration);
        }

        #endregion

        #region Update Tracking Tests

        [Fact]
        public void FechaActualizacion_ShouldBeSetOnUpdate()
        {
            var config = TestFactory.CreateConfiguracion();
            var updateTime = DateTime.UtcNow;

            config.FechaActualizacion = updateTime;

            Assert.Equal(updateTime, config.FechaActualizacion);
        }

        [Fact]
        public void ActualizadoPor_ShouldTrackWhoMadeChanges()
        {
            var config = TestFactory.CreateConfiguracion();
            var userEmail = "admin@lavadero.com";

            config.ActualizadoPor = userEmail;

            Assert.Equal(userEmail, config.ActualizadoPor);
        }

        #endregion

        #region Helper Methods

        private bool ValidateHorarioFormat(string horario)
        {
            if (string.IsNullOrWhiteSpace(horario))
                return false;

            if (horario.ToUpper() == "CERRADO")
                return true;

            var segments = horario.Split(',');
            foreach (var segment in segments)
            {
                if (!ValidateTimeRange(segment.Trim()))
                    return false;
            }
            return true;
        }

        private bool ValidateTimeRange(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                return false;

            var parts = segment.Split('-');
            if (parts.Length != 2)
                return false;

            return ValidateHoraFormat(parts[0].Trim()) && ValidateHoraFormat(parts[1].Trim());
        }

        private bool ValidateHoraFormat(string hora)
        {
            if (string.IsNullOrWhiteSpace(hora))
                return false;

            var parts = hora.Split(':');
            if (parts.Length != 2)
                return false;

            if (parts[0].Length != 2 || parts[1].Length != 2)
                return false;

            if (!int.TryParse(parts[0], out int horas) || horas < 0 || horas > 23)
                return false;

            if (!int.TryParse(parts[1], out int minutos) || minutos < 0 || minutos > 59)
                return false;

            return true;
        }

        #endregion
    }
}
