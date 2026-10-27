# Roles y Permisos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a complete Roles y Permisos module (CRUD, search/filter/sort/pagination, protected base roles, audit) and use registered roles to populate Personal's role dropdown.

**Architecture:** Follow the existing Firestore model + scoped service + `[Authorize(Roles = "Administrador")]` controller + Razor partials + page-specific JS pattern. Role permissions are stored as flat `<Modulo>.<Accion>` keys in a `roles` collection. The permission catalog and all business rules live in pure, unit-tested classes; the service handles Firestore persistence and seeding. Existing controller authorization stays role-name based in this iteration.

**Tech Stack:** ASP.NET Core MVC net9.0, Google.Cloud.Firestore, Tailwind/Flowbite, xUnit.

**Spec:** `Documentacion/1-Requisitos.md` CU-089–CU-092; `Documentacion/2-Análisis.md` Gestión de Roles; `Documentacion/3-Diseño.md` lines 2744–2862.

## Global Constraints

- Do not change existing `[Authorize]` attributes or the cookie role claim. Permissions are persisted/displayed but not enforced by authorization yet.
- Collection name is `roles`; employee collection remains `empleados`.
- Protected role names are exactly `Administrador` and `Empleado`, compared case-insensitively after trimming.
- Protected names cannot be changed and protected roles cannot be deleted.
- `Administrador` must always retain all four `Roles.*` permissions.
- Every role must keep at least one valid permission.
- New roles always start with `Estado = "Activo"` and `FechaCreacion = DateTime.UtcNow`.
- Delete is physical and blocked when any employee (active or inactive) has the role assigned.
- Audit actions are exactly `Alta Rol`, `Modificacion Rol`, `Eliminacion Rol`; target type is `Rol`.
- Use Spanish UI copy exactly as specified in this plan.
- Reuse existing Tailwind/Flowbite classes and modal patterns; do not add a CSS build step.

## Decisions

- **Permission catalog uses real modules, not the design's aspirational list.** The design mentions Turnos and Reportes, but those controllers/views do not exist. The catalog is: Clientes, Vehículos, Servicios, Paquetes, Lavados, Personal, Configuración, Auditoría, Roles y Permisos. Each module has Consultar, Crear, Modificar, Eliminar (36 keys).
- **Permissions are not enforced yet.** `Cliente`, `Vehiculo`, `TipoDocumento` still allow `Administrador,Empleado`; the remaining controllers still require `Administrador`. Dynamic permission enforcement would require replacing every authorization attribute and is a separate project.
- **No state toggle UI.** The table displays Estado, but the documented use cases define no activate/deactivate action. Existing roles keep their state; new roles start Activo.
- **Seeding is lazy but cached.** `AsegurarRolesBaseAsync()` runs on the Roles and Personal index pages, creates missing `rol-administrador` / `rol-empleado` documents, and is guarded by a 30-minute `IMemoryCache` entry.

## Review Focus

- Duplicate names with different case or surrounding spaces must be rejected (`Ventas`, `  ventas  `).
- Search must match case/accent variants (`administracion` finds `Administración`).
- A forged update that renames a protected role or strips `Roles.Consultar` from Administrador must be rejected server-side.
- Deleting a role assigned to inactive employees must still be blocked.
- Opening a page number beyond the last page must show the last valid page, not an empty grid.

## File Structure

**Create**
- `Services/RolCatalogo.cs` — permission catalog, protected names, base-role definitions.
- `Services/RolReglas.cs` — validation, duplicate/search normalization, protection rules.
- `Models/Rol.cs` — Firestore model.
- `Services/RolService.cs` — Firestore CRUD, filtering/sorting/pagination, employee counts, seeding.
- `Controllers/RolController.cs` — page, partials, AJAX endpoints, audit.
- `Views/Rol/Index.cshtml` — page shell, filters, modals, toast template.
- `Views/Rol/_RolTable.cshtml` — sortable table + pagination.
- `Views/Rol/_RolForm.cshtml` — create/edit modal form with permission grid.
- `wwwroot/js/roles.js` — search/filter/sort/pagination, modal, AJAX, toasts, delete flow.
- `Firebase.Tests/Services/RolCatalogoTests.cs`
- `Firebase.Tests/Services/RolReglasTests.cs`
- `Firebase.Tests/Services/RolModelTests.cs`

**Modify**
- `Program.cs` — register `RolService`.
- `Controllers/PersonalController.cs` — seed roles, populate role options in partials, validate assigned role.
- `Services/PersonalService.cs` — `ObtenerRolesUnicos()` reads active roles.
- `Views/Personal/_PersonalTable.cshtml` — role options from `ViewBag.TodosLosRoles`.
- `Views/Shared/_Layout.cshtml` — sidebar link.

---

### Task 1: Permission catalog and role rules

**Files:**
- Create: `Services/RolCatalogo.cs`
- Create: `Services/RolReglas.cs`
- Test: `Firebase.Tests/Services/RolCatalogoTests.cs`
- Test: `Firebase.Tests/Services/RolReglasTests.cs`

**Interfaces:**
- Produces: `RolCatalogo.Modulos`, `RolCatalogo.Acciones`, `RolCatalogo.TodosLosPermisos`, `RolCatalogo.PermisosCriticosAdministrador`, `RolCatalogo.NombresRolesProtegidos`, `RolCatalogo.RolesBase`, `RolCatalogo.Clave(string,string)`, `RolCatalogo.EsPermisoValido(string)`, `RolCatalogo.NormalizarPermisos(IEnumerable<string>)`, `RolCatalogo.EsRolProtegido(string)`, `RolCatalogo.EtiquetaPermiso(string)`.
- Produces: `RolReglas.ValidarNombre`, `ValidarDescripcion`, `ValidarPermisos`, `ValidarEdicion`, `ValidarEliminacion`, `NormalizarNombre`, `SonNombresIguales`, `ContieneTexto`, and message constants.

