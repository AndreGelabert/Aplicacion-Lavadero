using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Tests for VehiculoService, specifically the association key generation and validation.
    /// </summary>
    public class VehiculoServiceTests
    {
        /// <summary>
        /// Tests that GenerarClaveAsociacion creates a valid 9-character key with format XXXX-XXXX.
        /// </summary>
        [Fact]
        public void GenerarClaveAsociacion_ReturnsValidFormat()
        {
            // Act
            var clave = VehiculoService.GenerarClaveAsociacion();

            // Assert
            Assert.NotNull(clave);
            Assert.Equal(9, clave.Length); // XXXX-XXXX = 9 chars
            Assert.Equal('-', clave[4]); // Hyphen in middle
            Assert.Matches(@"^[A-Z2-9]{4}-[A-Z2-9]{4}$", clave); // Only allowed chars
        }

        /// <summary>
        /// Tests that GenerarClaveAsociacion creates unique keys.
        /// </summary>
        [Fact]
        public void GenerarClaveAsociacion_CreatesUniqueKeys()
        {
            // Act
            var claves = new HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                claves.Add(VehiculoService.GenerarClaveAsociacion());
            }

            // Assert - All 100 keys should be unique
            Assert.Equal(100, claves.Count);
        }

        /// <summary>
        /// Tests that GenerarClaveAsociacion doesn't use ambiguous characters (0, O, 1, I, L).
        /// </summary>
        [Fact]
        public void GenerarClaveAsociacion_ExcludesAmbiguousCharacters()
        {
            // Act & Assert - Generate many keys and verify no ambiguous chars
            for (int i = 0; i < 100; i++)
            {
                var clave = VehiculoService.GenerarClaveAsociacion();
                Assert.DoesNotContain("0", clave);
                Assert.DoesNotContain("O", clave);
                Assert.DoesNotContain("1", clave);
                Assert.DoesNotContain("I", clave);
                Assert.DoesNotContain("L", clave);
            }
        }

        /// <summary>
        /// Tests that HashClaveAsociacion creates a valid SHA256 hash.
        /// </summary>
        [Fact]
        public void HashClaveAsociacion_CreatesValidHash()
        {
            // Arrange
            var clave = "ABCD-EFGH";

            // Act
            var hash = VehiculoService.HashClaveAsociacion(clave);

            // Assert
            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length); // SHA256 produces 64 hex chars
            Assert.Matches(@"^[a-f0-9]{64}$", hash); // All lowercase hex
        }

        /// <summary>
        /// Tests that HashClaveAsociacion produces consistent hashes.
        /// </summary>
        [Fact]
        public void HashClaveAsociacion_IsConsistent()
        {
            // Arrange
            var clave = "WXYZ-1234";

            // Act
            var hash1 = VehiculoService.HashClaveAsociacion(clave);
            var hash2 = VehiculoService.HashClaveAsociacion(clave);

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that HashClaveAsociacion normalizes input (removes hyphen, converts to uppercase).
        /// </summary>
        [Fact]
        public void HashClaveAsociacion_NormalizesInput()
        {
            // Arrange
            var clave1 = "ABCD-EFGH";
            var clave2 = "abcd-efgh";
            var clave3 = "ABCDEFGH"; // No hyphen

            // Act
            var hash1 = VehiculoService.HashClaveAsociacion(clave1);
            var hash2 = VehiculoService.HashClaveAsociacion(clave2);
            var hash3 = VehiculoService.HashClaveAsociacion(clave3);

            // Assert - All should produce the same hash
            Assert.Equal(hash1, hash2);
            Assert.Equal(hash1, hash3);
        }

        /// <summary>
        /// Tests that HashClaveAsociacion returns empty for null/empty input.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void HashClaveAsociacion_ReturnsEmptyForInvalidInput(string input)
        {
            // Act
            var hash = VehiculoService.HashClaveAsociacion(input);

            // Assert
            Assert.Equal(string.Empty, hash);
        }

        /// <summary>
        /// Tests that ValidarClaveAsociacion returns true for matching key/hash.
        /// </summary>
        [Fact]
        public void ValidarClaveAsociacion_ReturnsTrueForValidKey()
        {
            // Arrange
            var clave = VehiculoService.GenerarClaveAsociacion();
            var hash = VehiculoService.HashClaveAsociacion(clave);

            // Act
            var result = VehiculoService.ValidarClaveAsociacion(clave, hash);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValidarClaveAsociacion returns false for non-matching key.
        /// </summary>
        [Fact]
        public void ValidarClaveAsociacion_ReturnsFalseForInvalidKey()
        {
            // Arrange
            var correctClave = VehiculoService.GenerarClaveAsociacion();
            var hash = VehiculoService.HashClaveAsociacion(correctClave);
            var wrongClave = "WRONG-KEYX";

            // Act
            var result = VehiculoService.ValidarClaveAsociacion(wrongClave, hash);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValidarClaveAsociacion is case-insensitive.
        /// </summary>
        [Fact]
        public void ValidarClaveAsociacion_IsCaseInsensitive()
        {
            // Arrange
            var clave = "ABCD-EFGH";
            var hash = VehiculoService.HashClaveAsociacion(clave);
            var lowercaseClave = "abcd-efgh";

            // Act
            var result = VehiculoService.ValidarClaveAsociacion(lowercaseClave, hash);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValidarClaveAsociacion returns false for null/empty inputs.
        /// </summary>
        [Theory]
        [InlineData(null, "somehash")]
        [InlineData("", "somehash")]
        [InlineData("ABCD-EFGH", null)]
        [InlineData("ABCD-EFGH", "")]
        [InlineData(null, null)]
        [InlineData("", "")]
        public void ValidarClaveAsociacion_ReturnsFalseForNullOrEmpty(string clave, string hash)
        {
            // Act
            var result = VehiculoService.ValidarClaveAsociacion(clave, hash);

            // Assert
            Assert.False(result);
        }
    }
}
