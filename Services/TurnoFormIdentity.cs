using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Firebase.Services;

// Tokens must be protected by a key ring shared and retained for the life of a form.
internal sealed class TurnoFormIdentity(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Turnos.Web.FormIdentity.v1");
    private sealed record Payload(string UserId, string Id);

    internal string Issue(ClaimsPrincipal user)
    {
        var userId = PrincipalId(user) ?? throw new InvalidOperationException("Usuario inválido.");
        return protector.Protect(JsonSerializer.Serialize(new Payload(userId, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))));
    }

    internal bool TryRead(ClaimsPrincipal user, string? token, out string id)
    {
        id = "";
        var userId = PrincipalId(user);
        if (userId == null || string.IsNullOrEmpty(token) || token.Length > 4096) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(protector.Unprotect(token));
            if (payload == null || payload.UserId != userId || payload.Id == null || payload.Id.Length != 64 ||
                payload.Id.Any(c => !char.IsAsciiHexDigitUpper(c) && !char.IsAsciiDigit(c))) return false;
            id = payload.Id;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return false;
        }
    }

    private static string? PrincipalId(ClaimsPrincipal? user)
    {
        var claims = user?.Identities.Where(i => i.IsAuthenticated)
            .SelectMany(i => i.FindAll(ClaimTypes.NameIdentifier)).Select(c => c.Value).ToArray();
        return claims is { Length: 1 } && !string.IsNullOrWhiteSpace(claims[0]) ? claims[0] : null;
    }
}