- [ ] **Step 1: Write the failing catalog tests**

```csharp
public class RolCatalogoTests
{
    [Fact]
    public void TodosLosPermisos_ShouldHave36UniqueKeys()
    {
        Assert.Equal(36, RolCatalogo.TodosLosPermisos.Count);
        Assert.Equal(36, RolCatalogo.TodosLosPermisos.Distinct().Count());
    }

    [Fact]
    public void EsPermisoValido_ShouldRejectUnknownAction()
    {
        Assert.True(RolCatalogo.EsPermisoValido("Clientes.Consultar"));
        Assert.False(RolCatalogo.EsPermisoValido("Clientes.Borrar"));
        Assert.False(RolCatalogo.EsPermisoValido("Turnos.Consultar"));
    }

    [Fact]
    public void NormalizarPermisos_ShouldDeduplicateAndKeepCatalogOrder()
    {
        var result = RolCatalogo.NormalizarPermisos(new[]
        {
            "Roles.Consultar", "Clientes.Consultar", "Clientes.Consultar", "Nope"
        });

        Assert.Equal(new[] { "Clientes.Consultar", "Roles.Consultar" }, result);
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
    public void RolesBase_ShouldContainAdministratorWithAllPermissions()
    {
        var admin = RolCatalogo.RolesBase.Single(r => r.Nombre == "Administrador");
        var empleado = RolCatalogo.RolesBase.Single(r => r.Nombre == "Empleado");

        Assert.Equal(36, admin.Permisos.Count);
        Assert.Equal(11, empleado.Permisos.Count);
        Assert.DoesNotContain(empleado.Permisos, p => p.StartsWith("Roles."));
    }
}
```

- [ ] **Step 2: Write the failing rules tests**

```csharp
using Firebase.Models;

public class RolReglasTests
{
    [Fact]
    public void ValidarNombre_ShouldRejectEmptyAndTooLong()
    {
        Assert.Equal(RolReglas.MensajeNombreObligatorio, RolReglas.ValidarNombre("   "));
        Assert.Equal(RolReglas.MensajeNombreLargo, RolReglas.ValidarNombre(new string('a', 51)));
        Assert.Null(RolReglas.ValidarNombre("Supervisor"));
    }

    [Fact]
    public void ValidarPermisos_ShouldRejectEmptyAndUnknown()
    {
        Assert.Equal(RolReglas.MensajePermisoRequerido, RolReglas.ValidarPermisos(Array.Empty<string>()));
        Assert.Equal(RolReglas.MensajePermisoInvalido, RolReglas.ValidarPermisos(new[] { "Nope" }));
        Assert.Null(RolReglas.ValidarPermisos(new[] { "Clientes.Consultar" }));
    }

    [Fact]
    public void ValidarEdicion_ShouldProtectAdministratorCriticalPermissions()
    {
        var admin = new Rol
        {
            Nombre = "Administrador",
            Permisos = RolCatalogo.TodosLosPermisos.ToList()
        };
        var sinCritico = admin.Permisos.Where(p => p != "Roles.Consultar").ToList();

        Assert.Equal(RolReglas.MensajePermisosCriticos,
            RolReglas.ValidarEdicion(admin, "Administrador", sinCritico));
        Assert.Equal(RolReglas.MensajeRolProtegidoNoEditable,
            RolReglas.ValidarEdicion(admin, "Admin", admin.Permisos));
        Assert.Null(RolReglas.ValidarEdicion(admin, "Administrador", admin.Permisos));
    }

    [Fact]
    public void ValidarEliminacion_ShouldBlockProtectedAndAssignedRoles()
    {
        var admin = new Rol { Nombre = "Administrador", Permisos = RolCatalogo.TodosLosPermisos.ToList() };
        var custom = new Rol { Nombre = "Supervisor", Permisos = new List<string> { "Clientes.Consultar" } };

        Assert.Equal(RolReglas.MensajeRolProtegidoNoEliminable,
            RolReglas.ValidarEliminacion(admin, 0));
        Assert.Equal(
            "No es posible eliminar el rol: Existen 3 empleados que tienen este rol asignado. Reasígnelos a otro rol antes de continuar",
            RolReglas.ValidarEliminacion(custom, 3));
        Assert.Null(RolReglas.ValidarEliminacion(custom, 0));
    }

    [Fact]
    public void DuplicateAndSearchHelpers_ShouldIgnoreCaseAccentsAndWhitespace()
    {
        Assert.True(RolReglas.SonNombresIguales("  Ventas ", "ventas"));
        Assert.True(RolReglas.ContieneTexto("Administración", "administracion"));
        Assert.True(RolReglas.ContieneTexto("Configuración", "CONFIGURACION"));
        Assert.False(RolReglas.ContieneTexto("Clientes", "lavados"));
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test Firebase.Tests/Firebase.Tests.csproj --filter "FullyQualifiedName~RolCatalogoTests|FullyQualifiedName~RolReglasTests"`
Expected: FAIL with missing types `RolCatalogo` / `RolReglas` and `Rol`.

- [ ] **Step 4: Implement `RolCatalogo`**

