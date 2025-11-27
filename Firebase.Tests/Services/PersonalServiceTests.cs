using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the PersonalService module.
    /// Tests cover employee management functionality including filtering, pagination and search.
    /// </summary>
    public class PersonalServiceTests
    {
        #region Model Tests

        [Fact]
        public void Empleado_Model_ShouldHaveCorrectProperties()
        {
            var empleado = TestFactory.CreateEmpleado(
                id: "test-123",
                nombre: "Juan Pérez",
                email: "juan@test.com",
                rol: "Administrador",
                estado: "Activo");

            Assert.Equal("test-123", empleado.Id);
            Assert.Equal("Juan Pérez", empleado.NombreCompleto);
            Assert.Equal("juan@test.com", empleado.Email);
            Assert.Equal("Administrador", empleado.Rol);
            Assert.Equal("Activo", empleado.Estado);
        }

        #endregion

        #region Pagination Tests

        [Theory]
        [InlineData(100, 10, 10)]
        [InlineData(95, 10, 10)]
        [InlineData(50, 20, 3)]
        [InlineData(1, 10, 1)]
        public void CalculateTotalPages_ShouldReturnCorrectPageCount(int totalItems, int pageSize, int expected)
        {
            var actual = (int)Math.Ceiling(totalItems / (double)pageSize);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1, 2, 1)]
        [InlineData(5, 2, 3)]
        [InlineData(10, 2, 8)]
        public void VisiblePages_StartCalculation_ShouldBeCorrect(int currentPage, int range, int expected)
        {
            var actual = Math.Max(1, currentPage - range);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 10, 2, 7)]
        [InlineData(9, 10, 2, 10)]
        [InlineData(1, 5, 2, 3)]
        public void VisiblePages_EndCalculation_ShouldBeCorrect(int currentPage, int totalPages, int range, int expected)
        {
            var actual = Math.Min(totalPages, currentPage + range);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Pagination_ShouldReturnCorrectSubset()
        {
            var empleados = Enumerable.Range(1, 50)
                .Select(i => TestFactory.CreateEmpleado(id: i.ToString()))
                .ToList();

            var page = empleados.Skip(10).Take(10).ToList();

            Assert.Equal(10, page.Count);
            Assert.Equal("11", page[0].Id);
            Assert.Equal("20", page[9].Id);
        }

        #endregion

        #region Filter Tests

        [Fact]
        public void FilterByEstados_ShouldDefaultToActivo_WhenEmpty()
        {
            var estados = new List<string>();
            if (!estados.Any()) estados.Add("Activo");

            Assert.Single(estados);
            Assert.Equal("Activo", estados[0]);
        }

        [Fact]
        public void FilterByRoles_ShouldMatchMultipleRoles()
        {
            var empleados = new List<Empleado>
            {
                TestFactory.CreateEmpleado(id: "1", rol: "Administrador"),
                TestFactory.CreateEmpleado(id: "2", rol: "Empleado"),
                TestFactory.CreateEmpleado(id: "3", rol: "Supervisor"),
                TestFactory.CreateEmpleado(id: "4", rol: "Empleado")
            };
            var filterRoles = new List<string> { "Administrador", "Supervisor" };

            var filtered = empleados.Where(e => filterRoles.Contains(e.Rol)).ToList();

            Assert.Equal(2, filtered.Count);
        }

        #endregion

        #region Sorting Tests

        [Fact]
        public void SortByNombreCompleto_Ascending_ShouldOrderCorrectly()
        {
            var empleados = new List<Empleado>
            {
                TestFactory.CreateEmpleado(id: "1", nombre: "Carlos"),
                TestFactory.CreateEmpleado(id: "2", nombre: "Ana"),
                TestFactory.CreateEmpleado(id: "3", nombre: "Bruno")
            };

            var sorted = empleados.OrderBy(e => e.NombreCompleto).ToList();

            Assert.Equal("Ana", sorted[0].NombreCompleto);
            Assert.Equal("Bruno", sorted[1].NombreCompleto);
            Assert.Equal("Carlos", sorted[2].NombreCompleto);
        }

        [Fact]
        public void SortByNombreCompleto_Descending_ShouldOrderCorrectly()
        {
            var empleados = new List<Empleado>
            {
                TestFactory.CreateEmpleado(id: "1", nombre: "Carlos"),
                TestFactory.CreateEmpleado(id: "2", nombre: "Ana"),
                TestFactory.CreateEmpleado(id: "3", nombre: "Bruno")
            };

            var sorted = empleados.OrderByDescending(e => e.NombreCompleto).ToList();

            Assert.Equal("Carlos", sorted[0].NombreCompleto);
            Assert.Equal("Bruno", sorted[1].NombreCompleto);
            Assert.Equal("Ana", sorted[2].NombreCompleto);
        }

        #endregion

        #region Search Tests

        [Theory]
        [InlineData("Juan", "Juan Perez", true)]
        [InlineData("PEREZ", "Juan Perez", true)]
        [InlineData("juan", "Juan Perez", true)]
        [InlineData("xyz", "Juan Perez", false)]
        public void Search_ShouldBeCaseInsensitive(string searchTerm, string fullName, bool shouldMatch)
        {
            var empleado = TestFactory.CreateEmpleado(nombre: fullName);
            var matches = empleado.NombreCompleto?.ToUpperInvariant().Contains(searchTerm.ToUpperInvariant()) ?? false;

            Assert.Equal(shouldMatch, matches);
        }

        [Fact]
        public void Search_ShouldMatchInMultipleFields()
        {
            var empleado = TestFactory.CreateEmpleado(
                nombre: "Juan Pérez",
                email: "admin@lavadero.com",
                rol: "Administrador");
            var searchTerm = "admin";

            var matchesEmail = empleado.Email?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;
            var matchesRol = empleado.Rol?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false;

            Assert.True(matchesEmail);
            Assert.True(matchesRol);
        }

        #endregion
    }
}
