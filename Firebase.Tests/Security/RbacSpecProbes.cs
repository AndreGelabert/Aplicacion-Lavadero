using System.Security.Claims;
using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using static Firebase.Models.AuthModels;

namespace Firebase.Tests.Security
{
    /// <summary>
    /// Independent spec probes for one or more S# items (see odd/tasks/rbac-ui-fixes.md).
    /// </summary>
    public class RbacSpecProbes
    {
        private static readonly Dictionary<string, string[]> MatrizEsperada = new()
        {
            ["Clientes"] = new[] { "Consultar", "Crear", "Modificar", "Eliminar" },
            ["Vehiculos"] = new[] { "Consultar", "Crear", "Modificar", "Eliminar" },
            ["Servicios"] = new[] { "Consultar", "Crear", "Modificar", "Eliminar" },
            ["Paquetes"] = new[] { "Consultar", "Crear", "Modificar", "Eliminar" },
            ["Lavados"] = new[] { "Consultar", "Crear", "Modificar" },
            ["Personal"] = new[] { "Consultar", "Modificar" },
            ["Configuracion"] = new[] { "Consultar", "Modificar" },
            ["Auditoria"] = new[] { "Consultar" },
            ["Roles"] = new[] { "Consultar", "Crear", "Modificar", "Eliminar" }
        };

        // S1
        [Fact]
        public void S1_Catalogo_ExposesExactly28PermissionsAndTheSpecifiedMatrix()
        {
            var esperados = MatrizEsperada.Sum(m => m.Value.Length);
            Assert.Equal(28, esperados);
            Assert.Equal(esperados, RolCatalogo.TodosLosPermisos.Count);
            Assert.Equal(esperados, RolCatalogo.TodosLosPermisos.Distinct().Count());

            foreach (var (modulo, acciones) in MatrizEsperada)
            {
                Assert.Equal(acciones, RolCatalogo.AccionesPara(modulo));
                foreach (var accion in acciones)
                {
                    Assert.True(RolCatalogo.EsPermisoValido($"{modulo}.{accion}"), $"{modulo}.{accion} debe ser válido");
                }
            }
        }

        // S1
        [Theory]
        [InlineData("Auditoria.Crear")]
        [InlineData("Auditoria.Modificar")]
        [InlineData("Auditoria.Eliminar")]
        [InlineData("Configuracion.Crear")]
        [InlineData("Configuracion.Eliminar")]
        [InlineData("Lavados.Eliminar")]
        [InlineData("Personal.Crear")]
        [InlineData("Personal.Eliminar")]
        [InlineData("")]
        [InlineData("Clientes")]
        [InlineData("Clientes.Consultar ")]
        [InlineData("clientes.consultar")]
        public void S1_EsPermisoValido_RejectsInvalidCombinations(string clave)
        {
            Assert.False(RolCatalogo.EsPermisoValido(clave));
            Assert.Empty(RolCatalogo.NormalizarPermisos(new[] { clave }));
        }

        // S1
        [Fact]
        public void S1_NormalizarPermisos_DropsInvalidAndDuplicatesAndKeepsCatalogOrder()
        {
            var entrada = new[]
            {
                "Roles.Eliminar", "Auditoria.Crear", "Clientes.Consultar", "Nope",
                "Clientes.Consultar", "Personal.Crear", "Configuracion.Eliminar",
                "Lavados.Eliminar", "Roles.Consultar"
            };

            var resultado = RolCatalogo.NormalizarPermisos(entrada);

            Assert.Equal(new[] { "Clientes.Consultar", "Roles.Consultar", "Roles.Eliminar" }, resultado);
        }

        // S1 / item 3
        [Fact]
        public void S1_Administrador_KeepsTheFourCriticalRolesPermissions()
        {
            var admin = RolCatalogo.RolesBase.Single(r => r.Nombre == "Administrador");

            Assert.All(
                new[] { "Roles.Consultar", "Roles.Crear", "Roles.Modificar", "Roles.Eliminar" },
                critico => Assert.Contains(critico, admin.Permisos));
            Assert.All(RolCatalogo.PermisosCriticosAdministrador, p => Assert.Contains(p, admin.Permisos));

            var rolAdmin = new Rol { Nombre = "Administrador", Permisos = admin.Permisos.ToList() };
            var sinCriticos = RolCatalogo.NormalizarPermisos(admin.Permisos.Where(p => !p.StartsWith("Roles.")));
            Assert.Equal(RolReglas.MensajePermisosCriticos, RolReglas.ValidarEdicion(rolAdmin, "Administrador", sinCriticos));
            Assert.Null(RolReglas.ValidarEdicion(rolAdmin, "Administrador", admin.Permisos));
        }

