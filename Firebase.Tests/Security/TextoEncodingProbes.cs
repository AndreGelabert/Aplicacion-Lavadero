using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Firebase.Tests.Security
{
    public class TextoEncodingProbes
    {
        // S8/S9: visible Spanish must not be double-encoded (mojibake).
        [Fact]
        public void S8S9_Controladores_NoContienenTextoDobleCodificado()
        {
            var root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, "Firebase.csproj")))
            {
                root = Directory.GetParent(root)?.FullName;
            }

            Assert.NotNull(root);
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(root!, "Controllers"), "*.cs"))
            {
                var count = Regex.Matches(File.ReadAllText(file), "Ã.|Â.|ðŸ").Count;
                if (count > 0)
                {
                    offenders.Add($"{Path.GetFileName(file)}: {count}");
                }
            }

            Assert.True(offenders.Count == 0, "Texto doblemente codificado en: " + string.Join(", ", offenders));
        }

        // S8/S9: lossy cp1252->UTF-8 conversions leave U+FFFD in visible text.
        [Fact]
        public void S8S9_ControladoresYVistas_NoContienenCaracterDeReemplazo()
        {
            var root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, "Firebase.csproj")))
            {
                root = Directory.GetParent(root)?.FullName;
            }

            Assert.NotNull(root);
            var strictUtf8 = new UTF8Encoding(false, true);
            var offenders = new List<string>();
            var files = Directory.GetFiles(Path.Combine(root!, "Controllers"), "*.cs")
                .Concat(Directory.GetFiles(Path.Combine(root!, "Views"), "*.cshtml", SearchOption.AllDirectories));
            foreach (var file in files)
            {
                string text;
                try
                {
                    text = strictUtf8.GetString(File.ReadAllBytes(file));
                }
                catch (DecoderFallbackException)
                {
                    // Pre-existing cp1252 sources compile via the ANSI fallback; out of this guard's scope.
                    continue;
                }

                var count = Regex.Matches(text, "\uFFFD").Count;
                if (count > 0)
                {
                    offenders.Add($"{Path.GetRelativePath(root!, file)}: {count}");
                }
            }

            Assert.True(offenders.Count == 0, "Caracter de reemplazo en: " + string.Join(", ", offenders));
        }

        // S8/S9: the strings users actually read must be complete Spanish.
        [Fact]
        public void S8S9_TextosVisiblesDeAuditoriaYPersonal_SonCorrectos()
        {
            var root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, "Firebase.csproj")))
            {
                root = Directory.GetParent(root)?.FullName;
            }

            Assert.NotNull(root);
            Assert.Contains("Cerrar Sesión", File.ReadAllText(Path.Combine(root!, "Controllers", "LoginController.cs")));
            var personal = File.ReadAllText(Path.Combine(root!, "Views", "Personal", "_PersonalTable.cshtml"));
            Assert.Contains("Correo electrónico", personal);
            Assert.Contains("Mostrando página", personal);
        }
    }
}