Create `Services/RolCatalogo.cs` in the global namespace with these exact values:

```csharp
public sealed record ModuloPermisos(string Clave, string Nombre);
public sealed record RolBase(string Id, string Nombre, string Descripcion, IReadOnlyList<string> Permisos);

public static class RolCatalogo
{
    public const string Consultar = "Consultar";
    public const string Crear = "Crear";
    public const string Modificar = "Modificar";
    public const string Eliminar = "Eliminar";

    public static IReadOnlyList<string> Acciones { get; } = new[] { Consultar, Crear, Modificar, Eliminar };
    public static IReadOnlyList<ModuloPermisos> Modulos { get; } = new[]
    {
        new ModuloPermisos("Clientes", "Clientes"),
        new ModuloPermisos("Vehiculos", "Vehículos"),
        new ModuloPermisos("Servicios", "Servicios"),
        new ModuloPermisos("Paquetes", "Paquetes"),
        new ModuloPermisos("Lavados", "Lavados"),
        new ModuloPermisos("Personal", "Personal"),
        new ModuloPermisos("Configuracion", "Configuración"),
        new ModuloPermisos("Auditoria", "Auditoría"),
        new ModuloPermisos("Roles", "Roles y Permisos")
    };
    public static IReadOnlyList<string> TodosLosPermisos { get; } = /* modules × actions in catalog order */;
    public static IReadOnlyList<string> PermisosCriticosAdministrador { get; } = new[]
    {
        "Roles.Consultar", "Roles.Crear", "Roles.Modificar", "Roles.Eliminar"
    };
    public static IReadOnlyList<string> NombresRolesProtegidos { get; } = new[] { "Administrador", "Empleado" };
    public static IReadOnlyList<RolBase> RolesBase { get; } = new[]
    {
        new RolBase("rol-administrador", "Administrador",
            "Rol predeterminado con acceso completo al sistema.", TodosLosPermisos),
        new RolBase("rol-empleado", "Empleado",
            "Rol operativo predeterminado para la atención de clientes, vehículos y lavados.",
            new[]
            {
                "Clientes.Consultar", "Clientes.Crear", "Clientes.Modificar",
                "Vehiculos.Consultar", "Vehiculos.Crear", "Vehiculos.Modificar",
                "Servicios.Consultar", "Paquetes.Consultar",
                "Lavados.Consultar", "Lavados.Crear", "Lavados.Modificar"
            })
    };

    public static string Clave(string modulo, string accion) => $"{modulo}.{accion}";
    public static bool EsPermisoValido(string clave) => /* exact match in TodosLosPermisos */;
    public static IReadOnlyList<string> NormalizarPermisos(IEnumerable<string> permisos) => /* distinct valid keys in catalog order */;
    public static bool EsRolProtegido(string nombre) => /* trim + case-insensitive match */;
    public static string EtiquetaPermiso(string clave) => /* "Clientes · Consultar" */;
}
```

Empleado's 11 keys are exactly the list shown. Administrator uses all 36.

- [ ] **Step 5: Implement `RolReglas`**

Create `Services/RolReglas.cs` in the global namespace:

```csharp
public static class RolReglas
{
    public const string MensajeNombreObligatorio = "El nombre del rol es obligatorio.";
    public const string MensajeNombreLargo = "El nombre del rol no puede superar los 50 caracteres.";
    public const string MensajeDescripcionLarga = "La descripción no puede superar los 250 caracteres.";
    public const string MensajePermisoRequerido = "Debe asignar al menos un permiso.";
    public const string MensajePermisoInvalido = "Uno o más permisos seleccionados no son válidos.";
    public const string MensajeRolProtegidoNoEditable = "No es posible cambiar el nombre de un rol protegido del sistema.";
    public const string MensajePermisosCriticos = "No es posible remover permisos críticos de un rol base protegido.";
    public const string MensajeRolProtegidoNoEliminable = "Acción no permitida: Los roles predeterminados del sistema no pueden ser eliminados.";
    public const string MensajeRolDuplicado = "Ya existe un rol registrado con esa denominación.";

    public static string MensajeUsuariosAsignados(int cantidad) =>
        $"No es posible eliminar el rol: Existen {cantidad} empleados que tienen este rol asignado. Reasígnelos a otro rol antes de continuar";

    public static string ValidarNombre(string nombre) { /* trim; empty -> obligatorio; >50 -> largo */ }
    public static string ValidarDescripcion(string descripcion) { /* null/trim; >250 -> larga */ }
    public static string ValidarPermisos(IEnumerable<string> permisos) { /* empty -> requerido; any invalid -> invalido */ }
    public static string ValidarEdicion(Rol rolActual, string nombreNuevo, IEnumerable<string> permisosNuevos) { /* rules below */ }
    public static string ValidarEliminacion(Rol rol, int usuariosAsignados) { /* protected then assigned */ }
    public static string NormalizarNombre(string nombre) => (nombre ?? string.Empty).Trim();
    public static bool SonNombresIguales(string a, string b) => /* OrdinalIgnoreCase on normalized names */;
    public static string NormalizarTexto(string texto) { /* FormD, remove NonSpacingMark, lower invariant */ }
    public static bool ContieneTexto(string texto, string termino) => /* normalized contains */;
}
```

