using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Firebase.Models;
using Firebase.Services;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Services;

public class TurnoEmailOriginWiringTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly FirestoreDb Db = (FirestoreDb)RuntimeHelpers.GetUninitializedObject(typeof(FirestoreDb));
    private static readonly MetaWhatsAppService WhatsApp = (MetaWhatsAppService)RuntimeHelpers.GetUninitializedObject(typeof(MetaWhatsAppService));
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    [Theory]
    [InlineData("https://trusted.example:8443", true)]
    [InlineData(null, false)]
    [InlineData("http://trusted.example", false)]
    [InlineData("https://trusted.example/path", false)]
    [InlineData("https://user:password@trusted.example", false)]
    public void ProductionConstructorsPassOriginToOfferResolver(string? origin, bool valid)
    {
        var sender = new Sender();
        var dispatcher = new TurnoEmailOutboxDispatcher(Db, sender, Lease, origin);
        var router = new TurnoOfferOutboxRouter(Db, WhatsApp, new TurnoOfertaNotificationResolver(Db, () => DateTime.UtcNow), sender, Lease, origin);
        foreach (var resolver in new[] { FindResolver(dispatcher), FindResolver(router) })
        {
            Assert.Equal(valid, resolver.HasOrigin);
            if (valid)
            {
                var message = resolver.AddLink(new EmailMessage("a@example.com", "subject", "body"), "cascade", "offer", "client", "secret");
                Assert.Contains("https://trusted.example:8443/turno-oferta-email/responder#", WebUtility.HtmlDecode(message.Html));
            }
        }
        Assert.Equal(0, sender.Count);
    }

    [Fact]
    public void ExistingConstructorsRemainAvailableAndFailClosed()
    {
        var sender = new Sender();
        var dispatcher = new TurnoEmailOutboxDispatcher(Db, sender, Lease);
        var router = new TurnoOfferOutboxRouter(Db, WhatsApp, new TurnoOfertaNotificationResolver(Db, () => DateTime.UtcNow), sender, Lease);
        Assert.False(FindResolver(dispatcher).HasOrigin);
        Assert.False(FindResolver(router).HasOrigin);
        Assert.Equal(0, sender.Count);
    }

    private static TurnoOfertaEmail FindResolver(object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var item))
        {
            if (!seen.Add(item)) continue;
            if (item is TurnoOfertaEmail resolver) return resolver;
            if (item is Delegate del) { if (del.Target != null) queue.Enqueue(del.Target); continue; }
            var type = item.GetType();
            if (type != typeof(TurnoEmailOutboxDispatcher) && type != typeof(TurnoOfferOutboxRouter) && !type.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;
            foreach (var field in type.GetFields(Fields))
                if (field.GetValue(item) is object value) queue.Enqueue(value);
        }
        throw new InvalidOperationException("Offer resolver not found in constructor wiring.");
    }

    private sealed class Sender : IEmailSender
    {
        internal int Count;
        public Task<ResultadoEnvioEmail> EnviarAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.FromResult(new ResultadoEnvioEmail(true, "accepted"));
        }
    }
}
