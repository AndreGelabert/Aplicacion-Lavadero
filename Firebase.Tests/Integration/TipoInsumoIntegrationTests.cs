using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Xunit;

namespace Firebase.Tests.Integration;

public class TipoInsumoIntegrationTests
{
    private static FirestoreDb CrearDb() => new FirestoreDbBuilder
    {
        ProjectId = "demo-tipo-insumo-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    [Fact]
    public void InicializacionHerramientaExigePostAntiforgeryYPermisoCrear()
    {
        var metodo = typeof(Firebase.Controllers.TiposInsumoController).GetMethod("InicializarHerramienta");
        Assert.NotNull(metodo);
        Assert.NotNull(Attribute.GetCustomAttribute(metodo, typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(metodo, typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute)));
        var permiso = Assert.IsType<Firebase.Security.PermisoAttribute>(Attribute.GetCustomAttribute(metodo, typeof(Firebase.Security.PermisoAttribute)));
        Assert.Equal(new[] { "Insumos.Crear" }, permiso.Permisos);
    }

    [Fact]
    public async Task GuardarNiegaPermisoDeOperacionDiferente()
    {
        var controller = new Firebase.Controllers.TiposInsumoController(null!);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim("permiso", "Insumos.Crear") }, "test"));
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.Guardar(new TipoInsumo(), false));
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim("permiso", "Insumos.Modificar") }, "test"));
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.Guardar(new TipoInsumo(), true));
    }

    [LocalFirestoreFact]
    public async Task InicializacionExplicitaAuditaActorYEsIdempotente()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);
        var controller = new Firebase.Controllers.TiposInsumoController(service);
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "admin-id"),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "admin@example.com"),
            new System.Security.Claims.Claim("permiso", "Insumos.Crear") }, "test"));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = context };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(context, new Moq.Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>().Object);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(await controller.InicializarHerramienta());
        Assert.Equal("El tipo Herramienta está disponible.", controller.TempData["Success"]);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(await controller.InicializarHerramienta());
        var tipos = await service.ObtenerTodos();
        var tipo = Assert.Single(tipos);
        Assert.Equal("Herramienta", tipo.Nombre);
        Assert.Equal("Unidades", tipo.Categoria);
        Assert.Equal("unidad", tipo.UnidadResumen);
        Assert.True(tipo.EsProtegido);
        Assert.Equal("Activo", tipo.Estado);
        var log = Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents).ConvertTo<AuditLog>();
        Assert.Equal("admin-id", log.UserId);
        Assert.Equal("admin@example.com", log.UserEmail);
        Assert.Equal(tipo.Id, log.TargetId);
        Assert.Equal("Creacion de tipo de insumo protegido", log.Action);
    }

    [LocalFirestoreFact]
    public async Task LecturaNoCreaNiReparaHerramienta()
    {
        var db = CrearDb();
        var servicio = new TipoInsumoService(db);
        Assert.Empty(await servicio.ObtenerTodos());
        Assert.Empty((await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        var doc = db.Collection("tiposInsumo").Document("historico");
        await doc.SetAsync(new TipoInsumo { Id = "historico", Nombre = "Herramienta", Categoria = "Masa", UnidadResumen = "g", Estado = "Inactivo" });
        var antes = (await doc.GetSnapshotAsync()).ToDictionary();
        await servicio.ObtenerTodos();
        await servicio.AsegurarTipoHerramienta("admin", "admin@example.com");
        Assert.Equal(antes, (await doc.GetSnapshotAsync()).ToDictionary());
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CrearTipoInsumo_ConDatosValidos_PersisteDocumentoYAuditoriaAtomics()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Detergente Industrial",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };

        var resultado = await service.GuardarConAuditoria(tipo, true, "user-1", "user1@example.com");

        Assert.True(resultado.Resultado.EsExitoso);
        Assert.False(string.IsNullOrWhiteSpace(resultado.TipoInsumoId));

        // Verificar persistencia del documento
        var doc = await db.Collection("tiposInsumo").Document(resultado.TipoInsumoId).GetSnapshotAsync();
        Assert.True(doc.Exists);
        var guardado = doc.ConvertTo<TipoInsumo>();
        Assert.Equal("Detergente Industrial", guardado.Nombre);
        Assert.Equal("Volumen", guardado.Categoria);
        Assert.Equal("l", guardado.UnidadResumen);
        Assert.Equal("Activo", guardado.Estado);
        Assert.False(guardado.EsProtegido);

        // Verificar auditoría atómica
        var logs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<AuditLog>()).ToList();
        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal("Creacion de tipo de insumo", log.Action);
        Assert.Equal("TipoInsumo", log.TargetType);
        Assert.Equal(resultado.TipoInsumoId, log.TargetId);
        Assert.Equal("Detergente Industrial", log.NombreHistorico);
        Assert.Equal("user-1", log.UserId);
        Assert.Equal("user1@example.com", log.UserEmail);
    }

    [LocalFirestoreFact]
    public async Task CrearTipoInsumo_NombreDuplicado_EsRechazadoYNoPersiste()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);

        var tipo1 = new TipoInsumo
        {
            Nombre = "Shampoo",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var res1 = await service.GuardarConAuditoria(tipo1, true, "u1", "u1@example.com");
        Assert.True(res1.Resultado.EsExitoso);

        // Intento con mismo nombre pero distinta mayúscula y espacios
        var tipo2 = new TipoInsumo
        {
            Nombre = "  shampoo  ",
            Categoria = "Volumen",
            UnidadResumen = "ml"
        };
        var res2 = await service.GuardarConAuditoria(tipo2, true, "u2", "u2@example.com");
        Assert.False(res2.Resultado.EsExitoso);
        Assert.Contains("Ya existe un tipo de insumo con ese nombre", res2.Resultado.MensajeError);

        var docs = (await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents;
        Assert.Single(docs);
    }

    [LocalFirestoreFact]
    public async Task CrearTipoInsumo_CategoriaOUnidadInvalida_EsRechazado()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);

        // Categoría inexistente
        var invalidaCat = new TipoInsumo
        {
            Nombre = "Tipo Invalido",
            Categoria = "Distancia",
            UnidadResumen = "m"
        };
        var resCat = await service.Guardar(invalidaCat, true, "u1", "u1@example.com");
        Assert.False(resCat.EsExitoso);
        Assert.Contains("categoría de magnitud no es válida", resCat.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Unidad incompatible con la categoría (Volumen con kg)
        var incompatible = new TipoInsumo
        {
            Nombre = "Incompatible",
            Categoria = "Volumen",
            UnidadResumen = "kg"
        };
        var resInc = await service.Guardar(incompatible, true, "u1", "u1@example.com");
        Assert.False(resInc.EsExitoso);
        Assert.Contains("no es válida para la categoría", resInc.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Nombre vacío
        var vacio = new TipoInsumo
        {
            Nombre = "   ",
            Categoria = "Masa",
            UnidadResumen = "kg"
        };
        var resVacio = await service.Guardar(vacio, true, "u1", "u1@example.com");
        Assert.False(resVacio.EsExitoso);
        Assert.Contains("nombre del tipo de insumo es obligatorio", resVacio.MensajeError, StringComparison.OrdinalIgnoreCase);

        Assert.Empty((await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task AsegurarTipoHerramienta_InicializaTipoProtegidoEIdempotente()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);

        var herramienta = await service.AsegurarTipoHerramienta("admin", "admin@example.com");
        Assert.NotNull(herramienta);
        Assert.Equal("Herramienta", herramienta.Nombre);
        Assert.Equal("Unidades", herramienta.Categoria);
        Assert.Equal("unidad", herramienta.UnidadResumen);
        Assert.True(herramienta.EsProtegido);
        Assert.Equal("Activo", herramienta.Estado);

        // Segunda llamada no duplica
        var segunda = await service.AsegurarTipoHerramienta("admin", "admin@example.com");
        Assert.Equal(herramienta.Id, segunda.Id);

        var docs = (await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents;
        Assert.Single(docs);
    }

    [LocalFirestoreFact]
    public async Task HerramientaProtegido_NoPuedeSerEliminadoNiDesactivado()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);
        var herramienta = await service.AsegurarTipoHerramienta("admin", "admin@example.com");

        // Intentar eliminar
        var resElim = await service.Eliminar(herramienta.Id, "admin", "admin@example.com");
        Assert.False(resElim.EsExitoso);
        Assert.Contains("protegido", resElim.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Intentar desactivar
        var resDesact = await service.CambiarEstado(herramienta.Id, "Inactivo", "admin", "admin@example.com");
        Assert.False(resDesact.EsExitoso);
        Assert.Contains("protegido", resDesact.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Sigue activo y existiendo
        var doc = await db.Collection("tiposInsumo").Document(herramienta.Id).GetSnapshotAsync();
        Assert.True(doc.Exists);
        Assert.Equal("Activo", doc.GetValue<string>("Estado"));
    }

    [LocalFirestoreFact]
    public async Task HerramientaProtegido_NoPuedeModificarCategoriaNiUnidadResumenNiNombre()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);
        var herramienta = await service.AsegurarTipoHerramienta("admin", "admin@example.com");

        // Intentar cambiar categoría a Volumen (con unidad válida de volumen)
        var modCat = new TipoInsumo
        {
            Id = herramienta.Id,
            Nombre = "Herramienta",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resCat = await service.Guardar(modCat, false, "admin", "admin@example.com");
        Assert.False(resCat.EsExitoso);
        Assert.Contains("categoría fija del tipo protegido", resCat.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Intentar cambiar unidad fija a otra unidad válida de conteo ('u')
        var modUnidad = new TipoInsumo
        {
            Id = herramienta.Id,
            Nombre = "Herramienta",
            Categoria = "Unidades",
            UnidadResumen = "u"
        };
        var resUnidad = await service.Guardar(modUnidad, false, "admin", "admin@example.com");
        Assert.False(resUnidad.EsExitoso);
        Assert.Contains("unidad protegida del tipo protegido", resUnidad.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Intentar cambiar nombre
        var modNombre = new TipoInsumo
        {
            Id = herramienta.Id,
            Nombre = "Herramienta Renombrada",
            Categoria = "Unidades",
            UnidadResumen = "unidad"
        };
        var resNombre = await service.Guardar(modNombre, false, "admin", "admin@example.com");
        Assert.False(resNombre.EsExitoso);
        Assert.Contains("nombre del tipo protegido", resNombre.MensajeError, StringComparison.OrdinalIgnoreCase);
    }

    [LocalFirestoreFact]
    public async Task TipoEnUsoPorInsumo_NoPuedeSerEliminadoNiDesactivado()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Desengrasante",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await service.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // Crear insumo que use este tipo
        var insumo = new Insumo
        {
            Id = "ins-1",
            Nombre = "Desengrasante Motor",
            Tipo = "Desengrasante",
            UnidadMedida = "l",
            StockActual = 5m,
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        // Intento de eliminar tipo en uso -> rechazado
        var resElim = await service.Eliminar(resTipo.TipoInsumoId, "admin", "admin@example.com");
        Assert.False(resElim.EsExitoso);
        Assert.Contains("asociados", resElim.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Intento de desactivar tipo en uso -> rechazado
        var resDesact = await service.CambiarEstado(resTipo.TipoInsumoId, "Inactivo", "admin", "admin@example.com");
        Assert.False(resDesact.EsExitoso);
        Assert.Contains("asociados", resDesact.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Sigue existiendo y activo
        var doc = await db.Collection("tiposInsumo").Document(resTipo.TipoInsumoId).GetSnapshotAsync();
        Assert.True(doc.Exists);
        Assert.Equal("Activo", doc.GetValue<string>("Estado"));
    }

    [LocalFirestoreFact]
    public async Task TipoEnUsoPorInsumo_NoPuedeModificarCategoriaNiUnidadResumen()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Líquido Refrigerante",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await service.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // Asociar insumo
        var insumo = new Insumo
        {
            Id = "ins-ref-1",
            Nombre = "Refrigerante 50%",
            Tipo = "Líquido Refrigerante",
            UnidadMedida = "l",
            StockActual = 10m,
            Estado = "Activo"
        };
        await db.Collection("insumos").Document(insumo.Id).SetAsync(insumo);

        // Intento de cambiar Categoria de Volumen a Masa -> rechazado
        var modCat = new TipoInsumo
        {
            Id = resTipo.TipoInsumoId,
            Nombre = "Líquido Refrigerante",
            Categoria = "Masa",
            UnidadResumen = "kg"
        };
        var resCat = await service.Guardar(modCat, false, "admin", "admin@example.com");
        Assert.False(resCat.EsExitoso);
        Assert.Contains("asociados", resCat.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Intento de cambiar UnidadResumen de l a ml -> rechazado por romper coherencia de productos
        var modUni = new TipoInsumo
        {
            Id = resTipo.TipoInsumoId,
            Nombre = "Líquido Refrigerante",
            Categoria = "Volumen",
            UnidadResumen = "ml"
        };
        var resUni = await service.Guardar(modUni, false, "admin", "admin@example.com");
        Assert.False(resUni.EsExitoso);
        Assert.Contains("asociados", resUni.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Edición sin alterar categoría ni unidad resumen -> permitida
        var modOk = new TipoInsumo
        {
            Id = resTipo.TipoInsumoId,
            Nombre = "Líquido Refrigerante Premium",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resOk = await service.Guardar(modOk, false, "admin", "admin@example.com");
        Assert.True(resOk.EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task TipoSinUso_PuedeModificarseDesactivarseYEliminarseConAuditoria()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Cera Rápida",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var res = await service.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(res.Resultado.EsExitoso);
        var id = res.TipoInsumoId;

        // Modificar
        var mod = new TipoInsumo
        {
            Id = id,
            Nombre = "Cera Sintética",
            Categoria = "Volumen",
            UnidadResumen = "ml"
        };
        var resMod = await service.GuardarConAuditoria(mod, false, "admin", "admin@example.com");
        Assert.True(resMod.Resultado.EsExitoso);

        // Desactivar
        var resDesact = await service.CambiarEstadoConAuditoria(id, "Inactivo", "admin", "admin@example.com");
        Assert.True(resDesact.Resultado.EsExitoso);
        var docInactivo = await db.Collection("tiposInsumo").Document(id).GetSnapshotAsync();
        Assert.Equal("Inactivo", docInactivo.GetValue<string>("Estado"));

        // Reactivar
        var resReact = await service.CambiarEstadoConAuditoria(id, "Activo", "admin", "admin@example.com");
        Assert.True(resReact.Resultado.EsExitoso);
        var docActivo = await db.Collection("tiposInsumo").Document(id).GetSnapshotAsync();
        Assert.Equal("Activo", docActivo.GetValue<string>("Estado"));

        // Eliminar
        var resElim = await service.EliminarConAuditoria(id, "admin", "admin@example.com");
        Assert.True(resElim.Resultado.EsExitoso);
        var docElim = await db.Collection("tiposInsumo").Document(id).GetSnapshotAsync();
        Assert.False(docElim.Exists);

        // Verificar que se hayan generado los eventos de auditoría correspondientes
        var logs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents
            .Select(d => d.ConvertTo<AuditLog>()).ToList();
        Assert.Contains(logs, l => l.Action == "Creacion de tipo de insumo" && l.TargetId == id);
        Assert.Contains(logs, l => l.Action == "Actualizacion de tipo de insumo" && l.TargetId == id);
        Assert.Contains(logs, l => l.Action == "Desactivacion de tipo de insumo" && l.TargetId == id);
        Assert.Contains(logs, l => l.Action == "Reactivacion de tipo de insumo" && l.TargetId == id);
        Assert.Contains(logs, l => l.Action == "Eliminacion de tipo de insumo" && l.TargetId == id);
    }

    private sealed class AuditWriterQueFalla : IAuditWriter
    {
        public int Llamadas { get; private set; }
        public void EnlistarEnTransaccion(Transaction transaction, AuditLog log)
        {
            Llamadas++;
            throw new InvalidOperationException("Fallo del audit writer");
        }
        private Task Fallar() { Llamadas++; throw new InvalidOperationException("Fallo del audit writer"); }
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType) => Fallar();
        public Task LogEvent(string userId, string userEmail, string action, string targetId, string targetType,
            Dictionary<string, string> antes, Dictionary<string, string> despues, string nombreHistorico = null!) => Fallar();
        public Task LogSessionEvent(string userId, string userEmail, string action, string targetId, string targetType) => Fallar();
    }

    [LocalFirestoreFact]
    public async Task CrearConAuditWriterInyectadoQueFalla_NoPersisteTipoNiAuditoria()
    {
        var db = CrearDb();
        var writer = new AuditWriterQueFalla();
        var service = new TipoInsumoService(db, auditWriter: writer);
        var tipo = new TipoInsumo { Nombre = "Cera", Categoria = "Volumen", UnidadResumen = "l" };
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GuardarConAuditoria(tipo, true, "admin", "admin@example.com"));
            Assert.Equal("Fallo del audit writer", error.Message);
            Assert.Equal(1, writer.Llamadas);
        }
        finally
        {
            Assert.Empty(await service.ObtenerTodos());
            Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task ActualizarConAuditWriterInyectadoQueFalla_NoModificaTipoNiAuditoria()
    {
        var db = CrearDb();
        var original = new TipoInsumo { Nombre = "Cera", Categoria = "Volumen", UnidadResumen = "l" };
        var seed = new TipoInsumoService(db);
        var creado = await seed.GuardarConAuditoria(original, true, "admin", "admin@example.com");
        Assert.True(creado.Resultado.EsExitoso);
        var writer = new AuditWriterQueFalla();
        var service = new TipoInsumoService(db, auditWriter: writer);
        var cambio = new TipoInsumo { Id = creado.TipoInsumoId, Nombre = "Cera nueva", Categoria = "Volumen", UnidadResumen = "l" };
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GuardarConAuditoria(cambio, false, "admin", "admin@example.com"));
            Assert.Equal("Fallo del audit writer", error.Message);
            Assert.Equal(1, writer.Llamadas);
        }
        finally
        {
            Assert.Equal("Cera", (await service.ObtenerPorId(creado.TipoInsumoId))!.Nombre);
            var log = Assert.Single((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents).ConvertTo<AuditLog>();
            Assert.Equal("Creacion de tipo de insumo", log.Action);
        }
    }

    [LocalFirestoreFact]
    public async Task AuditServiceInyectado_CreaYActualizaConUnEventoPorOperacionYActor()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db, auditWriter: new AuditService(db));
        var creado = await service.GuardarConAuditoria(new TipoInsumo { Nombre = "Cera", Categoria = "Volumen", UnidadResumen = "l" }, true, "actor", "actor@example.com");
        Assert.True(creado.Resultado.EsExitoso);
        var actualizado = await service.GuardarConAuditoria(new TipoInsumo { Id = creado.TipoInsumoId, Nombre = "Cera nueva", Categoria = "Volumen", UnidadResumen = "l" }, false, "actor", "actor@example.com");
        Assert.True(actualizado.Resultado.EsExitoso);
        Assert.Equal("Cera nueva", (await service.ObtenerPorId(creado.TipoInsumoId))!.Nombre);
        var logs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents.Select(d => d.ConvertTo<AuditLog>()).ToList();
        Assert.Equal(2, logs.Count);
        foreach (var action in new[] { "Creacion de tipo de insumo", "Actualizacion de tipo de insumo" })
        {
            var log = Assert.Single(logs, l => l.Action == action);
            Assert.Equal("actor", log.UserId);
            Assert.Equal("actor@example.com", log.UserEmail);
            Assert.Equal(creado.TipoInsumoId, log.TargetId);
            Assert.Equal("TipoInsumo", log.TargetType);
        }
    }

    [LocalFirestoreFact]
    public async Task TipoInsumo_FalloEnAuditoria_HaceRollbackTotal()
    {
        var db = CrearDb();
        var service = new TipoInsumoService(db);
        service.InterceptorAuditoria = log =>
        {
            if (log.Action == "Creacion de tipo de insumo")
                throw new InvalidOperationException("Fallo forzado en auditoría de tipo de insumo");
        };

        var tipo = new TipoInsumo
        {
            Nombre = "Microfibra",
            Categoria = "Unidades",
            UnidadResumen = "unidad"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GuardarConAuditoria(tipo, true, "admin", "admin@example.com"));

        Assert.Empty((await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CrearTipoInsumo_ConcurrentesConMismoNombre_GarantizaUnicidadYRechazaDuplicado()
    {
        var db = CrearDb();
        var service1 = new TipoInsumoService(db);
        var service2 = new TipoInsumoService(db);

        // Barrera determinista para forzar la carrera:
        // Ambos leen antes de que cualquiera haga commit
        var t1InRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var t2InRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        service1.InterceptorAuditoria = _ =>
        {
            t1InRead.TrySetResult(true);
            t2InRead.Task.Wait(TimeSpan.FromSeconds(5));
        };

        service2.InterceptorAuditoria = _ =>
        {
            t2InRead.TrySetResult(true);
            t1InRead.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var tipo1 = new TipoInsumo
        {
            Nombre = "Desengrasante Motor",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var tipo2 = new TipoInsumo
        {
            Nombre = "  desengrasante motor  ",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };

        var task1 = Task.Run(() => service1.GuardarConAuditoria(tipo1, true, "u1", "u1@example.com"));
        var task2 = Task.Run(() => service2.GuardarConAuditoria(tipo2, true, "u2", "u2@example.com"));

        var resultados = await Task.WhenAll(task1, task2);
        var res1 = resultados[0];
        var res2 = resultados[1];

        // Exactamente una debe ser exitosa y la otra debe ser rechazada por duplicado
        var exitos = resultados.Count(r => r.Resultado.EsExitoso);
        var fallos = resultados.Count(r => !r.Resultado.EsExitoso);

        Assert.Equal(1, exitos);
        Assert.Equal(1, fallos);

        var fallo = resultados.First(r => !r.Resultado.EsExitoso);
        Assert.Contains("Ya existe un tipo de insumo con ese nombre", fallo.Resultado.MensajeError);

        // En la base de datos debe existir estrictamente un único documento
        var docs = (await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents;
        Assert.Single(docs);

        // Auditoría debe contener estrictamente una creación de TipoInsumo
        var auditDocs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Single(auditDocs);
    }

    [LocalFirestoreFact]
    public async Task AsegurarTipoHerramienta_Concurrente_NoGeneraDocumentosDuplicados()
    {
        var db = CrearDb();
        var service1 = new TipoInsumoService(db);
        var service2 = new TipoInsumoService(db);

        var t1InRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var t2InRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        service1.InterceptorAuditoria = _ =>
        {
            t1InRead.TrySetResult(true);
            t2InRead.Task.Wait(TimeSpan.FromSeconds(5));
        };

        service2.InterceptorAuditoria = _ =>
        {
            t2InRead.TrySetResult(true);
            t1InRead.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var task1 = Task.Run(() => service1.AsegurarTipoHerramienta("u1", "u1@example.com"));
        var task2 = Task.Run(() => service2.AsegurarTipoHerramienta("u2", "u2@example.com"));

        var resultados = await Task.WhenAll(task1, task2);
        var h1 = resultados[0];
        var h2 = resultados[1];

        Assert.NotNull(h1);
        Assert.NotNull(h2);
        Assert.Equal(h1.Id, h2.Id);

        // En la base de datos debe existir estrictamente un único documento para Herramienta
        var docs = (await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents;
        Assert.Single(docs);
        Assert.Equal("Herramienta", docs[0].GetValue<string>("Nombre"));

        // Auditoría: solo un evento de creación inicial
        var auditDocs = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Single(auditDocs);
    }

    [LocalFirestoreFact]
    public async Task CrearTipoInsumo_ConcurrenteConColeccionNoVacia_GarantizaUnicidadYRechazaDuplicado()
    {
        var db = CrearDb();
        var service1 = new TipoInsumoService(db);
        var service2 = new TipoInsumoService(db);

        // Pre-cargar tipos existentes
        for (int i = 1; i <= 3; i++)
        {
            var pre = new TipoInsumo
            {
                Nombre = $"Tipo Existente {i}",
                Categoria = "Unidades",
                UnidadResumen = "unidad"
            };
            await service1.GuardarConAuditoria(pre, true, "seed", "seed@example.com");
        }

        var intentosT1 = 0;
        var intentosT2 = 0;

        var t1InCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var t2InCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        service1.InterceptorAuditoria = _ =>
        {
            Interlocked.Increment(ref intentosT1);
            t1InCallback.TrySetResult(true);
            t2InCallback.Task.Wait(TimeSpan.FromSeconds(5));
        };

        service2.InterceptorAuditoria = _ =>
        {
            Interlocked.Increment(ref intentosT2);
            t2InCallback.TrySetResult(true);
            t1InCallback.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var tipo1 = new TipoInsumo
        {
            Nombre = "Químico Nuevo Concurrente",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var tipo2 = new TipoInsumo
        {
            Nombre = "Químico Nuevo Concurrente",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };

        var task1 = Task.Run(() => service1.GuardarConAuditoria(tipo1, true, "u1", "u1@example.com"));
        var task2 = Task.Run(() => service2.GuardarConAuditoria(tipo2, true, "u2", "u2@example.com"));

        var resultados = await Task.WhenAll(task1, task2);

        var totalIntentos = intentosT1 + intentosT2;
        var docs = (await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents;
        var exitos = resultados.Count(r => r.Resultado.EsExitoso);
        var fallos = resultados.Count(r => !r.Resultado.EsExitoso);

        // Preexistentes (3) + 1 nuevo = 4 total
        Assert.Equal(4, docs.Count);
        Assert.Equal(1, exitos);
        Assert.Equal(1, fallos);
        // El interceptor se invoca solo al registrar auditoría: un reintento que
        // detecta el duplicado retorna antes y no incrementa este contador.
        Assert.True(intentosT1 >= 1 && intentosT2 >= 1, $"Ambas transacciones deben alcanzar la barrera (totalIntentos={totalIntentos})");
        Assert.Contains("Ya existe un tipo de insumo con ese nombre", resultados.Single(r => !r.Resultado.EsExitoso).Resultado.MensajeError);
        var auditorias = (await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents;
        Assert.Equal(4, auditorias.Count);
    }
}