`ValidarEdicion` order:
1. `ValidarPermisos(permisosNuevos)`; if error return it.
2. If `EsRolProtegido(rolActual.Nombre)` and the trimmed new name differs case-insensitively from the current name, return `MensajeRolProtegidoNoEditable`.
3. If `rolActual.Nombre` is `Administrador` (case-insensitive) and any key in `PermisosCriticosAdministrador` is absent from the normalized new permissions, return `MensajePermisosCriticos`.
4. Return `null`.

Return type of validators is `string?`; treat non-null as the error message.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Firebase.Tests/Firebase.Tests.csproj --filter "FullyQualifiedName~RolCatalogoTests|FullyQualifiedName~RolReglasTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Services/RolCatalogo.cs Services/RolReglas.cs Firebase.Tests/Services/RolCatalogoTests.cs Firebase.Tests/Services/RolReglasTests.cs
git commit -m "feat: add role permission catalog and rules"
```

---

### Task 2: Role model and Firestore service

**Files:**
- Create: `Models/Rol.cs`
- Create: `Services/RolService.cs`
- Test: `Firebase.Tests/Services/RolModelTests.cs`

**Interfaces:**
- Produces: `Rol` with `Id`, `Nombre`, `Descripcion`, `Permisos`, `Estado`, `FechaCreacion`.
- Produces: `RolService` methods:
  - `Task<List<Rol>> ObtenerRoles(string estado = null, int pageNumber = 1, int pageSize = 10, string sortBy = "Nombre", string sortOrder = "asc")`
  - `Task<List<Rol>> BuscarRoles(string searchTerm, string estado = null, int pageNumber = 1, int pageSize = 10, string sortBy = "Nombre", string sortOrder = "asc")`
  - `Task<int> ObtenerTotalPaginas(string estado, int pageSize)`
  - `Task<int> ObtenerTotalRolesBusqueda(string searchTerm, string estado = null)`
  - `Task<Rol> ObtenerRol(string id)`
  - `Task<bool> ExisteRolConNombre(string nombre, string excluirId = null)`
  - `Task<bool> ExisteRolActivo(string nombre)`
  - `Task<string> CrearRol(Rol rol)`
  - `Task ActualizarRol(Rol rol)`
  - `Task EliminarRol(string id)`
  - `Task<Dictionary<string, int>> ObtenerConteoUsuariosPorRol()`
  - `Task AsegurarRolesBaseAsync()`
  - `public const string COLLECTION_NAME = "roles";`

- [ ] **Step 1: Write the failing model tests**

```csharp
using Firebase.Models;

public class RolModelTests
{
    [Fact]
    public void NewRol_ShouldDefaultToActiveWithEmptyPermissions()
    {
        var rol = new Rol();

        Assert.Equal("Activo", rol.Estado);
        Assert.Empty(rol.Permisos);
        Assert.True((DateTime.UtcNow - rol.FechaCreacion).TotalMinutes < 1);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test Firebase.Tests/Firebase.Tests.csproj --filter "FullyQualifiedName~RolModelTests"`
Expected: FAIL because `Rol` does not exist.

- [ ] **Step 3: Implement `Rol`**

Create `Models/Rol.cs` in `Firebase.Models`:

```csharp
[FirestoreData]
public class Rol
{
    [FirestoreProperty] public string Id { get; set; } = string.Empty;
    [FirestoreProperty] public string Nombre { get; set; } = string.Empty;
    [FirestoreProperty] public string Descripcion { get; set; } = string.Empty;
    [FirestoreProperty] public List<string> Permisos { get; set; } = new();
    [FirestoreProperty] public string Estado { get; set; } = "Activo";
    [FirestoreProperty] public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
```

Add XML doc comments in the style of `Models/PaqueteServicio.cs` (collection, permission key format, defaults).

- [ ] **Step 4: Implement `RolService` persistence**

Create `Services/RolService.cs` in the global namespace. Constructor: `RolService(FirestoreDb firestore, IMemoryCache cache)` with null checks. Use these exact behaviors:

- `ObtenerRol`: read `roles/{id}`, map, return `null` if missing.
- `ObtenerRolesFiltrados(estado)`: read the whole `roles` collection once; map every document; if `estado` is `Activo`/`Inactivo`, filter case-insensitively; otherwise return all. Sort by `sortBy`/`sortOrder` (`Nombre`, `Descripcion`, `Permisos` count, `Estado`, `FechaCreacion`; default `Nombre asc`). Search uses `RolReglas.ContieneTexto` on `Nombre` and `Descripcion`.
- `ObtenerRoles`: filter, sort, then skip/take using `Math.Max(1, pageNumber)` and `Math.Max(1, pageSize)`.
- `BuscarRoles`: same base list, apply search, sort, paginate.
- `ObtenerTotalPaginas` / `ObtenerTotalRolesBusqueda`: count the filtered list and return `Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)))` (the search total ignores page size).
- `ExisteRolConNombre`: read all roles, exclude `excluirId`, use `RolReglas.SonNombresIguales`.
- `ExisteRolActivo`: match a role name case-insensitively with `Estado == "Activo"`.
- `CrearRol`: create an auto-ID document, assign `rol.Id = docRef.Id`, persist fields `Nombre`, `Descripcion`, `Permisos`, `Estado`, `FechaCreacion` via a dictionary (do not persist the `Id` field). Return the new ID.
- `ActualizarRol`: require a non-empty ID; `SetAsync(dictionary, SetOptions.Overwrite)` on `roles/{Id}`.
- `EliminarRol`: delete `roles/{id}`.
- `ObtenerConteoUsuariosPorRol`: read all `empleados` documents once; read `Rol` with fallback `"Empleado"`; group by role name using `StringComparer.OrdinalIgnoreCase`; return `Dictionary<string,int>` with the same comparer. Count all employees regardless of `Estado`.
- Mapping: `Id = doc.Id`; `Nombre` fallback empty; `Descripcion` fallback empty; `Permisos` from `GetValue<List<object>>("Permisos")` converted to strings; `Estado` fallback `"Activo"`; `FechaCreacion` from `GetValue<Timestamp>("FechaCreacion").ToDateTime()` with fallback `DateTime.UtcNow`.
- `AsegurarRolesBaseAsync`: if `_cache.TryGetValue("roles_base_asegurados", out _)` return. Read all roles once. For each `RolCatalogo.RolesBase`:
  - If a role with the same name exists, do not create a duplicate. If it is `Administrador` and lacks any critical permission, add the missing critical keys and update it.
  - Otherwise create `roles/{RolBase.Id}` with the base definition and `Estado = "Activo"`, `FechaCreacion = DateTime.UtcNow`.
  - Set cache entry `"roles_base_asegurados"` for 30 minutes.
- Use `RolCatalogo.NormalizarPermisos` when writing permissions.

- [ ] **Step 5: Run the model test and build**

Run: `dotnet test Firebase.Tests/Firebase.Tests.csproj --filter "FullyQualifiedName~RolModelTests"`
Expected: PASS.
Run: `dotnet build Firebase.sln`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Models/Rol.cs Services/RolService.cs Firebase.Tests/Services/RolModelTests.cs
git commit -m "feat: add role model and Firestore service"
```