        // S4
        [Fact]
        public void S4_CreateUserClaims_AddsOnePermisoClaimPerNormalizedRolePermission()
        {
            var servicio = CrearAuthenticationService();
            var userInfo = new UserInfo
            {
                Uid = "u1",
                Name = "Prueba RBAC",
                Email = "prueba@example.test",
                Role = "Auditor"
            };
            var permisos = new[]
            {
                "Auditoria.Consultar", "Auditoria.Crear", "Auditoria.Consultar",
                "Configuracion.Consultar", "Configuracion.Modificar"
            };

            var claims = servicio.CreateUserClaims(userInfo, permisos);

            Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "u1");
            Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "Prueba RBAC");
            Assert.Contains(claims, c => c.Type == ClaimTypes.Email && c.Value == "prueba@example.test");
            Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "Auditor");

            var permisosEmitidos = claims.Where(c => c.Type == "permiso").Select(c => c.Value).ToList();
            Assert.Equal(new[] { "Configuracion.Consultar", "Configuracion.Modificar", "Auditoria.Consultar" }, permisosEmitidos);
        }

        // S4
        [Fact]
        public async Task S4_PolicyProvider_MultipleKeysAreOr()
        {
            var authorization = CrearAuthorizationService(out _);

            var conPrimera = await authorization.AuthorizeAsync(
                Principal("Clientes.Consultar"), null, "Permiso:Clientes.Consultar|Clientes.Crear");
            var conSegunda = await authorization.AuthorizeAsync(
                Principal("Clientes.Crear"), null, "Permiso:Clientes.Consultar|Clientes.Crear");

            Assert.True(conPrimera.Succeeded);
            Assert.True(conSegunda.Succeeded);
        }

        // S4/S5
        [Fact]
        public async Task S4_PolicyProvider_DeniesAuthenticatedUsersWithoutThePermission()
        {
            var authorization = CrearAuthorizationService(out _);

            var sinClaim = await authorization.AuthorizeAsync(Principal(), null, "Permiso:Clientes.Consultar");
            var otroClaim = await authorization.AuthorizeAsync(Principal("Lavados.Consultar"), null, "Permiso:Clientes.Consultar");
            var desconocido = await authorization.AuthorizeAsync(Principal("Clientes.Consultar"), null, "Permiso:Clientes.Inventado");
            var anonimo = await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, "Permiso:Clientes.Consultar");

            Assert.False(sinClaim.Succeeded);
            Assert.False(otroClaim.Succeeded);
            Assert.False(desconocido.Succeeded);
            Assert.False(anonimo.Succeeded);
        }

        // S4
        [Fact]
        public void S4_PermisoAttribute_EncodesOrPolicyAndRejectsEmptyKeys()
        {
            var atributo = new PermisoAttribute("Clientes.Consultar", "Clientes.Crear", "Clientes.Consultar");

            Assert.Equal("Permiso:Clientes.Consultar|Clientes.Crear", atributo.Policy);
            Assert.Equal(new[] { "Clientes.Consultar", "Clientes.Crear" }, atributo.Permisos);
            Assert.Throws<ArgumentException>(() => new PermisoAttribute());
            Assert.Throws<ArgumentException>(() => new PermisoAttribute(" "));
        }

        // S6
        [Fact]
        public void S6_Navegacion_IsASinglePermissionFilteredListInSidebarOrder()
        {
            var esperados = new[]
            {
                ("Lavados", "/Lavados", "Lavados.Consultar"),
                ("Cliente", "/Cliente", "Clientes.Consultar"),
                ("Vehiculo", "/Vehiculo", "Vehiculos.Consultar"),
                ("Servicio", "/Servicio", "Servicios.Consultar"),
                ("PaqueteServicio", "/PaqueteServicio", "Paquetes.Consultar"),
                ("Personal", "/Personal", "Personal.Consultar"),
                ("Auditoria", "/Auditoria", "Auditoria.Consultar"),
                ("Configuracion", "/Configuracion", "Configuracion.Consultar"),
                ("Rol", "/Rol", "Roles.Consultar")
            };

            Assert.Equal(esperados.Length, NavegacionSistema.Modulos.Count);
            for (var i = 0; i < esperados.Length; i++)
            {
                Assert.Equal(esperados[i].Item1, NavegacionSistema.Modulos[i].Controlador);
                Assert.Equal(esperados[i].Item2, NavegacionSistema.Modulos[i].Ruta);
                Assert.Equal(esperados[i].Item3, NavegacionSistema.Modulos[i].Permiso);
            }
        }

        // S7
        [Fact]
        public void S7_Landing_UsesSidebarOrderAndFallsBackToAccessDenied()
        {
            Assert.Equal("/Lavados", NavegacionSistema.RutaLanding(new[] { "Clientes.Consultar", "Lavados.Consultar" }));
            Assert.Equal("/Auditoria", NavegacionSistema.RutaLanding(new[] { "Auditoria.Consultar", "Configuracion.Consultar", "Configuracion.Modificar" }));
            Assert.Equal("/Rol", NavegacionSistema.RutaLanding(new[] { "Roles.Consultar" }));
            Assert.Equal("/Home/AccessDenied", NavegacionSistema.RutaLanding(Array.Empty<string>()));
            Assert.Equal("/Home/AccessDenied", NavegacionSistema.RutaLanding(new[] { "Clientes.Crear" }));
            Assert.Empty(NavegacionSistema.ModulosVisibles(new[] { "Clientes.Crear" }));
        }

        private static AuthenticationService CrearAuthenticationService()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Firebase:ApiKey"] = "prueba" })
                .Build();

            return new AuthenticationService(configuration, null!, null!, new HttpClient());
        }

        private static IAuthorizationService CrearAuthorizationService(out PermisoPolicyProvider provider)
        {
            provider = new PermisoPolicyProvider(Options.Create(new AuthorizationOptions()));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization();
            services.AddSingleton<IAuthorizationPolicyProvider>(provider);
            return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        private static ClaimsPrincipal Principal(params string[] permisos)
        {
            var identity = new ClaimsIdentity(
                permisos.Select(p => new Claim("permiso", p)),
                "Cookies");
            return new ClaimsPrincipal(identity);
        }
    }
}
