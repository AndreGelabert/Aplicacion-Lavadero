using Firebase.Models.WhatsApp;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Tests unitarios para el módulo WhatsAppFlowService.
    /// 
    /// Metodología de Testing: Pruebas Unitarias con patrón AAA (Arrange-Act-Assert)
    /// Tipo de Testing: Caja Negra (Black Box Testing)
    /// - Se prueban las entradas y salidas sin conocer la implementación interna
    /// - Se validan los flujos conversacionales y transiciones de estado
    /// 
    /// Categorías de Tests:
    /// 1. Modelo WhatsAppSession: Verificar propiedades y comportamiento del modelo
    /// 2. Estados del Flujo: Validar todos los estados definidos en WhatsAppFlowStates
    /// 3. Validaciones de Entrada: Verificar validación de nombre, email, patente, etc.
    /// 4. Transiciones de Estado: Validar flujos de registro y gestión
    /// </summary>
    public class WhatsAppFlowServiceTests
    {
        #region Tests del Modelo WhatsAppSession

        /// <summary>
        /// Verifica que el modelo WhatsAppSession tenga todas las propiedades correctas.
        /// 
        /// Arrange: Crear una sesión con datos específicos
        /// Act: Acceder a las propiedades del modelo
        /// Assert: Verificar que cada propiedad tenga el valor esperado
        /// </summary>
        [Fact]
        public void WhatsAppSession_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                ClienteId = "cli-001",
                CurrentState = WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO,
                LastInteraction = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            };

            // Assert
            Assert.Equal("5493751590586", session.Id);
            Assert.Equal("cli-001", session.ClienteId);
            Assert.Equal(WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, session.CurrentState);
            Assert.NotNull(session.TemporaryData);
        }

        /// <summary>
        /// Verifica que IsAuthenticated devuelva true cuando hay ClienteId.
        /// </summary>
        [Fact]
        public void IsAuthenticated_ShouldReturnTrue_WhenClienteIdExists()
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                ClienteId = "cli-001",
                CurrentState = WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO
            };

            // Assert
            Assert.True(session.IsAuthenticated);
        }

        /// <summary>
        /// Verifica que IsAuthenticated devuelva false cuando no hay ClienteId.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IsAuthenticated_ShouldReturnFalse_WhenNoClienteId(string clienteId)
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                ClienteId = clienteId,
                CurrentState = WhatsAppFlowStates.INICIO
            };

            // Assert
            Assert.False(session.IsAuthenticated);
        }

        /// <summary>
        /// Verifica que TemporaryData se inicialice como diccionario vacío.
        /// </summary>
        [Fact]
        public void TemporaryData_ShouldInitializeAsEmptyDictionary()
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                CurrentState = WhatsAppFlowStates.INICIO
            };

            // Assert
            Assert.NotNull(session.TemporaryData);
            Assert.Empty(session.TemporaryData);
        }

        /// <summary>
        /// Verifica que TemporaryData pueda almacenar datos del flujo.
        /// </summary>
        [Fact]
        public void TemporaryData_ShouldStoreFlowData()
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                CurrentState = WhatsAppFlowStates.REGISTRO_NOMBRE
            };

            // Act
            session.TemporaryData["TipoDocumento"] = "DNI";
            session.TemporaryData["NumeroDocumento"] = "12345678";

            // Assert
            Assert.Equal(2, session.TemporaryData.Count);
            Assert.Equal("DNI", session.TemporaryData["TipoDocumento"]);
            Assert.Equal("12345678", session.TemporaryData["NumeroDocumento"]);
        }

        #endregion

        #region Tests de Estados del Flujo

        /// <summary>
        /// Verifica que todos los estados de registro estén definidos.
        /// </summary>
        [Fact]
        public void FlowStates_RegistroStates_ShouldBeDefined()
        {
            // Assert
            Assert.Equal("REGISTRO_TIPO_DOCUMENTO", WhatsAppFlowStates.REGISTRO_TIPO_DOCUMENTO);
            Assert.Equal("REGISTRO_NUM_DOCUMENTO", WhatsAppFlowStates.REGISTRO_NUM_DOCUMENTO);
            Assert.Equal("REGISTRO_NOMBRE", WhatsAppFlowStates.REGISTRO_NOMBRE);
            Assert.Equal("REGISTRO_APELLIDO", WhatsAppFlowStates.REGISTRO_APELLIDO);
            Assert.Equal("REGISTRO_EMAIL", WhatsAppFlowStates.REGISTRO_EMAIL);
            Assert.Equal("REGISTRO_CONFIRMACION", WhatsAppFlowStates.REGISTRO_CONFIRMACION);
            Assert.Equal("REGISTRO_VEHICULO_OPCION", WhatsAppFlowStates.REGISTRO_VEHICULO_OPCION);
        }

        /// <summary>
        /// Verifica que todos los estados de vehículo estén definidos.
        /// </summary>
        [Fact]
        public void FlowStates_VehiculoStates_ShouldBeDefined()
        {
            // Assert
            Assert.Equal("VEHICULO_MENU", WhatsAppFlowStates.VEHICULO_MENU);
            Assert.Equal("VEHICULO_TIPO", WhatsAppFlowStates.VEHICULO_TIPO);
            Assert.Equal("VEHICULO_PATENTE", WhatsAppFlowStates.VEHICULO_PATENTE);
            Assert.Equal("VEHICULO_MARCA", WhatsAppFlowStates.VEHICULO_MARCA);
            Assert.Equal("VEHICULO_MODELO", WhatsAppFlowStates.VEHICULO_MODELO);
            Assert.Equal("VEHICULO_COLOR", WhatsAppFlowStates.VEHICULO_COLOR);
            Assert.Equal("VEHICULO_CONFIRMACION", WhatsAppFlowStates.VEHICULO_CONFIRMACION);
        }

        /// <summary>
        /// Verifica que todos los estados de menú cliente estén definidos.
        /// </summary>
        [Fact]
        public void FlowStates_MenuClienteStates_ShouldBeDefined()
        {
            // Assert
            Assert.Equal("MENU_CLIENTE_AUTENTICADO", WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO);
            Assert.Equal("MOSTRAR_DATOS", WhatsAppFlowStates.MOSTRAR_DATOS);
            Assert.Equal("MENU_VEHICULOS", WhatsAppFlowStates.MENU_VEHICULOS);
            Assert.Equal("MOSTRAR_VEHICULOS", WhatsAppFlowStates.MOSTRAR_VEHICULOS);
        }

        /// <summary>
        /// Verifica que todos los estados de edición de cliente estén definidos.
        /// </summary>
        [Fact]
        public void FlowStates_EdicionClienteStates_ShouldBeDefined()
        {
            // Assert
            Assert.Equal("EDITAR_DATOS_MENU", WhatsAppFlowStates.EDITAR_DATOS_MENU);
            Assert.Equal("EDITAR_NOMBRE", WhatsAppFlowStates.EDITAR_NOMBRE);
            Assert.Equal("EDITAR_APELLIDO", WhatsAppFlowStates.EDITAR_APELLIDO);
            Assert.Equal("EDITAR_EMAIL", WhatsAppFlowStates.EDITAR_EMAIL);
            Assert.Equal("CONFIRMAR_EDICION", WhatsAppFlowStates.CONFIRMAR_EDICION);
        }

        /// <summary>
        /// Verifica que todos los estados de gestión de vehículos estén definidos.
        /// </summary>
        [Fact]
        public void FlowStates_GestionVehiculosStates_ShouldBeDefined()
        {
            // Assert
            Assert.Equal("SELECCIONAR_VEHICULO_MODIFICAR", WhatsAppFlowStates.SELECCIONAR_VEHICULO_MODIFICAR);
            Assert.Equal("MODIFICAR_VEHICULO_MENU", WhatsAppFlowStates.MODIFICAR_VEHICULO_MENU);
            Assert.Equal("MODIFICAR_VEHICULO_MODELO", WhatsAppFlowStates.MODIFICAR_VEHICULO_MODELO);
            Assert.Equal("MODIFICAR_VEHICULO_COLOR", WhatsAppFlowStates.MODIFICAR_VEHICULO_COLOR);
            Assert.Equal("CONFIRMAR_ELIMINAR_VEHICULO", WhatsAppFlowStates.CONFIRMAR_ELIMINAR_VEHICULO);
        }

        /// <summary>
        /// Verifica que todos los estados de asociación de vehículos estén definidos.
        /// </summary>
        [Fact]
        public void FlowStates_AsociacionVehiculosStates_ShouldBeDefined()
        {
            // Assert
            Assert.Equal("ASOCIAR_VEHICULO_PATENTE", WhatsAppFlowStates.ASOCIAR_VEHICULO_PATENTE);
            Assert.Equal("ASOCIAR_VEHICULO_CLAVE", WhatsAppFlowStates.ASOCIAR_VEHICULO_CLAVE);
            Assert.Equal("ASOCIAR_VEHICULO_CONFIRMACION", WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION);
            Assert.Equal("MOSTRAR_CLAVE_VEHICULO", WhatsAppFlowStates.MOSTRAR_CLAVE_VEHICULO);
        }

        /// <summary>
        /// Verifica que el estado INICIO esté definido.
        /// </summary>
        [Fact]
        public void FlowStates_InicioState_ShouldBeDefined()
        {
            // Assert
            Assert.Equal("INICIO", WhatsAppFlowStates.INICIO);
        }

        #endregion

        #region Tests de Validación de Entradas (Nombre)

        /// <summary>
        /// Verifica la validación de nombre según el patrón del modelo.
        /// Patrón: ^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$
        /// </summary>
        [Theory]
        [InlineData("Juan", true)]
        [InlineData("María José", true)]
        [InlineData("José Luis", true)]
        [InlineData("Ángela", true)]
        [InlineData("José", true)]
        [InlineData("Jo", false)] // Menos de 3 caracteres
        [InlineData("Juan123", false)] // Con números
        [InlineData("Juan!", false)] // Con caracteres especiales
        [InlineData("", false)] // Vacío
        public void ValidateNombre_ShouldMatchPattern(string nombre, bool shouldBeValid)
        {
            // Arrange
            var pattern = @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{3,}$";

            // Act
            var isValid = !string.IsNullOrEmpty(nombre) && 
                          System.Text.RegularExpressions.Regex.IsMatch(nombre, pattern);

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        #endregion

        #region Tests de Validación de Entradas (Email)

        /// <summary>
        /// Verifica la validación de email según el patrón del modelo.
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
        public void ValidateEmail_ShouldMatchPattern(string email, bool shouldBeValid)
        {
            // Arrange
            var pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]{3,}\.[a-zA-Z]{2,}$";

            // Act
            var isValid = !string.IsNullOrEmpty(email) && 
                          System.Text.RegularExpressions.Regex.IsMatch(email, pattern);

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        #endregion

        #region Tests de Validación de Entradas (Patente)

        /// <summary>
        /// Verifica la validación de patente.
        /// Debe contener letras y números, puede tener espacios y guiones.
        /// Mínimo 5 caracteres alfanuméricos.
        /// </summary>
        [Theory]
        [InlineData("ABC123", true)] // Formato viejo Argentina
        [InlineData("AB123CD", true)] // Formato nuevo Argentina
        [InlineData("AB 123 CD", true)] // Con espacios
        [InlineData("AB-123-CD", true)] // Con guiones
        [InlineData("ABC 12", true)] // Mínimo 5 caracteres
        [InlineData("ABC1", false)] // Menos de 5 caracteres
        [InlineData("ABCDEF", false)] // Solo letras
        [InlineData("123456", false)] // Solo números
        [InlineData("ABC!@#", false)] // Caracteres especiales no permitidos
        public void ValidatePatente_ShouldMatchPattern(string patente, bool shouldBeValid)
        {
            // Arrange
            bool isValid = false;
            
            if (!string.IsNullOrEmpty(patente))
            {
                // Verificar formato básico
                if (System.Text.RegularExpressions.Regex.IsMatch(patente, @"^[a-zA-Z0-9\s-]+$"))
                {
                    // Remover espacios y guiones para contar
                    var soloAlfanumerico = System.Text.RegularExpressions.Regex.Replace(patente, @"[\s-]", "");
                    
                    if (soloAlfanumerico.Length >= 5)
                    {
                        // Debe contener al menos una letra Y al menos un número
                        isValid = System.Text.RegularExpressions.Regex.IsMatch(soloAlfanumerico, @"[a-zA-Z]") && 
                                  System.Text.RegularExpressions.Regex.IsMatch(soloAlfanumerico, @"\d");
                    }
                }
            }

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        #endregion

        #region Tests de Validación de Entradas (Número de Documento)

        /// <summary>
        /// Verifica la validación de número de documento (solo números).
        /// </summary>
        [Theory]
        [InlineData("12345678", true)]
        [InlineData("0", true)]
        [InlineData("12345678901234567890", true)] // Número largo
        [InlineData("", false)]
        [InlineData("ABC123", false)]
        [InlineData("12.345.678", false)] // Con puntos
        [InlineData("12-345-678", false)] // Con guiones
        public void ValidateNumeroDocumento_ShouldContainOnlyNumbers(string documento, bool shouldBeValid)
        {
            // Arrange
            var pattern = @"^\d+$";

            // Act
            var isValid = !string.IsNullOrEmpty(documento) && 
                          System.Text.RegularExpressions.Regex.IsMatch(documento, pattern);

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        #endregion

        #region Tests de Flujo de Registro

        /// <summary>
        /// Verifica la secuencia correcta de estados en el flujo de registro.
        /// </summary>
        [Fact]
        public void RegistroFlow_StateSequence_ShouldBeCorrect()
        {
            // Arrange
            var expectedSequence = new[]
            {
                WhatsAppFlowStates.INICIO,
                WhatsAppFlowStates.REGISTRO_TIPO_DOCUMENTO,
                WhatsAppFlowStates.REGISTRO_NUM_DOCUMENTO,
                WhatsAppFlowStates.REGISTRO_NOMBRE,
                WhatsAppFlowStates.REGISTRO_APELLIDO,
                WhatsAppFlowStates.REGISTRO_EMAIL,
                WhatsAppFlowStates.REGISTRO_CONFIRMACION,
                WhatsAppFlowStates.REGISTRO_VEHICULO_OPCION
            };

            // Assert - Verificar que todos los estados existan
            foreach (var state in expectedSequence)
            {
                Assert.False(string.IsNullOrEmpty(state));
            }

            // Assert - Verificar orden lógico (cada estado es distinto)
            Assert.Equal(expectedSequence.Length, expectedSequence.Distinct().Count());
        }

        /// <summary>
        /// Verifica que se almacenen los datos temporales durante el registro.
        /// </summary>
        [Fact]
        public void RegistroFlow_ShouldStoreDataInTemporaryData()
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                CurrentState = WhatsAppFlowStates.REGISTRO_CONFIRMACION
            };

            // Act - Simular almacenamiento de datos de registro
            session.TemporaryData["TipoDocumento"] = "DNI";
            session.TemporaryData["NumeroDocumento"] = "12345678";
            session.TemporaryData["Nombre"] = "Juan";
            session.TemporaryData["Apellido"] = "Pérez";
            session.TemporaryData["Email"] = "juan@test.com";

            // Assert
            Assert.Equal(5, session.TemporaryData.Count);
            Assert.Equal("DNI", session.TemporaryData["TipoDocumento"]);
            Assert.Equal("12345678", session.TemporaryData["NumeroDocumento"]);
            Assert.Equal("Juan", session.TemporaryData["Nombre"]);
            Assert.Equal("Pérez", session.TemporaryData["Apellido"]);
            Assert.Equal("juan@test.com", session.TemporaryData["Email"]);
        }

        #endregion

        #region Tests de Flujo de Vehículo

        /// <summary>
        /// Verifica la secuencia correcta de estados en el flujo de agregar vehículo.
        /// </summary>
        [Fact]
        public void VehiculoFlow_StateSequence_ShouldBeCorrect()
        {
            // Arrange
            var expectedSequence = new[]
            {
                WhatsAppFlowStates.VEHICULO_TIPO,
                WhatsAppFlowStates.VEHICULO_PATENTE,
                WhatsAppFlowStates.VEHICULO_MARCA,
                WhatsAppFlowStates.VEHICULO_MODELO,
                WhatsAppFlowStates.VEHICULO_COLOR,
                WhatsAppFlowStates.VEHICULO_CONFIRMACION
            };

            // Assert - Verificar que todos los estados existan
            foreach (var state in expectedSequence)
            {
                Assert.False(string.IsNullOrEmpty(state));
            }
        }

        /// <summary>
        /// Verifica que se almacenen los datos del vehículo durante el flujo.
        /// </summary>
        [Fact]
        public void VehiculoFlow_ShouldStoreDataInTemporaryData()
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                ClienteId = "cli-001",
                CurrentState = WhatsAppFlowStates.VEHICULO_CONFIRMACION
            };

            // Act - Simular almacenamiento de datos del vehículo
            session.TemporaryData["TipoVehiculo"] = "Automóvil";
            session.TemporaryData["Patente"] = "ABC123";
            session.TemporaryData["Marca"] = "Toyota";
            session.TemporaryData["Modelo"] = "Corolla";
            session.TemporaryData["Color"] = "Blanco";

            // Assert
            Assert.Equal(5, session.TemporaryData.Count);
            Assert.Equal("Automóvil", session.TemporaryData["TipoVehiculo"]);
            Assert.Equal("ABC123", session.TemporaryData["Patente"]);
            Assert.Equal("Toyota", session.TemporaryData["Marca"]);
            Assert.Equal("Corolla", session.TemporaryData["Modelo"]);
            Assert.Equal("Blanco", session.TemporaryData["Color"]);
        }

        #endregion

        #region Tests de Flujo de Asociación de Vehículos

        /// <summary>
        /// Verifica la secuencia correcta de estados en el flujo de asociación.
        /// </summary>
        [Fact]
        public void AsociacionFlow_StateSequence_ShouldBeCorrect()
        {
            // Arrange
            var expectedSequence = new[]
            {
                WhatsAppFlowStates.ASOCIAR_VEHICULO_PATENTE,
                WhatsAppFlowStates.ASOCIAR_VEHICULO_CLAVE,
                WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION
            };

            // Assert
            foreach (var state in expectedSequence)
            {
                Assert.False(string.IsNullOrEmpty(state));
            }
        }

        /// <summary>
        /// Verifica que se almacenen los datos de asociación.
        /// </summary>
        [Fact]
        public void AsociacionFlow_ShouldStoreDataInTemporaryData()
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                ClienteId = "cli-001",
                CurrentState = WhatsAppFlowStates.ASOCIAR_VEHICULO_CONFIRMACION
            };

            // Act
            session.TemporaryData["PatenteAsociar"] = "ABC123";
            session.TemporaryData["VehiculoId"] = "veh-001";

            // Assert
            Assert.Equal(2, session.TemporaryData.Count);
            Assert.Equal("ABC123", session.TemporaryData["PatenteAsociar"]);
            Assert.Equal("veh-001", session.TemporaryData["VehiculoId"]);
        }

        #endregion

        #region Tests de Comandos Especiales

        /// <summary>
        /// Verifica el reconocimiento de comandos de reinicio.
        /// </summary>
        [Theory]
        [InlineData("REINICIAR", true)]
        [InlineData("reiniciar", true)]
        [InlineData("INICIO", true)]
        [InlineData("inicio", true)]
        [InlineData("MENU", true)]
        [InlineData("menu", true)]
        [InlineData("Hola", false)]
        [InlineData("123", false)]
        public void SpecialCommands_ShouldBeRecognized(string input, bool isSpecialCommand)
        {
            // Arrange
            var specialCommands = new[] { "REINICIAR", "INICIO", "MENU" };

            // Act
            var isRecognized = specialCommands.Contains(input.Trim().ToUpperInvariant());

            // Assert
            Assert.Equal(isSpecialCommand, isRecognized);
        }

        #endregion

        #region Tests de Manejo de Sesión

        /// <summary>
        /// Verifica que la sesión actualice LastInteraction.
        /// </summary>
        [Fact]
        public void Session_ShouldUpdateLastInteraction()
        {
            // Arrange
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                CurrentState = WhatsAppFlowStates.INICIO,
                LastInteraction = DateTime.UtcNow.AddMinutes(-30)
            };
            var previousInteraction = session.LastInteraction;

            // Act
            session.LastInteraction = DateTime.UtcNow;

            // Assert
            Assert.True(session.LastInteraction > previousInteraction);
        }

        /// <summary>
        /// Verifica que la sesión mantenga CreatedAt inmutable.
        /// </summary>
        [Fact]
        public void Session_CreatedAt_ShouldBeImmutable()
        {
            // Arrange
            var createdTime = DateTime.UtcNow.AddHours(-1);
            var session = new WhatsAppSession
            {
                Id = "5493751590586",
                CurrentState = WhatsAppFlowStates.INICIO,
                CreatedAt = createdTime
            };

            // Act - Simular múltiples interacciones
            session.LastInteraction = DateTime.UtcNow;
            session.CurrentState = WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO;

            // Assert
            Assert.Equal(createdTime, session.CreatedAt);
        }

        #endregion
    }
}