---

### Task 3: Roles controller and dependency injection

**Files:**
- Create: `Controllers/RolController.cs`
- Modify: `Program.cs:109-122`

**Interfaces:**
- Consumes: `RolService`, `AuditService`, `RolReglas`, `RolCatalogo`.
- Produces endpoints listed below.

- [ ] **Step 1: Implement the controller**

Create `Controllers/RolController.cs`:

```csharp
[Authorize(Roles = "Administrador")]
public class RolController : Controller
{
    public RolController(RolService rolService, AuditService auditService)

    [HttpGet] public Task<IActionResult> Index(string estado, int pageNumber = 1, int pageSize = 10, string sortBy = "Nombre", string sortOrder = "asc")
    [HttpGet] public Task<IActionResult> SearchPartial(string searchTerm, string estado, int pageNumber = 1, int pageSize = 10, string sortBy = "Nombre", string sortOrder = "asc")
    [HttpGet] public Task<IActionResult> TablePartial(string estado, int pageNumber = 1, int pageSize = 10, string sortBy = "Nombre", string sortOrder = "asc")
    [HttpGet] public Task<IActionResult> FormPartial(string id = null)
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CrearRolAjax(Rol rol)
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> ActualizarRolAjax(Rol rol)
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> EliminarRolAjax(string id)
}
```

Behavior:

- `Index`: call `_rolService.AsegurarRolesBaseAsync()`. Compute `totalPages`, clamp `pageNumber` to `1..totalPages`, load roles, load `ObtenerConteoUsuariosPorRol()`, set `ViewBag.CurrentPage`, `TotalPages`, `VisiblePages` (current ±2), `Estado`, `SortBy`, `SortOrder`, `ConteoUsuarios`, `SearchTerm`. Return `View(roles)`.
- `SearchPartial`/`TablePartial`: compute total pages from `ObtenerTotalRolesBusqueda` / `ObtenerTotalPaginas`, clamp `pageNumber` to `1..totalPages`, load roles, set the same ViewBag (including counts), and return `PartialView("_RolTable", roles)`.
- `FormPartial`: if `id` is null return `PartialView("_RolForm", new Rol())`; otherwise load the role and return `NotFound()` when missing.
- `CrearRolAjax`:
  - Trim name/description; run `RolReglas.ValidarNombre`, `ValidarDescripcion`, `ValidarPermisos`.
  - If the name is valid and `ExisteRolConNombre` is true, add `RolReglas.MensajeRolDuplicado` to the `Nombre` errors.
  - On errors return `Json(new { success = false, message = "Revise los errores del formulario.", errors })` with a `Dictionary<string,string>`.
  - Normalize permissions, set `Estado = "Activo"`, `FechaCreacion = DateTime.UtcNow`, persist, audit `Alta Rol`, return `Json(new { success = true, message = "Rol creado exitosamente" })`.
- `ActualizarRolAjax`:
  - Load the existing role; if missing return `success = false` with `"No se encontró el rol a actualizar."`.
  - Validate name/description/permissions, then `RolReglas.ValidarEdicion(actual, rol.Nombre, rol.Permisos)`; map the returned message to `Nombre` when it is the protected-name message, otherwise to `Permisos`.
  - Check duplicate excluding `rol.Id`.
  - Keep `Estado` and `FechaCreacion` from the existing document; set trimmed name (or keep the protected existing name), description, normalized permissions; persist; audit `Modificacion Rol`; return `"Rol actualizado exitosamente"`.
- `EliminarRolAjax`:
  - Load the role; missing -> error `"No se encontró el rol a eliminar."`.
  - Get `ObtenerConteoUsuariosPorRol()` and call `RolReglas.ValidarEliminacion(rol, count)`. Return `success = false` with that exact message when blocked.
  - Delete, audit `Eliminacion Rol`, return `"Rol eliminado exitosamente"`.
- Audit helper reads `ClaimTypes.NameIdentifier` and `ClaimTypes.Email`, calls `_auditService.LogEvent(userId, userEmail, action, targetId, "Rol")`.

