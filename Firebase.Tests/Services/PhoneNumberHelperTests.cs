using Firebase.Services;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Tests unitarios para PhoneNumberHelper.
    /// 
    /// Metodología de Testing: Pruebas Unitarias con patrón AAA (Arrange-Act-Assert)
    /// Tipo de Testing: Pruebas de Unidad Funcionales
    /// - Se prueban las entradas y salidas verificando el comportamiento esperado
    /// - Se validan casos específicos del dominio (formato argentino, WhatsApp)
    /// 
    /// Categorías de Tests:
    /// 1. Normalización: Verificar normalización de números con diferentes formatos
    /// 2. Código de País: Verificar adición y remoción de códigos de país
    /// 3. Formato WhatsApp: Verificar conversión a formato de API de WhatsApp
    /// 4. Validación: Verificar validación de números de teléfono
    /// 5. Comparación: Verificar comparación de números en diferentes formatos
    /// </summary>
    public class PhoneNumberHelperTests
    {
        #region Tests de Normalización

        /// <summary>
        /// Verifica que NormalizePhoneNumber remueva caracteres no numéricos.
        /// 
        /// Arrange: Número con diferentes formatos
        /// Act: Normalizar el número
        /// Assert: El resultado solo contiene dígitos
        /// </summary>
        [Theory]
        [InlineData("+54 3751 59-0586", "543751590586")]
        [InlineData("54 3751 590586", "543751590586")]
        [InlineData("+54-3751-590586", "543751590586")]
        [InlineData("543751590586", "543751590586")]
        [InlineData("3751590586", "3751590586")]
        public void NormalizePhoneNumber_ShouldRemoveNonNumericCharacters(string input, string expected)
        {
            // Act
            var result = PhoneNumberHelper.NormalizePhoneNumber(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que NormalizePhoneNumber maneje entradas vacías o nulas.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizePhoneNumber_ShouldHandleEmptyInput(string input)
        {
            // Act
            var result = PhoneNumberHelper.NormalizePhoneNumber(input);

            // Assert
            Assert.Equal(input, result);
        }

        /// <summary>
        /// Verifica que el signo + se remueva del inicio.
        /// </summary>
        [Fact]
        public void NormalizePhoneNumber_ShouldRemovePlusSign()
        {
            // Arrange
            var input = "+543751590586";

            // Act
            var result = PhoneNumberHelper.NormalizePhoneNumber(input);

            // Assert
            Assert.Equal("543751590586", result);
            Assert.DoesNotContain("+", result);
        }

        #endregion

        #region Tests de Código de País

        /// <summary>
        /// Verifica que AddCountryCode agregue el código de país correctamente.
        /// </summary>
        [Theory]
        [InlineData("3751590586", "54", "543751590586")]
        [InlineData("5551234567", "52", "525551234567")]
        public void AddCountryCode_ShouldAddCode(string phoneNumber, string countryCode, string expected)
        {
            // Act
            var result = PhoneNumberHelper.AddCountryCode(phoneNumber, countryCode);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que AddCountryCode no duplique el código si ya existe.
        /// </summary>
        [Fact]
        public void AddCountryCode_ShouldNotDuplicate()
        {
            // Arrange
            var phoneNumber = "543751590586";

            // Act
            var result = PhoneNumberHelper.AddCountryCode(phoneNumber, "54");

            // Assert
            Assert.Equal("543751590586", result);
            Assert.DoesNotContain("5454", result);
        }

        /// <summary>
        /// Verifica que RemoveCountryCode remueva el código de país.
        /// </summary>
        [Theory]
        [InlineData("543751590586", "54", "3751590586")]
        [InlineData("5493751590586", "54", "3751590586")] // Con 9 de Argentina
        [InlineData("11234567890", "1", "1234567890")]
        public void RemoveCountryCode_ShouldRemoveCode(string phoneNumber, string countryCode, string expected)
        {
            // Act
            var result = PhoneNumberHelper.RemoveCountryCode(phoneNumber, countryCode);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que RemoveCountryCode remueva el 9 adicional de Argentina.
        /// </summary>
        [Fact]
        public void RemoveCountryCode_ShouldRemoveArgentine9()
        {
            // Arrange
            var phoneNumber = "5493751590586";

            // Act
            var result = PhoneNumberHelper.RemoveCountryCode(phoneNumber, "54");

            // Assert
            Assert.Equal("3751590586", result);
            Assert.DoesNotContain("9", result.Substring(0, 1)); // El primer dígito no debe ser 9
        }

        /// <summary>
        /// Verifica que RemoveCountryCode maneje números sin código de país.
        /// </summary>
        [Fact]
        public void RemoveCountryCode_ShouldHandleNumberWithoutCode()
        {
            // Arrange
            var phoneNumber = "3751590586";

            // Act
            var result = PhoneNumberHelper.RemoveCountryCode(phoneNumber, "54");

            // Assert
            Assert.Equal("3751590586", result);
        }

        #endregion

        #region Tests de Formato WhatsApp

        /// <summary>
        /// Verifica la conversión a formato WhatsApp.
        /// </summary>
        [Theory]
        [InlineData("3751590586", "543751590586")]
        [InlineData("543751590586", "543751590586")]
        public void ToWhatsAppFormat_ShouldConvertCorrectly(string input, string expected)
        {
            // Act
            var result = PhoneNumberHelper.ToWhatsAppFormat(input, "54");

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que PrepareForMetaAPI remueva el 9 de Argentina.
        /// </summary>
        [Theory]
        [InlineData("5493751590586", "543751590586")]
        [InlineData("543751590586", "543751590586")] // Sin 9
        [InlineData("3751590586", "3751590586")] // Sin código de país
        public void PrepareForMetaAPI_ShouldRemoveArgentine9(string input, string expected)
        {
            // Act
            var result = PhoneNumberHelper.PrepareForMetaAPI(input, "54");

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica el formateo para mostrar con +.
        /// </summary>
        [Theory]
        [InlineData("543751590586", "+543751590586")]
        [InlineData("+543751590586", "+543751590586")] // Ya tiene +
        public void FormatForDisplay_ShouldAddPlusSign(string input, string expected)
        {
            // Act
            var result = PhoneNumberHelper.FormatForDisplay(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que FormatForDisplay maneje entradas vacías.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FormatForDisplay_ShouldHandleEmptyInput(string input)
        {
            // Act
            var result = PhoneNumberHelper.FormatForDisplay(input);

            // Assert
            Assert.Equal(input, result);
        }

        #endregion

        #region Tests de Validación

        /// <summary>
        /// Verifica que IsValidPhoneNumber valide números correctos.
        /// </summary>
        [Theory]
        [InlineData("3751590586", true)] // 10 dígitos
        [InlineData("543751590586", true)] // 12 dígitos con código de país
        [InlineData("5493751590586", true)] // 13 dígitos con código y 9
        [InlineData("12345678901234", true)] // 14 dígitos
        [InlineData("123456789012345", true)] // 15 dígitos (máximo)
        [InlineData("123456789", false)] // 9 dígitos (muy corto)
        [InlineData("1234567890123456", false)] // 16 dígitos (muy largo)
        public void IsValidPhoneNumber_ShouldValidateLength(string phoneNumber, bool expected)
        {
            // Act
            var result = PhoneNumberHelper.IsValidPhoneNumber(phoneNumber);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que IsValidPhoneNumber rechace entradas inválidas.
        /// </summary>
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("abc1234567", false)] // Con letras
        public void IsValidPhoneNumber_ShouldRejectInvalidInput(string phoneNumber, bool expected)
        {
            // Act
            var result = PhoneNumberHelper.IsValidPhoneNumber(phoneNumber);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que IsValidPhoneNumber acepte números con formato.
        /// </summary>
        [Theory]
        [InlineData("+54 3751 590586", true)]
        [InlineData("54-3751-590586", true)]
        [InlineData("(54) 3751-590586", true)]
        public void IsValidPhoneNumber_ShouldAcceptFormattedNumbers(string phoneNumber, bool expected)
        {
            // Act
            var result = PhoneNumberHelper.IsValidPhoneNumber(phoneNumber);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Tests de Comparación

        /// <summary>
        /// Verifica que AreEqual compare números correctamente.
        /// </summary>
        [Theory]
        [InlineData("3751590586", "3751590586", true)]
        [InlineData("543751590586", "3751590586", true)]
        [InlineData("5493751590586", "3751590586", true)]
        [InlineData("+54 3751 590586", "3751590586", true)]
        [InlineData("3751590586", "3751590587", false)] // Diferente
        public void AreEqual_ShouldCompareCorrectly(string phone1, string phone2, bool expected)
        {
            // Act
            var result = PhoneNumberHelper.AreEqual(phone1, phone2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica que AreEqual maneje entradas nulas o vacías.
        /// </summary>
        [Theory]
        [InlineData(null, "3751590586", false)]
        [InlineData("3751590586", null, false)]
        [InlineData(null, null, false)]
        [InlineData("", "3751590586", false)]
        [InlineData("3751590586", "", false)]
        public void AreEqual_ShouldHandleNullOrEmpty(string phone1, string phone2, bool expected)
        {
            // Act
            var result = PhoneNumberHelper.AreEqual(phone1, phone2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifica la comparación de números con diferentes formatos de Argentina.
        /// </summary>
        [Fact]
        public void AreEqual_ShouldHandleArgentineFormats()
        {
            // Arrange - Diferentes formatos del mismo número argentino
            var dbFormat = "3751590586"; // Formato en DB (sin código)
            var whatsappIncoming = "5493751590586"; // Formato WhatsApp entrante (con 54 y 9)
            var whatsappOutgoing = "543751590586"; // Formato WhatsApp saliente (con 54, sin 9)

            // Act & Assert
            Assert.True(PhoneNumberHelper.AreEqual(dbFormat, whatsappIncoming));
            Assert.True(PhoneNumberHelper.AreEqual(dbFormat, whatsappOutgoing));
            Assert.True(PhoneNumberHelper.AreEqual(whatsappIncoming, whatsappOutgoing));
        }

        /// <summary>
        /// Verifica la comparación usando los últimos 10 dígitos como fallback.
        /// </summary>
        [Fact]
        public void AreEqual_ShouldCompareLast10DigitsAsFallback()
        {
            // Arrange - Números con diferente prefijo pero mismos últimos 10 dígitos
            var phone1 = "123751590586";
            var phone2 = "993751590586";

            // Act
            var result = PhoneNumberHelper.AreEqual(phone1, phone2);

            // Assert - Deberían coincidir por los últimos 10 dígitos
            Assert.True(result);
        }

        #endregion

        #region Tests de Casos Especiales

        /// <summary>
        /// Verifica el manejo de números de WhatsApp reales de Argentina.
        /// </summary>
        [Fact]
        public void RealWorldScenario_ArgentinaWhatsApp()
        {
            // Arrange
            var phoneFromWhatsApp = "5493751590586"; // Número que llega de WhatsApp
            var phoneInDatabase = "3751590586"; // Número guardado en DB

            // Act - Normalizar para guardar en DB
            var normalizedForDB = PhoneNumberHelper.RemoveCountryCode(phoneFromWhatsApp, "54");
            
            // Act - Preparar para enviar a WhatsApp
            var forWhatsApp = PhoneNumberHelper.ToWhatsAppFormat(phoneInDatabase, "54");

            // Assert
            Assert.Equal("3751590586", normalizedForDB); // Sin código ni 9
            Assert.Equal("543751590586", forWhatsApp); // Con código, sin 9
        }

        /// <summary>
        /// Verifica el flujo completo de búsqueda de cliente por teléfono.
        /// </summary>
        [Fact]
        public void RealWorldScenario_ClienteLookup()
        {
            // Arrange - Simular teléfonos en DB
            var telefonosEnDB = new List<string>
            {
                "3751590586",
                "3511234567",
                "1156789012"
            };
            
            var phoneFromWhatsApp = "5493751590586";

            // Act - Buscar cliente
            var found = telefonosEnDB.Any(tel => PhoneNumberHelper.AreEqual(tel, phoneFromWhatsApp));

            // Assert
            Assert.True(found);
        }

        #endregion
    }
}
