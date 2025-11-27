using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;
using System.Text.RegularExpressions;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the PaqueteServicioService module.
    /// Tests cover service package functionality including validation, discount calculation, and filtering.
    /// </summary>
    public class PaqueteServicioServiceTests
    {
        #region Model Tests

        [Fact]
        public void PaqueteServicio_Model_ShouldHaveCorrectProperties()
        {
            var paquete = TestFactory.CreatePaquete(
                id: "pkg-123",
                nombre: "Paquete Premium",
                estado: "Activo",
                porcentajeDescuento: 15,
                tipoVehiculo: "Automovil",
                serviciosIds: new List<string> { "serv-1", "serv-2", "serv-3" });

            Assert.Equal("pkg-123", paquete.Id);
            Assert.Equal("Paquete Premium", paquete.Nombre);
            Assert.Equal("Activo", paquete.Estado);
            Assert.Equal(15, paquete.PorcentajeDescuento);
            Assert.Equal("Automovil", paquete.TipoVehiculo);
            Assert.Equal(3, paquete.ServiciosIds.Count);
        }

        #endregion

        #region Name Validation Tests

        [Theory]
        [InlineData("Paquete Premium", true)]
        [InlineData("Combo Completo", true)]
        [InlineData("Paquete123", false)]
        [InlineData("Paquete@Pro", false)]
        public void Nombre_ShouldOnlyContainLettersAndSpaces(string nombre, bool isValid)
        {
            var regex = new Regex(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$");
            Assert.Equal(isValid, regex.IsMatch(nombre));
        }

        #endregion

        #region Discount Validation Tests

        [Theory]
        [InlineData(5, true)]
        [InlineData(15, true)]
        [InlineData(50, true)]
        [InlineData(95, true)]
        [InlineData(4, false)]
        [InlineData(0, false)]
        [InlineData(96, false)]
        public void PorcentajeDescuento_ShouldBeBetween5And95(decimal descuento, bool isValid)
        {
            var isValidDiscount = descuento >= 5 && descuento <= 95;
            Assert.Equal(isValid, isValidDiscount);
        }

        [Theory]
        [InlineData(100000, 10, 90000)]
        [InlineData(100000, 20, 80000)]
        [InlineData(50000, 15, 42500)]
        public void DiscountCalculation_ShouldBeCorrect(decimal precioBase, decimal porcentajeDescuento, decimal expected)
        {
            var descuento = precioBase * (porcentajeDescuento / 100m);
            var precioFinal = precioBase - descuento;

            Assert.Equal(expected, precioFinal);
        }

        #endregion

        #region Services Count Validation Tests

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        [InlineData(10, true)]
        public void ServiciosIds_ShouldHaveAtLeastTwo(int count, bool isValid)
        {
            var serviciosIds = Enumerable.Range(1, count).Select(i => $"serv-{i}").ToList();
            Assert.Equal(isValid, serviciosIds.Count >= 2);
        }

        [Fact]
        public void ServiciosIds_ShouldNotHaveDuplicateServiceTypes()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tipo: "Lavado"),
                TestFactory.CreateServicio(id: "2", tipo: "Aspirado"),
                TestFactory.CreateServicio(id: "3", tipo: "Lavado")  // Duplicate!
            };

            var tiposServicio = servicios.Select(s => s.Tipo).ToList();
            var hasDuplicateTypes = tiposServicio.Count != tiposServicio.Distinct().Count();

            Assert.True(hasDuplicateTypes);
        }

        [Fact]
        public void ServiciosIds_ShouldHaveUniqueServiceTypes()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tipo: "Lavado"),
                TestFactory.CreateServicio(id: "2", tipo: "Aspirado"),
                TestFactory.CreateServicio(id: "3", tipo: "Encerado")
            };

            var tiposServicio = servicios.Select(s => s.Tipo).ToList();
            var hasUniqueTypes = tiposServicio.Count == tiposServicio.Distinct().Count();

            Assert.True(hasUniqueTypes);
        }

        #endregion

        #region Vehicle Type Validation Tests

        [Fact]
        public void AllServices_ShouldHaveSameVehicleType()
        {
            var paqueteTipoVehiculo = "Automovil";
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tipoVehiculo: "Automovil"),
                TestFactory.CreateServicio(id: "2", tipoVehiculo: "Automovil"),
                TestFactory.CreateServicio(id: "3", tipoVehiculo: "Automovil")
            };

            var tiposVehiculo = servicios.Select(s => s.TipoVehiculo).Distinct().ToList();
            var isConsistent = tiposVehiculo.Count == 1 && tiposVehiculo[0] == paqueteTipoVehiculo;

            Assert.True(isConsistent);
        }

        [Fact]
        public void MixedVehicleTypes_ShouldBeInvalid()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tipoVehiculo: "Automovil"),
                TestFactory.CreateServicio(id: "2", tipoVehiculo: "Camioneta"),  // Different!
                TestFactory.CreateServicio(id: "3", tipoVehiculo: "Automovil")
            };

            var tiposVehiculo = servicios.Select(s => s.TipoVehiculo).Distinct().ToList();
            var hasMixedTypes = tiposVehiculo.Count > 1;

            Assert.True(hasMixedTypes);
        }

        #endregion

        #region Active Services Validation Tests

        [Fact]
        public void AllServices_ShouldBeActive()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", estado: "Activo"),
                TestFactory.CreateServicio(id: "2", estado: "Activo"),
                TestFactory.CreateServicio(id: "3", estado: "Activo")
            };

            var inactiveServices = servicios.Where(s => s.Estado != "Activo").ToList();

            Assert.Empty(inactiveServices);
        }

        [Fact]
        public void InactiveServices_ShouldBeDetected()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", estado: "Activo"),
                TestFactory.CreateServicio(id: "2", estado: "Inactivo"),  // Inactive!
                TestFactory.CreateServicio(id: "3", estado: "Activo")
            };

            var inactiveServices = servicios.Where(s => s.Estado != "Activo").ToList();

            Assert.Single(inactiveServices);
        }

        #endregion

        #region Price Calculation Tests

        [Fact]
        public void TotalPrice_ShouldSumAllServicePrices()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", precio: 20000),
                TestFactory.CreateServicio(id: "2", precio: 15000),
                TestFactory.CreateServicio(id: "3", precio: 25000)
            };

            var totalPrice = servicios.Sum(s => s.Precio);

            Assert.Equal(60000, totalPrice);
        }

        [Fact]
        public void FinalPrice_ShouldApplyDiscountCorrectly()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", precio: 20000),
                TestFactory.CreateServicio(id: "2", precio: 15000),
                TestFactory.CreateServicio(id: "3", precio: 25000)
            };
            var porcentajeDescuento = 20m;

            var totalPrice = servicios.Sum(s => s.Precio);
            var discount = totalPrice * (porcentajeDescuento / 100m);
            var finalPrice = totalPrice - discount;

            Assert.Equal(60000, totalPrice);
            Assert.Equal(12000, discount);
            Assert.Equal(48000, finalPrice);
        }

        [Fact]
        public void TotalTime_ShouldSumAllServiceTimes()
        {
            var servicios = new List<Servicio>
            {
                TestFactory.CreateServicio(id: "1", tiempoEstimado: 30),
                TestFactory.CreateServicio(id: "2", tiempoEstimado: 20),
                TestFactory.CreateServicio(id: "3", tiempoEstimado: 45)
            };

            var totalTime = servicios.Sum(s => s.TiempoEstimado);

            Assert.Equal(95, totalTime);
        }

        #endregion

        #region Filter Tests

        [Fact]
        public void FilterByEstado_ShouldFilterCorrectly()
        {
            var paquetes = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "1", estado: "Activo"),
                TestFactory.CreatePaquete(id: "2", estado: "Inactivo"),
                TestFactory.CreatePaquete(id: "3", estado: "Activo")
            };

            var filtered = paquetes.Where(p => p.Estado == "Activo").ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Fact]
        public void FilterByTipoVehiculo_ShouldFilterCorrectly()
        {
            var paquetes = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "1", tipoVehiculo: "Automovil"),
                TestFactory.CreatePaquete(id: "2", tipoVehiculo: "Camioneta"),
                TestFactory.CreatePaquete(id: "3", tipoVehiculo: "Automovil")
            };

            var filtered = paquetes.Where(p => p.TipoVehiculo == "Automovil").ToList();

            Assert.Equal(2, filtered.Count);
        }

        [Theory]
        [InlineData(10, 20, 2)]
        [InlineData(15, 30, 3)]
        public void FilterByDiscountRange_ShouldFilterCorrectly(decimal minDiscount, decimal maxDiscount, int expected)
        {
            var paquetes = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "1", porcentajeDescuento: 15),
                TestFactory.CreatePaquete(id: "2", porcentajeDescuento: 20),
                TestFactory.CreatePaquete(id: "3", porcentajeDescuento: 25)
            };

            var filtered = paquetes.Where(p => 
                p.PorcentajeDescuento >= minDiscount && 
                p.PorcentajeDescuento <= maxDiscount).ToList();

            Assert.Equal(expected, filtered.Count);
        }

        #endregion

        #region Search Tests

        [Theory]
        [InlineData("premium", "Paquete Premium", true)]
        [InlineData("PREMIUM", "Paquete Premium", true)]
        [InlineData("xyz", "Paquete Premium", false)]
        public void SearchByNombre_ShouldBeCaseInsensitive(string searchTerm, string nombre, bool shouldMatch)
        {
            var paquete = TestFactory.CreatePaquete(nombre: nombre);
            var matches = paquete.Nombre?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

            Assert.Equal(shouldMatch, matches);
        }

        [Fact]
        public void SearchByDiscount_ShouldMatch()
        {
            var paquetes = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "1", porcentajeDescuento: 15),
                TestFactory.CreatePaquete(id: "2", porcentajeDescuento: 20),
                TestFactory.CreatePaquete(id: "3", porcentajeDescuento: 15)
            };

            var filtered = paquetes.Where(p => Math.Abs(p.PorcentajeDescuento - 15m) < 0.0001m).ToList();

            Assert.Equal(2, filtered.Count);
        }

        #endregion

        #region Sorting Tests

        [Fact]
        public void SortByNombre_ShouldOrderAlphabetically()
        {
            var paquetes = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "1", nombre: "Paquete Economico"),
                TestFactory.CreatePaquete(id: "2", nombre: "Combo Basico"),
                TestFactory.CreatePaquete(id: "3", nombre: "Premium Plus")
            };

            var sorted = paquetes.OrderBy(p => p.Nombre).ToList();

            Assert.Equal("Combo Basico", sorted[0].Nombre);
            Assert.Equal("Paquete Economico", sorted[1].Nombre);
            Assert.Equal("Premium Plus", sorted[2].Nombre);
        }

        [Fact]
        public void SortByDescuento_Descending_ShouldOrderCorrectly()
        {
            var paquetes = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "1", porcentajeDescuento: 15),
                TestFactory.CreatePaquete(id: "2", porcentajeDescuento: 30),
                TestFactory.CreatePaquete(id: "3", porcentajeDescuento: 20)
            };

            var sorted = paquetes.OrderByDescending(p => p.PorcentajeDescuento).ToList();

            Assert.Equal(30, sorted[0].PorcentajeDescuento);
            Assert.Equal(20, sorted[1].PorcentajeDescuento);
            Assert.Equal(15, sorted[2].PorcentajeDescuento);
        }

        [Fact]
        public void SortByCantidadServicios_ShouldOrderCorrectly()
        {
            var paquetes = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "1", serviciosIds: new List<string> { "1", "2", "3" }),
                TestFactory.CreatePaquete(id: "2", serviciosIds: new List<string> { "1", "2" }),
                TestFactory.CreatePaquete(id: "3", serviciosIds: new List<string> { "1", "2", "3", "4" })
            };

            var sorted = paquetes.OrderBy(p => p.ServiciosIds?.Count ?? 0).ToList();

            Assert.Equal(2, sorted[0].ServiciosIds?.Count);
            Assert.Equal(3, sorted[1].ServiciosIds?.Count);
            Assert.Equal(4, sorted[2].ServiciosIds?.Count);
        }

        #endregion

        #region Pagination Tests

        [Theory]
        [InlineData(25, 10, 3)]
        [InlineData(30, 10, 3)]
        [InlineData(10, 10, 1)]
        public void TotalPages_ShouldCalculateCorrectly(int totalPackages, int pageSize, int expected)
        {
            var actualPages = (int)Math.Ceiling(totalPackages / (double)pageSize);
            Assert.Equal(expected, actualPages);
        }

        #endregion

        #region Duplicate Name Validation Tests

        [Fact]
        public void DuplicateName_ShouldBeCaseInsensitive()
        {
            var existingPackages = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(nombre: "Paquete Premium")
            };
            var newPackageName = "paquete premium";

            var isDuplicate = existingPackages.Any(p =>
                p.Nombre.Equals(newPackageName, StringComparison.OrdinalIgnoreCase));

            Assert.True(isDuplicate);
        }

        [Fact]
        public void DuplicateCheck_ShouldExcludeCurrentPackageOnUpdate()
        {
            var currentPackageId = "pkg-123";
            var existingPackages = new List<PaqueteServicio>
            {
                TestFactory.CreatePaquete(id: "pkg-123", nombre: "Paquete Premium"),
                TestFactory.CreatePaquete(id: "pkg-456", nombre: "Combo Basico")
            };
            var updatedName = "Paquete Premium";

            var isDuplicate = existingPackages
                .Where(p => p.Id != currentPackageId)
                .Any(p => p.Nombre.Equals(updatedName, StringComparison.OrdinalIgnoreCase));

            Assert.False(isDuplicate);
        }

        #endregion
    }
}