- [ ] **Step 2: Register the service**

In `Program.cs`, next to the other scoped services:

```csharp
builder.Services.AddScoped<RolService>();
```

- [ ] **Step 3: Build**

Run: `dotnet build Firebase.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Controllers/RolController.cs Program.cs
git commit -m "feat: add roles controller and service registration"
```

---

### Task 4: Roles page, table, and create/edit modal

**Files:**
- Create: `Views/Rol/Index.cshtml`
- Create: `Views/Rol/_RolTable.cshtml`
- Create: `Views/Rol/_RolForm.cshtml`
- Create: `wwwroot/js/roles.js`

**Interfaces:**
- Consumes: controller endpoints from Task 3, `RolCatalogo`.
- Produces: page at `/Rol` with search, status filter, sort, pagination, create/edit modal, toast helper.

- [ ] **Step 1: Create `Views/Rol/Index.cshtml`**

Structure:

- `@model List<Firebase.Models.Rol>`, title `"Roles y Permisos"`.
- Header: `h1` "Roles y Permisos", `button id="btn-nuevo-rol"` with `onclick="openRolModal()"` and text "+ Nuevo Rol", plus a Flowbite help popover describing role management.
- Filters row: search input `id="rol-search"` placeholder `"Buscar rol..."`; status select `id="rol-estado"` with `value=""` "Todos", `"Activo"`, `"Inactivo"` and `selected` from `ViewBag.Estado`; sort select `id="rol-sort"` with options `Nombre|asc` "Nombre (A-Z)", `Nombre|desc` "Nombre (Z-A)", `FechaCreacion|desc` "Más recientes", `Permisos|desc` "Más permisos", `Estado|asc` "Estado".
- Table container `id="rol-table-container"` rendering `_RolTable`.
- Create/edit modal `id="rol-modal"` (hidden, fixed overlay, max-w-4xl) with header `id="rol-modal-title"`, close button, and body `id="rol-form-container"`.
- Delete modal `id="rol-delete-modal"`: title "Confirmar Eliminación", `p id="rol-delete-text"`, Cancelar, and `button id="rol-delete-confirm-btn"` "Confirmar Eliminación".
- Block modal `id="rol-block-modal"`: warning icon, `p id="rol-block-message"`, "Entendido".
- Delete form `id="rol-delete-form"` with `@Html.AntiForgeryToken()` and hidden `id="rol-delete-id"`; no visible inputs.
- Toast container `id="rol-toast-container"` fixed top-right. Include one hidden toast template `id="rol-toast-template"` with the classes `flex items-center w-full max-w-xs p-4 mb-4 text-sm rounded-lg shadow-sm` so Tailwind keeps the layout classes.
- `@section Scripts { <script src="~/js/roles.js" asp-append-version="true"></script> }`.

- [ ] **Step 2: Create `Views/Rol/_RolTable.cshtml`**

- Model `List<Firebase.Models.Rol>`.
- Hidden inputs `current-page-value`, `current-sort-by`, `current-sort-order`.
- Columns: Nombre (sortable), Descripción (sortable), Permisos (sortable; render badge with `@rol.Permisos.Count` and `title="@string.Join(", ", rol.Permisos.Select(RolCatalogo.EtiquetaPermiso))"`), Usuarios Asignados (count from `ViewBag.ConteoUsuarios as Dictionary<string,int>`), Estado (sortable badge: green for Activo, red/gray for Inactivo), Acciones.
- Acciones: Editar button always with `data-id="@rol.Id"` and `onclick="openRolModal(this.dataset.id)"`; Eliminar button with `data-id`, `data-nombre="@rol.Nombre"`, `data-protegido="@(RolCatalogo.EsRolProtegido(rol.Nombre) ? "true" : "false")"`, `data-usuarios="@count"`, and `onclick="openRolDeleteModal(this)"`. Do not interpolate the role name into a JS string. For protected roles render the button `disabled` with `title="Rol protegido del sistema"` and reduced opacity.
- Empty state: one row with `colspan="6"` and exactly `"No se encontraron roles registrados que coincidan con la búsqueda"`.
- Pagination: only render the `<nav>` when `Model.Any()`. Buttons call `changeRolPage(n)`. Show "Mostrando página X de Y".

- [ ] **Step 3: Create `Views/Rol/_RolForm.cshtml`**

- Model `Firebase.Models.Rol`; `var edit = !string.IsNullOrWhiteSpace(Model?.Id); var protegido = RolCatalogo.EsRolProtegido(Model?.Nombre); var permisos = Model?.Permisos ?? new List<string>();`
- Form `id="rol-form"` with `data-edit="@edit"`, action `CrearRolAjax` or `ActualizarRolAjax`, `onsubmit="return submitRolAjax(this, event);"`, anti-forgery token, hidden `Id`.
- Nombre input: `name="Nombre"`, `id="Nombre"`, `maxlength="50"`, `required`, value. When `protegido`, add `readonly`, muted classes, and a badge with exact text `"Rol Protegido del Sistema"`. Error span `data-valmsg-for="Nombre"`.
- Descripción textarea: `name="Descripcion"`, `maxlength="250"`, rows 3, value. Error span `data-valmsg-for="Descripcion"`.
- Permission grid: iterate `RolCatalogo.Modulos`; each module card shows `modulo.Nombre` and four checkboxes for `RolCatalogo.Acciones`, with:
  - `name="Permisos"`, `value="@RolCatalogo.Clave(modulo.Clave, accion)"`
  - `id="perm-@modulo.Clave-@accion"`
  - `checked` when `permisos.Contains(clave)`
  - `disabled` for `Roles.*` when `protegido && RolReglas.SonNombresIguales(Model?.Nombre, "Administrador")`, with `title="Permiso crítico del sistema"`. Immediately after each disabled critical checkbox, render a hidden input with the same `name="Permisos"` and value so the four critical permissions are always submitted.
