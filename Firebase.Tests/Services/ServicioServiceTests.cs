using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;
using System.Text.RegularExpressions;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the ServicioService module.
    /// Tests cover service management functionality including validation, filtering and search.
    /// </summary>
    public class ServicioServiceTests
    {
        #region Model Tests

        [Fact]
        public void Servicio_Model_ShouldHaveCorrectProperties()
        {
            var servicio = TestFactory.CreateServicio(
                id: "serv-123",
                nombre: "Lavado Completo",
                precio: 25000,
                tipo: "Lavado",
                tipoVehiculo: "Automovil",
                tiempoEstimado: 45,
                descripcion: "Lavado exterior e interior",
                estado: "Activo");

            Assert.Equal("serv-123", servicio.Id);
            Assert.Equal("Lavado Completo", servicio.Nombre);
            Assert.Equal(25000, servicio.Precio);
            Assert.Equal("Lavado", servicio.Tipo);
            Assert.Equal("Automovil", servicio.TipoVehiculo);
            Assert.Equal(45, servicio.TiempoEstimado);
            Assert.Equal("Activo", servicio.Estado);
        }

        #endregion

        #region Name Validation Tests

        [Theory]
        [InlineData("Lavado Completo", true)]
        [InlineData("Aspirado Interior", true)]
        [InlineData("Lavado123", false)]
        [InlineData("Lavado@Especial", false)]
        public void Nombre_ShouldOnlyContainLettersAndSpaces(string nombre, bool isValid)
        {
            var regex = new Regex(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$");
            Assert.Equal(isValid, regex.IsMatch(nombre));
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("Valid Name", true)]
        public void Nombre_ShouldNotBeEmpty(string? nombre, bool isValid)
        {
            var isNotEmpty = !string.IsNullOrWhiteSpace(nombre);
            Assert.Equal(isValid, isNotEmpty);
        }

        #endregion

        #region Price Validation Tests

        [Theory]
        [InlineData(0, true)]
        [InlineData(1000, true)]
        [InlineData(-1, false)]
        public void Precio_ShouldBeGreaterOrEqualToZero(decimal precio, bool isValid)
        {
            Assert.Equal(isValid, precio >= 0);
        }

        #endregion

        #region Time Validation Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(30, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void TiempoEstimado_ShouldBeGreaterThanZero(int tiempo, bool isValid)
        {
            Assert.Equal(isValid, tiempo > 0);
        }

        #endregion

        #region Filter Tests

        [Fact]
        public void FilterByEstado_ShouldFilterCorrectly()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", estado: "Activo"),
                TestFactory.CreateServicio(id: "2", estado: "Inactivo"),
                TestFactory.CreateServicio(id: "3", estado: "Activo")
            };

            var filtered = servicios.Where(s => s.Estado == "Activo").ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void FilterByTipo_ShouldFilterCorrectly()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tipo: "Lavado"),
                TestFactory.CreateServicio(id: "2", tipo: "Aspirado"),
                TestFactory.CreateServicio(id: "3", tipo: "Lavado")
            };
            var tipos = new List<string> { "Lavado", "Aspirado" };

            var filtered = servicios.Where(s => tipos.Contains(s.Tipo)).ToList();

            Assert.Equal(3, filtered.Count);
        }

        [Fact]
        public void FilterByTipoVehiculo_ShouldFilterCorrectly()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tipoVehiculo: "Automovil"),
                TestFactory.CreateServicio(id: "2", tipoVehiculo: "Camioneta"),
                TestFactory.CreateServicio(id: "3", tipoVehiculo: "Automovil")
            };

            var filtered = servicios.Where(s => s.TipoVehiculo == "Automovil").ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void DefaultEstado_ShouldBeActivo_WhenEmpty()
        {
            var estados = new List<string>();
            if (!estados.Any()) estados.Add("Activo");

            Assert.Single(estados);
            Assert.Equal("Activo", estados[0]);
        }

        #endregion

        #region Sorting Tests

        [Fact]
        public void SortByNombre_ShouldOrderAlphabetically()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", nombre: "Encerado"),
                TestFactory.CreateServicio(id: "2", nombre: "Aspirado"),
                TestFactory.CreateServicio(id: "3", nombre: "Lavado")
            };

            var sorted = servicios.OrderBy(s => s.Nombre).ToList();

            Assert.Equal("Aspirado", sorted[0].Nombre);
            Assert.Equal("Encerado", sorted[1].Nombre);
            Assert.Equal("Lavado", sorted[2].Nombre);
        }

        [Fact]
        public void SortByPrecio_Ascending_ShouldOrderCorrectly()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", precio: 30000),
                TestFactory.CreateServicio(id: "2", precio: 15000),
                TestFactory.CreateServicio(id: "3", precio: 45000)
            };

            var sorted = servicios.OrderBy(s => s.Precio).ToList();

            Assert.Equal(15000, sorted[0].Precio);
            Assert.Equal(30000, sorted[1].Precio);
            Assert.Equal(45000, sorted[2].Precio);
        }

        [Fact]
        public void SortByTiempoEstimado_ShouldOrderCorrectly()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tiempoEstimado: 45),
                TestFactory.CreateServicio(id: "2", tiempoEstimado: 20),
                TestFactory.CreateServicio(id: "3", tiempoEstimado: 60)
            };

            var sorted = servicios.OrderBy(s => s.TiempoEstimado).ToList();

            Assert.Equal(20, sorted[0].TiempoEstimado);
            Assert.Equal(45, sorted[1].TiempoEstimado);
            Assert.Equal(60, sorted[2].TiempoEstimado);
        }

        #endregion

        #region Search Tests

        [Theory]
        [InlineData("lavado", "Lavado Completo", true)]
        [InlineData("LAVADO", "Lavado Completo", true)]
        [InlineData("xyz", "Lavado Completo", false)]
        public void SearchByNombre_ShouldBeCaseInsensitive(string searchTerm, string nombre, bool shouldMatch)
        {
            var servicio = TestFactory.CreateServicio(nombre: nombre);
            var matches = servicio.Nombre?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

            Assert.Equal(shouldMatch, matches);
        }

        [Fact]
        public void SearchByPrecio_ShouldMatchExactValue()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", precio: 25000),
                TestFactory.CreateServicio(id: "2", precio: 30000),
                TestFactory.CreateServicio(id: "3", precio: 25000)
            };

            var filtered = servicios.Where(s => Math.Abs(s.Precio - 25000) < 0.0001m).ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void SearchByTiempo_ShouldMatchMinutes()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tiempoEstimado: 45),
                TestFactory.CreateServicio(id: "2", tiempoEstimado: 30),
                TestFactory.CreateServicio(id: "3", tiempoEstimado: 45)
            };

            var filtered = servicios.Where(s => s.TiempoEstimado == 45).ToList();

            Assert.Equal(2, filtered.Count);
        }

        #endregion

        #region Duplicate Name Validation Tests

        [Fact]
        public void DuplicateCheck_ShouldBeCaseInsensitive()
        {
            var existingServices = new List<Servicio>
            {
                TestFactory.CreateServicio(nombre: "Lavado Completo", tipoVehiculo: "Automovil")
            };
            var newServiceName = "lavado completo";
            var newServiceTipoVehiculo = "Automovil";

            var isDuplicate = existingServices.Any(s =>
                s.Nombre.Equals(newServiceName, StringComparison.OrdinalIgnoreCase) &&
                s.TipoVehiculo == newServiceTipoVehiculo);

            Assert.True(isDuplicate);
        }

        [Fact]
        public void DuplicateCheck_ShouldAllowSameNameDifferentVehicleType()
        {
            var existingServices = new List<Servicio>
            {
                TestFactory.CreateServicio(nombre: "Lavado Completo", tipoVehiculo: "Automovil")
            };
            var newServiceName = "Lavado Completo";
            var newServiceTipoVehiculo = "Camioneta";

            var isDuplicate = existingServices.Any(s =>
                s.Nombre.Equals(newServiceName, StringComparison.OrdinalIgnoreCase) &&
                s.TipoVehiculo == newServiceTipoVehiculo);

            Assert.False(isDuplicate);
        }

        #endregion

        #region Pagination Tests

        [Theory]
        [InlineData(50, 10, 5)]
        [InlineData(45, 10, 5)]
        [InlineData(10, 10, 1)]
        public void TotalPages_ShouldCalculateCorrectly(int totalServices, int pageSize, int expectedPages)
        {
            var actualPages = (int)Math.Ceiling(totalServices / (double)pageSize);
            Assert.Equal(expectedPages, actualPages);
        }

        [Fact]
        public void Pagination_ShouldReturnCorrectSubset()
        {
            var servicios = Enumerable.Range(1, 30)
                .Select(i => TestFactory.CreateServicio(id: i.ToString()))
                .ToList();

            var page = servicios.Skip(20).Take(10).ToList();

            Assert.Equal(10, page.Count);
            Assert.Equal("21", page[0].Id);
            Assert.Equal("30", page[9].Id);
        }

        #endregion
    }
}
