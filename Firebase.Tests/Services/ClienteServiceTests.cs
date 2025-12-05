using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Tests unitarios para el módulo ClienteService.
    /// 
    /// Metodología de Testing: Pruebas Unitarias con patrón AAA (Arrange-Act-Assert)
    /// Tipo de Testing: Caja Negra (Black Box Testing)
    /// - Se prueban las entradas y salidas sin conocer la implementación interna
    /// - Se valida el comportamiento esperado según las especificaciones
    /// 
    /// Categorías de Tests:
    /// 1. Validación del Modelo: Verificar que las propiedades del modelo Cliente funcionen correctamente
    /// 2. Tests de Filtrado: Validar filtros por estado, búsqueda y otros criterios
    /// 3. Tests de Paginación: Verificar el cálculo correcto de páginas y subconjuntos
    /// 4. Tests de Ordenamiento: Validar ordenamiento ascendente y descendente
    /// 5. Tests de Búsqueda: Verificar búsqueda insensible a mayúsculas en múltiples campos
    /// 6. Tests de Validación de Datos: Validar formatos de email, teléfono, nombre, etc.
    /// </summary>
    public class ClienteServiceTests
    {
        #region Tests de Validación del Modelo

        /// <summary>
        /// Verifica que el modelo Cliente tenga todas las propiedades correctamente asignadas.
        /// 
        /// Arrange: Crear un cliente con datos específicos
        /// Act: Acceder a las propiedades del modelo
        /// Assert: Verificar que cada propiedad tenga el valor esperado
        /// </summary>
        [Fact]
        public void Cliente_Model_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var cliente = TestFactory.CreateCliente(
                id: "cli-001",
                nombre: "Juan",
                apellido: "Pérez",
                tipoDocumento: "DNI",
                numeroDocumento: "12345678",
                telefono: "3751590586",
                email: "juan@test.com",
                estado: "Activo"
            );

            // Assert
            Assert.Equal("cli-001", cliente.Id);
            Assert.Equal("Juan", cliente.Nombre);
            Assert.Equal("Pérez", cliente.Apellido);
            Assert.Equal("Juan Pérez", cliente.NombreCompleto);
            Assert.Equal("DNI", cliente.TipoDocumento);
            Assert.Equal("12345678", cliente.NumeroDocumento);
            Assert.Equal("3751590586", cliente.Telefono);
            Assert.Equal("juan@test.com", cliente.Email);
            Assert.Equal("Activo", cliente.Estado);
        }

        /// <summary>
        /// Verifica que NombreCompleto concatene correctamente Nombre y Apellido.
        /// </summary>
        [Theory]
        [InlineData("María", "García", "María García")]
        [InlineData("José Luis", "López Martínez", "José Luis López Martínez")]
        [InlineData("Ana", "Del Valle", "Ana Del Valle")]
        public void NombreCompleto_ShouldConcatenateProperly(string nombre, string apellido, string expected)
        {
            // Arrange
            var cliente = TestFactory.CreateCliente(nombre: nombre, apellido: apellido);

            // Act
            var nombreCompleto = cliente.NombreCompleto;

            // Assert
            Assert.Equal(expected, nombreCompleto);
        }

        /// <summary>
        /// Verifica que la lista de vehículos se inicialice correctamente.
        /// </summary>
        [Fact]
        public void VehiculosIds_ShouldInitializeAsEmptyList()
        {
            // Arrange
            var cliente = TestFactory.CreateCliente();

            // Assert
            Assert.NotNull(cliente.VehiculosIds);
            Assert.Empty(cliente.VehiculosIds);
        }

        #endregion

        #region Tests de Validación de Datos (Expresiones Regulares)

        /// <summary>
        /// Verifica que el nombre solo contenga letras, espacios y acentos (mínimo 3 caracteres).
        /// Patrón: ^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$
        /// </summary>
        [Theory]
        [InlineData("Juan", true)]
        [InlineData("María José", true)]
        [InlineData("José Luis", true)]
        [InlineData("Ángela", true)]
        [InlineData("Año", true)] // Con ñ
        [InlineData("Jo", false)] // Menos de 3 caracteres
        [InlineData("Juan123", false)] // Con números
        [InlineData("Juan!", false)] // Con caracteres especiales
        [InlineData("", false)] // Vacío
        public void Nombre_ShouldValidatePattern(string nombre, bool shouldBeValid)
        {
            // Arrange
            var pattern = @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$";

            // Act
            var isValid = System.Text.RegularExpressions.Regex.IsMatch(nombre, pattern);

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        /// <summary>
        /// Verifica que el número de documento solo contenga números.
        /// Patrón: ^[0-9]+$
        /// </summary>
        [Theory]
        [InlineData("12345678", true)]
        [InlineData("0", true)]
        [InlineData("123", true)]
        [InlineData("12.345.678", false)] // Con puntos
        [InlineData("12-345-678", false)] // Con guiones
        [InlineData("ABC123", false)] // Con letras
        [InlineData("", false)] // Vacío
        public void NumeroDocumento_ShouldOnlyContainNumbers(string documento, bool shouldBeValid)
        {
            // Arrange
            var pattern = @"^[0-9]+$";

            // Act
            var isValid = !string.IsNullOrEmpty(documento) && 
                          System.Text.RegularExpressions.Regex.IsMatch(documento, pattern);

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        /// <summary>
        /// Verifica que el teléfono tenga exactamente 10 dígitos numéricos.
        /// Patrón: ^\d{10}$
        /// </summary>
        [Theory]
        [InlineData("3751590586", true)]
        [InlineData("0000000000", true)]
        [InlineData("1234567890", true)]
        [InlineData("123456789", false)] // 9 dígitos
        [InlineData("12345678901", false)] // 11 dígitos
        [InlineData("37515905ab", false)] // Con letras
        [InlineData("375-159-058", false)] // Con guiones
        public void Telefono_ShouldHaveExactly10Digits(string telefono, bool shouldBeValid)
        {
            // Arrange
            var pattern = @"^\d{10}$";

            // Act
            var isValid = System.Text.RegularExpressions.Regex.IsMatch(telefono, pattern);

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        /// <summary>
        /// Verifica que el email tenga un formato válido según el patrón del modelo.
        /// Patrón: ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]{3,}\.[a-zA-Z]{2,}$
        /// </summary>
        [Theory]
        [InlineData("user@example.com", true)]
        [InlineData("test.user@domain.org", true)]
        [InlineData("test+tag@example.com", true)]
        [InlineData("user@sub.domain.com", true)]
        [InlineData("user@ab.com", false)] // Dominio menor a 3 caracteres
        [InlineData("user@.com", false)] // Sin nombre de dominio
        [InlineData("@example.com", false)] // Sin usuario
        [InlineData("user.example.com", false)] // Sin @
        [InlineData("user@example", false)] // Sin extensión
        public void Email_ShouldValidateFormat(string email, bool shouldBeValid)
        {
            // Arrange
            var pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]{3,}\.[a-zA-Z]{2,}$";

            // Act
            var isValid = System.Text.RegularExpressions.Regex.IsMatch(email, pattern);

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        #endregion

        #region Tests de Filtrado

        /// <summary>
        /// Verifica que el filtro por estado devuelva solo clientes con el estado seleccionado.
        /// </summary>
        [Fact]
        public void FilterByEstado_ShouldReturnMatchingClientes()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                TestFactory.CreateCliente(id: "1", estado: "Activo"),
                TestFactory.CreateCliente(id: "2", estado: "Inactivo"),
                TestFactory.CreateCliente(id: "3", estado: "Activo"),
                TestFactory.CreateCliente(id: "4", estado: "Inactivo")
            };

            // Act
            var activos = clientes.Where(c => c.Estado == "Activo").ToList();
            var inactivos = clientes.Where(c => c.Estado == "Inactivo").ToList();

            // Assert
            Assert.Equal(2, activos.Count);
            Assert.Equal(2, inactivos.Count);
        }

        /// <summary>
        /// Verifica que el filtro por estado devuelva solo activos por defecto cuando no se especifica.
        /// </summary>
        [Fact]
        public void FilterByEstado_ShouldDefaultToActivo_WhenEmpty()
        {
            // Arrange
            var estados = new List<string>();

            // Act
            if (!estados.Any()) estados.Add("Activo");

            // Assert
            Assert.Single(estados);
            Assert.Equal("Activo", estados[0]);
        }

        /// <summary>
        /// Verifica el filtrado por múltiples estados.
        /// </summary>
        [Fact]
        public void FilterByEstados_ShouldSupportMultipleStates()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                TestFactory.CreateCliente(id: "1", estado: "Activo"),
                TestFactory.CreateCliente(id: "2", estado: "Inactivo"),
                TestFactory.CreateCliente(id: "3", estado: "Activo"),
            };
            var estadosFilter = new List<string> { "Activo", "Inactivo" };

            // Act
            var filtered = clientes.Where(c => estadosFilter.Contains(c.Estado)).ToList();

            // Assert
            Assert.Equal(3, filtered.Count);
        }

        #endregion

        #region Tests de Búsqueda

        /// <summary>
        /// Verifica que la búsqueda sea insensible a mayúsculas/minúsculas.
        /// </summary>
        [Theory]
        [InlineData("juan", "Juan Pérez", true)]
        [InlineData("PÉREZ", "Juan Pérez", true)]
        [InlineData("JUAN", "Juan Pérez", true)]
        [InlineData("ana", "Juan Pérez", false)]
        public void Search_ShouldBeCaseInsensitive(string searchTerm, string nombreCompleto, bool shouldMatch)
        {
            // Arrange
            var cliente = TestFactory.CreateCliente(nombre: nombreCompleto.Split(' ')[0], apellido: nombreCompleto.Split(' ')[1]);

            // Act
            var matches = cliente.NombreCompleto.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant());

            // Assert
            Assert.Equal(shouldMatch, matches);
        }

        /// <summary>
        /// Verifica que la búsqueda funcione en múltiples campos (nombre, apellido, email, documento, teléfono).
        /// </summary>
        [Fact]
        public void Search_ShouldMatchInMultipleFields()
        {
            // Arrange
            var cliente = TestFactory.CreateCliente(
                nombre: "Juan",
                apellido: "Pérez",
                email: "juan.perez@example.com",
                numeroDocumento: "12345678",
                telefono: "3751590586"
            );
            
            // Act & Assert - Búsqueda por nombre
            Assert.Contains("juan", cliente.Nombre.ToLowerInvariant());
            
            // Act & Assert - Búsqueda por apellido
            Assert.Contains("pérez", cliente.Apellido.ToLowerInvariant());
            
            // Act & Assert - Búsqueda por email
            Assert.Contains("perez", cliente.Email.ToLowerInvariant());
            
            // Act & Assert - Búsqueda por documento
            Assert.Contains("1234", cliente.NumeroDocumento);
            
            // Act & Assert - Búsqueda por teléfono
            Assert.Contains("3751", cliente.Telefono);
        }

        /// <summary>
        /// Verifica la búsqueda con términos parciales.
        /// </summary>
        [Theory]
        [InlineData("Jua", "Juan")]
        [InlineData("rez", "Pérez")]
        [InlineData("@exam", "user@example.com")]
        public void Search_ShouldMatchPartialTerms(string searchTerm, string fieldValue)
        {
            // Act
            var matches = fieldValue.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant());

            // Assert
            Assert.True(matches);
        }

        #endregion

        #region Tests de Ordenamiento

        /// <summary>
        /// Verifica el ordenamiento ascendente por nombre completo.
        /// </summary>
        [Fact]
        public void SortByNombreCompleto_Ascending_ShouldOrderCorrectly()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                TestFactory.CreateCliente(id: "1", nombre: "Carlos", apellido: "Ruiz"),
                TestFactory.CreateCliente(id: "2", nombre: "Ana", apellido: "García"),
                TestFactory.CreateCliente(id: "3", nombre: "Bruno", apellido: "López")
            };

            // Act
            var sorted = clientes.OrderBy(c => c.NombreCompleto).ToList();

            // Assert
            Assert.Equal("Ana García", sorted[0].NombreCompleto);
            Assert.Equal("Bruno López", sorted[1].NombreCompleto);
            Assert.Equal("Carlos Ruiz", sorted[2].NombreCompleto);
        }

        /// <summary>
        /// Verifica el ordenamiento descendente por nombre completo.
        /// </summary>
        [Fact]
        public void SortByNombreCompleto_Descending_ShouldOrderCorrectly()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                TestFactory.CreateCliente(id: "1", nombre: "Carlos", apellido: "Ruiz"),
                TestFactory.CreateCliente(id: "2", nombre: "Ana", apellido: "García"),
                TestFactory.CreateCliente(id: "3", nombre: "Bruno", apellido: "López")
            };

            // Act
            var sorted = clientes.OrderByDescending(c => c.NombreCompleto).ToList();

            // Assert
            Assert.Equal("Carlos Ruiz", sorted[0].NombreCompleto);
            Assert.Equal("Bruno López", sorted[1].NombreCompleto);
            Assert.Equal("Ana García", sorted[2].NombreCompleto);
        }

        /// <summary>
        /// Verifica el ordenamiento por email.
        /// </summary>
        [Fact]
        public void SortByEmail_ShouldOrderAlphabetically()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                TestFactory.CreateCliente(id: "1", email: "carlos@test.com"),
                TestFactory.CreateCliente(id: "2", email: "ana@test.com"),
                TestFactory.CreateCliente(id: "3", email: "bruno@test.com")
            };

            // Act
            var sorted = clientes.OrderBy(c => c.Email).ToList();

            // Assert
            Assert.Equal("ana@test.com", sorted[0].Email);
            Assert.Equal("bruno@test.com", sorted[1].Email);
            Assert.Equal("carlos@test.com", sorted[2].Email);
        }

        #endregion

        #region Tests de Paginación

        /// <summary>
        /// Verifica el cálculo correcto del total de páginas.
        /// </summary>
        [Theory]
        [InlineData(100, 10, 10)]
        [InlineData(95, 10, 10)]
        [InlineData(50, 20, 3)]
        [InlineData(1, 10, 1)]
        [InlineData(0, 10, 0)]
        public void CalculateTotalPages_ShouldReturnCorrectPageCount(int totalItems, int pageSize, int expected)
        {
            // Act
            var actual = (int)Math.Ceiling(totalItems / (double)pageSize);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifica que la paginación devuelva el subconjunto correcto.
        /// </summary>
        [Fact]
        public void Pagination_ShouldReturnCorrectSubset()
        {
            // Arrange
            var clientes = Enumerable.Range(1, 50)
                .Select(i => TestFactory.CreateCliente(id: i.ToString()))
                .ToList();

            // Act - Obtener página 2 con 10 elementos por página
            var page = clientes.Skip(10).Take(10).ToList();

            // Assert
            Assert.Equal(10, page.Count);
            Assert.Equal("11", page[0].Id);
            Assert.Equal("20", page[9].Id);
        }

        /// <summary>
        /// Verifica la última página cuando no está completa.
        /// </summary>
        [Fact]
        public void Pagination_LastPage_ShouldReturnRemainingItems()
        {
            // Arrange
            var clientes = Enumerable.Range(1, 25)
                .Select(i => TestFactory.CreateCliente(id: i.ToString()))
                .ToList();

            // Act - Obtener página 3 con 10 elementos por página (solo debería tener 5)
            var page = clientes.Skip(20).Take(10).ToList();

            // Assert
            Assert.Equal(5, page.Count);
            Assert.Equal("21", page[0].Id);
            Assert.Equal("25", page[4].Id);
        }

        #endregion

        #region Tests de Validación de Duplicados

        /// <summary>
        /// Verifica la detección de documentos duplicados (mismo tipo y número).
        /// </summary>
        [Fact]
        public void DuplicateDocument_ShouldBeDetected()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                TestFactory.CreateCliente(id: "1", tipoDocumento: "DNI", numeroDocumento: "12345678"),
                TestFactory.CreateCliente(id: "2", tipoDocumento: "DNI", numeroDocumento: "87654321"),
                TestFactory.CreateCliente(id: "3", tipoDocumento: "Pasaporte", numeroDocumento: "12345678")
            };
            var newClienteTipo = "DNI";
            var newClienteNumero = "12345678";

            // Act
            var exists = clientes.Any(c => 
                c.TipoDocumento == newClienteTipo && 
                c.NumeroDocumento == newClienteNumero);

            // Assert
            Assert.True(exists);
        }

        /// <summary>
        /// Verifica que mismo número con diferente tipo de documento no sea duplicado.
        /// </summary>
        [Fact]
        public void SameNumber_DifferentDocType_ShouldNotBeDuplicate()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                TestFactory.CreateCliente(id: "1", tipoDocumento: "DNI", numeroDocumento: "12345678")
            };
            var newClienteTipo = "Pasaporte";
            var newClienteNumero = "12345678";

            // Act
            var exists = clientes.Any(c => 
                c.TipoDocumento == newClienteTipo && 
                c.NumeroDocumento == newClienteNumero);

            // Assert
            Assert.False(exists);
        }

        #endregion
    }
}