- "Seleccionar todos" checkbox `id="perm-select-all"` calling `seleccionarTodosPermisos(this.checked)`.
- Inline error span `data-valmsg-for="Permisos"`, global alert `id="rol-form-message"` (hidden).
- Buttons: submit text `"Guardar"` (create) or `"Guardar Cambios"` (edit); Cancel `"Cancelar"` calling `closeRolModal()`.

- [ ] **Step 4: Create `wwwroot/js/roles.js`**

Implement an IIFE with `window.PageModules.roles = { init: initRolesPage }`; call `initRolesPage()` on `DOMContentLoaded`. Keep all functions private except the handlers referenced from Razor. Required functions and behavior:

- `initRolesPage()`: bind search input (clone to remove stale handlers), debounce 400 ms; bind `rol-estado` and `rol-sort` changes; set initial sort from hidden inputs.
- `buildRolParams()`: `URLSearchParams` with `searchTerm`, `estado`, `sortBy`, `sortOrder`.
- `reloadRolTable(page)`: fetch `/Rol/SearchPartial` when search term is non-empty, otherwise `/Rol/TablePartial`; set `pageNumber`; replace `#rol-table-container` innerHTML; update `dataset.currentPage`.
- `window.changeRolPage(page)` and `window.sortRolTable(sortBy)`: toggle sort if same column, otherwise `asc`; update hidden inputs and reload page 1.
- `openRolModal(id)`: show modal, set title `"Crear Nuevo Rol"` or `"Modificar Rol"`, fetch `/Rol/FormPartial?id=...` (encode id), insert HTML, reset `#perm-select-all` state, focus `#Nombre`.
- `closeRolModal()`: hide and clear form container.
- `submitRolAjax(form, event)`: prevent default; POST `FormData` with `X-Requested-With: XMLHttpRequest`; parse JSON; on success close modal, `showRolToast(message, "success")`, clear search, reload page 1; on failure show `#rol-form-message`, fill `[data-valmsg-for]` spans from `errors`, add red border classes to invalid inputs.
- `seleccionarTodosPermisos(checked)`: check/uncheck all enabled `input[name="Permisos"]`.
- `showRolToast(message, type)`: clone the template, set text, use green classes for success and red classes for error, append to `#rol-toast-container`, remove after 4 s. Do not use `innerHTML` for the message.
- Event delegation updates `#perm-select-all` when individual permissions change.
- `window.openRolDeleteModal`, `window.closeRolDeleteModal`, `window.showRolBlockModal`, `window.closeRolBlockModal` are added in Task 5; for this task, declare the delete handlers as no-ops so table buttons do not throw.

- [ ] **Step 5: Build and manually verify the read/create/edit flow**

Run: `dotnet build Firebase.sln`
Expected: Build succeeded.
Start the app, open `/Rol`. Verify: seeded Administrador and Empleado appear, search/status/sort/pagination work, "Nuevo Rol" opens the modal, saving a role closes it and shows the success toast, editing preloads name/description/checkboxes, and protected roles show the badge and disabled name.

- [ ] **Step 6: Commit**

```bash
git add Views/Rol/Index.cshtml Views/Rol/_RolTable.cshtml Views/Rol/_RolForm.cshtml wwwroot/js/roles.js
git commit -m "feat: add roles page with create and edit modal"
```

---

### Task 5: Delete flow with protected/assigned blocking

**Files:**
- Modify: `Views/Rol/Index.cshtml` (only if modal markup is missing)
- Modify: `wwwroot/js/roles.js`

**Interfaces:**
- Consumes: `EliminarRolAjax`, `#rol-delete-form`, table data attributes.
- Produces: `openRolDeleteModal(button)`, `closeRolDeleteModal()`, `showRolBlockModal(mensaje)`, `closeRolBlockModal()`.

- [ ] **Step 1: Implement the delete JS**

Replace the no-op handlers from Task 4:

- `openRolDeleteModal(button)`:
  - Read `id`, `nombre`, `protegido`, and `usuarios` from `button.dataset` (`data-protegido="true"` and `data-usuarios` are strings; parse the number).
  - If `protegido`, call `showRolBlockModal("Acción no permitida: Los roles predeterminados del sistema no pueden ser eliminados")` and return.
  - If `usuarios > 0`, call `showRolBlockModal("No es posible eliminar el rol: Existen " + usuarios + " empleados que tienen este rol asignado. Reasígnelos a otro rol antes de continuar")` and return.
  - Set `#rol-delete-id` to `id`, set `#rol-delete-text` to `¿Está seguro de que desea eliminar el rol ${nombre}? Esta operación es irreversible.` using `textContent`, and show `#rol-delete-modal`.
- `closeRolDeleteModal()` hides the modal.
- `showRolBlockModal(mensaje)` sets `#rol-block-message` text and shows `#rol-block-modal`; `closeRolBlockModal()` hides it.
- Confirm button handler: prevent default; fetch `/Rol/EliminarRolAjax` with `new FormData(#rol-delete-form)`; parse JSON. On success close the delete modal, `showRolToast(message, "success")`, reload the current page (if the current page becomes empty, reload page 1). On failure close the delete modal and `showRolBlockModal(message)` for blocked messages, otherwise `showRolToast(message, "error")`.
- Load roles into the state list.

