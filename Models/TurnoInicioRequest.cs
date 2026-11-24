using System.Text.Json.Serialization;

namespace Firebase.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class TurnoInicioRequest
{
    public string? TurnoId { get; set; }
    public Dictionary<int, string>? Seleccion { get; set; }

    public bool EsValido() => IdValido(TurnoId) && (Seleccion == null ||
        (Seleccion.Count <= 500 && Seleccion.All(x => x.Key >= 0 && IdValido(x.Value))));

    private static bool IdValido(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 1500 &&
        id == id.Trim() && !id.Contains('/') && id is not "." and not ".." &&
        System.Text.Encoding.UTF8.GetByteCount(id) <= 1500 && !id.Any(char.IsControl);
}
