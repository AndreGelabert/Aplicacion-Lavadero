using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Firebase.Services;

internal sealed class TurnoEditFormIdentity(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Turnos.Web.EditIdentity.v1");
    private sealed record Payload(string UserId, string Target, string Key);
    private static string? UserId(ClaimsPrincipal user)
    {
        var ids = user.Identities.Where(i => i.IsAuthenticated).SelectMany(i => i.FindAll(ClaimTypes.NameIdentifier)).Select(c => c.Value).ToArray();
        return ids.Length == 1 && !string.IsNullOrWhiteSpace(ids[0]) ? ids[0] : null;
    }
    internal string Issue(ClaimsPrincipal user, string target) => protector.Protect(JsonSerializer.Serialize(
        new Payload(UserId(user) ?? throw new InvalidOperationException(), target, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))));
    internal bool TryRead(ClaimsPrincipal user, string target, string? token, out string key)
    {
        key = "";
        if (UserId(user) is not string uid || string.IsNullOrEmpty(token) || token.Length > 4096) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(protector.Unprotect(token));
            if (payload == null || payload.UserId != uid || payload.Target != target || payload.Key == null || payload.Key.Length != 64 || payload.Key.Any(c => !char.IsAsciiHexDigitUpper(c) && !char.IsAsciiDigit(c))) return false;
            key = payload.Key; return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException) { return false; }
    }
}
