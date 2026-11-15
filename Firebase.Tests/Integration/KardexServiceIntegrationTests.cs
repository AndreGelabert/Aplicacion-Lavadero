using Firebase.Services;
using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Xunit;

namespace Firebase.Tests.Integration;

public class KardexServiceIntegrationTests
{
    [Fact]
    public void CursorSerializaBase64UrlSinPaddingYLeeFormatoAnterior()
    {
        var fecha = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const string id = "ÿÿ";
        var raw = $"next|{fecha:O}|{id}";
        var legacy = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
        Assert.True(KardexCursor.TratarDeDecodificar(legacy, out var anterior, out _));
        Assert.Equal(id, anterior!.DocumentId);
        var token = KardexCursor.Crear("next", fecha, id);
        Assert.Equal(legacy.TrimEnd('=').Replace('+', '-').Replace('/', '_'), token);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.True(KardexCursor.TratarDeDecodificar(token, out var decoded, out _));
        Assert.Equal("next", decoded!.Direccion);
        Assert.Equal(fecha, decoded.FechaUtc);
        Assert.Equal(id, decoded.DocumentId);
    }

    [LocalFirestoreFact]
    public async Task CursoresUsanIdentidadDocumentoConIdsAlmacenadosDiferentes()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-kardex-ids-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var fecha = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var ids = new[] { "a", "b", "c", "d", "e", "f" };
        foreach (var id in ids)
            await db.Collection("movimientosStock").Document(id).SetAsync(new MovimientoStock { Id = "z-" + id, InsumoId = "objetivo", Tipo = "IngresoManual", Fecha = fecha, Justificacion = id });
        var stock = new StockService(db);
        foreach (var orden in new[] { "asc", "desc" })
        {
            var esperado = orden == "asc" ? ids : ids.Reverse().ToArray();
            var paginas = new List<ResultadoKardexPaginado>();
            string? cursor = null;
            for (var i = 0; i < 3; i++)
            {
                var pagina = await stock.ObtenerKardexPaginado("objetivo", pageSize: 2, sortOrder: orden, cursor: cursor);
                Assert.Equal(esperado.Skip(i * 2).Take(2), pagina.Movimientos.Select(m => m.Justificacion));
                paginas.Add(pagina);
                cursor = pagina.CursorSiguiente;
                if (i < 2) Assert.NotNull(cursor); else Assert.Null(cursor);
            }
            Assert.Equal(6, paginas.SelectMany(p => p.Movimientos).Select(m => m.Justificacion).Distinct().Count());
            cursor = paginas[2].CursorAnterior;
            for (var i = 1; i >= 0; i--)
            {
                Assert.NotNull(cursor);
                var pagina = await stock.ObtenerKardexPaginado("objetivo", pageSize: 2, sortOrder: orden, cursor: cursor);
                Assert.Equal(esperado.Skip(i * 2).Take(2), pagina.Movimientos.Select(m => m.Justificacion));
                cursor = pagina.CursorAnterior;
            }
            Assert.Null(cursor);
        }
    }

    [LocalFirestoreFact]
    public async Task RechazaCursoresMalformadosAntesDeConsultar()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-kardex-token-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var stock = new StockService(db);
        static string Token(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        foreach (var cursor in new[] { "token_invalido_sin_base64", Token("solo_una_parte"), Token("invalido|2025-01-01T00:00:00Z|doc1"), Token("next|fecha_invalida|doc1"), Token("next|2025-01-01T00:00:00Z|   ") })
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => stock.ObtenerKardexPaginado(cursor: cursor));
            Assert.StartsWith("Cursor no válido:", error.Message);
        }
    }
}
