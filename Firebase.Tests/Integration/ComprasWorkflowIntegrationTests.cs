using System.Reflection;
using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Moq;

namespace Firebase.Tests.Integration;

public sealed class ComprasWorkflowIntegrationTests
{
    private static async Task<FirestoreDb> PrepararCotizacionRed()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-quote-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", Estado = "Activo", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });
        return db;
    }

    [LocalFirestoreFact]
    public async Task QuoteRedSolicitudIndividualInvalidaNoPersiste()
    {
        var db = await PrepararCotizacionRed();
        var compras = new ComprasService(db);
        var resultado = await compras.CrearSolicitud(new SolicitudCotizacion { ProveedorId = "inexistente", Items = new() { new() { InsumoId = "i", Cantidad = 0 } } });
        Assert.False(resultado.EsExitoso);
        Assert.Equal("Utilice el formulario de cotizaciones para emitir la solicitud.", resultado.MensajeError);
        var valida = await compras.CrearSolicitud(new SolicitudCotizacion { ProveedorId = "p", Items = new() { new() { InsumoId = "i", Cantidad = 2 } } });
        Assert.False(valida.EsExitoso);
        Assert.Equal("Utilice el formulario de cotizaciones para emitir la solicitud.", valida.MensajeError);
        Assert.Empty(await compras.ObtenerSolicitudes());
        Assert.Empty(await compras.ObtenerOrdenes());
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task QuoteRedEmisionRegistraAuditoriaGrupo()
    {
        var db = await PrepararCotizacionRed();
        var compras = new ComprasService(db);
        Assert.True((await compras.CrearSolicitudes(new() { "p" }, new() { new() { InsumoId = "i", Cantidad = 2 } }, null)).EsExitoso);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        Assert.NotEmpty(solicitud.GrupoId);
        var logs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Single(logs, d => d.GetValue<string>("TargetType") == "SolicitudCotizacion" && d.GetValue<string>("TargetId") == solicitud.GrupoId);
        Assert.Empty(await compras.ObtenerOrdenes());
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task QuoteRedRespuestaRegistraAuditoria()
    {
        var db = await PrepararCotizacionRed();
        var compras = new ComprasService(db);
        Assert.True((await compras.CrearSolicitudes(new() { "p" }, new() { new() { InsumoId = "i", Cantidad = 2 } }, null)).EsExitoso);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        var previos = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();
        await Responder(compras, solicitud);
        Assert.Equal("Respondida", Assert.Single(await compras.ObtenerSolicitudes()).Estado);
        var logs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Single(logs, d => !previos.Contains(d.Id) && d.GetValue<string>("TargetType") == "SolicitudCotizacion" && d.GetValue<string>("TargetId") == solicitud.Id);
        Assert.Empty(await compras.ObtenerOrdenes());
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task QuoteRedAdjudicacionTransporteInciertoNoSimulaEnvioNiReenvia()
    {
        var db = await PrepararCotizacionRed();
        var compras = new ComprasService(db);
        Assert.True((await compras.CrearSolicitudes(new() { "p" }, new() { new() { InsumoId = "i", Cantidad = 2 } }, null)).EsExitoso);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        await Responder(compras, solicitud);
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(false, "incierto"));
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var controller = new Firebase.Controllers.InsumosController(new InsumoService(db), new ProveedorService(db), new StockService(db), compras, Mock.Of<IAuditWriter>(), sender.Object, new ConfiguracionService(db, cache));
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = context };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(context, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        await controller.Adjudicar(solicitud.Id, null);
        var orden = Assert.Single(await compras.ObtenerOrdenes());
        Assert.Equal("RevisionManual", orden.Estado);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        var antes = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count;
        await controller.Adjudicar(solicitud.Id, null);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(await compras.ObtenerOrdenes());
        Assert.Equal(antes, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task QuoteRedEmisionYAdjudicacionEnvianCorreoFormalAceptado()
    {
        var db = await PrepararCotizacionRed();
        var compras = new ComprasService(db);
        var sender = new Mock<IEmailSender>();
        var mensajes = new List<EmailMessage>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => mensajes.Add(m))
            .ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var controller = new Firebase.Controllers.InsumosController(new InsumoService(db), new ProveedorService(db), new StockService(db), compras, Mock.Of<IAuditWriter>(), sender.Object, new ConfiguracionService(db, cache));
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = context };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(context, Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        await controller.CrearSolicitudes(new() { "p" }, new() { new() { InsumoId = "i", Cantidad = 2 } }, null);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        Assert.Equal("p@example.invalid", Assert.Single(mensajes).To);
        await Responder(compras, solicitud);
        await controller.Adjudicar(solicitud.Id, null);
        Assert.Equal("Enviada", Assert.Single(await compras.ObtenerOrdenes()).Estado);
        Assert.Equal(2, mensajes.Count);
        Assert.Equal("p@example.invalid", mensajes[1].To);
        await controller.Adjudicar(solicitud.Id, null);
        Assert.Equal(2, mensajes.Count);
        Assert.Single(await compras.ObtenerOrdenes());
    }

    [LocalFirestoreFact]
    public async Task QuoteRedAdjudicacionSinTransporteNoDeclaraEnvio()
    {
        var db = await PrepararCotizacionRed();
        var compras = new ComprasService(db);
        Assert.True((await compras.CrearSolicitudes(new() { "p" }, new() { new() { InsumoId = "i", Cantidad = 2 } }, null)).EsExitoso);
        var solicitud = Assert.Single(await compras.ObtenerSolicitudes());
        await Responder(compras, solicitud);
        var resultado = await compras.Adjudicar(solicitud.Id);
        Assert.False(resultado.EsExitoso);
        Assert.Equal("RevisionManual", Assert.Single(await compras.ObtenerOrdenes()).Estado);
    }

    [LocalFirestoreFact]
    public async Task S11OrdenManualSinCotizacionNiAntecedenteNoDespachaNiPersiste()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-s11-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", Estado = "Activo", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        var compras = new ComprasService(db);
        var orden = new OrdenCompra { ProveedorId = "p", Items = new() { new() { InsumoId = "i", Cantidad = 3, PrecioUnitario = 5 } } };
        var resultado = await compras.CrearOrdenManualConAuditoria(orden, sender.Object, "actor", "actor@example.invalid");
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(resultado.Resultado.EsExitoso);
        Assert.Empty(await compras.ObtenerOrdenes());
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        Assert.Empty(await compras.ObtenerSolicitudes());
    }

    [LocalFirestoreFact]
    public async Task S11OrdenManualSinTransporteNoSimulaEnvioSinCotizacionNiAntecedente()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-s11-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", Estado = "Activo", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });
        var compras = new ComprasService(db);
        var orden = new OrdenCompra { ProveedorId = "p", Items = new() { new() { InsumoId = "i", Cantidad = 3, PrecioUnitario = 5 } } };
        var resultado = await compras.CrearOrdenManualConAuditoria(orden, "actor", "actor@example.invalid");
        Assert.False(resultado.Resultado.EsExitoso);
        Assert.Empty(await compras.ObtenerOrdenes());
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        Assert.Empty(await compras.ObtenerSolicitudes());
    }

    [LocalFirestoreFact]
    public async Task OrdenPendienteNoPuedeMarcarseEnviadaSinAprobacionYTransporte()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-orden-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("ordenesCompra").Document("pendiente").SetAsync(new OrdenCompra { Id = "pendiente", Estado = "PendienteAprobacion" });
        var compras = new ComprasService(db);
        var resultado = await compras.CambiarEstadoOrden("pendiente", "Enviada");
        Assert.False(resultado.EsExitoso);
        Assert.Equal("PendienteAprobacion", Assert.Single(await compras.ObtenerOrdenes()).Estado);
    }

    [LocalFirestoreFact]
    public async Task AprobacionConcurrenteConCancelacionRespetaEstado()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-carrera-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var compras = new ComprasService(db);
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", EmailOrdenes = "p@example.invalid" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", StockActual = 3 });
        for (var n = 0; n < 10; n++)
        {
            var id = "o" + n;
            await db.Collection("ordenesCompra").Document(id).SetAsync(new OrdenCompra { Id = id, ProveedorId = "p", Estado = "PendienteAprobacion", Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 2, PrecioUnitario = 5 } } });
            var sender = new Moq.Mock<IEmailSender>();
            sender.Setup(x => x.EnviarAsync(Moq.It.IsAny<EmailMessage>(), Moq.It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(false, "incierto"));
            var aprobar = compras.AprobarOrden(id, sender.Object);
            var cancelar = compras.CambiarEstadoOrden(id, "Cancelada", "prueba");
            await Task.WhenAll(aprobar, cancelar);
            var orden = (await db.Collection("ordenesCompra").Document(id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
            Assert.Equal(cancelar.Result.EsExitoso ? "Cancelada" : "RevisionManual", orden.Estado);
            sender.Verify(x => x.EnviarAsync(Moq.It.IsAny<EmailMessage>(), Moq.It.IsAny<CancellationToken>()), cancelar.Result.EsExitoso ? Moq.Times.Never() : Moq.Times.Once());
            Assert.False(aprobar.Result.EsExitoso);
        }
        Assert.Equal(3, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
    }

    private static IEmailSender TransporteAceptado()
    {
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        return sender.Object;
    }

    private static async Task<SolicitudCotizacion> Emitir(ComprasService compras, SolicitudCotizacion solicitud)
    {
        var antes = (await compras.ObtenerSolicitudes()).Select(s => s.Id).ToHashSet();
        Assert.True((await compras.CrearSolicitudes(new() { solicitud.ProveedorId }, solicitud.Items, solicitud.Notas)).EsExitoso);
        return Assert.Single((await compras.ObtenerSolicitudes()).Where(s => !antes.Contains(s.Id)));
    }

    private static async Task Responder(ComprasService compras, SolicitudCotizacion solicitud)
    {
        var respuesta = new CotizacionRespuesta { SolicitudId = solicitud.Id, PlazoEntrega = "2", Items = solicitud.Items.Select(x => new OrdenCompraItem { InsumoId = x.InsumoId, PrecioUnitario = 5 }).ToList() };
        Assert.True((await compras.RegistrarRespuesta(respuesta)).EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionesConcurrentesDelMismoGrupoSoloGeneranUnaOrden()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var compras = new ComprasService(db);
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Insumo" });
        for (var i = 0; i < 10; i++)
        {
            var ids = new List<string>();
            for (var j = 0; j < 3; j++)
            {
                var proveedor = new Proveedor { RazonSocial = $"Proveedor{i}-{j}", IdentificacionTributaria = $"id{i}-{j}", Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
                Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
                ids.Add(proveedor.Id);
            }
            var antes = (await compras.ObtenerSolicitudes()).Select(s => s.Id).ToHashSet();
            Assert.True((await compras.CrearSolicitudes(ids, new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 1 } }, null)).EsExitoso);
            var solicitudes = (await compras.ObtenerSolicitudes()).Where(s => !antes.Contains(s.Id)).ToList();
            Assert.Equal(3, solicitudes.Count);
            var grupo = solicitudes[0].GrupoId;
            foreach (var solicitud in solicitudes) await Responder(compras, solicitud);
            async Task<bool> Intentar(SolicitudCotizacion solicitud)
            {
                try { return (await compras.Adjudicar(solicitud.Id, null, TransporteAceptado())).EsExitoso; }
                catch (InvalidOperationException ex) { Assert.Equal("La solicitud ya fue resuelta.", ex.Message); return false; }
            }
            var resultados = await Task.WhenAll(solicitudes.Select(Intentar));
            Assert.Single(resultados.Where(x => x));
            var ganador = solicitudes[Array.IndexOf(resultados, true)];
            var estados = (await compras.ObtenerSolicitudes()).Where(s => s.GrupoId == grupo).ToList();
            Assert.Equal(3, estados.Count);
            Assert.All(estados, s => Assert.Equal(s.Id == ganador.Id ? "Adjudicada" : "Descartada", s.Estado));
            var orden = Assert.Single((await compras.ObtenerOrdenes()).Where(o => o.GrupoCotizacionId == grupo));
            Assert.Equal(ganador.ProveedorId, orden.ProveedorId);
            Assert.Equal("Enviada", orden.Estado);
            foreach (var solicitud in solicitudes) Assert.False(await Intentar(solicitud));
            Assert.Single((await compras.ObtenerOrdenes()).Where(o => o.GrupoCotizacionId == grupo));
            Assert.All((await compras.ObtenerSolicitudes()).Where(s => s.GrupoId == grupo), s => Assert.Equal(s.Id == ganador.Id ? "Adjudicada" : "Descartada", s.Estado));
        }
    }

    [LocalFirestoreFact]
    public async Task DesactivacionYAdjudicacionConcurrentesNoDejanOrdenAbiertaConProveedorInactivo()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var compras = new ComprasService(db);
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Insumo" });
        for (var i = 0; i < 5; i++)
        {
            var proveedor = new Proveedor { RazonSocial = "Proveedor" + i, IdentificacionTributaria = "id" + i, Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
            Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
            var solicitud = await Emitir(compras, new SolicitudCotizacion { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 1 } } });
            await Responder(compras, solicitud);
            var auditoriasPrevias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();
            var ordenesPrevias = (await compras.ObtenerOrdenes()).Select(o => o.Id).ToHashSet();
            async Task<bool> Adjudicar()
            {
                try { return (await compras.Adjudicar(solicitud.Id, null, TransporteAceptado())).EsExitoso; }
                catch (InvalidOperationException ex) { Assert.Equal("El proveedor no existe o está inactivo.", ex.Message); return false; }
            }
            var adjudicacion = Adjudicar();
            var desactivacion = proveedores.CambiarEstado(proveedor.Id, "Inactivo");
            await Task.WhenAll(adjudicacion, desactivacion);
            Assert.NotEqual(adjudicacion.Result, desactivacion.Result.EsExitoso);
            var actual = (await proveedores.Obtener(proveedor.Id))!;
            var ordenes = (await compras.ObtenerOrdenes()).Where(o => o.ProveedorId == proveedor.Id).ToList();
            Assert.Equal(adjudicacion.Result ? "Activo" : "Inactivo", actual.Estado);
            Assert.Equal(adjudicacion.Result ? 1 : 0, ordenes.Count);
            Assert.Equal(adjudicacion.Result ? "Adjudicada" : "Respondida", (await compras.ObtenerSolicitudes()).Single(s => s.Id == solicitud.Id).Estado);
            if (ordenes.Count == 1) Assert.Equal("Enviada", ordenes[0].Estado);
            var todasOrdenes = await compras.ObtenerOrdenes();
            Assert.All(ordenesPrevias, id => Assert.Contains(todasOrdenes, o => o.Id == id));
            Assert.Equal(adjudicacion.Result ? 1 : 0, todasOrdenes.Count(o => !ordenesPrevias.Contains(o.Id)));
            var auditorias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
            Assert.All(auditoriasPrevias, id => Assert.Contains(auditorias, d => d.Id == id));
            Assert.Equal(adjudicacion.Result ? 4 : 1, auditorias.Count(d => !auditoriasPrevias.Contains(d.Id)));
        }
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionYDesactivacionConcurrentesNoDejanOrdenConProveedorInactivo()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var compras = new ComprasService(db);
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Insumo" });
        for (var i = 0; i < 5; i++)
        {
            var proveedor = new Proveedor { RazonSocial = "Proveedor" + i, IdentificacionTributaria = "id" + i, Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
            Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
            var solicitud = await Emitir(compras, new SolicitudCotizacion { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 1 } } });
            await Responder(compras, solicitud);
            async Task<bool> Adjudicar()
            {
                try { return (await compras.Adjudicar(solicitud.Id, null, TransporteAceptado())).EsExitoso; }
                catch (InvalidOperationException ex) { Assert.Equal("El proveedor no existe o está inactivo.", ex.Message); return false; }
            }
            var adjudicacion = Adjudicar();
            var desactivacion = proveedores.CambiarEstado(proveedor.Id, "Inactivo");
            await Task.WhenAll(adjudicacion, desactivacion);
            Assert.NotEqual(adjudicacion.Result, desactivacion.Result.EsExitoso);
            var ordenes = (await compras.ObtenerOrdenes()).Where(o => o.ProveedorId == proveedor.Id).ToList();
            Assert.Equal(adjudicacion.Result ? "Activo" : "Inactivo", (await proveedores.Obtener(proveedor.Id))!.Estado);
            Assert.Equal(adjudicacion.Result ? 1 : 0, ordenes.Count);
            Assert.Equal(adjudicacion.Result ? "Adjudicada" : "Respondida", (await compras.ObtenerSolicitudes()).Single(s => s.Id == solicitud.Id).Estado);
        }
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionAntesDeDesactivarConservaProveedorActivoYOrdenAbierta()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var compras = new ComprasService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Insumo" });
        var solicitud = await Emitir(compras, new SolicitudCotizacion { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 1 } } });
        await Responder(compras, solicitud);
        Assert.True((await compras.Adjudicar(solicitud.Id, null, TransporteAceptado())).EsExitoso);
        var resultado = await proveedores.CambiarEstado(proveedor.Id, "Inactivo");
        Assert.False(resultado.EsExitoso);
        Assert.Equal("No se puede desactivar un proveedor con órdenes pendientes.", resultado.MensajeError);
        Assert.Equal("Activo", (await proveedores.Obtener(proveedor.Id))!.Estado);
        var orden = Assert.Single(await compras.ObtenerOrdenes());
        Assert.Equal(proveedor.Id, orden.ProveedorId);
        Assert.Equal("Enviada", orden.Estado);
        Assert.Equal("Adjudicada", Assert.Single(await compras.ObtenerSolicitudes()).Estado);
    }

    [LocalFirestoreFact]
    public async Task EdicionYDesactivacionConcurrentesPreservanEstado()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        for (var i = 0; i < 30; i++)
        {
            var proveedor = new Proveedor { RazonSocial = "Proveedor" + i, IdentificacionTributaria = "id" + i, Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
            Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
            proveedor.Contacto = "0987654321";
            var resultados = await Task.WhenAll(proveedores.Guardar(proveedor, false), proveedores.CambiarEstado(proveedor.Id, "Inactivo"));
            Assert.All(resultados, r => Assert.True(r.EsExitoso));
            var actual = (await proveedores.Obtener(proveedor.Id))!;
            Assert.Equal("Inactivo", actual.Estado);
            Assert.Equal("0987654321", actual.Contacto);
        }
    }

    [LocalFirestoreFact]
    public async Task ProveedorInactivoNoRecibeOrdenManualNiAdjudicada()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var proveedores = new ProveedorService(db);
        var compras = new ComprasService(db);
        var proveedor = new Proveedor { RazonSocial = "Proveedor", IdentificacionTributaria = "123", Contacto = "1234567890", EmailOrdenes = "p@example.invalid" };
        Assert.True((await proveedores.Guardar(proveedor, true)).EsExitoso);
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Insumo" });
        var solicitud = await Emitir(compras, new SolicitudCotizacion { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 1 } } });
        await Responder(compras, solicitud);
        Assert.True((await proveedores.CambiarEstado(proveedor.Id, "Inactivo")).EsExitoso);
        var manual = await compras.CrearOrdenManual(new OrdenCompra { ProveedorId = proveedor.Id, Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 1 } } });
        Assert.False(manual.EsExitoso);
        Assert.Equal("Debe adjudicar una cotización o preparar una repetición reciente para aprobación.", manual.MensajeError);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => compras.Adjudicar(solicitud.Id));
        Assert.Equal("El proveedor no existe o está inactivo.", error.Message);
        Assert.Empty(await compras.ObtenerOrdenes());
        Assert.Equal("Respondida", Assert.Single(await compras.ObtenerSolicitudes()).Estado);
        Assert.Equal("Inactivo", (await proveedores.Obtener(proveedor.Id))!.Estado);
    }

    [LocalFirestoreFact]
    public async Task FalloAuditoriaCreacionEnAdjudicacionRevierteOrdenSolicitudYMovimientos()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });

        var compras = new ComprasService(db);
        var solicitud = await Emitir(compras, new SolicitudCotizacion { ProveedorId = "p", Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 3 } } });
        await Responder(compras, solicitud);
        var ordenesPrevias = (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        var movimientosPrevios = (await db.Collection("movimientosStock").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        var auditoriasPrevias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        compras.InterceptorAuditoria = log =>
        {
            if (log.Action == "Creacion de orden de compra")
                throw new InvalidOperationException("Fallo forzado al auditar creacion transaccional");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => compras.Adjudicar(solicitud.Id, null, "actor-uid", "actor@example.invalid"));

        Assert.Equal(ordenesPrevias, (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal(movimientosPrevios, (await db.Collection("movimientosStock").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal(auditoriasPrevias, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal("Respondida", (await compras.ObtenerSolicitudes()).Single(s => s.Id == solicitud.Id).Estado);
        Assert.Equal("Activo", (await db.Collection("proveedores").Document("p").GetSnapshotAsync()).ConvertTo<Proveedor>().Estado);
        Assert.Equal("Activo", (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().Estado);
    }

    [LocalFirestoreFact]
    public async Task FalloSimuladoDeAuditoriaEnRecepcionHaceRollbackDeStockKardexYOrden()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", StockActual = 5m, Estado = "Activo" });

        var orden = new OrdenCompra
        {
            Id = "ord-rec",
            ProveedorId = "p",
            Estado = "Enviada",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 4m, CantidadRecibida = 0m } }
        };
        await db.Collection("ordenesCompra").Document(orden.Id).SetAsync(orden);

        var compras = new ComprasService(db);
        compras.InterceptorAuditoria = log =>
        {
            if (log.Action == "Recepción de orden de compra")
                throw new InvalidOperationException("Fallo forzado al auditar recepcion transaccional");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            compras.RecibirConAuditoria(orden.Id, new Dictionary<string, decimal> { ["i"] = 4m }, userId: "actor-uid", userEmail: "actor@example.invalid"));

        var insumo = (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal(5m, insumo.StockActual);
        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
        var ordenDb = (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Enviada", ordenDb.Estado);
        Assert.Equal(0m, ordenDb.Items[0].CantidadRecibida);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task FalloSimuladoDeAuditoriaEnAprobacionFase1HaceRollbackYSinCorreo()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });

        var orden = new OrdenCompra
        {
            Id = "ord-aprob-fase1",
            ProveedorId = "p",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2m, PrecioUnitario = 5m } }
        };
        await db.Collection("ordenesCompra").Document(orden.Id).SetAsync(orden);

        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));

        var compras = new ComprasService(db);
        compras.InterceptorAuditoria = log =>
        {
            if (log.Action == "Paso de orden de compra a revision manual")
                throw new InvalidOperationException("Fallo forzado al auditar aprobacion fase 1");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid"));

        var ordenDb = (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("PendienteAprobacion", ordenDb.Estado);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AprobacionExitosaRegistraDosEventosTransaccionalesYDesacoplaTransporte()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });

        var orden = new OrdenCompra
        {
            Id = "ord-aprob-2fases",
            ProveedorId = "p",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2m, PrecioUnitario = 5m } }
        };
        await db.Collection("ordenesCompra").Document(orden.Id).SetAsync(orden);

        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));

        var compras = new ComprasService(db);
        var resultado = await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid");
        Assert.True(resultado.Resultado.EsExitoso);

        var ordenDb = (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Enviada", ordenDb.Estado);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);

        var auditDocs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents;
        Assert.Equal(2, auditDocs.Count);

        var ev1 = Assert.Single(auditDocs, d => d.GetValue<string>("Action") == "Paso de orden de compra a revision manual");
        Assert.Equal("actor-uid", ev1.GetValue<string>("UserId"));
        Assert.Equal("actor@example.invalid", ev1.GetValue<string>("UserEmail"));
        Assert.Equal("PendienteAprobacion", ev1.GetValue<Dictionary<string, string>>("Antes")["Estado"]);
        Assert.Equal("RevisionManual", ev1.GetValue<Dictionary<string, string>>("Despues")["Estado"]);

        var ev2 = Assert.Single(auditDocs, d => d.GetValue<string>("Action") == "Aprobacion de orden de compra");
        Assert.Equal("actor-uid", ev2.GetValue<string>("UserId"));
        Assert.Equal("actor@example.invalid", ev2.GetValue<string>("UserEmail"));
        Assert.Equal("RevisionManual", ev2.GetValue<Dictionary<string, string>>("Antes")["Estado"]);
        Assert.Equal("Enviada", ev2.GetValue<Dictionary<string, string>>("Despues")["Estado"]);
    }

    [LocalFirestoreFact]
    public async Task AprobacionInterrumpidaEnTransporteConservaEventoFase1SinEventoFase2YSinReenvio()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });

        var orden = new OrdenCompra
        {
            Id = "ord-aprob-interrumpida",
            ProveedorId = "p",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2m, PrecioUnitario = 5m } }
        };
        await db.Collection("ordenesCompra").Document(orden.Id).SetAsync(orden);

        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("Interrupción simulada de red"));

        var compras = new ComprasService(db);
        var resultado = await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid");
        Assert.False(resultado.Resultado.EsExitoso);
        Assert.True(resultado.TransicionARevisionManual);

        var ordenDb = (await db.Collection("ordenesCompra").Document(orden.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("RevisionManual", ordenDb.Estado);

        var auditDocs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents;
        var evFase1 = Assert.Single(auditDocs);
        Assert.Equal("Paso de orden de compra a revision manual", evFase1.GetValue<string>("Action"));
        Assert.Equal("PendienteAprobacion", evFase1.GetValue<Dictionary<string, string>>("Antes")["Estado"]);
        Assert.Equal("RevisionManual", evFase1.GetValue<Dictionary<string, string>>("Despues")["Estado"]);

        // Reintento: no reenvía automáticamente y no duplica evento
        var reintento = await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid");
        Assert.False(reintento.Resultado.EsExitoso);
        Assert.Equal("La orden no está pendiente de aprobación; revise manualmente el resultado del envío.", reintento.Resultado.MensajeError);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents);
    }

    private static async Task<OrdenCompra> PrepararRepeticionReciente(FirestoreDb db, ComprasService compras, string email)
    {
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", Estado = "Activo", EmailOrdenes = email });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo", StockMinimo = 1, CantidadReabastecimiento = 3, ReabastecimientoAutomatico = true, ProveedoresIds = new() { "p" } });
        await db.Collection("ordenesCompra").Document("anterior").SetAsync(new OrdenCompra { Id = "anterior", ProveedorId = "p", Estado = "Recibida", Items = new() { new() { InsumoId = "i", Cantidad = 3, PrecioUnitario = 5 } } });
        await db.Collection("movimientosStock").Document("recepcion").SetAsync(new MovimientoStock { Id = "recepcion", InsumoId = "i", ReferenciaId = "anterior", Tipo = "RecepcionOrden", Cantidad = 3, Fecha = DateTime.UtcNow });
        await new StockService(db).EvaluarReabastecimiento("i");
        return Assert.Single(await compras.ObtenerOrdenes(), o => o.Estado == "PendienteAprobacion");
    }

    [LocalFirestoreFact]
    public async Task AprobacionConTransporteAceptadoRegistraDosEventosYDesacoplaTransporte()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var compras = new ComprasService(db);
        var orden = await PrepararRepeticionReciente(db, compras, "p@example.invalid");
        var previos = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();
        Assert.Single(previos);
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEnvioEmail(true, "Aceptado"));
        var res = await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid");
        Assert.True(res.Resultado.EsExitoso);
        Assert.Contains("El transporte aceptó la solicitud; esto no confirma entrega ni lectura.", res.Resultado.MensajeExito);

        var ordenDb = (await db.Collection("ordenesCompra").Document(res.OrdenId).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Enviada", ordenDb.Estado);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);

        var auditDocs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", res.OrdenId).GetSnapshotAsync()).Documents;
        Assert.Equal(3, auditDocs.Count);
        Assert.All(previos, id => Assert.Contains(auditDocs, d => d.Id == id));
        var ev1 = Assert.Single(auditDocs, d => d.GetValue<string>("Action") == "Paso de orden de compra a revision manual");
        Assert.Equal("actor-uid", ev1.GetValue<string>("UserId"));
        Assert.Equal("actor@example.invalid", ev1.GetValue<string>("UserEmail"));
        Assert.Equal("PendienteAprobacion", ev1.GetValue<Dictionary<string, string>>("Antes")["Estado"]);
        Assert.Equal("RevisionManual", ev1.GetValue<Dictionary<string, string>>("Despues")["Estado"]);

        var ev2 = Assert.Single(auditDocs, d => d.GetValue<string>("Action") == "Aprobacion de orden de compra");
        Assert.Equal("actor-uid", ev2.GetValue<string>("UserId"));
        Assert.Equal("actor@example.invalid", ev2.GetValue<string>("UserEmail"));
        Assert.Equal("RevisionManual", ev2.GetValue<Dictionary<string, string>>("Antes")["Estado"]);
        Assert.Equal("Enviada", ev2.GetValue<Dictionary<string, string>>("Despues")["Estado"]);
    }

    [LocalFirestoreFact]
    public async Task AprobacionConFalloDeTransporteConservaOrdenEnRevisionManualYUnSoloEvento()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var compras = new ComprasService(db);
        var orden = await PrepararRepeticionReciente(db, compras, "p@example.invalid");
        var previos = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();
        Assert.Single(previos);
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoEnvioEmail(false, "Rechazo del proveedor"));
        var res = await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid");
        Assert.False(res.Resultado.EsExitoso);
        Assert.True(res.TransicionARevisionManual);
        Assert.Equal("Resultado incierto. Se requiere revisión manual.", res.Resultado.MensajeError);

        var ordenDb = (await db.Collection("ordenesCompra").Document(res.OrdenId).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("RevisionManual", ordenDb.Estado);

        var auditDocs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", res.OrdenId).GetSnapshotAsync()).Documents;
        Assert.Equal(2, auditDocs.Count);
        Assert.All(previos, id => Assert.Contains(auditDocs, d => d.Id == id));
        var ev1 = Assert.Single(auditDocs.Where(d => !previos.Contains(d.Id)));
        Assert.Equal("Paso de orden de compra a revision manual", ev1.GetValue<string>("Action"));
        Assert.Equal("RevisionManual", ev1.GetValue<Dictionary<string, string>>("Despues")["Estado"]);
        Assert.False((await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object)).Resultado.EsExitoso);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task AprobacionConExcepcionEnTransporteConservaOrdenEnRevisionManualSinFase2()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var compras = new ComprasService(db);
        var orden = await PrepararRepeticionReciente(db, compras, "p@example.invalid");
        var previos = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();
        Assert.Single(previos);
        var sender = new Mock<IEmailSender>();
        sender.Setup(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Falla de red simulada"));
        var res = await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid");
        Assert.False(res.Resultado.EsExitoso);
        Assert.True(res.TransicionARevisionManual);
        Assert.Equal("Resultado incierto. Se requiere revisión manual.", res.Resultado.MensajeError);

        var ordenDb = (await db.Collection("ordenesCompra").Document(res.OrdenId).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("RevisionManual", ordenDb.Estado);

        var auditDocs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", res.OrdenId).GetSnapshotAsync()).Documents;
        Assert.Equal(2, auditDocs.Count);
        Assert.All(previos, id => Assert.Contains(auditDocs, d => d.Id == id));
        var ev1 = Assert.Single(auditDocs.Where(d => !previos.Contains(d.Id)));
        Assert.Equal("Paso de orden de compra a revision manual", ev1.GetValue<string>("Action"));
        Assert.False((await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object)).Resultado.EsExitoso);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task AprobacionConProveedorSinEmailConservaOrdenEnRevisionManualYSinDisparoDeTransporte()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var compras = new ComprasService(db);
        var orden = await PrepararRepeticionReciente(db, compras, "");
        var previos = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents.Select(d => d.Id).ToHashSet();
        Assert.Single(previos);
        var sender = new Mock<IEmailSender>();
        var res = await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object, "actor-uid", "actor@example.invalid");
        Assert.False(res.Resultado.EsExitoso);
        Assert.True(res.TransicionARevisionManual);
        Assert.Equal("Resultado incierto. Se requiere revisión manual.", res.Resultado.MensajeError);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);

        var ordenDb = (await db.Collection("ordenesCompra").Document(res.OrdenId).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("RevisionManual", ordenDb.Estado);

        var auditDocs = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", res.OrdenId).GetSnapshotAsync()).Documents;
        Assert.Equal(2, auditDocs.Count);
        Assert.All(previos, id => Assert.Contains(auditDocs, d => d.Id == id));
        var ev1 = Assert.Single(auditDocs.Where(d => !previos.Contains(d.Id)));
        Assert.Equal("Paso de orden de compra a revision manual", ev1.GetValue<string>("Action"));
        Assert.False((await compras.AprobarOrdenConAuditoria(orden.Id, sender.Object)).Resultado.EsExitoso);
        sender.Verify(x => x.EnviarAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(2, (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Count);
    }

    [LocalFirestoreFact]
    public async Task FalloAuditoriaSolicitudEnAdjudicacionRevierteOrdenSolicitudYMovimientos()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", Estado = "Activo" });

        var compras = new ComprasService(db);
        var solicitud = await Emitir(compras, new SolicitudCotizacion { ProveedorId = "p", Items = new() { new OrdenCompraItem { InsumoId = "i", Cantidad = 3m } } });
        await Responder(compras, solicitud);
        var ordenesPrevias = (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        var movimientosPrevios = (await db.Collection("movimientosStock").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        var auditoriasPrevias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray();
        compras.InterceptorAuditoria = log =>
        {
            if (log.Action == "Adjudicación de cotización")
                throw new InvalidOperationException("Fallo forzado al auditar adjudicacion");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => compras.Adjudicar(solicitud.Id, null, "actor-uid", "actor@example.invalid"));

        Assert.Equal(ordenesPrevias, (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal(movimientosPrevios, (await db.Collection("movimientosStock").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal(auditoriasPrevias, (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.Id).OrderBy(id => id).ToArray());
        Assert.Equal("Respondida", (await compras.ObtenerSolicitudes()).Single(s => s.Id == solicitud.Id).Estado);
        Assert.Equal("Activo", (await db.Collection("proveedores").Document("p").GetSnapshotAsync()).ConvertTo<Proveedor>().Estado);
        Assert.Equal("Activo", (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().Estado);
    }

    [LocalFirestoreFact]
    public async Task OperacionesNoOpEnEdicionCambioEstadoYRecepcionNoMutanNiGeneranAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-compras-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Jabon", StockActual = 10m, Estado = "Activo" });

        var ordenPendiente = new OrdenCompra
        {
            Id = "ord-noop-pendiente",
            ProveedorId = "p",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Jabon", Cantidad = 2m, PrecioUnitario = 5m } }
        };
        await db.Collection("ordenesCompra").Document(ordenPendiente.Id).SetAsync(ordenPendiente);

        var compras = new ComprasService(db);

        // 1. Edición sin cambios numéricos normalizados
        var resEd = await compras.EditarOrdenPendienteConAuditoria(ordenPendiente.Id, "i", 2.00m, 5.000m, "actor-uid", "actor@example.invalid");
        Assert.True(resEd.Resultado.EsExitoso);
        Assert.Empty((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenPendiente.Id).GetSnapshotAsync()).Documents);

        // 2. Cambio de estado al mismo estado actual
        var resCambio = await compras.CambiarEstadoOrdenConAuditoria(ordenPendiente.Id, "PendienteAprobacion", userId: "actor-uid", userEmail: "actor@example.invalid");
        // Wait, CambiarEstadoOrden only accepts valid target states Enviada, Despachada, Confirmada, Recibida, Cancelada
        // If state is Enviada and change to Enviada:
        await db.Collection("ordenesCompra").Document(ordenPendiente.Id).UpdateAsync("Estado", "Enviada");
        var resCambioEnviada = await compras.CambiarEstadoOrdenConAuditoria(ordenPendiente.Id, "Enviada", userId: "actor-uid", userEmail: "actor@example.invalid");
        Assert.True(resCambioEnviada.Resultado.EsExitoso);
        Assert.Empty((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenPendiente.Id).GetSnapshotAsync()).Documents);

        // 3. Recepción sobre orden ya Recibida
        await db.Collection("ordenesCompra").Document(ordenPendiente.Id).UpdateAsync("Estado", "Recibida");
        var resRecibir = await compras.RecibirConAuditoria(ordenPendiente.Id, userId: "actor-uid", userEmail: "actor@example.invalid");
        Assert.True(resRecibir.Resultado.EsExitoso);
        Assert.Empty((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenPendiente.Id).GetSnapshotAsync()).Documents);
        Assert.Equal(10m, (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>().StockActual);
    }

    [Fact]
    public void InterceptorAuditoriaGlobalNoDebeEstarExpuestoEnComprasService()
    {
        var propiedadEstatica = typeof(ComprasService).GetProperty(
            "InterceptorAuditoriaGlobal",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.Null(propiedadEstatica);
    }

    [LocalFirestoreFact]
    public async Task AdjudicacionDeCotizacionRegistraAuditoriaTransaccionalDeOrdenYSolicitudSinDuplicados()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-adjudicar-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p1").SetAsync(new Proveedor { Id = "p1", RazonSocial = "Proveedor 1", EmailOrdenes = "p1@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon Liquido", Estado = "Activo" });

        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-1",
            GrupoId = "grupo-adj",
            ProveedorId = "p1",
            Estado = "Respondida",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon Liquido", Cantidad = 10m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);

        var respuesta = new CotizacionRespuesta
        {
            Id = "resp-1",
            SolicitudId = solicitud.Id,
            ProveedorId = "p1",
            PlazoEntrega = "5",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon Liquido", Cantidad = 10m, PrecioUnitario = 15m } }
        };
        await db.Collection("cotizacionesRespuestas").Document(respuesta.Id).SetAsync(respuesta);

        var compras = new ComprasService(db);
        var resultado = await compras.Adjudicar(solicitud.Id, respuesta.Id, TransporteAceptado());
        Assert.True(resultado.EsExitoso);

        var ordenes = (await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<OrdenCompra>()).ToList();
        var orden = Assert.Single(ordenes);
        Assert.Equal("Enviada", orden.Estado);
        Assert.Equal("grupo-adj", orden.GrupoCotizacionId);
        Assert.Equal("p1", orden.ProveedorId);

        var auditOrden = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Documents;
        Assert.Equal(3, auditOrden.Count);
        var evOrden = Assert.Single(auditOrden, d => d.GetValue<string>("Action") == "Creacion de orden de compra");
        Assert.Equal("OrdenCompra", evOrden.GetValue<string>("TargetType"));

        var auditSol = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", solicitud.Id).GetSnapshotAsync()).Documents;
        var evSol = Assert.Single(auditSol);
        Assert.Equal("Adjudicación de cotización", evSol.GetValue<string>("Action"));
        Assert.Equal("SolicitudCotizacion", evSol.GetValue<string>("TargetType"));

        // Reintento: no-op/fallo de precondición sin duplicar orden ni eventos de auditoría
        await Assert.ThrowsAsync<InvalidOperationException>(() => compras.Adjudicar(solicitud.Id, respuesta.Id));
        Assert.Single((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
        Assert.Equal(3, (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", orden.Id).GetSnapshotAsync()).Count);
        Assert.Single((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", solicitud.Id).GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task FalloSimuladoDeAuditoriaEnAdjudicacionHaceRollbackTotalDeOrdenYSolicitud()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-adjudicar-rollback-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p1").SetAsync(new Proveedor { Id = "p1", RazonSocial = "Proveedor 1", EmailOrdenes = "p1@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo { Id = "i1", Nombre = "Jabon Liquido", Estado = "Activo" });

        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-fail",
            GrupoId = "grupo-fail",
            ProveedorId = "p1",
            Estado = "Respondida",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon Liquido", Cantidad = 10m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);

        var respuesta = new CotizacionRespuesta
        {
            Id = "resp-fail",
            SolicitudId = solicitud.Id,
            ProveedorId = "p1",
            PlazoEntrega = "5",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", NombreInsumo = "Jabon Liquido", Cantidad = 10m, PrecioUnitario = 15m } }
        };
        await db.Collection("cotizacionesRespuestas").Document(respuesta.Id).SetAsync(respuesta);

        var compras = new ComprasService(db);
        compras.InterceptorAuditoria = log =>
        {
            if (log.TargetType == "OrdenCompra" || log.Action == "Creacion de orden de compra")
                throw new InvalidOperationException("Fallo forzado al auditar orden generada");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => compras.Adjudicar(solicitud.Id, respuesta.Id));

        Assert.Empty((await db.Collection("ordenesCompra").GetSnapshotAsync()).Documents);
        var solDb = (await db.Collection("solicitudesCotizacion").Document(solicitud.Id).GetSnapshotAsync()).ConvertTo<SolicitudCotizacion>();
        Assert.Equal("Respondida", solDb.Estado);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CancelacionAsistidaDeOrdenesConcurrentesRegistraAuditoriaTransaccionalDeRivalSinDuplicados()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-asistida-audit-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Cera", StockActual = 5m, Estado = "Activo" });

        var ordenRec = new OrdenCompra
        {
            Id = "ord-rec-asistida",
            ProveedorId = "p",
            GrupoCotizacionId = "grupo-asistida",
            Estado = "Confirmada",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Cera", Cantidad = 4m, CantidadRecibida = 0m } }
        };
        var ordenRival = new OrdenCompra
        {
            Id = "ord-rival-asistida",
            ProveedorId = "p",
            GrupoCotizacionId = "grupo-asistida",
            Estado = "Enviada",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Cera", Cantidad = 4m, CantidadRecibida = 0m } }
        };
        var ordenYaCancelada = new OrdenCompra
        {
            Id = "ord-ya-cancelada",
            ProveedorId = "p",
            GrupoCotizacionId = "grupo-asistida",
            Estado = "Cancelada",
            MotivoCancelacion = "Cancelada previamente",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Cera", Cantidad = 4m, CantidadRecibida = 0m } }
        };

        await db.Collection("ordenesCompra").Document(ordenRec.Id).SetAsync(ordenRec);
        await db.Collection("ordenesCompra").Document(ordenRival.Id).SetAsync(ordenRival);
        await db.Collection("ordenesCompra").Document(ordenYaCancelada.Id).SetAsync(ordenYaCancelada);

        var compras = new ComprasService(db);
        var res = await compras.RecibirConAuditoria(
            ordenRec.Id,
            new Dictionary<string, decimal> { ["i"] = 4m },
            cancelarConcurrentes: true,
            userId: "actor-uid",
            userEmail: "actor@example.invalid");

        Assert.True(res.Resultado.EsExitoso);

        // La rival fue cancelada con motivo
        var rivalDb = (await db.Collection("ordenesCompra").Document(ordenRival.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Cancelada", rivalDb.Estado);
        Assert.Equal($"Adjudicacion orden {ordenRec.Id}", rivalDb.MotivoCancelacion);

        // La rival cancelada asistidamente debe tener auditoría registrada en la misma transacción
        var rivalAudits = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenRival.Id).GetSnapshotAsync()).Documents;
        var evRival = Assert.Single(rivalAudits);
        Assert.Equal("Cambio de estado de orden a Cancelada", evRival.GetValue<string>("Action"));
        Assert.Equal("OrdenCompra", evRival.GetValue<string>("TargetType"));
        Assert.Equal("actor-uid", evRival.GetValue<string>("UserId"));
        Assert.Equal("actor@example.invalid", evRival.GetValue<string>("UserEmail"));
        Assert.Equal("Enviada", evRival.GetValue<Dictionary<string, string>>("Antes")["Estado"]);
        Assert.Equal("Cancelada", evRival.GetValue<Dictionary<string, string>>("Despues")["Estado"]);
        Assert.Equal($"Adjudicacion orden {ordenRec.Id}", evRival.GetValue<Dictionary<string, string>>("Despues")["MotivoCancelacion"]);

        // La ya cancelada no debe tener eventos nuevos
        Assert.Empty((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenYaCancelada.Id).GetSnapshotAsync()).Documents);

        // Reintento: no-op sin eventos duplicados
        var reintento = await compras.RecibirConAuditoria(
            ordenRec.Id,
            new Dictionary<string, decimal> { ["i"] = 4m },
            cancelarConcurrentes: true,
            userId: "actor-uid",
            userEmail: "actor@example.invalid");
        Assert.True(reintento.Resultado.EsExitoso);
        Assert.Single((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenRival.Id).GetSnapshotAsync()).Documents);
        Assert.Single((await db.Collection("registros_auditoria").WhereEqualTo("TargetId", ordenRec.Id).GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task FalloSimuladoDeAuditoriaEnCancelacionAsistidaHaceRollbackTotal()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-asistida-rollback-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        await db.Collection("proveedores").Document("p").SetAsync(new Proveedor { Id = "p", RazonSocial = "Proveedor", EmailOrdenes = "p@example.invalid", Estado = "Activo" });
        await db.Collection("insumos").Document("i").SetAsync(new Insumo { Id = "i", Nombre = "Cera", StockActual = 5m, Estado = "Activo" });

        var ordenRec = new OrdenCompra
        {
            Id = "ord-rec-fail",
            ProveedorId = "p",
            GrupoCotizacionId = "grupo-fail-asistida",
            Estado = "Confirmada",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Cera", Cantidad = 4m, CantidadRecibida = 0m } }
        };
        var ordenRival = new OrdenCompra
        {
            Id = "ord-rival-fail",
            ProveedorId = "p",
            GrupoCotizacionId = "grupo-fail-asistida",
            Estado = "Enviada",
            Items = new() { new OrdenCompraItem { InsumoId = "i", NombreInsumo = "Cera", Cantidad = 4m, CantidadRecibida = 0m } }
        };

        await db.Collection("ordenesCompra").Document(ordenRec.Id).SetAsync(ordenRec);
        await db.Collection("ordenesCompra").Document(ordenRival.Id).SetAsync(ordenRival);

        var compras = new ComprasService(db);
        compras.InterceptorAuditoria = log =>
        {
            if (log.TargetId == ordenRival.Id)
                throw new InvalidOperationException("Fallo forzado al auditar rival");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            compras.RecibirConAuditoria(
                ordenRec.Id,
                new Dictionary<string, decimal> { ["i"] = 4m },
                cancelarConcurrentes: true,
                userId: "actor-uid",
                userEmail: "actor@example.invalid"));

        // Rollback total: la recibida sigue Confirmada, la rival sigue Enviada, stock sin cambios, Kardex vacío, auditoría vacía
        var recDb = (await db.Collection("ordenesCompra").Document(ordenRec.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Confirmada", recDb.Estado);
        Assert.Equal(0m, recDb.Items[0].CantidadRecibida);

        var rivalDb = (await db.Collection("ordenesCompra").Document(ordenRival.Id).GetSnapshotAsync()).ConvertTo<OrdenCompra>();
        Assert.Equal("Enviada", rivalDb.Estado);

        var insumoDb = (await db.Collection("insumos").Document("i").GetSnapshotAsync()).ConvertTo<Insumo>();
        Assert.Equal(5m, insumoDb.StockActual);

        Assert.Empty((await db.Collection("movimientosStock").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AprobacionDeSolicitudCotizacionRegistraAuditoriaTransaccionalConActorYBeforeAfter()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-aprob-sol-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-aprob-1",
            GrupoId = "grupo-1",
            ProveedorId = "prov-1",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "ins-1", NombreInsumo = "Jabon", Cantidad = 10m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);
        var compras = new ComprasService(db);

        var resultado = await compras.AprobarSolicitud(solicitud.Id, "user-aprobador", "aprobador@example.invalid");

        Assert.True(resultado.EsExitoso);
        var solDb = (await db.Collection("solicitudesCotizacion").Document(solicitud.Id).GetSnapshotAsync()).ConvertTo<SolicitudCotizacion>();
        Assert.Equal("Pendiente", solDb.Estado);

        var auditorias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<AuditLog>()).ToList();
        var log = Assert.Single(auditorias);
        Assert.Equal("user-aprobador", log.UserId);
        Assert.Equal("aprobador@example.invalid", log.UserEmail);
        Assert.Equal("Aprobación de solicitud de cotización", log.Action);
        Assert.Equal(solicitud.Id, log.TargetId);
        Assert.Equal("SolicitudCotizacion", log.TargetType);
        Assert.Equal($"Solicitud {solicitud.Id}", log.NombreHistorico);
        Assert.NotNull(log.Antes);
        Assert.NotNull(log.Despues);
        Assert.Equal("PendienteAprobacion", log.Antes["Estado"]);
        Assert.Equal("Pendiente", log.Despues["Estado"]);
    }

    [LocalFirestoreFact]
    public async Task FalloSimuladoDeAuditoriaEnAprobacionDeSolicitudHaceRollbackTotalYSinAuditoria()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-aprob-sol-fail-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-fail-1",
            GrupoId = "grupo-1",
            ProveedorId = "prov-1",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "ins-1", NombreInsumo = "Jabon", Cantidad = 10m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);
        var compras = new ComprasService(db);
        compras.InterceptorAuditoria = log =>
        {
            if (log.Action == "Aprobación de solicitud de cotización")
                throw new InvalidOperationException("Fallo forzado al auditar aprobacion de solicitud");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            compras.AprobarSolicitud(solicitud.Id, "user-aprobador", "aprobador@example.invalid"));

        var solDb = (await db.Collection("solicitudesCotizacion").Document(solicitud.Id).GetSnapshotAsync()).ConvertTo<SolicitudCotizacion>();
        Assert.Equal("PendienteAprobacion", solDb.Estado);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task RechazoYNoOpEnAprobacionDeSolicitudNoGeneranAuditoriaNiMutanEstado()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-aprob-sol-noop-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-adjudicada",
            GrupoId = "grupo-1",
            ProveedorId = "prov-1",
            Estado = "Adjudicada",
            Items = new() { new OrdenCompraItem { InsumoId = "ins-1", Cantidad = 10m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);
        var compras = new ComprasService(db);

        // Inexistente
        var rInexistente = await compras.AprobarSolicitud("no-existe", "actor", "actor@example.invalid");
        Assert.False(rInexistente.EsExitoso);

        // Estado inválido (Adjudicada)
        var rInvalido = await compras.AprobarSolicitud(solicitud.Id, "actor", "actor@example.invalid");
        Assert.False(rInvalido.EsExitoso);

        // Ya aprobada / en Pendiente (no-op)
        var solPendiente = new SolicitudCotizacion
        {
            Id = "sol-pendiente",
            GrupoId = "grupo-2",
            ProveedorId = "prov-1",
            Estado = "Pendiente",
            Items = new() { new OrdenCompraItem { InsumoId = "ins-1", Cantidad = 5m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solPendiente.Id).SetAsync(solPendiente);

        var rNoOp = await compras.AprobarSolicitud(solPendiente.Id, "actor", "actor@example.invalid");
        // No debe mutar ni registrar auditoría
        Assert.Equal("Pendiente", (await db.Collection("solicitudesCotizacion").Document(solPendiente.Id).GetSnapshotAsync()).ConvertTo<SolicitudCotizacion>().Estado);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AprobacionesConcurrentesDeMismaSolicitudSoloGeneranUnaAuditoriaYNoOpEsIdempotente()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-aprob-sol-conc-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        var solicitud = new SolicitudCotizacion
        {
            Id = "sol-conc-1",
            GrupoId = "grupo-conc",
            ProveedorId = "prov-1",
            Estado = "PendienteAprobacion",
            Items = new() { new OrdenCompraItem { InsumoId = "ins-1", Cantidad = 8m } }
        };
        await db.Collection("solicitudesCotizacion").Document(solicitud.Id).SetAsync(solicitud);
        var compras = new ComprasService(db);

        var tareas = Enumerable.Range(0, 5)
            .Select(i => compras.AprobarSolicitud(solicitud.Id, $"actor-{i}", $"actor{i}@example.invalid"))
            .ToList();
        await Task.WhenAll(tareas);

        var solDb = (await db.Collection("solicitudesCotizacion").Document(solicitud.Id).GetSnapshotAsync()).ConvertTo<SolicitudCotizacion>();
        Assert.Equal("Pendiente", solDb.Estado);

        var auditorias = (await db.Collection("registros_auditoria").WhereEqualTo("TargetId", solicitud.Id).GetSnapshotAsync()).Documents;
        Assert.Single(auditorias);
    }
}
