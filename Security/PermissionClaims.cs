using System.Security.Claims;

namespace Firebase.Security;

public static class PermissionClaims
{
    public static bool Refresh(ClaimsIdentity identity, IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(permissions);

        var normalized = RolCatalogo.NormalizarPermisos(permissions);
        var current = RolCatalogo.NormalizarPermisos(
            identity.FindAll(PermisoClaim.Tipo).Select(claim => claim.Value));
        if (current.SequenceEqual(normalized, StringComparer.Ordinal))
            return false;

        foreach (var claim in identity.FindAll(PermisoClaim.Tipo).ToArray())
            identity.RemoveClaim(claim);
        foreach (var permission in normalized)
            identity.AddClaim(new Claim(PermisoClaim.Tipo, permission));
        return true;
    }
}
