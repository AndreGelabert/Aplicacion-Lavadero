using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Grpc.Core;

namespace Firebase.Security;

/// <summary>Creates synthetic records only when the local emulator documents do not already exist.</summary>
public static class LocalEmulatorSeed
{
    public const string UserId = "local-seed-admin";
    public const string Email = "admin@local.test";
    public const string CreateOnlyUserId = "local-seed-create-only";
    public const string CreateOnlyEmail = "create-only@local.test";
    public const string CreateOnlyRoleId = "local-seed-create-only-role";
    public const string CreateOnlyRoleName = "Pruebas solo creación";

    public static string RequireInitialPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= 6
            ? password
            : throw new InvalidOperationException("Set LOCAL_EMULATOR_PASSWORD to a local-only password of at least six characters before the first seed.");

    public static Dictionary<(string Collection, string Id), Dictionary<string, object>> Documents() => new()
    {
        [("roles", "local-seed-rol")] = new() { ["Nombre"] = "Pruebas locales", ["Descripcion"] = "Datos ficticios", ["Permisos"] = RolCatalogo.TodosLosPermisos.ToList(), ["Estado"] = "Activo", ["FechaCreacion"] = DateTime.UtcNow },
        [("empleados", UserId)] = new() { ["Uid"] = UserId, ["Nombre"] = "Admin Prueba", ["Email"] = Email, ["Rol"] = "Pruebas locales", ["Estado"] = "Activo", ["EmailVerificado"] = true },
        [("roles", CreateOnlyRoleId)] = new() { ["Nombre"] = CreateOnlyRoleName, ["Descripcion"] = "Usuario ficticio para pruebas de acceso solo creación", ["Permisos"] = new[] { "Clientes.Crear", "Vehiculos.Crear", "Servicios.Crear", "Servicios.Consultar", "Paquetes.Crear", "Lavados.Crear", "Roles.Crear" }, ["Estado"] = "Activo", ["FechaCreacion"] = DateTime.UtcNow },
        [("empleados", CreateOnlyUserId)] = new() { ["Uid"] = CreateOnlyUserId, ["Nombre"] = "Prueba Solo Creación", ["Email"] = CreateOnlyEmail, ["Rol"] = CreateOnlyRoleName, ["Estado"] = "Activo", ["EmailVerificado"] = true },
        [("tiposDocumento", "local-seed-dni")] = new() { ["Nombre"] = "DNI", ["Formato"] = "nnnnnnnn" },
        [("tiposVehiculos", "local-seed-auto")] = new() { ["Nombre"] = "Automóvil", ["CantidadEmpleadosRequeridos"] = 1, ["FormatoPatente"] = "llnnnll" },
        [("tiposServicio", "local-seed-lavado")] = new() { ["Nombre"] = "Lavado" },
        [("clientes", "local-seed-cliente")] = new() { ["Id"] = "local-seed-cliente", ["Nombre"] = "Juan", ["Apellido"] = "Prueba", ["TipoDocumento"] = "DNI", ["NumeroDocumento"] = "35123456", ["Telefono"] = "3751000000", ["Email"] = "cliente@local.test", ["VehiculosIds"] = new[] { "local-seed-vehiculo" }, ["Estado"] = "Activo" },
        [("vehiculos", "local-seed-vehiculo")] = new() { ["Id"] = "local-seed-vehiculo", ["Patente"] = "AB123CD", ["TipoVehiculo"] = "Automóvil", ["Marca"] = "Toyota", ["Modelo"] = "Corolla", ["Color"] = "Blanco", ["ClienteId"] = "local-seed-cliente", ["ClientesIds"] = new[] { "local-seed-cliente" }, ["ClienteNombreCompleto"] = "Juan Prueba", ["Estado"] = "Activo" },
        [("servicios", "local-seed-servicio")] = new() { ["Nombre"] = "Lavado Completo", ["Descripcion"] = "Lavado de prueba", ["Precio"] = 15000d, ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Automóvil", ["TiempoEstimado"] = 45, ["Estado"] = "Activo", ["Etapas"] = new[] { new Dictionary<string, object> { ["Id"] = "local-etapa", ["Nombre"] = "Lavado" } } },
        [("servicios", "local-seed-servicio-2")] = new() { ["Nombre"] = "Lavado Interior", ["Descripcion"] = "Limpieza de prueba", ["Precio"] = 8000d, ["Tipo"] = "Lavado", ["TipoVehiculo"] = "Automóvil", ["TiempoEstimado"] = 30, ["Estado"] = "Activo", ["Etapas"] = Array.Empty<object>() }
    };

    public static async Task RunAsync(FirestoreDb db, FirebaseApp app, bool development)
    {
        if (!LocalEmulatorSettings.Validate(development, Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"), Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST")) ||
            db.ProjectId != LocalEmulatorSettings.ProjectId || app.Options.ProjectId != LocalEmulatorSettings.ProjectId)
            throw new InvalidOperationException("Seed requires the local demo emulators.");

        var auth = FirebaseAuth.GetAuth(app);
        await EnsureUserAsync(auth, UserId, Email, "Admin Prueba");
        await EnsureUserAsync(auth, CreateOnlyUserId, CreateOnlyEmail, "Prueba Solo Creación");

        foreach (var (key, data) in Documents())
        {
            try { await db.Collection(key.Collection).Document(key.Id).CreateAsync(data); }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
            {
                Console.WriteLine($"Existing local document preserved: {key.Collection}/{key.Id}");
            }
        }
    }

    private static async Task EnsureUserAsync(FirebaseAuth auth, string uid, string email, string displayName)
    {
        try
        {
            var existing = await auth.GetUserAsync(uid);
            if (!string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The local seed user ID belongs to a different account.");
            Console.WriteLine($"Local demo user {email} already exists; credentials were not changed.");
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            var password = RequireInitialPassword(Environment.GetEnvironmentVariable("LOCAL_EMULATOR_PASSWORD"));
            await auth.CreateUserAsync(new UserRecordArgs { Uid = uid, Email = email, Password = password, EmailVerified = true, DisplayName = displayName });
            Console.WriteLine($"Local emulator user created: {email}. The password was not logged.");
        }
    }
}