- [ ] **Step 2: Verify blocked paths**

Manual checks:
- Protected row: delete button disabled, tooltip present.
- Role assigned to an employee: clicking delete shows the block modal with the exact employee count.
- Unassigned custom role: confirmation modal shows exact text; confirming deletes it and shows the success toast.
- Server-side: POSTing a protected ID directly returns `success = false` with the protected-role message.

- [ ] **Step 3: Build**

Run: `dotnet build Firebase.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Views/Rol/Index.cshtml wwwroot/js/roles.js
git commit -m "feat: add role deletion with protected and assigned guards"
```

---

### Task 6: Personal integration, sidebar, and final verification

**Files:**
- Modify: `Controllers/PersonalController.cs`
- Modify: `Services/PersonalService.cs:165-178`
- Modify: `Views/Personal/_PersonalTable.cshtml:108-116`
- Modify: `Views/Shared/_Layout.cshtml` (admin `<ul>` around lines 186–224)

**Interfaces:**
- Consumes: `RolService.AsegurarRolesBaseAsync`, `RolService.ExisteRolActivo`, `PersonalService.ObtenerRolesUnicos`.
- Produces: Personal dropdown options from registered active roles; sidebar link.

- [ ] **Step 1: Update `PersonalService.ObtenerRolesUnicos`**

Query the `roles` collection instead of `empleados`:

```csharp
var snapshot = await _firestore.Collection("roles").GetSnapshotAsync();
return snapshot.Documents
    .Where(doc => !doc.ContainsField("Estado") || doc.GetValue<string>("Estado") == "Activo")
    .Select(doc => doc.ContainsField("Nombre") ? doc.GetValue<string>("Nombre") : null)
    .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(nombre => nombre)
    .ToList();
```

Keep the method signature and XML comment; update the comment to "roles activos registrados".

- [ ] **Step 2: Update `PersonalController`**

- Add `private readonly RolService _rolService;` and constructor parameter; assign it.
- At the start of `Index`, `SearchPartial`, and `TablePartial`, call `await _rolService.AsegurarRolesBaseAsync();`.
- In `SearchPartial` and `TablePartial`, add `ViewBag.TodosLosRoles = await _personalService.ObtenerRolesUnicos();` before returning the partial.
- In `UpdateRole`, before `_personalService.ActualizarRol`, check `await _rolService.ExisteRolActivo(newRole)`. If false, return the existing AJAX error shape with `"El rol seleccionado no existe o no está activo."` (or set `TempData["Error"]` and redirect for non-AJAX).

- [ ] **Step 3: Update `_PersonalTable.cshtml`**

Replace the hardcoded `<option>` elements with a loop over `ViewBag.TodosLosRoles as List<string>`. Inside the employee loop, build a local list that includes `empleado.Rol` when it is missing from the registered list, so legacy assignments remain visible. Mark the matching option `selected`. Escape values with Razor (`@rol`, `value="@rol"`).

- [ ] **Step 4: Add the sidebar link**

Inside the admin-only `<ul>` in `_Layout.cshtml`, add a `<li>` before Configuración:

```html
<a href="/Rol"
   class="flex items-center p-2 text-base font-medium text-gray-900 rounded-lg transition duration-75 hover:bg-gray-100 dark:hover:bg-gray-700 dark:text-white group @(ViewContext.RouteData.Values["controller"]?.ToString() == "Rol" ? "bg-gray-100 dark:bg-gray-700" : "")">
    <!-- shield/key SVG -->
    <span class="ml-3">Roles y Permisos</span>
</a>
```

Use a shield or key SVG consistent with the existing icon sizing.

- [ ] **Step 5: Run the full test suite and build**

Run: `dotnet test Firebase.Tests/Firebase.Tests.csproj`
Expected: all tests PASS.
Run: `dotnet build Firebase.sln`
Expected: Build succeeded.

- [ ] **Step 6: Manual acceptance checks**

Start the app as Administrador and verify:

1. `/Rol` seeds Administrador and Empleado only once; reopening does not duplicate them.
2. Create "Supervisor" with some permissions: appears in the table, success toast, audit entry `Alta Rol`.
3. Duplicate " supervisor " is rejected with `"Ya existe un rol registrado con esa denominación."`.
4. Create with no permissions / >50-char name shows inline errors and does not save.
5. Edit "Supervisor": values preload, save shows `"Rol actualizado exitosamente"`, audit entry `Modificacion Rol`.
6. Protected Administrador: name field readonly with badge; unchecking any `Roles.*` permission is blocked client- and server-side.
7. Personal: the role dropdown includes Supervisor. Assign it to an employee. `/Rol` shows Usuarios Asignados = 1 for Supervisor.
8. Delete Supervisor while assigned: block modal with the exact message. Unassign it, then delete: success toast, audit `Eliminacion Rol`.
9. Delete Administrador/Empleado: button disabled and direct POST blocked.
10. Search `administracion` finds `Administración`; status filter, sorting, and pagination behave; page beyond the last clamps to the last page.

- [ ] **Step 7: Commit**

```bash
git add Controllers/PersonalController.cs Services/PersonalService.cs Views/Personal/_PersonalTable.cshtml Views/Shared/_Layout.cshtml
git commit -m "feat: use registered roles in personal and add roles sidebar link"
```
