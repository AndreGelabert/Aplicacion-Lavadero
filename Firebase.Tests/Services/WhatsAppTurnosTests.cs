using System.Net;
using System.Text.Json;
using Firebase.Models;
using Firebase.Models.WhatsApp;
using Firebase.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Firebase.Tests.Services;

public class WhatsAppTurnosTests
{
    [Fact]
    public void UpcomingQuery_HasCompositeIndex()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "firestore.indexes.json")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "firestore.indexes.json")));
        Assert.Contains(json.RootElement.GetProperty("indexes").EnumerateArray(), index =>
            index.GetProperty("collectionGroup").GetString() == "turnos" &&
            index.GetProperty("queryScope").GetString() == "COLLECTION" &&
            index.GetProperty("fields").EnumerateArray().Select(f => (f.GetProperty("fieldPath").GetString(), f.GetProperty("order").GetString()))
                .SequenceEqual(new[] { ("ClienteId", "ASCENDING"), ("Estado", "ASCENDING"), ("InicioUtc", "ASCENDING") }));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Messages { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Messages.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static WhatsAppFlowService Flow(WhatsAppSession session, Handler handler, Func<string, DateTime, Task<List<Turno>>> query)
    {
        var meta = new MetaWhatsAppService(new HttpClient(handler), Options.Create(new WhatsAppSettings { AccessToken = "test", VerifyToken = "test", PhoneNumberId = "123" }), NullLogger<MetaWhatsAppService>.Instance);
        var signature = new[] { typeof(MetaWhatsAppService), typeof(Func<string, Task<WhatsAppSession>>), typeof(Func<string, DateTime, Task<List<Turno>>>) };
        var ctor = typeof(WhatsAppFlowService).GetConstructor(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, signature, null);
        Assert.NotNull(ctor);
        return (WhatsAppFlowService)ctor.Invoke(new object[] { meta, (Func<string, Task<WhatsAppSession>>)(_ => Task.FromResult(session)), query });
    }

    [Fact]
    public async Task AuthenticatedLookup_UsesSessionOwner_AndDisplaysBuenosAiresTime()
    {
        var handler = new Handler();
        string? owner = null;
        var flow = Flow(new WhatsAppSession { Id = "session", CurrentState = WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, ClienteId = "owner-a" }, handler,
            (id, now) => { owner = id; return Task.FromResult(new List<Turno> { new() { InicioUtc = new DateTime(2099, 1, 2, 2, 30, 0, DateTimeKind.Utc) } }); });
        await flow.ProcessMessage("5491111111111", "mis_turnos");
        Assert.Equal("owner-a", owner);
        var body = JsonDocument.Parse(Assert.Single(handler.Messages)).RootElement.GetProperty("text").GetProperty("body").GetString();
        Assert.Contains("01/01/2099 23:30", body);
    }

    [Fact]
    public async Task MissingOwner_FailsClosedWithoutQuery()
    {
        var handler = new Handler();
        var calls = 0;
        var flow = Flow(new WhatsAppSession { Id = "session", CurrentState = WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO }, handler,
            (id, now) => { calls++; return Task.FromResult(new List<Turno>()); });
        await flow.ProcessMessage("5491111111111", "mis_turnos");
        Assert.Equal(0, calls);
        Assert.Contains("No se pudo identificar", JsonDocument.Parse(Assert.Single(handler.Messages)).RootElement.GetProperty("text").GetProperty("body").GetString());
    }

    [Fact]
    public async Task UnknownOption_RemainsUnrecognizedAndCannotSelectOwner()
    {
        var handler = new Handler();
        var calls = 0;
        var flow = Flow(new WhatsAppSession { Id = "session", CurrentState = WhatsAppFlowStates.MENU_CLIENTE_AUTENTICADO, ClienteId = "owner-a" }, handler,
            (id, now) => { calls++; return Task.FromResult(new List<Turno>()); });
        await flow.ProcessMessage("5491111111111", "owner-b");
        Assert.Equal(0, calls);
        Assert.Contains("Opción no reconocida", JsonDocument.Parse(Assert.Single(handler.Messages)).RootElement.GetProperty("text").GetProperty("body").GetString());
    }
}
