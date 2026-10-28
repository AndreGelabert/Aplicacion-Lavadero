using System.Globalization;
using System.Text;
using Firebase.Models;

/// <summary>
/// Reglas de negocio para la creación, edición y eliminación de roles.
/// </summary>
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

    /// <summary>
    /// Mensaje de bloqueo cuando el rol tiene empleados asignados.
    /// </summary>
    public static string MensajeUsuariosAsignados(int cantidad) =>
        $"No es posible desactivar el rol: Existen {cantidad} empleados que tienen este rol asignado. Reasígnelos a otro rol antes de continuar";

    /// <summary>
    /// Valida el nombre de un rol. Devuelve null cuando es válido.
    /// </summary>
    public static string? ValidarNombre(string? nombre)
    {
        var normalizado = NormalizarNombre(nombre);

        if (normalizado.Length == 0)
        {
            return MensajeNombreObligatorio;
        }

        if (normalizado.Length > 50)
        {
            return MensajeNombreLargo;
        }

        return null;
    }

    /// <summary>
    /// Valida la descripción de un rol. Devuelve null cuando es válida.
    /// </summary>
    public static string? ValidarDescripcion(string? descripcion)
    {
        var normalizada = (descripcion ?? string.Empty).Trim();
        return normalizada.Length > 250 ? MensajeDescripcionLarga : null;
    }

    /// <summary>
    /// Valida que exista al menos un permiso y que todos pertenezcan al catálogo.
    /// </summary>
    public static string? ValidarPermisos(IEnumerable<string>? permisos)
    {
        var lista = (permisos ?? Array.Empty<string>()).ToList();

        if (lista.Count == 0)
        {
            return MensajePermisoRequerido;
        }

        if (lista.Any(p => !RolCatalogo.EsPermisoValido(p)))
        {
            return MensajePermisoInvalido;
        }

        return null;
    }

    /// <summary>
    /// Valida una edición, incluyendo la protección del nombre y de los permisos críticos
    /// del rol Administrador. Devuelve null cuando la edición es válida.
    /// </summary>
    public static string? ValidarEdicion(Rol rolActual, string? nombreNuevo, IEnumerable<string>? permisosNuevos)
    {
        var errorPermisos = ValidarPermisos(permisosNuevos);
        if (errorPermisos != null)
        {
            return errorPermisos;
        }

        if (RolCatalogo.EsRolProtegido(rolActual.Nombre)
            && !SonNombresIguales(rolActual.Nombre, nombreNuevo))
        {
            return MensajeRolProtegidoNoEditable;
        }

        var esAdministrador = string.Equals(
            NormalizarNombre(rolActual.Nombre),
            "Administrador",
            StringComparison.OrdinalIgnoreCase);

        if (esAdministrador)
        {
            var normalizados = RolCatalogo.NormalizarPermisos(permisosNuevos);
            if (RolCatalogo.PermisosCriticosAdministrador.Any(critico => !normalizados.Contains(critico)))
            {
                return MensajePermisosCriticos;
            }
        }

        return null;
    }

    /// <summary>
    /// Valida si un rol puede eliminarse. Devuelve null cuando la eliminación es válida.
    /// </summary>
    public static string? ValidarEliminacion(Rol rol, int usuariosAsignados)
    {
        if (RolCatalogo.EsRolProtegido(rol.Nombre))
        {
            return MensajeRolProtegidoNoEliminable;
        }

        return usuariosAsignados > 0 ? MensajeUsuariosAsignados(usuariosAsignados) : null;
    }

    /// <summary>
    /// Normaliza un nombre de rol eliminando espacios al inicio y al final.
    /// </summary>
    public static string NormalizarNombre(string? nombre) => (nombre ?? string.Empty).Trim();

    /// <summary>
    /// Compara dos nombres de rol ignorando mayúsculas y espacios externos.
    /// </summary>
    public static bool SonNombresIguales(string? a, string? b) =>
        string.Equals(NormalizarNombre(a), NormalizarNombre(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normaliza texto para búsquedas: elimina acentos y convierte a minúsculas invariantes.
    /// </summary>
    public static string NormalizarTexto(string? texto)
    {
        var descompuesto = (texto ?? string.Empty).Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                constructor.Append(caracter);
            }
        }

        return constructor.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Indica si un texto contiene el término de búsqueda, ignorando mayúsculas y acentos.
    /// </summary>
    public static bool ContieneTexto(string? texto, string? termino)
    {
        var terminoNormalizado = NormalizarTexto(termino);

        if (terminoNormalizado.Length == 0)
        {
            return true;
        }

        return NormalizarTexto(texto).Contains(terminoNormalizado, StringComparison.Ordinal);
    }
}
