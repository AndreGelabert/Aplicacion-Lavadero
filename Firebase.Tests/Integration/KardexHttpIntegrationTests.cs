using System.Net;
using Firebase.Controllers;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Firebase.Tests.Integration;

public sealed class KardexHttpIntegrationTests
{
    [LocalFirestoreFact]
    public async Task KardexRechazaTokensDeCursorMalformadosPublicoHttp()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-kardex-malformed-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
            };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Kardex");

            // 1. Token de texto plano sin codificación Base64
            var r1 = await client.GetAsync("/Insumos/Kardex?cursor=token_invalido_sin_base64");
            Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);
            Assert.Contains("Cursor no válido", await r1.Content.ReadAsStringAsync());

            // 2. Base64 válido pero con estructura incorrecta (no contiene las 3 partes requeridas)
            var b64Incompleto = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("solo_una_parte"));
            var r2 = await client.GetAsync("/Insumos/Kardex?cursor=" + b64Incompleto);
            Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
            Assert.Contains("Cursor no válido", await r2.Content.ReadAsStringAsync());

            // 3. Dirección no válida (debe ser 'next' o 'prev')
            var b64DirInvalida = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("invalido|2025-01-01T00:00:00Z|doc1"));
            var r3 = await client.GetAsync("/Insumos/Kardex?cursor=" + b64DirInvalida);
            Assert.Equal(HttpStatusCode.BadRequest, r3.StatusCode);
            Assert.Contains("Cursor no válido", await r3.Content.ReadAsStringAsync());

            // 4. Fecha no parseable
            var b64FechaInvalida = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("next|fecha_invalida|doc1"));
            var r4 = await client.GetAsync("/Insumos/Kardex?cursor=" + b64FechaInvalida);
            Assert.Equal(HttpStatusCode.BadRequest, r4.StatusCode);
            Assert.Contains("Cursor no válido", await r4.Content.ReadAsStringAsync());

            // 5. DocumentId vacío
            var b64IdVacio = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("next|2025-01-01T00:00:00Z|   "));
            var r5 = await client.GetAsync("/Insumos/Kardex?cursor=" + b64IdVacio);
            Assert.Equal(HttpStatusCode.BadRequest, r5.StatusCode);
            Assert.Contains("Cursor no válido", await r5.Content.ReadAsStringAsync());
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [LocalFirestoreFact]
    public async Task KardexFiltraFechasInclusivasInsumoYTipoSinCambiarLecturaNiPermisos()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-kardex-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon" });
        static DateTime LocalUtc(int day, int hour, int minute, int second) => new DateTime(2025, 1, day, hour, minute, second, DateTimeKind.Local).ToUniversalTime();
        var movimientos = new[] {
            new MovimientoStock { Id = "inicio", InsumoId = "i", Tipo = "IngresoManual", Fecha = LocalUtc(2, 0, 0, 0), Justificacion = "limite-inicio" },
            new MovimientoStock { Id = "fin", InsumoId = "i", Tipo = "IngresoManual", Fecha = LocalUtc(2, 23, 59, 59), Justificacion = "limite-fin" },
            new MovimientoStock { Id = "antes", InsumoId = "i", Tipo = "IngresoManual", Fecha = LocalUtc(1, 23, 59, 59), Justificacion = "fuera-antes" },
            new MovimientoStock { Id = "otro", InsumoId = "i", Tipo = "EgresoManual", Fecha = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc), Justificacion = "otro-tipo" },
            new MovimientoStock { Id = "insumo", InsumoId = "otro", Tipo = "IngresoManual", Fecha = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc), Justificacion = "otro-insumo" }
        };
        foreach (var m in movimientos) await db.Collection("movimientosStock").Document(m.Id).SetAsync(m);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db); builder.Services.AddMemoryCache(); builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>(); builder.Services.AddScoped<ProveedorService>(); builder.Services.AddScoped<StockService>(); builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>()); builder.Services.AddSingleton(Mock.Of<IEmailSender>());
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); await app.StartAsync();
        try {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Modificar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Insumos/Kardex")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permission"); client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Kardex");
            var previo = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Kardex"));
            Assert.Contains("fuera-antes", previo); Assert.Contains("otro-tipo", previo); Assert.Contains("otro-insumo", previo);
            Assert.Contains("name=\"insumoId\"", previo); Assert.Contains("name=\"tipo\"", previo); Assert.Contains("name=\"desde\"", previo); Assert.Contains("name=\"hasta\"", previo);
            var filtrado = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Kardex?insumoId=i&tipo=IngresoManual&desde=2025-01-02&hasta=2025-01-02"));
            Assert.Contains("limite-inicio", filtrado); Assert.Contains("limite-fin", filtrado);
            Assert.DoesNotContain("fuera-antes", filtrado); Assert.DoesNotContain("otro-tipo", filtrado); Assert.DoesNotContain("otro-insumo", filtrado);
            var sinResultados = WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Kardex?desde=2026-01-01"));
            Assert.Contains("No hay movimientos", sinResultados);
            async Task<string> Page(string query) => WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Kardex" + query));
            var porInsumo = await Page("?insumoId=otro");
            Assert.Contains("otro-insumo", porInsumo); Assert.DoesNotContain("otro-tipo", porInsumo);
            var porTipo = await Page("?tipo=EgresoManual");
            Assert.Contains("otro-tipo", porTipo); Assert.DoesNotContain("limite-inicio", porTipo);
            var desde = await Page("?desde=2025-01-02");
            Assert.Contains("limite-inicio", desde); Assert.Contains("limite-fin", desde); Assert.DoesNotContain("fuera-antes", desde);
            var hasta = await Page("?hasta=2025-01-01");
            Assert.Contains("fuera-antes", hasta); Assert.DoesNotContain("limite-inicio", hasta);
            foreach (var query in new[] { "?desde=invalida", "?hasta=2025-02-30", "?desde=2025-01-03&hasta=2025-01-02" })
            {
                var respuesta = await client.GetAsync("/Insumos/Kardex" + query);
                Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
                Assert.Equal("Rango de fechas no válido.", await respuesta.Content.ReadAsStringAsync());
            }
            Assert.Equal(5, (await new StockService(db).ObtenerMovimientos()).Count);
            for (var n = 0; n < 21; n++)
                await db.Collection("movimientosStock").Document("p" + n).SetAsync(new MovimientoStock { Id = "p" + n, InsumoId = "paginado", Tipo = "IngresoManual", Fecha = LocalUtc(3, 0, 0, 0).AddMinutes(n), Justificacion = "fila-" + n.ToString("D2") });
            var primera = await Page("?insumoId=paginado&sortOrder=asc");
            Assert.Contains("fila-00", primera); Assert.DoesNotContain("fila-20", primera);
            Assert.Contains("cursor=", primera); Assert.Contains("sortOrder=asc", primera); Assert.Contains("insumoId=paginado", primera);
            var matchNext = System.Text.RegularExpressions.Regex.Match(primera, @"cursor=([a-zA-Z0-9%_-]+)");
            Assert.True(matchNext.Success, "Debe contener enlace con cursor siguiente.");
            var nextCursor = Uri.UnescapeDataString(matchNext.Groups[1].Value);
            var segunda = await Page("?insumoId=paginado&sortOrder=asc&cursor=" + Uri.EscapeDataString(nextCursor));
            Assert.Contains("fila-20", segunda); Assert.DoesNotContain("fila-00", segunda);
            var matchPrev = System.Text.RegularExpressions.Regex.Match(segunda, @"cursor=([a-zA-Z0-9%_-]+)");
            Assert.True(matchPrev.Success, "Debe contener enlace con cursor anterior.");
            var prevCursor = Uri.UnescapeDataString(matchPrev.Groups[1].Value);
            var vuelta = await Page("?insumoId=paginado&sortOrder=asc&cursor=" + Uri.EscapeDataString(prevCursor));
            Assert.Contains("fila-00", vuelta); Assert.DoesNotContain("fila-20", vuelta);
            var descendente = await Page("?insumoId=paginado&sortOrder=desc");
            Assert.Contains("fila-20", descendente); Assert.DoesNotContain("fila-00", descendente);
            Assert.Equal(26, (await new StockService(db).ObtenerMovimientos()).Count);
        } finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task ObtenerMovimientosConFiltroInsumoNoCargaDocumentosDeOtrosInsumos()
    {
        var projectId = "demo-kardex-isolation-" + Guid.NewGuid().ToString("N")[..16];
        var db = new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

        // Movimiento válido para el insumo objetivo
        await db.Collection("movimientosStock").Document("mov_valido").SetAsync(new MovimientoStock
        {
            Id = "mov_valido",
            InsumoId = "insumo_objetivo",
            Tipo = "IngresoManual",
            Cantidad = 5,
            SaldoResultante = 10,
            Fecha = DateTime.UtcNow,
            Justificacion = "valido"
        });

        // Documento de otro insumo con campo 'Cantidad' de tipo incompatible (string en vez de decimal/número)
        // Si ObtenerMovimientos escanea toda la colección, ConvertTo<MovimientoStock>() fallará.
        // Si filtra en Firestore por InsumoId, este documento ajeno nunca es leído ni deserializado.
        await db.Collection("movimientosStock").Document("mov_ajeno_incompatible").SetAsync(new Dictionary<string, object>
        {
            ["Id"] = "mov_ajeno_incompatible",
            ["InsumoId"] = "otro_insumo",
            ["Tipo"] = "EgresoManual",
            ["Cantidad"] = "no_es_un_numero",
            ["SaldoResultante"] = 0,
            ["Fecha"] = Timestamp.GetCurrentTimestamp(),
            ["Justificacion"] = "incompatible"
        });

        var stock = new StockService(db);

        // ACT: Debe consultar únicamente los movimientos de 'insumo_objetivo'
        var resultado = await stock.ObtenerMovimientos("insumo_objetivo");

        Assert.Single(resultado);
        Assert.Equal("mov_valido", resultado[0].Id);

        // ACT 2: Si filtramos por tipo 'IngresoManual', no debe leer documentos de 'EgresoManual'
        var porTipo = await stock.ObtenerMovimientos(tipo: "IngresoManual");
        Assert.Single(porTipo);
        Assert.Equal("mov_valido", porTipo[0].Id);
    }

    [LocalFirestoreFact]
    public async Task KardexPaginacionServidorMultiplesPaginasYLimites()
    {
        var projectId = "demo-kardex-paged-" + Guid.NewGuid().ToString("N")[..16];
        var db = new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

        static DateTime LocalUtc(int day, int hour, int minute, int second) =>
            new DateTime(2025, 2, day, hour, minute, second, DateTimeKind.Local).ToUniversalTime();

        // 45 movimientos válidos para insumo objetivo distribuidos en el tiempo
        for (var i = 1; i <= 45; i++)
        {
            var id = $"mov_{i:D2}";
            await db.Collection("movimientosStock").Document(id).SetAsync(new MovimientoStock
            {
                Id = id,
                InsumoId = "insumo_pag",
                Tipo = "IngresoManual",
                Cantidad = i,
                SaldoResultante = i * 10,
                Fecha = LocalUtc(1, 0, 0, 0).AddMinutes(i),
                Justificacion = $"just_{i:D2}"
            });
        }

        // Documento incompatible en otro insumo: si el conteo o paginación escanea toda la colección, fallará
        await db.Collection("movimientosStock").Document("incompatible_otro").SetAsync(new Dictionary<string, object>
        {
            ["Id"] = "incompatible_otro",
            ["InsumoId"] = "otro_insumo",
            ["Tipo"] = "EgresoManual",
            ["Cantidad"] = "no_es_numero",
            ["SaldoResultante"] = 0,
            ["Fecha"] = Timestamp.GetCurrentTimestamp(),
            ["Justificacion"] = "incompatible"
        });

        var stock = new StockService(db);

        // Página 1 (ascendente)
        var p1 = await stock.ObtenerKardexPaginado("insumo_pag", pageSize: 20, sortOrder: "asc");
        Assert.True(p1.TienePaginaSiguiente);
        Assert.False(p1.TienePaginaAnterior);
        Assert.NotNull(p1.CursorSiguiente);
        Assert.Null(p1.CursorAnterior);
        Assert.Equal(20, p1.Movimientos.Count);
        Assert.Equal("mov_01", p1.Movimientos.First().Id);
        Assert.Equal("mov_20", p1.Movimientos.Last().Id);

        // Página 2 (ascendente vía cursor)
        var p2 = await stock.ObtenerKardexPaginado("insumo_pag", pageSize: 20, sortOrder: "asc", cursor: p1.CursorSiguiente);
        Assert.True(p2.TienePaginaSiguiente);
        Assert.True(p2.TienePaginaAnterior);
        Assert.NotNull(p2.CursorSiguiente);
        Assert.NotNull(p2.CursorAnterior);
        Assert.Equal(20, p2.Movimientos.Count);
        Assert.Equal("mov_21", p2.Movimientos.First().Id);
        Assert.Equal("mov_40", p2.Movimientos.Last().Id);

        // Página 3 (ascendente: residual de 5 vía cursor)
        var p3 = await stock.ObtenerKardexPaginado("insumo_pag", pageSize: 20, sortOrder: "asc", cursor: p2.CursorSiguiente);
        Assert.False(p3.TienePaginaSiguiente);
        Assert.True(p3.TienePaginaAnterior);
        Assert.Null(p3.CursorSiguiente);
        Assert.NotNull(p3.CursorAnterior);
        Assert.Equal(5, p3.Movimientos.Count);
        Assert.Equal("mov_41", p3.Movimientos.First().Id);
        Assert.Equal("mov_45", p3.Movimientos.Last().Id);

        // Navegación hacia atrás desde Página 3 a Página 2
        var p2Back = await stock.ObtenerKardexPaginado("insumo_pag", pageSize: 20, sortOrder: "asc", cursor: p3.CursorAnterior);
        Assert.True(p2Back.TienePaginaSiguiente);
        Assert.True(p2Back.TienePaginaAnterior);
        Assert.Equal(20, p2Back.Movimientos.Count);
        Assert.Equal("mov_21", p2Back.Movimientos.First().Id);
        Assert.Equal("mov_40", p2Back.Movimientos.Last().Id);

        // Navegación hacia atrás desde Página 2 a Página 1
        var p1Back = await stock.ObtenerKardexPaginado("insumo_pag", pageSize: 20, sortOrder: "asc", cursor: p2Back.CursorAnterior);
        Assert.True(p1Back.TienePaginaSiguiente);
        Assert.False(p1Back.TienePaginaAnterior);
        Assert.Null(p1Back.CursorAnterior);
        Assert.Equal(20, p1Back.Movimientos.Count);
        Assert.Equal("mov_01", p1Back.Movimientos.First().Id);
        Assert.Equal("mov_20", p1Back.Movimientos.Last().Id);

        // Página 1 (descendente)
        var p1Desc = await stock.ObtenerKardexPaginado("insumo_pag", pageSize: 20, sortOrder: "desc");
        Assert.True(p1Desc.TienePaginaSiguiente);
        Assert.Equal(20, p1Desc.Movimientos.Count);
        Assert.Equal("mov_45", p1Desc.Movimientos.First().Id);
        Assert.Equal("mov_26", p1Desc.Movimientos.Last().Id);

        // Página 2 (descendente vía cursor)
        var p2Desc = await stock.ObtenerKardexPaginado("insumo_pag", pageSize: 20, sortOrder: "desc", cursor: p1Desc.CursorSiguiente);
        Assert.True(p2Desc.TienePaginaSiguiente);
        Assert.Equal(20, p2Desc.Movimientos.Count);
        Assert.Equal("mov_25", p2Desc.Movimientos.First().Id);
        Assert.Equal("mov_06", p2Desc.Movimientos.Last().Id);
    }

    [LocalFirestoreFact]
    public async Task KardexEmpatesTimestampsOrdenEstable()
    {
        var projectId = "demo-kardex-ties-" + Guid.NewGuid().ToString("N")[..16];
        var db = new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

        var fechaFija = new DateTime(2025, 3, 10, 14, 30, 0, DateTimeKind.Utc);

        // 6 movimientos con EXACTAMENTE el mismo timestamp Fecha
        var ids = new[] { "mov_empate_c", "mov_empate_a", "mov_empate_f", "mov_empate_b", "mov_empate_e", "mov_empate_d" };
        foreach (var id in ids)
        {
            await db.Collection("movimientosStock").Document(id).SetAsync(new MovimientoStock
            {
                Id = id,
                InsumoId = "insumo_empates",
                Tipo = "IngresoManual",
                Cantidad = 1,
                SaldoResultante = 10,
                Fecha = fechaFija,
                Justificacion = "empate-" + id
            });
        }

        var stock = new StockService(db);

        // Paginando de a 2 elementos en orden ascendente con cursores
        var seenAsc = new List<string>();
        string? cursorAsc = null;
        ResultadoKardexPaginado? page3Asc = null;
        for (var p = 1; p <= 3; p++)
        {
            page3Asc = await stock.ObtenerKardexPaginado("insumo_empates", pageSize: 2, sortOrder: "asc", cursor: cursorAsc);
            Assert.Equal(2, page3Asc.Movimientos.Count);
            seenAsc.AddRange(page3Asc.Movimientos.Select(m => m.Id));
            cursorAsc = page3Asc.CursorSiguiente;
        }

        // Sin duplicados ni omisiones en orden ascendente
        Assert.Equal(6, seenAsc.Distinct().Count());
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal).ToList(), seenAsc);

        // Navegación hacia atrás en orden ascendente con cursores
        Assert.NotNull(page3Asc!.CursorAnterior);
        var p2BackAsc = await stock.ObtenerKardexPaginado("insumo_empates", pageSize: 2, sortOrder: "asc", cursor: page3Asc.CursorAnterior);
        Assert.Equal(2, p2BackAsc.Movimientos.Count);
        Assert.Equal(seenAsc.Skip(2).Take(2).ToList(), p2BackAsc.Movimientos.Select(m => m.Id).ToList());

        var p1BackAsc = await stock.ObtenerKardexPaginado("insumo_empates", pageSize: 2, sortOrder: "asc", cursor: p2BackAsc.CursorAnterior);
        Assert.Equal(2, p1BackAsc.Movimientos.Count);
        Assert.Equal(seenAsc.Take(2).ToList(), p1BackAsc.Movimientos.Select(m => m.Id).ToList());
        Assert.False(p1BackAsc.TienePaginaAnterior);

        // Paginando de a 2 elementos en orden descendente con cursores
        var seenDesc = new List<string>();
        string? cursorDesc = null;
        ResultadoKardexPaginado? page3Desc = null;
        for (var p = 1; p <= 3; p++)
        {
            page3Desc = await stock.ObtenerKardexPaginado("insumo_empates", pageSize: 2, sortOrder: "desc", cursor: cursorDesc);
            Assert.Equal(2, page3Desc.Movimientos.Count);
            seenDesc.AddRange(page3Desc.Movimientos.Select(m => m.Id));
            cursorDesc = page3Desc.CursorSiguiente;
        }

        // Sin duplicados ni omisiones en orden descendente
        Assert.Equal(6, seenDesc.Distinct().Count());
        Assert.Equal(ids.OrderByDescending(x => x, StringComparer.Ordinal).ToList(), seenDesc);

        // Navegación hacia atrás en orden descendente con cursores
        Assert.NotNull(page3Desc!.CursorAnterior);
        var p2BackDesc = await stock.ObtenerKardexPaginado("insumo_empates", pageSize: 2, sortOrder: "desc", cursor: page3Desc.CursorAnterior);
        Assert.Equal(2, p2BackDesc.Movimientos.Count);
        Assert.Equal(seenDesc.Skip(2).Take(2).ToList(), p2BackDesc.Movimientos.Select(m => m.Id).ToList());

        var p1BackDesc = await stock.ObtenerKardexPaginado("insumo_empates", pageSize: 2, sortOrder: "desc", cursor: p2BackDesc.CursorAnterior);
        Assert.Equal(2, p1BackDesc.Movimientos.Count);
        Assert.Equal(seenDesc.Take(2).ToList(), p1BackDesc.Movimientos.Select(m => m.Id).ToList());
        Assert.False(p1BackDesc.TienePaginaAnterior);
    }

    [LocalFirestoreFact]
    public async Task KardexFiltrosCombinadosServidor()
    {
        var projectId = "demo-kardex-comb-" + Guid.NewGuid().ToString("N")[..16];
        var db = new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

        static DateTime LocalUtc(int day, int hour, int minute, int second) =>
            new DateTime(2025, 4, day, hour, minute, second, DateTimeKind.Local).ToUniversalTime();

        // 1. Objetivo: Insumo A, Tipo IngresoManual, Fecha día 10
        await db.Collection("movimientosStock").Document("target_ok").SetAsync(new MovimientoStock
        {
            Id = "target_ok",
            InsumoId = "insumo_A",
            Tipo = "IngresoManual",
            Cantidad = 10,
            SaldoResultante = 10,
            Fecha = LocalUtc(10, 12, 0, 0),
            Justificacion = "objetivo"
        });

        // 2. Mismo insumo A, día 10, pero Tipo EgresoManual (incompatible)
        await db.Collection("movimientosStock").Document("incomp_tipo").SetAsync(new Dictionary<string, object>
        {
            ["Id"] = "incomp_tipo",
            ["InsumoId"] = "insumo_A",
            ["Tipo"] = "EgresoManual",
            ["Cantidad"] = "no_es_numero",
            ["SaldoResultante"] = 0,
            ["Fecha"] = Timestamp.FromDateTime(LocalUtc(10, 12, 0, 0)),
            ["Justificacion"] = "incompatible_tipo"
        });

        // 3. Mismo Tipo IngresoManual, día 10, pero Insumo B (incompatible)
        await db.Collection("movimientosStock").Document("incomp_insumo").SetAsync(new Dictionary<string, object>
        {
            ["Id"] = "incomp_insumo",
            ["InsumoId"] = "insumo_B",
            ["Tipo"] = "IngresoManual",
            ["Cantidad"] = "no_es_numero",
            ["SaldoResultante"] = 0,
            ["Fecha"] = Timestamp.FromDateTime(LocalUtc(10, 12, 0, 0)),
            ["Justificacion"] = "incompatible_insumo"
        });

        // 4. Mismo Insumo A y Tipo IngresoManual, pero fuera de rango (día 15)
        await db.Collection("movimientosStock").Document("incomp_fecha").SetAsync(new Dictionary<string, object>
        {
            ["Id"] = "incomp_fecha",
            ["InsumoId"] = "insumo_A",
            ["Tipo"] = "IngresoManual",
            ["Cantidad"] = "no_es_numero",
            ["SaldoResultante"] = 0,
            ["Fecha"] = Timestamp.FromDateTime(LocalUtc(15, 12, 0, 0)),
            ["Justificacion"] = "incompatible_fecha"
        });

        var stock = new StockService(db);

        // Consulta con filtros combinados: InsumoId + Tipo + Desde + Hasta
        var res = await stock.ObtenerKardexPaginado(
            insumoId: "insumo_A",
            tipo: "IngresoManual",
            desde: new DateTime(2025, 4, 10),
            hasta: new DateTime(2025, 4, 10),
            page: 1,
            pageSize: 20);

        Assert.Equal(1, res.TotalRegistros);
        var mov = Assert.Single(res.Movimientos);
        Assert.Equal("target_ok", mov.Id);
    }

    [Fact]
    public void KardexConfiguracionIndicesCompuestosValida()
    {
        var repoRoot = AppContext.BaseDirectory;
        while (repoRoot != null && !File.Exists(Path.Combine(repoRoot, "Firebase.csproj")))
        {
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }
        Assert.NotNull(repoRoot);
        var indexesPath = Path.Combine(repoRoot, "firestore.indexes.json");
        var firebaseJsonPath = Path.Combine(repoRoot, "firebase.json");

        Assert.True(File.Exists(indexesPath), $"El archivo de configuración de índices '{indexesPath}' debe existir.");
        Assert.True(File.Exists(firebaseJsonPath), $"El archivo de configuración '{firebaseJsonPath}' debe existir.");

        var firebaseJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(firebaseJsonPath));
        Assert.True(firebaseJson.RootElement.TryGetProperty("firestore", out var firestoreElem), "firebase.json debe tener sección firestore.");
        Assert.True(firestoreElem.TryGetProperty("indexes", out var indexesElem), "firebase.json debe apuntar a indexes.");
        Assert.Equal("firestore.indexes.json", indexesElem.GetString());

        var indexesDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(indexesPath));
        Assert.True(indexesDoc.RootElement.TryGetProperty("indexes", out var indexesArray), "firestore.indexes.json debe tener array 'indexes'.");

        var allIndexes = new List<(string collection, string queryScope, List<(string field, string order)> fields)>();
        foreach (var index in indexesArray.EnumerateArray())
        {
            var coll = index.GetProperty("collectionGroup").GetString()!;
            var queryScope = index.TryGetProperty("queryScope", out var qsElem) ? qsElem.GetString()! : "COLLECTION";

            var fields = new List<(string field, string order)>();
            foreach (var f in index.GetProperty("fields").EnumerateArray())
            {
                fields.Add((f.GetProperty("fieldPath").GetString()!, f.GetProperty("order").GetString()!));
            }
            allIndexes.Add((coll, queryScope, fields));
        }

        // =========================================================================================
        // 1. Conservación estricta de los 9 índices preexistentes observados en consola Firestore
        //    (Capturas de pantalla/{6AD02778-1869-4AD7-9529-647202D1FDBA}.png)
        // =========================================================================================
        bool ContieneIndice(string coleccion, string scope, params (string field, string order)[] camposEsperados)
        {
            return allIndexes.Any(idx =>
                idx.collection == coleccion &&
                idx.queryScope == scope &&
                idx.fields.Count == camposEsperados.Length &&
                Enumerable.Range(0, camposEsperados.Length).All(i =>
                    idx.fields[i].field == camposEsperados[i].field &&
                    idx.fields[i].order == camposEsperados[i].order));
        }

        // 1) empleados: Estado ASC, Nombre ASC
        Assert.True(ContieneIndice("empleados", "COLLECTION", ("Estado", "ASCENDING"), ("Nombre", "ASCENDING")),
            "Debe conservar índice preexistente en 'empleados' (Estado ASC, Nombre ASC).");

        // 2) paquetes_servicios: Estado ASC, TipoVehiculo ASC
        Assert.True(ContieneIndice("paquetes_servicios", "COLLECTION", ("Estado", "ASCENDING"), ("TipoVehiculo", "ASCENDING")),
            "Debe conservar índice preexistente en 'paquetes_servicios' (Estado ASC, TipoVehiculo ASC).");

        // 3) paquetes_servicios: Estado ASC, Nombre ASC
        Assert.True(ContieneIndice("paquetes_servicios", "COLLECTION", ("Estado", "ASCENDING"), ("Nombre", "ASCENDING")),
            "Debe conservar índice preexistente en 'paquetes_servicios' (Estado ASC, Nombre ASC).");

        // 4) paquetes_servicios: Estado ASC, PorcentajeDescuento ASC
        Assert.True(ContieneIndice("paquetes_servicios", "COLLECTION", ("Estado", "ASCENDING"), ("PorcentajeDescuento", "ASCENDING")),
            "Debe conservar índice preexistente en 'paquetes_servicios' (Estado ASC, PorcentajeDescuento ASC).");

        // 5) registros_auditoria: Action ASC, Timestamp DESC
        Assert.True(ContieneIndice("registros_auditoria", "COLLECTION", ("Action", "ASCENDING"), ("Timestamp", "DESCENDING")),
            "Debe conservar índice preexistente en 'registros_auditoria' (Action ASC, Timestamp DESC).");

        // 6) registros_auditoria: TargetType ASC, Timestamp DESC
        Assert.True(ContieneIndice("registros_auditoria", "COLLECTION", ("TargetType", "ASCENDING"), ("Timestamp", "DESCENDING")),
            "Debe conservar índice preexistente en 'registros_auditoria' (TargetType ASC, Timestamp DESC).");

        // 7) servicios: Estado ASC, Tipo ASC, Nombre ASC
        Assert.True(ContieneIndice("servicios", "COLLECTION", ("Estado", "ASCENDING"), ("Tipo", "ASCENDING"), ("Nombre", "ASCENDING")),
            "Debe conservar índice preexistente en 'servicios' (Estado ASC, Tipo ASC, Nombre ASC).");

        // 8) servicios: Estado ASC, Nombre ASC
        Assert.True(ContieneIndice("servicios", "COLLECTION", ("Estado", "ASCENDING"), ("Nombre", "ASCENDING")),
            "Debe conservar índice preexistente en 'servicios' (Estado ASC, Nombre ASC).");

        // 9) servicios: Tipo ASC, Estado ASC, Nombre ASC
        Assert.True(ContieneIndice("servicios", "COLLECTION", ("Tipo", "ASCENDING"), ("Estado", "ASCENDING"), ("Nombre", "ASCENDING")),
            "Debe conservar índice preexistente en 'servicios' (Tipo ASC, Estado ASC, Nombre ASC).");

        // =========================================================================================
        // 2. Cobertura de consultas reales de Kardex con cursores (movimientosStock)
        // =========================================================================================
        var kardexIndexes = allIndexes.Where(i => i.collection == "movimientosStock").Select(i => i.fields).ToList();

        // InsumoId + Fecha (ASC y DESC)
        Assert.Contains(kardexIndexes, idx => idx.Count == 2 && idx[0] == ("InsumoId", "ASCENDING") && idx[1] == ("Fecha", "ASCENDING"));
        Assert.Contains(kardexIndexes, idx => idx.Count == 2 && idx[0] == ("InsumoId", "ASCENDING") && idx[1] == ("Fecha", "DESCENDING"));

        // Tipo + Fecha (ASC y DESC)
        Assert.Contains(kardexIndexes, idx => idx.Count == 2 && idx[0] == ("Tipo", "ASCENDING") && idx[1] == ("Fecha", "ASCENDING"));
        Assert.Contains(kardexIndexes, idx => idx.Count == 2 && idx[0] == ("Tipo", "ASCENDING") && idx[1] == ("Fecha", "DESCENDING"));

        // InsumoId + Tipo + Fecha (ASC y DESC)
        Assert.Contains(kardexIndexes, idx => idx.Count == 3 && idx[0] == ("InsumoId", "ASCENDING") && idx[1] == ("Tipo", "ASCENDING") && idx[2] == ("Fecha", "ASCENDING"));
        Assert.Contains(kardexIndexes, idx => idx.Count == 3 && idx[0] == ("InsumoId", "ASCENDING") && idx[1] == ("Tipo", "ASCENDING") && idx[2] == ("Fecha", "DESCENDING"));

        // =========================================================================================
        // 3. Evitar redundancia demostrable en movimientosStock sin borrar índices del usuario
        //    (En Firestore, la igualdad en InsumoId y Tipo satisface tanto [InsumoId, Tipo, Fecha]
        //    como [Tipo, InsumoId, Fecha], por lo que duplicar la permutación inversa en movimientosStock
        //    es redundancia innecesaria que incrementa el costo de escritura).
        // =========================================================================================
        Assert.DoesNotContain(kardexIndexes, idx => idx.Count == 3 && idx[0] == ("Tipo", "ASCENDING") && idx[1] == ("InsumoId", "ASCENDING") && idx[2] == ("Fecha", "ASCENDING"));
        Assert.DoesNotContain(kardexIndexes, idx => idx.Count == 3 && idx[0] == ("Tipo", "ASCENDING") && idx[1] == ("InsumoId", "ASCENDING") && idx[2] == ("Fecha", "DESCENDING"));
        Assert.Equal(6, kardexIndexes.Count);
    }

    [LocalFirestoreFact]
    public async Task KardexSoportaDocumentosHistoricosConCamposOpcionalesOIncompletos()
    {
        var projectId = "demo-kardex-hist-" + Guid.NewGuid().ToString("N")[..16];
        var db = new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

        // Documento histórico: sin Id, sin UsuarioId, sin Justificacion, sin ReferenciaId en Firestore
        // y con valores numéricos enteros/doubles compatibles
        var docRef = db.Collection("movimientosStock").Document("mov_historico_1");
        await docRef.SetAsync(new Dictionary<string, object>
        {
            ["InsumoId"] = "insumo_hist",
            ["Tipo"] = "IngresoManual",
            ["Cantidad"] = 15.5,
            ["SaldoResultante"] = 30,
            ["Fecha"] = Timestamp.FromDateTime(new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc))
        });

        var stock = new StockService(db);
        var res = await stock.ObtenerKardexPaginado("insumo_hist", page: 1, pageSize: 20);

        Assert.Equal(1, res.TotalRegistros);
        var mov = Assert.Single(res.Movimientos);
        Assert.Equal("mov_historico_1", mov.Id);
        Assert.Equal("insumo_hist", mov.InsumoId);
        Assert.Equal(15.5m, mov.Cantidad);
        Assert.Equal(30m, mov.SaldoResultante);
        Assert.Equal(string.Empty, mov.Justificacion);
        Assert.Equal(string.Empty, mov.UsuarioId);
        Assert.Equal(string.Empty, mov.ReferenciaId);
    }

    [LocalFirestoreFact]
    public async Task KardexNavegacionBidireccionalPorCursoresPublicoHttp()
    {
        var projectId = "demo-kardex-bidir-" + Guid.NewGuid().ToString("N")[..16];
        var db = new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();

        await db.Collection("insumos").Document("insumo_bidir").SetAsync(new Insumo { Id = "insumo_bidir", Nombre = "Insumo Bidir" });

        var fechaBase = new DateTime(2025, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 45; i++)
        {
            var id = $"mov_b_{i:D2}";
            await db.Collection("movimientosStock").Document(id).SetAsync(new MovimientoStock
            {
                Id = id,
                InsumoId = "insumo_bidir",
                Tipo = "IngresoManual",
                Cantidad = 1,
                SaldoResultante = i + 1,
                Fecha = fechaBase.AddMinutes(i),
                Justificacion = $"bidir-{i:D2}"
            });
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(InsumosController).Assembly);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, ProveedorCatalogIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>();
        builder.Services.AddScoped<ProveedorService>();
        builder.Services.AddScoped<StockService>();
        builder.Services.AddScoped<ComprasService>();
        builder.Services.AddSingleton(Mock.Of<IAuditWriter>());
        builder.Services.AddSingleton(Mock.Of<IEmailSender>());

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();

        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
            };
            client.DefaultRequestHeaders.Add("X-Test-Permission", "Insumos.Kardex");

            async Task<string> Fetch(string query) => WebUtility.HtmlDecode(await client.GetStringAsync("/Insumos/Kardex" + query));

            // Paso 1: Página inicial (Página 1, 20 elementos)
            var p1 = await Fetch("?insumoId=insumo_bidir&sortOrder=asc");
            Assert.Contains("bidir-00", p1);
            Assert.Contains("bidir-19", p1);
            Assert.DoesNotContain("bidir-20", p1);
            Assert.DoesNotContain("bidir-44", p1);
            Assert.Contains("aria-disabled=\"true\"><</span>", p1);

            var nextMatch1 = System.Text.RegularExpressions.Regex.Match(p1, @"href=""[^""]*cursor=([a-zA-Z0-9%_-]+)[^""]*""[^>]*aria-label=""Siguiente""");
            Assert.True(nextMatch1.Success, "Página 1 debe tener enlace a Siguiente.");
            var nextCursor1 = Uri.UnescapeDataString(nextMatch1.Groups[1].Value);

            // Paso 2: Avanzar a Página 2 vía nextCursor1 (20 elementos)
            var p2 = await Fetch($"?insumoId=insumo_bidir&sortOrder=asc&cursor={Uri.EscapeDataString(nextCursor1)}");
            Assert.Contains("bidir-20", p2);
            Assert.Contains("bidir-39", p2);
            Assert.DoesNotContain("bidir-00", p2);
            Assert.DoesNotContain("bidir-19", p2);
            Assert.DoesNotContain("bidir-40", p2);

            var prevMatch2 = System.Text.RegularExpressions.Regex.Match(p2, @"href=""[^""]*cursor=([a-zA-Z0-9%_-]+)[^""]*""[^>]*aria-label=""Anterior""");
            Assert.True(prevMatch2.Success, "Página 2 debe tener enlace a Anterior.");
            var prevCursor2 = Uri.UnescapeDataString(prevMatch2.Groups[1].Value);

            var nextMatch2 = System.Text.RegularExpressions.Regex.Match(p2, @"href=""[^""]*cursor=([a-zA-Z0-9%_-]+)[^""]*""[^>]*aria-label=""Siguiente""");
            Assert.True(nextMatch2.Success, "Página 2 debe tener enlace a Siguiente.");
            var nextCursor2 = Uri.UnescapeDataString(nextMatch2.Groups[1].Value);

            // Paso 3: Avanzar a Página 3 vía nextCursor2 (5 elementos residuales)
            var p3 = await Fetch($"?insumoId=insumo_bidir&sortOrder=asc&cursor={Uri.EscapeDataString(nextCursor2)}");
            Assert.Contains("bidir-40", p3);
            Assert.Contains("bidir-44", p3);
            Assert.DoesNotContain("bidir-39", p3);
            Assert.Contains("aria-disabled=\"true\">></span>", p3);

            var prevMatch3 = System.Text.RegularExpressions.Regex.Match(p3, @"href=""[^""]*cursor=([a-zA-Z0-9%_-]+)[^""]*""[^>]*aria-label=""Anterior""");
            Assert.True(prevMatch3.Success, "Página 3 debe tener enlace a Anterior.");
            var prevCursor3 = Uri.UnescapeDataString(prevMatch3.Groups[1].Value);

            // Paso 4: Retroceder desde Página 3 a Página 2 vía prevCursor3
            var p2Back = await Fetch($"?insumoId=insumo_bidir&sortOrder=asc&cursor={Uri.EscapeDataString(prevCursor3)}");
            Assert.Contains("bidir-20", p2Back);
            Assert.Contains("bidir-39", p2Back);
            Assert.DoesNotContain("bidir-00", p2Back);
            Assert.DoesNotContain("bidir-44", p2Back);

            var prevMatchBack = System.Text.RegularExpressions.Regex.Match(p2Back, @"href=""[^""]*cursor=([a-zA-Z0-9%_-]+)[^""]*""[^>]*aria-label=""Anterior""");
            Assert.True(prevMatchBack.Success, "Página 2 retrocedida debe tener enlace a Anterior.");
            var prevCursorBack = Uri.UnescapeDataString(prevMatchBack.Groups[1].Value);

            // Paso 5: Retroceder desde Página 2 a Página 1 vía prevCursorBack
            var p1Back = await Fetch($"?insumoId=insumo_bidir&sortOrder=asc&cursor={Uri.EscapeDataString(prevCursorBack)}");
            Assert.Contains("bidir-00", p1Back);
            Assert.Contains("bidir-19", p1Back);
            Assert.DoesNotContain("bidir-20", p1Back);
            Assert.Contains("aria-disabled=\"true\"><</span>", p1Back);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
