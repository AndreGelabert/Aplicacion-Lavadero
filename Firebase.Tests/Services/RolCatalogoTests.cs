using Firebase.Tests.Integration;
using Google.Api.Gax;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Unit tests for the role permission catalog.
    /// Covers the per-module permission matrix, normalization, protected roles, and base role definitions.
    /// </summary>
    public class RolCatalogoTests
    {
        [Fact]
        public void TodosLosPermisos_ShouldHave39UniqueKeys()
        {
            Assert.Equal(39, RolCatalogo.TodosLosPermisos.Count);
            Assert.Equal(39, RolCatalogo.TodosLosPermisos.Distinct().Count());
        }

        [Theory]
        [InlineData("Clientes", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Vehiculos", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Servicios", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Paquetes", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Lavados", "Consultar,Crear,Modificar")]
        [InlineData("Turnos", "Consultar,Crear,Modificar,Cancelar")]
        [InlineData("Personal", "Consultar,Modificar")]
        [InlineData("Configuracion", "Consultar,Modificar")]
        [InlineData("Auditoria", "Consultar")]
        [InlineData("Roles", "Consultar,Crear,Modificar,Eliminar")]
        [InlineData("Insumos", "Consultar,Crear,Modificar,Eliminar,AjustarStock,Recepcionar,Kardex")]
        public void AccionesPara_ShouldReturnTheActionsThatExistForEachModule(string modulo, string expected)
        {
            var esperadas = expected.Split(',');
            Assert.Equal(esperadas, RolCatalogo.AccionesPara(modulo));
        }

        [Fact]
        public void AccionesPara_ShouldReturnEmptyForUnknownModule()
        {
            Assert.Empty(RolCatalogo.AccionesPara("Desconocido"));
        }

        [Theory]
        [InlineData("Auditoria.Crear")]
        [InlineData("Auditoria.Modificar")]
        [InlineData("Auditoria.Eliminar")]
        [InlineData("Configuracion.Crear")]
        [InlineData("Configuracion.Eliminar")]
        [InlineData("Lavados.Eliminar")]
        [InlineData("Turnos.Eliminar")]
        [InlineData("Personal.Crear")]
        [InlineData("Personal.Eliminar")]
        public void EsPermisoValido_ShouldRejectActionsThatDoNotExistForTheModule(string clave)
        {
            Assert.False(RolCatalogo.EsPermisoValido(clave));
        }

        [Fact]
        public void EsPermisoValido_ShouldRejectUnknownAction()
        {
            Assert.True(RolCatalogo.EsPermisoValido("Clientes.Consultar"));
            Assert.False(RolCatalogo.EsPermisoValido("Clientes.Borrar"));
            Assert.True(RolCatalogo.EsPermisoValido("Turnos.Consultar"));
            Assert.True(RolCatalogo.EsPermisoValido("Turnos.Crear"));
            Assert.True(RolCatalogo.EsPermisoValido("Turnos.Modificar"));
            Assert.True(RolCatalogo.EsPermisoValido("Turnos.Cancelar"));
            Assert.False(RolCatalogo.EsPermisoValido("Turnos.Borrar"));
        }

        [Fact]
        public void NormalizarPermisos_ShouldDeduplicateKeepCatalogOrderAndDropInvalid()
        {
            var result = RolCatalogo.NormalizarPermisos(new[]
            {
                "Roles.Consultar", "Clientes.Consultar", "Clientes.Consultar", "Nope",
                "Auditoria.Crear", "Configuracion.Eliminar", "Lavados.Eliminar", "Personal.Crear"
            });

            Assert.Equal(new[] { "Clientes.Consultar", "Roles.Consultar" }, result);
        }

        [Fact]
        public void NormalizarPermisos_ShouldNotGrantTurnosToExistingPermissions()
        {
            var existing = new[] { "Clientes.Consultar", "Lavados.Crear" };
            Assert.Equal(existing, RolCatalogo.NormalizarPermisos(existing));
            Assert.Equal(new[] { "Lavados.Crear", "Turnos.Consultar", "Turnos.Modificar" },
                RolCatalogo.NormalizarPermisos(new[] { "Turnos.Modificar", "Lavados.Crear", "Turnos.Consultar", "Turnos.Modificar", "Turnos.Eliminar" }));
        }

        [Fact]
        public void NormalizarPermisos_ShouldPreserveEveryValidPermission()
        {
            var result = RolCatalogo.NormalizarPermisos(RolCatalogo.TodosLosPermisos);
            Assert.Equal(RolCatalogo.TodosLosPermisos, result);
        }

        [Theory]
        [InlineData("administrador", true)]
        [InlineData(" Empleado ", true)]
        [InlineData("Supervisor", false)]
        public void EsRolProtegido_ShouldIgnoreCaseAndWhitespace(string nombre, bool expected)
        {
            Assert.Equal(expected, RolCatalogo.EsRolProtegido(nombre));
        }

        [Fact]
        public void RolesBase_Insumos_ShouldApplyTheConfirmedDefaultMatrix()
        {
            var admin = RolCatalogo.RolesBase.Single(r => r.Nombre == "Administrador");
            var empleado = RolCatalogo.RolesBase.Single(r => r.Nombre == "Empleado");

            Assert.Equal(RolCatalogo.TodosLosPermisos, admin.Permisos);
            Assert.Equal(new[] { "Insumos.Consultar", "Insumos.Recepcionar" },
                empleado.Permisos.Where(p => p.StartsWith("Insumos.")));
            Assert.Equal(new[]
            {
                "Clientes.Consultar", "Clientes.Crear", "Clientes.Modificar",
                "Vehiculos.Consultar", "Vehiculos.Crear", "Vehiculos.Modificar",
                "Servicios.Consultar", "Paquetes.Consultar",
                "Lavados.Consultar", "Lavados.Crear", "Lavados.Modificar",
                "Turnos.Consultar", "Turnos.Crear", "Turnos.Modificar", "Turnos.Cancelar"
            }, empleado.Permisos.Where(p => !p.StartsWith("Insumos.")));
        }

        [LocalFirestoreFact]
        public async Task Inicializacion_NoModificaRolesExistentesNiConcedeTurnos()
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-rol-existing-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            var admin = db.Collection("roles").Document("admin-existing");
            var empleado = db.Collection("roles").Document("employee-existing");
            var custom = db.Collection("roles").Document("custom-existing");
            await admin.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Administrador", ["Estado"] = "Activo", ["Permisos"] = RolCatalogo.PermisosCriticosAdministrador.ToArray(), ["Extra"] = "preservar" });
            await empleado.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Empleado", ["Estado"] = "Activo", ["Permisos"] = new[] { "Lavados.Consultar" }, ["Extra"] = "preservar" });
            await custom.SetAsync(new Dictionary<string, object> { ["Nombre"] = "Especial", ["Estado"] = "Activo", ["Permisos"] = new[] { "Clientes.Consultar" }, ["Extra"] = "preservar" });
            var references = new[] { admin, empleado, custom };
            var before = new List<DocumentSnapshot>();
            foreach (var reference in references) before.Add(await reference.GetSnapshotAsync());
            using var cache = new MemoryCache(new MemoryCacheOptions());
            await new RolService(db, cache).AsegurarRolesBaseAsync();
            foreach (var snapshot in before)
            {
                var after = await snapshot.Reference.GetSnapshotAsync();
                Assert.Equal(snapshot.UpdateTime, after.UpdateTime);
                Assert.Equal(snapshot.GetValue<List<string>>("Permisos"), after.GetValue<List<string>>("Permisos"));
                Assert.DoesNotContain(after.GetValue<List<string>>("Permisos"), p => p.StartsWith("Turnos."));
                Assert.Equal("preservar", after.GetValue<string>("Extra"));
            }
        }

        [LocalFirestoreFact]
        public async Task Inicializacion_RolesNuevosRecibenPermisosTurnos()
        {
            var db = new FirestoreDbBuilder { ProjectId = "demo-rol-new-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
            using var cache = new MemoryCache(new MemoryCacheOptions());
            await new RolService(db, cache).AsegurarRolesBaseAsync();
            var admin = await db.Collection("roles").Document("rol-administrador").GetSnapshotAsync();
            var empleado = await db.Collection("roles").Document("rol-empleado").GetSnapshotAsync();
            Assert.True(admin.Exists);
            Assert.True(empleado.Exists);
            Assert.Equal(RolCatalogo.TodosLosPermisos, admin.GetValue<List<string>>("Permisos"));
            Assert.Equal(new[] { "Turnos.Consultar", "Turnos.Crear", "Turnos.Modificar", "Turnos.Cancelar" }, admin.GetValue<List<string>>("Permisos").Where(p => p.StartsWith("Turnos.")));
            Assert.Equal(new[] { "Turnos.Consultar", "Turnos.Crear", "Turnos.Modificar", "Turnos.Cancelar" }, empleado.GetValue<List<string>>("Permisos").Where(p => p.StartsWith("Turnos.")));
        }

        [Fact]
        public void RolesBase_ShouldContainAdministratorWithAllPermissions()
        {
            var admin = RolCatalogo.RolesBase.Single(r => r.Nombre == "Administrador");
            var empleado = RolCatalogo.RolesBase.Single(r => r.Nombre == "Empleado");

            Assert.Equal(39, admin.Permisos.Count);
            Assert.Equal(new[] { "Turnos.Consultar", "Turnos.Crear", "Turnos.Modificar", "Turnos.Cancelar" },
                admin.Permisos.Where(p => p.StartsWith("Turnos.")));
            Assert.Equal(new[] { "Turnos.Consultar", "Turnos.Crear", "Turnos.Modificar", "Turnos.Cancelar" },
                empleado.Permisos.Where(p => p.StartsWith("Turnos.")));
            Assert.Equal(17, empleado.Permisos.Count);
            Assert.DoesNotContain(empleado.Permisos, p => p.StartsWith("Roles."));
            Assert.All(RolCatalogo.PermisosCriticosAdministrador, p => Assert.Contains(p, admin.Permisos));
        }
    }
}
