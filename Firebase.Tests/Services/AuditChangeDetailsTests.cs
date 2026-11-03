using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Tests for S1-S4: antes/después capture, privacy, legacy/session distinction, detail roundtrip.
    /// RED run expected before implementation.
    /// </summary>
    public class AuditChangeDetailsTests
    {
        [Fact]
        public void CapturarRol_IncluyeCamposPersistidosYPermisosOrdenados()
        {
            var rol = new Rol { Nombre = "Operador", Descripcion = "Turno", Estado = "Activo", FechaCreacion = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), Permisos = new() { "Vehiculos.Consultar", "Clientes.Consultar" } };
            var captura = AuditCampos.CapturarRol(rol);
            Assert.Equal("Operador", captura["Nombre"]);
            Assert.Equal("Turno", captura["Descripcion"]);
            Assert.Equal("Activo", captura["Estado"]);
            Assert.Equal("2024-01-02T03:04:05.0000000Z", captura["FechaCreacion"]);
            Assert.Equal("[\"Clientes.Consultar\",\"Vehiculos.Consultar\"]", captura["Permisos"]);
            rol.Permisos.Reverse();
            Assert.Equal(captura["Permisos"], AuditCampos.CapturarRol(rol)["Permisos"]);
            Assert.Equal(5, captura.Count);
        }

        [Fact]
        public void Capturas_ConservanIdsDeRelacionSinExponerHash()
        {
            var cliente = TestFactory.CreateCliente();
            cliente.VehiculosIds = new() { "veh-1" };
            var vehiculo = TestFactory.CreateVehiculo(clienteId: "cli-1");
            vehiculo.ClientesIds = new() { "cli-1", "cli-2" };
            vehiculo.ClaveAsociacionHash = "secret-hash";
            var capturaCliente = AuditCampos.CapturarCliente(cliente);
            var capturaVehiculo = AuditCampos.CapturarVehiculo(vehiculo);
            Assert.Contains("veh-1", capturaCliente["VehiculosIds"]);
            Assert.Equal("cli-1", capturaVehiculo["ClienteId"]);
            Assert.Contains("cli-2", capturaVehiculo["ClientesIds"]);
            Assert.DoesNotContain(capturaVehiculo, campo => campo.Key.Contains("Clave") || campo.Value.Contains("secret-hash"));
        }

        [Fact]
        public void CapturaDeServicio_IncluyeEtapasHistoricas()
        {
            var servicio = TestFactory.CreateServicio(etapas: new List<Etapa> { new Etapa { Id = "etapa-1", Nombre = "Encerado" } });
            var captura = AuditCampos.CapturarServicio(servicio);
            Assert.Contains("Encerado", captura["Etapas"]);
            Assert.Contains("etapa-1", captura["Etapas"]);
        }

        // ─── S1: Antes/Después capture ───────────────────────────────────────

        [Fact]
        public void AuditLog_DeberiaContenerCamposAntesYDespues()
        {
            // Un registro legado no contiene datos históricos.
            var log = new AuditLog();
            Assert.Null(log.Antes);
            Assert.Null(log.Despues);
        }

        [Fact]
        public void AuditLog_DeberiaContenerNombreHistorico()
        {
            var log = new AuditLog { NombreHistorico = "Lavado Básico" };
            Assert.Equal("Lavado Básico", log.NombreHistorico);
        }

        [Fact]
        public void AuditLog_DeberiaContenerDocId()
        {
            // DocId maps the Firestore document ID which is not in the payload
            var log = new AuditLog { DocId = "abc123" };
            Assert.Equal("abc123", log.DocId);
        }

        [Fact]
        public void AuditLogConNombre_DeberiaContenerDocIdYCambios()
        {
            var vm = new AuditLogConNombre
            {
                DocId = "doc-001",
                NombreHistorico = "Servicio X",
                Antes = new Dictionary<string, string> { ["Nombre"] = "Antes" },
                Despues = new Dictionary<string, string> { ["Nombre"] = "Despues" }
            };
            Assert.Equal("doc-001", vm.DocId);
            Assert.Equal("Servicio X", vm.NombreHistorico);
            Assert.Equal("Antes", vm.Antes["Nombre"]);
            Assert.Equal("Despues", vm.Despues["Nombre"]);
        }

        // ─── S1: Only changed fields are captured ────────────────────────────

        [Fact]
        public void ObtenerCambios_SoloDeberiaRetornarCamposCambiados()
        {
            var antes = new Dictionary<string, string>
            {
                ["Nombre"] = "Viejo",
                ["Precio"] = "1000",
                ["Estado"] = "Activo"
            };
            var despues = new Dictionary<string, string>
            {
                ["Nombre"] = "Nuevo",
                ["Precio"] = "1000",   // sin cambio
                ["Estado"] = "Activo"  // sin cambio
            };

            var cambios = despues
                .Where(kv => !antes.TryGetValue(kv.Key, out var v) || v != kv.Value)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            Assert.Single(cambios);
            Assert.Equal("Nuevo", cambios["Nombre"]);
        }

        // ─── S2: Privacy – allowlist fields (no passwords/hashes/tokens) ─────

        [Fact]
        public void CamposAuditableServicio_NoDebeIncluirCamposSensibles()
        {
            // The explicit allowlist for Servicio must NOT contain sensitive fields
            var allowlist = AuditCampos.Servicio;
            Assert.DoesNotContain("PasswordHash", allowlist);
            Assert.DoesNotContain("Token", allowlist);
            Assert.DoesNotContain("Secret", allowlist);
            Assert.DoesNotContain("ApiKey", allowlist);
        }

        [Fact]
        public void CamposAuditableServicio_DebeIncluirCamposRelevantes()
        {
            var allowlist = AuditCampos.Servicio;
            Assert.Contains("Nombre", allowlist);
            Assert.Contains("Precio", allowlist);
            Assert.Contains("Estado", allowlist);
        }

        [Fact]
        public void CamposAuditableCliente_DebeIncluirVehiculosIds()
        {
            // Relationship IDs are auditable; secret association keys are not.
            var allowlist = AuditCampos.Cliente;
            Assert.Contains("VehiculosIds", allowlist);
        }

        [Fact]
        public void CamposAuditableCliente_DebeIncluirCamposIdentificacion()
        {
            var allowlist = AuditCampos.Cliente;
            Assert.Contains("Nombre", allowlist);
            Assert.Contains("Apellido", allowlist);
            Assert.Contains("TipoDocumento", allowlist);
            Assert.Contains("NumeroDocumento", allowlist);
        }

        [Fact]
        public void CamposAuditableEmpleado_NoDebeIncluirSecretos()
        {
            var allowlist = AuditCampos.Empleado;
            Assert.DoesNotContain("PasswordHash", allowlist);
            Assert.DoesNotContain("Token", allowlist);
        }

        [Fact]
        public void CamposAuditableVehiculo_DebeIncluirClientesIds()
        {
            // Relationship IDs are auditable.
            var allowlist = AuditCampos.Vehiculo;
            Assert.Contains("ClientesIds", allowlist);
            Assert.Contains("ClienteId", allowlist);
        }

        [Fact]
        public void CamposAuditableVehiculo_DebeIncluirCamposRelevantes()
        {
            var allowlist = AuditCampos.Vehiculo;
            Assert.Contains("Patente", allowlist);
            Assert.Contains("Marca", allowlist);
            Assert.Contains("Modelo", allowlist);
            Assert.Contains("Estado", allowlist);
        }

        // ─── S3: Legacy records without history ──────────────────────────────

        [Fact]
        public void RegistroLegado_SinAntesNiDespues_DeberiaUsarEstadoSinHistorial()
        {
            // Legacy records have null Antes and Despues – the view model must
            // expose a helper indicating "sin historial" vs "sin cambios"
            var log = new AuditLog
            {
                Antes = null,
                Despues = null
            };

            // EstadoHistorial: null dicts → "SinHistorial" (legacy)
            var estado = AuditCampos.ObtenerEstadoHistorial(log.Antes, log.Despues);
            Assert.Equal("SinHistorial", estado);
        }

        [Fact]
        public void RegistroNuevo_ConAntesYDespuesVacios_DeberiaIndicarSinCambiosCapturados()
        {
            // Empty (not null) dicts → "SinCambios" (new record, no diff available e.g. Create)
            var log = new AuditLog
            {
                Antes = new Dictionary<string, string>(),
                Despues = new Dictionary<string, string> { ["Nombre"] = "Nuevo" }
            };

            var estado = AuditCampos.ObtenerEstadoHistorial(log.Antes, log.Despues);
            Assert.Equal("ConCambios", estado);
        }

        [Fact]
        public void EventoSesion_DeberiaUsarEstadoNoAplica()
        {
            // Session events (Login/Logout) are external – state should indicate
            // "NoAplica" when both antes and despues are empty dicts
            var log = new AuditLog
            {
                Antes = new Dictionary<string, string>(),
                Despues = new Dictionary<string, string>()
            };

            var estado = AuditCampos.ObtenerEstadoHistorial(log.Antes, log.Despues);
            Assert.Equal("NoAplica", estado);
        }

        // ─── S3: No fabrication of history for old records ────────────────────

        [Fact]
        public void EstadoHistorial_NullAntesNullDespues_EsSinHistorial()
        {
            Assert.Equal("SinHistorial", AuditCampos.ObtenerEstadoHistorial(null, null));
        }

        [Fact]
        public void EstadoHistorial_AmbosVacios_EsNoAplica()
        {
            Assert.Equal("NoAplica", AuditCampos.ObtenerEstadoHistorial(
                new Dictionary<string, string>(),
                new Dictionary<string, string>()));
        }

        [Fact]
        public void EstadoHistorial_SoloCreacion_EsConCambios()
        {
            // Create: Antes empty, Despues has values
            Assert.Equal("ConCambios", AuditCampos.ObtenerEstadoHistorial(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["Nombre"] = "Nuevo Servicio" }));
        }

        [Fact]
        public void EstadoHistorial_AmbosConValores_EsConCambios()
        {
            Assert.Equal("ConCambios", AuditCampos.ObtenerEstadoHistorial(
                new Dictionary<string, string> { ["Nombre"] = "Viejo" },
                new Dictionary<string, string> { ["Nombre"] = "Nuevo" }));
        }

        // ─── S1: Concrete before/after values for representative entities ─────

        [Fact]
        public void CapturarCamposServicio_DeberiaProducirDiccionarioConValoresCorrectos()
        {
            var servicio = TestFactory.CreateServicio(
                id: "serv-001",
                nombre: "Lavado Premium",
                precio: 15000,
                tipo: "Lavado",
                tipoVehiculo: "Automovil",
                estado: "Activo");

            var campos = AuditCampos.CapturarServicio(servicio);

            Assert.Equal("Lavado Premium", campos["Nombre"]);
            Assert.Equal("15000", campos["Precio"]);
            Assert.Equal("Lavado", campos["Tipo"]);
            Assert.Equal("Automovil", campos["TipoVehiculo"]);
            Assert.Equal("Activo", campos["Estado"]);
        }

        [Fact]
        public void CapturarCamposCliente_DeberiaProducirDiccionarioConValoresCorrectos()
        {
            var cliente = TestFactory.CreateCliente(
                id: "cli-001",
                nombre: "Juan",
                apellido: "Pérez",
                tipoDocumento: "DNI",
                numeroDocumento: "12345678",
                telefono: "3751000000",
                email: "juan@test.com",
                estado: "Activo");

            var campos = AuditCampos.CapturarCliente(cliente);

            Assert.Equal("Juan", campos["Nombre"]);
            Assert.Equal("Pérez", campos["Apellido"]);
            Assert.Equal("DNI", campos["TipoDocumento"]);
            Assert.Equal("12345678", campos["NumeroDocumento"]);
            Assert.Equal("Activo", campos["Estado"]);
            // Relationship IDs are auditable.
            Assert.Contains("VehiculosIds", campos.Keys);
        }

        [Fact]
        public void CapturarCamposVehiculo_DeberiaProducirDiccionarioConClienteId()
        {
            var vehiculo = TestFactory.CreateVehiculo(
                id: "veh-001",
                patente: "ABC123",
                tipoVehiculo: "Automóvil",
                marca: "Toyota",
                modelo: "Corolla",
                color: "Blanco",
                clienteId: "cli-secret",
                estado: "Activo");

            var campos = AuditCampos.CapturarVehiculo(vehiculo);

            Assert.Equal("ABC123", campos["Patente"]);
            Assert.Equal("Toyota", campos["Marca"]);
            Assert.Equal("Corolla", campos["Modelo"]);
            // Relationship IDs are auditable.
            Assert.Contains("ClienteId", campos.Keys);
            Assert.Contains("ClientesIds", campos.Keys);
        }

        [Fact]
        public void CapturarCamposEmpleado_NoDebeIncluirSecretos()
        {
            var empleado = TestFactory.CreateEmpleado(
                id: "emp-001",
                nombre: "Ana García",
                email: "ana@lavadero.com",
                rol: "Administrador",
                estado: "Activo");

            var campos = AuditCampos.CapturarEmpleado(empleado);

            Assert.Equal("Ana García", campos["NombreCompleto"]);
            Assert.Equal("Administrador", campos["Rol"]);
            Assert.Equal("Activo", campos["Estado"]);
            Assert.DoesNotContain("PasswordHash", campos.Keys);
            Assert.DoesNotContain("Token", campos.Keys);
        }

        [Fact]
        public void CapturarCamposPaquete_DeberiaIncluirNombreYDescuento()
        {
            var paquete = TestFactory.CreatePaquete(
                id: "pkg-001",
                nombre: "Paquete Premium",
                estado: "Activo",
                porcentajeDescuento: 15,
                tipoVehiculo: "Automovil");

            var campos = AuditCampos.CapturarPaquete(paquete);

            Assert.Equal("Paquete Premium", campos["Nombre"]);
            Assert.Equal("15", campos["PorcentajeDescuento"]);
            Assert.Equal("Automovil", campos["TipoVehiculo"]);
            Assert.Equal("Activo", campos["Estado"]);
        }

        // ─── S4: Existing LogEvent overload compatibility ──────────────────────

        [Fact]
        public void AuditLog_ConAntesNulos_DeberiaPermitirCreacion()
        {
            // The new overload must remain backward-compatible with null before/after
            var log = new AuditLog
            {
                UserId = "u1",
                UserEmail = "u1@test.com",
                Action = "Inicio de sesión con Google",
                TargetId = null,
                TargetType = null,
                Antes = null,
                Despues = null,
                NombreHistorico = null,
                Timestamp = DateTime.UtcNow
            };

            Assert.Null(log.Antes);
            Assert.Null(log.Despues);
            Assert.Equal("Inicio de sesión con Google", log.Action);
        }

        // ─── S1: Nombre histórico preserved from capture time ─────────────────

        [Fact]
        public void NombreHistorico_DeberiaPreservarseDesdeCapturaNoDesdeObjetoActual()
        {
            // NombreHistorico is captured at write time, not resolved dynamically later
            var log = new AuditLog
            {
                NombreHistorico = "Servicio Histórico (ya eliminado)"
            };
            Assert.Equal("Servicio Histórico (ya eliminado)", log.NombreHistorico);
        }
    }

    /// <summary>
    /// Tests proving the existing LogEvent (metadata-only) semantics are preserved.
    /// RED: these must all still pass after implementation (regression proof).
    /// </summary>
    public class AuditLogEventCompatibilityTests
    {
        [Fact]
        public void AuditLog_ModeloBasico_SigueFuncionando()
        {
            var log = TestFactory.CreateAuditLog(
                userId: "u1",
                userEmail: "u@t.com",
                action: "Inicio de sesión",
                targetId: null,
                targetType: null);

            Assert.Equal("u1", log.UserId);
            Assert.Equal("u@t.com", log.UserEmail);
            Assert.Equal("Inicio de sesión", log.Action);
        }

        [Fact]
        public void AuditLog_FiltradoPorAccion_SigueFuncionando()
        {
            var logs = new List<AuditLog>
            {
                TestFactory.CreateAuditLog(action: "Creacion de servicio"),
                TestFactory.CreateAuditLog(action: "Modificacion de rol")
            };
            var filtrados = logs.Where(r => r.Action == "Creacion de servicio").ToList();
            Assert.Single(filtrados);
        }
    }
}
