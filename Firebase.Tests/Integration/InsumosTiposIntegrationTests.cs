using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Xunit;

namespace Firebase.Tests.Integration;

public class InsumosTiposIntegrationTests
{
    private static FirestoreDb CrearDb() => new FirestoreDbBuilder
    {
        ProjectId = "demo-insumo-tipo-" + Guid.NewGuid().ToString("N")[..16],
        EmulatorDetection = EmulatorDetection.EmulatorOnly
    }.Build();

    [LocalFirestoreFact]
    public async Task HerramientaImplicitaSinTipoNoCreaRegistros()
    {
        var db = CrearDb();
        var servicio = new InsumoService(db);
        var resultado = await servicio.GuardarConAuditoria(new Insumo { Nombre = "Llave", Tipo = "Herramienta", UnidadMedida = "unidad" }, true, "admin", "admin@example.com");
        Assert.False(resultado.Resultado.EsExitoso);
        Assert.Empty((await db.Collection("tiposInsumo").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("insumos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("registros_auditoria").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoConfigurableActivoYUnidadCompatible_PersisteTipoIdYTipoYUnidad()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Shampoo Automotriz",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        var insumo = new Insumo
        {
            Nombre = "Shampoo pH Neutro 5L",
            TipoId = resTipo.TipoInsumoId,
            UnidadMedida = "ml",
            StockMinimo = 500m,
            CantidadReabastecimiento = 2000m
        };

        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);
        Assert.False(string.IsNullOrWhiteSpace(resInsumo.InsumoId));

        var doc = await db.Collection("insumos").Document(resInsumo.InsumoId).GetSnapshotAsync();
        Assert.True(doc.Exists);
        var guardado = doc.ConvertTo<Insumo>();
        Assert.Equal(resTipo.TipoInsumoId, guardado.TipoId);
        Assert.Equal("Shampoo Automotriz", guardado.Tipo);
        Assert.Equal("ml", guardado.UnidadMedida);
        Assert.Equal(0m, guardado.StockActual);
        Assert.Equal("Activo", guardado.Estado);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoInactivo_EsRechazado()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Cera Obsolescente",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // Desactivar el tipo
        var resDesact = await tiposService.CambiarEstado(resTipo.TipoInsumoId, "Inactivo", "admin", "admin@example.com");
        Assert.True(resDesact.EsExitoso);

        // Intentar crear insumo con el tipo inactivo
        var insumo = new Insumo
        {
            Nombre = "Cera Líquida",
            TipoId = resTipo.TipoInsumoId,
            UnidadMedida = "l"
        };

        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.False(resInsumo.Resultado.EsExitoso);
        Assert.Contains("inactivo", resInsumo.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Verificar que no se creó el insumo
        var docs = (await db.Collection("insumos").GetSnapshotAsync()).Documents;
        Assert.Empty(docs);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConUnidadIncompatibleConCategoriaDelTipo_EsRechazado()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        // 1. Tipo Volumen con unidad de masa (kg)
        var tipoVolumen = new TipoInsumo
        {
            Nombre = "Desengrasante Líquido",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resVol = await tiposService.GuardarConAuditoria(tipoVolumen, true, "admin", "admin@example.com");
        Assert.True(resVol.Resultado.EsExitoso);

        var insumoVolIncompatible = new Insumo
        {
            Nombre = "Desengrasante A",
            TipoId = resVol.TipoInsumoId,
            UnidadMedida = "kg"
        };
        var resIncomp1 = await insumosService.GuardarConAuditoria(insumoVolIncompatible, true, "admin", "admin@example.com");
        Assert.False(resIncomp1.Resultado.EsExitoso);
        Assert.Contains("no es compatible", resIncomp1.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        // 2. Tipo Masa con unidad de volumen (ml)
        var tipoMasa = new TipoInsumo
        {
            Nombre = "Pasta Pulir",
            Categoria = "Masa",
            UnidadResumen = "kg"
        };
        var resMasa = await tiposService.GuardarConAuditoria(tipoMasa, true, "admin", "admin@example.com");
        Assert.True(resMasa.Resultado.EsExitoso);

        var insumoMasaIncompatible = new Insumo
        {
            Nombre = "Pasta Fina",
            TipoId = resMasa.TipoInsumoId,
            UnidadMedida = "ml"
        };
        var resIncomp2 = await insumosService.GuardarConAuditoria(insumoMasaIncompatible, true, "admin", "admin@example.com");
        Assert.False(resIncomp2.Resultado.EsExitoso);
        Assert.Contains("no es compatible", resIncomp2.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        // 3. Tipo Unidades con unidad de volumen (litro)
        var tipoConteo = new TipoInsumo
        {
            Nombre = "Cepillos",
            Categoria = "Unidades",
            UnidadResumen = "unidad"
        };
        var resConteo = await tiposService.GuardarConAuditoria(tipoConteo, true, "admin", "admin@example.com");
        Assert.True(resConteo.Resultado.EsExitoso);

        var insumoConteoIncompatible = new Insumo
        {
            Nombre = "Cepillo Cerdas Suaves",
            TipoId = resConteo.TipoInsumoId,
            UnidadMedida = "l"
        };
        var resIncomp3 = await insumosService.GuardarConAuditoria(insumoConteoIncompatible, true, "admin", "admin@example.com");
        Assert.False(resIncomp3.Resultado.EsExitoso);
        Assert.Contains("no es compatible", resIncomp3.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        var docs = (await db.Collection("insumos").GetSnapshotAsync()).Documents;
        Assert.Empty(docs);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoPorNombre_ResuelveTipoIdYValidaActivoYUnidad()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Silicona de Tableros",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // Alta pasando Tipo por nombre y TipoId vacío
        var insumo = new Insumo
        {
            Nombre = "Silicona Brillante 500cc",
            Tipo = "Silicona de Tableros",
            TipoId = "",
            UnidadMedida = "cc"
        };

        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);

        var doc = await db.Collection("insumos").Document(resInsumo.InsumoId).GetSnapshotAsync();
        Assert.True(doc.Exists);
        var guardado = doc.ConvertTo<Insumo>();
        Assert.Equal(resTipo.TipoInsumoId, guardado.TipoId);
        Assert.Equal("Silicona de Tableros", guardado.Tipo);
        Assert.Equal("cc", guardado.UnidadMedida);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoInexistente_EsRechazado()
    {
        var db = CrearDb();
        var insumosService = new InsumoService(db);

        // Con TipoId inexistente
        var insumoConIdInexistente = new Insumo
        {
            Nombre = "Insumo Fantasma 1",
            TipoId = "id-que-no-existe-12345",
            UnidadMedida = "l"
        };
        var res1 = await insumosService.GuardarConAuditoria(insumoConIdInexistente, true, "admin", "admin@example.com");
        Assert.False(res1.Resultado.EsExitoso);
        Assert.Contains("no existe", res1.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Con Tipo por nombre desconocido que no coincide con ninguno ni con legacy
        var insumoConNombreInexistente = new Insumo
        {
            Nombre = "Insumo Fantasma 2",
            Tipo = "TipoCompletamenteInexistente",
            UnidadMedida = "l"
        };
        var res2 = await insumosService.GuardarConAuditoria(insumoConNombreInexistente, true, "admin", "admin@example.com");
        Assert.False(res2.Resultado.EsExitoso);
        Assert.Contains("no existe", res2.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        var docs = (await db.Collection("insumos").GetSnapshotAsync()).Documents;
        Assert.Empty(docs);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoHerramienta_AseguraTipoProtegidoYExigeUnidadCompatible()
    {
        var db = CrearDb();
        var insumosService = new InsumoService(db);
        await new TipoInsumoService(db).AsegurarTipoHerramienta("admin", "admin@example.com");

        // Unidad válida (unidad) para Herramienta
        var hidro = new Insumo
        {
            Nombre = "Hidrolavadora Karcher K5",
            Tipo = "Herramienta",
            UnidadMedida = "unidad"
        };
        var resValido = await insumosService.GuardarConAuditoria(hidro, true, "admin", "admin@example.com");
        Assert.True(resValido.Resultado.EsExitoso);

        var docHidro = await db.Collection("insumos").Document(resValido.InsumoId).GetSnapshotAsync();
        Assert.True(docHidro.Exists);
        var guardado = docHidro.ConvertTo<Insumo>();
        Assert.Equal("Herramienta", guardado.Tipo);
        Assert.False(string.IsNullOrWhiteSpace(guardado.TipoId));
        Assert.Equal("unidad", guardado.UnidadMedida);

        // Verificar que el tipo protegido Herramienta existe en la colección tiposInsumo
        var docTipo = await db.Collection("tiposInsumo").Document(guardado.TipoId).GetSnapshotAsync();
        Assert.True(docTipo.Exists);
        Assert.Equal("Herramienta", docTipo.GetValue<string>("Nombre"));
        Assert.Equal("Unidades", docTipo.GetValue<string>("Categoria"));
        Assert.True(docTipo.GetValue<bool>("EsProtegido"));

        // Unidad incompatible (ml) para Herramienta
        var aspiradoraInvalida = new Insumo
        {
            Nombre = "Aspiradora Industrial",
            Tipo = "Herramienta",
            UnidadMedida = "ml"
        };
        var resInvalido = await insumosService.GuardarConAuditoria(aspiradoraInvalida, true, "admin", "admin@example.com");
        Assert.False(resInvalido.Resultado.EsExitoso);
        Assert.Contains("no es compatible", resInvalido.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConcurrenteConDesactivacionDeTipo_GarantizaConsistenciaTransaccionalSinCarrera()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Aromatizante",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // Disparar concurrentemente la desactivación del tipo y el alta de un insumo con ese tipo
        var barrera = new TaskCompletionSource();

        var tareaDesactivar = Task.Run(async () =>
        {
            await barrera.Task;
            return await tiposService.CambiarEstado(resTipo.TipoInsumoId, "Inactivo", "admin", "admin@example.com");
        });

        var tareaAltaInsumo = Task.Run(async () =>
        {
            await barrera.Task;
            var insumo = new Insumo
            {
                Nombre = "Pino Silvestre 1L",
                TipoId = resTipo.TipoInsumoId,
                UnidadMedida = "l"
            };
            return await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        });

        barrera.SetResult();
        await Task.WhenAll(tareaDesactivar, tareaAltaInsumo);

        var resDesact = await tareaDesactivar;
        var resAlta = await tareaAltaInsumo;

        // Invariante transaccional:
        // No pueden haber tenido éxito simultáneo dejando un insumo activo asociado a un tipo inactivo.
        if (resAlta.Resultado.EsExitoso)
        {
            // Si el insumo fue creado con éxito, la desactivación debió ser rechazada por estar en uso,
            // o si ocurrió antes, el tipo debió seguir Activo al momento del alta.
            var docTipo = await db.Collection("tiposInsumo").Document(resTipo.TipoInsumoId).GetSnapshotAsync();
            Assert.True(docTipo.Exists);
            Assert.Equal("Activo", docTipo.GetValue<string>("Estado"));
            Assert.False(resDesact.EsExitoso);
        }
        else
        {
            // Si el alta falló, debió ser porque el tipo ya estaba inactivo
            Assert.True(resDesact.EsExitoso);
            var docTipo = await db.Collection("tiposInsumo").Document(resTipo.TipoInsumoId).GetSnapshotAsync();
            Assert.True(docTipo.Exists);
            Assert.Equal("Inactivo", docTipo.GetValue<string>("Estado"));
        }
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_IntegridadPorTipoId_RenombrarTipoInsumoNoRompeVinculoNiDeteccionEnUso()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Cera Pasta",
            Categoria = "Masa",
            UnidadResumen = "kg"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        var insumo = new Insumo
        {
            Nombre = "Cera Carnauba 500g",
            TipoId = resTipo.TipoInsumoId,
            UnidadMedida = "g"
        };
        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);

        // Renombrar el tipo de insumo
        var tipoModificado = new TipoInsumo
        {
            Id = resTipo.TipoInsumoId,
            Nombre = "Cera Carnauba Premium",
            Categoria = "Masa",
            UnidadResumen = "kg"
        };
        var resRename = await tiposService.GuardarConAuditoria(tipoModificado, false, "admin", "admin@example.com");
        Assert.True(resRename.Resultado.EsExitoso);

        // El tipo de insumo renombrado sigue estando en uso por el insumo mediante TipoId
        var enUso = await tiposService.EstaEnUso(resTipo.TipoInsumoId, "Cera Carnauba Premium");
        Assert.True(enUso);

        // Intento de eliminar o desactivar debe ser bloqueado por estar en uso
        var resElim = await tiposService.Eliminar(resTipo.TipoInsumoId, "admin", "admin@example.com");
        Assert.False(resElim.EsExitoso);
        Assert.Contains("asociados", resElim.MensajeError, StringComparison.OrdinalIgnoreCase);
    }

    [LocalFirestoreFact]
    public async Task Preservar_InsumosHistoricos_CreacionSinTipoConfigurable_SigueFuncionando()
    {
        var db = CrearDb();
        var insumosService = new InsumoService(db);

        // Insumo químico legacy sin crear TipoInsumo previo
        var insumoQuimico = new Insumo
        {
            Nombre = "Shampoo Tradicional",
            Tipo = "Quimico",
            UnidadMedida = "ml"
        };
        var resQuim = await insumosService.GuardarConAuditoria(insumoQuimico, true, "admin", "admin@example.com");
        Assert.True(resQuim.Resultado.EsExitoso);

        var docQuim = await db.Collection("insumos").Document(resQuim.InsumoId).GetSnapshotAsync();
        Assert.True(docQuim.Exists);
        Assert.Equal("Quimico", docQuim.GetValue<string>("Tipo"));

        // Insumo físico legacy sin crear TipoInsumo previo
        var insumoFisico = new Insumo
        {
            Nombre = "Microfibra Amarilla",
            Tipo = "Fisico",
            UnidadMedida = "unidad"
        };
        var resFis = await insumosService.GuardarConAuditoria(insumoFisico, true, "admin", "admin@example.com");
        Assert.True(resFis.Resultado.EsExitoso);

        var docFis = await db.Collection("insumos").Document(resFis.InsumoId).GetSnapshotAsync();
        Assert.True(docFis.Exists);
        Assert.Equal("Fisico", docFis.GetValue<string>("Tipo"));
    }

    [LocalFirestoreFact]
    public async Task Preservar_InsumosHistoricos_EdicionNoAlteraTipoNiUnidad()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Limpia Llantas",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        var insumo = new Insumo
        {
            Nombre = "Llantas Pro 1L",
            TipoId = resTipo.TipoInsumoId,
            UnidadMedida = "l",
            StockMinimo = 5m
        };
        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);

        // Modificación válida de nombre y stock mínimo
        var edicionValida = new Insumo
        {
            Id = resInsumo.InsumoId,
            Nombre = "Llantas Pro Ultra 1L",
            TipoId = resTipo.TipoInsumoId,
            Tipo = "Limpia Llantas",
            UnidadMedida = "l",
            StockMinimo = 10m
        };
        var resEdit = await insumosService.GuardarConAuditoria(edicionValida, false, "admin", "admin@example.com");
        Assert.True(resEdit.Resultado.EsExitoso);

        // Intento de modificar Tipo o UnidadMedida
        var edicionInvalida = new Insumo
        {
            Id = resInsumo.InsumoId,
            Nombre = "Llantas Pro Ultra 1L",
            TipoId = resTipo.TipoInsumoId,
            Tipo = "Limpia Llantas Modificado",
            UnidadMedida = "ml",
            StockMinimo = 10m
        };
        var resEditInvalido = await insumosService.GuardarConAuditoria(edicionInvalida, false, "admin", "admin@example.com");
        Assert.False(resEditInvalido.Resultado.EsExitoso);
        Assert.Contains("no se pueden modificar", resEditInvalido.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);
    }

    [LocalFirestoreFact]
    public async Task EdicionInsumo_ConTipoIdDistinto_EsRechazadaSinMutacionNiAuditoria()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo1 = new TipoInsumo
        {
            Nombre = "Lava Cristales",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo1 = await tiposService.GuardarConAuditoria(tipo1, true, "admin", "admin@example.com");
        Assert.True(resTipo1.Resultado.EsExitoso);

        var tipo2 = new TipoInsumo
        {
            Nombre = "Limpia Motores",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo2 = await tiposService.GuardarConAuditoria(tipo2, true, "admin", "admin@example.com");
        Assert.True(resTipo2.Resultado.EsExitoso);

        var insumo = new Insumo
        {
            Nombre = "Cristal Clear 500ml",
            TipoId = resTipo1.TipoInsumoId,
            UnidadMedida = "ml",
            StockMinimo = 10m
        };
        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);

        // Intento de edición enviando un TipoId distinto
        var edicionConTipoIdDistinto = new Insumo
        {
            Id = resInsumo.InsumoId,
            Nombre = "Cristal Clear Modificado",
            TipoId = resTipo2.TipoInsumoId,
            Tipo = "Lava Cristales",
            UnidadMedida = "ml",
            StockMinimo = 20m
        };
        var resEdit = await insumosService.GuardarConAuditoria(edicionConTipoIdDistinto, false, "admin", "admin@example.com");

        // Verificaciones de rechazo estricto
        Assert.False(resEdit.Resultado.EsExitoso);
        Assert.Contains("no se pueden modificar", resEdit.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        // El insumo en Firestore NO debe haber mutado
        var docInsumo = await db.Collection("insumos").Document(resInsumo.InsumoId).GetSnapshotAsync();
        Assert.True(docInsumo.Exists);
        var persistido = docInsumo.ConvertTo<Insumo>();
        Assert.Equal("Cristal Clear 500ml", persistido.Nombre);
        Assert.Equal(resTipo1.TipoInsumoId, persistido.TipoId);
        Assert.Equal(10m, persistido.StockMinimo);

        // NO debe existir auditoría de actualización para el intento rechazado
        var audits = (await db.Collection("registros_auditoria")
            .WhereEqualTo("TargetId", resInsumo.InsumoId)
            .WhereEqualTo("Action", "Actualizacion de insumo")
            .GetSnapshotAsync()).Documents;
        Assert.Empty(audits);
    }

    [LocalFirestoreFact]
    public async Task EdicionInsumo_HistoricoSinTipoId_ConTipoIdDistinto_EsRechazadaSinMutacionNiAuditoria()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Acondicionador Plasticos",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // Crear insumo histórico sin TipoId
        var insumoHistorico = new Insumo
        {
            Nombre = "Silicona Antigua",
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = 5m
        };
        var resCrear = await insumosService.GuardarConAuditoria(insumoHistorico, true, "admin", "admin@example.com");
        Assert.True(resCrear.Resultado.EsExitoso);

        // Intentar asignar un TipoId en edición
        var intentoMutarTipoId = new Insumo
        {
            Id = resCrear.InsumoId,
            Nombre = "Silicona Antigua Modificada",
            TipoId = resTipo.TipoInsumoId,
            Tipo = "Quimico",
            UnidadMedida = "ml",
            StockMinimo = 10m
        };
        var resEdit = await insumosService.GuardarConAuditoria(intentoMutarTipoId, false, "admin", "admin@example.com");

        Assert.False(resEdit.Resultado.EsExitoso);
        Assert.Contains("no se pueden modificar", resEdit.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        var docInsumo = await db.Collection("insumos").Document(resCrear.InsumoId).GetSnapshotAsync();
        var persistido = docInsumo.ConvertTo<Insumo>();
        Assert.Equal("Silicona Antigua", persistido.Nombre);
        Assert.True(string.IsNullOrEmpty(persistido.TipoId));
        Assert.Equal(5m, persistido.StockMinimo);
    }

    [LocalFirestoreFact]
    public async Task FiltrarInsumos_TrasRenombrarTipoInsumo_ReflejaNuevoNombreYFiltraCorrectamente()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Cera Pasta",
            Categoria = "Masa",
            UnidadResumen = "kg"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        var insumo = new Insumo
        {
            Nombre = "Cera Rápida 200g",
            TipoId = resTipo.TipoInsumoId,
            UnidadMedida = "g",
            StockMinimo = 2m
        };
        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);

        // Renombrar el tipo de insumo
        var tipoModificado = new TipoInsumo
        {
            Id = resTipo.TipoInsumoId,
            Nombre = "Cera Carnauba Premium",
            Categoria = "Masa",
            UnidadResumen = "kg"
        };
        var resRename = await tiposService.GuardarConAuditoria(tipoModificado, false, "admin", "admin@example.com");
        Assert.True(resRename.Resultado.EsExitoso);

        // Obtener todos los insumos y verificar que el insumo refleja el nombre actual del tipo
        var todos = await insumosService.ObtenerTodos();
        var insumoRecuperado = todos.FirstOrDefault(x => x.Id == resInsumo.InsumoId);
        Assert.NotNull(insumoRecuperado);
        Assert.Equal("Cera Carnauba Premium", insumoRecuperado.Tipo);

        // Filtro por el nuevo nombre del tipo debe encontrar el insumo
        var filtradosNuevoNombre = todos.Where(x => string.Equals(x.Tipo, "Cera Carnauba Premium", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(filtradosNuevoNombre);
        Assert.Equal(resInsumo.InsumoId, filtradosNuevoNombre[0].Id);

        // Filtro por el nombre anterior no debe encontrar el insumo renombrado
        var filtradosNombreAnterior = todos.Where(x => string.Equals(x.Tipo, "Cera Pasta", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(filtradosNombreAnterior);
    }

    [LocalFirestoreFact]
    public async Task EdicionInsumo_TrasRenombrarTipoInsumo_PermiteEdicionValidaSinRechazarPorNombreDeTipo()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Desengrasante Motor",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        var insumo = new Insumo
        {
            Nombre = "Desengrasante Fuerte 1L",
            TipoId = resTipo.TipoInsumoId,
            UnidadMedida = "l",
            StockMinimo = 5m
        };
        var resInsumo = await insumosService.GuardarConAuditoria(insumo, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);

        // Renombrar el tipo
        var tipoModificado = new TipoInsumo
        {
            Id = resTipo.TipoInsumoId,
            Nombre = "Desengrasante Biodegradable",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resRename = await tiposService.GuardarConAuditoria(tipoModificado, false, "admin", "admin@example.com");
        Assert.True(resRename.Resultado.EsExitoso);

        // Editar el insumo usando el nuevo nombre del tipo y su TipoId intacto
        var edicionValida = new Insumo
        {
            Id = resInsumo.InsumoId,
            Nombre = "Desengrasante Fuerte Biodegradable 1L",
            TipoId = resTipo.TipoInsumoId,
            Tipo = "Desengrasante Biodegradable",
            UnidadMedida = "l",
            StockMinimo = 8m
        };
        var resEdit = await insumosService.GuardarConAuditoria(edicionValida, false, "admin", "admin@example.com");
        Assert.True(resEdit.Resultado.EsExitoso);

        var docInsumo = await db.Collection("insumos").Document(resInsumo.InsumoId).GetSnapshotAsync();
        var persistido = docInsumo.ConvertTo<Insumo>();
        Assert.Equal("Desengrasante Fuerte Biodegradable 1L", persistido.Nombre);
        Assert.Equal(resTipo.TipoInsumoId, persistido.TipoId);
        Assert.Equal(8m, persistido.StockMinimo);
    }

    [LocalFirestoreFact]
    public async Task RenombrarTipoInsumo_NoReescribeInsumosEnFirestore_PreservaDocumentosHistoricosYResuelveEnLectura()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        // 1. Tipo configurable
        var tipo = new TipoInsumo
        {
            Nombre = "Shampoo Neutro Base",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // 2. Insumo con TipoId
        var insumoConTipoId = new Insumo
        {
            Nombre = "Shampoo Neutro 5L",
            TipoId = resTipo.TipoInsumoId,
            Tipo = "Shampoo Neutro Base",
            UnidadMedida = "l",
            StockMinimo = 4m
        };
        var resInsumo = await insumosService.GuardarConAuditoria(insumoConTipoId, true, "admin", "admin@example.com");
        Assert.True(resInsumo.Resultado.EsExitoso);

        // 3. Insumo histórico sin TipoId pero con mismo nombre de tipo en su campo "Tipo"
        var idHistorico = Guid.NewGuid().ToString("N");
        await db.Collection("insumos").Document(idHistorico).SetAsync(new Dictionary<string, object>
        {
            ["Id"] = idHistorico,
            ["Nombre"] = "Shampoo Granel Antiguo",
            ["Tipo"] = "Shampoo Neutro Base",
            ["UnidadMedida"] = "l",
            ["StockMinimo"] = 1.0,
            ["StockActual"] = 0.0,
            ["CantidadReabastecimiento"] = 0.0,
            ["ReabastecimientoAutomatico"] = false,
            ["ProveedoresIds"] = new List<string>(),
            ["Estado"] = "Activo"
        });

        // 4. Insumo con otro TipoId pero con coincidencia cosmética de nombre (no debe vincularse por nombre)
        var otroTipo = new TipoInsumo
        {
            Nombre = "Otro Tipo",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resOtroTipo = await tiposService.GuardarConAuditoria(otroTipo, true, "admin", "admin@example.com");
        Assert.True(resOtroTipo.Resultado.EsExitoso);

        var insumoOtroTipo = new Insumo
        {
            Nombre = "Insumo Con Otro TipoId",
            TipoId = resOtroTipo.TipoInsumoId,
            Tipo = "Otro Tipo",
            UnidadMedida = "l",
            StockMinimo = 2m
        };
        var resOtro = await insumosService.GuardarConAuditoria(insumoOtroTipo, true, "admin", "admin@example.com");
        Assert.True(resOtro.Resultado.EsExitoso);

        // Estado crudo en Firestore antes del renombrado
        var snapAntesInsumo = await db.Collection("insumos").Document(resInsumo.InsumoId).GetSnapshotAsync();
        var snapAntesHistorico = await db.Collection("insumos").Document(idHistorico).GetSnapshotAsync();
        var snapAntesOtro = await db.Collection("insumos").Document(resOtro.InsumoId).GetSnapshotAsync();

        // 5. Renombrar el primer tipo
        var tipoRenombrado = new TipoInsumo
        {
            Id = resTipo.TipoInsumoId,
            Nombre = "Shampoo Espuma Activa Premium",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resRename = await tiposService.GuardarConAuditoria(tipoRenombrado, false, "admin", "admin@example.com");
        Assert.True(resRename.Resultado.EsExitoso);

        // COMPROBACIÓN CLAVE: Los documentos en Firestore NO fueron reescritos ni mutados
        var snapDespuesInsumo = await db.Collection("insumos").Document(resInsumo.InsumoId).GetSnapshotAsync();
        var snapDespuesHistorico = await db.Collection("insumos").Document(idHistorico).GetSnapshotAsync();
        var snapDespuesOtro = await db.Collection("insumos").Document(resOtro.InsumoId).GetSnapshotAsync();

        Assert.Equal(snapAntesInsumo.UpdateTime, snapDespuesInsumo.UpdateTime);
        Assert.Equal(snapAntesHistorico.UpdateTime, snapDespuesHistorico.UpdateTime);
        Assert.Equal(snapAntesOtro.UpdateTime, snapDespuesOtro.UpdateTime);

        // El insumo histórico sigue sin TipoId en Firestore
        Assert.False(snapDespuesHistorico.ContainsField("TipoId") && !string.IsNullOrWhiteSpace(snapDespuesHistorico.GetValue<string>("TipoId")));
        Assert.Equal("Shampoo Neutro Base", snapDespuesHistorico.GetValue<string>("Tipo"));

        // Sin embargo, en lectura por servicio (Obtener / ObtenerTodos), el insumo vinculado por TipoId
        // resuelve dinámicamente el nombre vigente
        var obtenidoConTipoId = await insumosService.Obtener(resInsumo.InsumoId);
        Assert.NotNull(obtenidoConTipoId);
        Assert.Equal("Shampoo Espuma Activa Premium", obtenidoConTipoId.Tipo);

        // Y el histórico conserva su denominación sin migración artificial
        var obtenidoHistorico = await insumosService.Obtener(idHistorico);
        Assert.NotNull(obtenidoHistorico);
        Assert.Equal("Shampoo Neutro Base", obtenidoHistorico.Tipo);

        // El insumo con otro TipoId sigue asociado a su propio tipo
        var obtenidoOtro = await insumosService.Obtener(resOtro.InsumoId);
        Assert.NotNull(obtenidoOtro);
        Assert.Equal(resOtroTipo.TipoInsumoId, obtenidoOtro.TipoId);
        Assert.Equal("Otro Tipo", obtenidoOtro.Tipo);

        // Detección de en uso en TipoInsumoService:
        // El primer tipo está en uso (por resInsumo vía TipoId y por resHistorico vía fallback legacy)
        Assert.True(await tiposService.EstaEnUso(resTipo.TipoInsumoId, "Shampoo Espuma Activa Premium"));

        // NO se registraron auditorías individuales para los insumos durante el renombre del tipo
        var auditoriasInsumos = (await db.Collection("registros_auditoria")
            .WhereIn("TargetId", new[] { resInsumo.InsumoId, idHistorico, resOtro.InsumoId })
            .WhereEqualTo("Action", "Actualizacion de insumo")
            .GetSnapshotAsync()).Documents;
        Assert.Empty(auditoriasInsumos);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoIdATipoExplicitoIncompatible_EsRechazadoSinMutacionNiAuditoria()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipoA = new TipoInsumo
        {
            Nombre = "Detergente Concentrado",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipoA = await tiposService.GuardarConAuditoria(tipoA, true, "admin", "admin@example.com");
        Assert.True(resTipoA.Resultado.EsExitoso);

        var tipoB = new TipoInsumo
        {
            Nombre = "Cera Líquida Premium",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipoB = await tiposService.GuardarConAuditoria(tipoB, true, "admin", "admin@example.com");
        Assert.True(resTipoB.Resultado.EsExitoso);

        // Alta con TipoId de A pero Tipo explícito de B (identidades incompatibles)
        var insumoIncompatible = new Insumo
        {
            Nombre = "Producto Con Conflicto De Tipo",
            TipoId = resTipoA.TipoInsumoId,
            Tipo = "Cera Líquida Premium",
            UnidadMedida = "l",
            StockMinimo = 5m
        };

        var res = await insumosService.GuardarConAuditoria(insumoIncompatible, true, "admin", "admin@example.com");

        Assert.False(res.Resultado.EsExitoso);
        Assert.Contains("no coincide", res.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        // Sin mutación en Firestore
        var docs = (await db.Collection("insumos")
            .WhereEqualTo("Nombre", "Producto Con Conflicto De Tipo")
            .GetSnapshotAsync()).Documents;
        Assert.Empty(docs);

        // Sin auditoría registrada para el intento rechazado
        var audits = (await db.Collection("registros_auditoria")
            .WhereEqualTo("Action", "Creacion de insumo")
            .WhereEqualTo("NombreHistorico", "Producto Con Conflicto De Tipo")
            .GetSnapshotAsync()).Documents;
        Assert.Empty(audits);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoIdATipoExplicitoHerramientaIncompatible_EsRechazadoSinMutacionNiAuditoria()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipoQuimico = new TipoInsumo
        {
            Nombre = "Shampoo Carroceria",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipoQuimico, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // TipoId es Shampoo (volumen), pero Tipo explícito es "Herramienta"
        var insumoCoercionInvalida = new Insumo
        {
            Nombre = "Shampoo Pretendiendo Ser Herramienta",
            TipoId = resTipo.TipoInsumoId,
            Tipo = TipoInsumo.TipoHerramienta,
            UnidadMedida = "l",
            StockMinimo = 2m
        };

        var res = await insumosService.GuardarConAuditoria(insumoCoercionInvalida, true, "admin", "admin@example.com");

        Assert.False(res.Resultado.EsExitoso);
        Assert.Contains("no coincide", res.Resultado.MensajeError, StringComparison.OrdinalIgnoreCase);

        var docs = (await db.Collection("insumos")
            .WhereEqualTo("Nombre", "Shampoo Pretendiendo Ser Herramienta")
            .GetSnapshotAsync()).Documents;
        Assert.Empty(docs);

        // Sin auditoría registrada para el intento rechazado
        var audits = (await db.Collection("registros_auditoria")
            .WhereEqualTo("Action", "Creacion de insumo")
            .WhereEqualTo("NombreHistorico", "Shampoo Pretendiendo Ser Herramienta")
            .GetSnapshotAsync()).Documents;
        Assert.Empty(audits);
    }

    [LocalFirestoreFact]
    public async Task CrearInsumo_ConTipoIdSinTipoExplicito_ConservaComportamientoYResuelveNombre()
    {
        var db = CrearDb();
        var tiposService = new TipoInsumoService(db);
        var insumosService = new InsumoService(db);

        var tipo = new TipoInsumo
        {
            Nombre = "Abrillantador Neumaticos",
            Categoria = "Volumen",
            UnidadResumen = "l"
        };
        var resTipo = await tiposService.GuardarConAuditoria(tipo, true, "admin", "admin@example.com");
        Assert.True(resTipo.Resultado.EsExitoso);

        // Caller que omite Tipo (es null o vacío) pero provee TipoId válido
        var insumoSinNombreTipo = new Insumo
        {
            Nombre = "Abrillantador Black 500ml",
            TipoId = resTipo.TipoInsumoId,
            Tipo = "",
            UnidadMedida = "ml",
            StockMinimo = 3m
        };

        var res = await insumosService.GuardarConAuditoria(insumoSinNombreTipo, true, "admin", "admin@example.com");

        Assert.True(res.Resultado.EsExitoso);
        Assert.False(string.IsNullOrWhiteSpace(res.InsumoId));

        var doc = await db.Collection("insumos").Document(res.InsumoId).GetSnapshotAsync();
        Assert.True(doc.Exists);
        var persistido = doc.ConvertTo<Insumo>();
        Assert.Equal(resTipo.TipoInsumoId, persistido.TipoId);
        Assert.Equal("Abrillantador Neumaticos", persistido.Tipo);
    }
}
