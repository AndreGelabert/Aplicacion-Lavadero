using Firebase.Services;
using Firebase.Models;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public class TurnoOfertaPersistenceTests
{
    private static readonly DateTime T = new(2030, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private static (FirestoreDb, TurnoOfertaPersistence) Setup()
    {
        var db = new FirestoreDbBuilder { ProjectId = "demo-offer-" + Guid.NewGuid().ToString("N")[..16], EmulatorDetection = EmulatorDetection.EmulatorOnly }.Build();
        return (db, new(db));
    }
    private static Turno Booking(string id, DateTime start) => new() { Id = id, ClienteId = "client", Estado = "Pendiente", Origen = "test", ClaveIdempotencia = id, AceptaAdelanto = true, InicioUtc = start, FinUtc = start.AddMinutes(30), Vehiculos = [new() { VehiculoId = "vehicle", CantidadEmpleadosRequeridos = 1, Servicios = [new() { ServicioId = "service", TiempoEstimado = 30 }] }] };
    private static async Task Seed(FirestoreDb db)
    {
        await db.Collection("turnos").Document("appointment").SetAsync(Booking("appointment", T.AddDays(2)));
        await db.Collection("empleados").Document("one").SetAsync(new { Estado = "Activo" });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion { CapacidadMaximaConcurrente = 1, HorariosOperacion = new() { ["Miércoles"] = "00:00-23:59", ["Jueves"] = "00:00-23:59", ["Viernes"] = "00:00-23:59", ["Sábado"] = "00:00-23:59", ["Domingo"] = "00:00-23:59", ["Lunes"] = "00:00-23:59", ["Martes"] = "00:00-23:59" } });
    }
    private static TurnoOfertaPersistence.Request Offer(string id) => new("cascade", id, "appointment", T, T.AddMinutes(10), T.AddDays(2), T.AddDays(1));
    private static TurnoOfertaPersistence.Request RootOffer(string id, string cascade) => Offer(id) with
    { CascadeId = cascade, ProposedUtc = T, ExpectedVacancyVersion = 0, ExpectedVacancyStartUtc = T, ExpectedVacancyEndUtc = T.AddMinutes(30) }; 

    [LocalFirestoreFact]
    public async Task RootedAcceptanceAllowsIntervalLongerThanVacancyWhenCapacityAvailable()
    {
        var (db, offers) = Setup(); await Seed(db);
        var shortVacancy = Booking("cancelled", T);
        shortVacancy.FinUtc = T.AddMinutes(15);
        shortVacancy.Vehiculos[0].Servicios[0].TiempoEstimado = 15;
        await db.Collection("turnos").Document("cancelled").SetAsync(shortVacancy);
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var request = RootOffer("one", cascade.Id) with { CreatedUtc = T.AddMinutes(-30), ExpiresUtc = T.AddMinutes(-20), ExpectedVacancyEndUtc = T.AddMinutes(15) };
        await offers.Create(request);
        await offers.Accept(cascade.Id, "one", T.AddMinutes(-29));
        var moved = await db.Collection("turnos").Document("appointment").GetSnapshotAsync();
        Assert.Equal(T, moved.GetValue<DateTime>("InicioUtc"));
        Assert.Equal(T.AddMinutes(30), moved.GetValue<DateTime>("FinUtc"));
        Assert.False((await db.Collection("turno_ofertas_pendientes").Document("appointment").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task RootedCreationRejectsProposalOutsideVacancyWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var before = await cascade.Reference.GetSnapshotAsync();
        foreach (var proposed in new[] { T.AddMinutes(15), T.AddDays(1) })
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(RootOffer("one", cascade.Id) with { ProposedUtc = proposed }));
        Assert.Equal(before.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.False((await db.Collection("turno_ofertas").Document("one").GetSnapshotAsync()).Exists);
        Assert.False((await db.Collection("turno_ofertas_pendientes").Document("appointment").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task TerminalReplayRejectsInvalidPersistedAppointmentIdWithoutWrites()
    {
        foreach (var expire in new[] { false, true })
        {
            var (db, offers) = Setup(); await Seed(db); await offers.Create(Offer("one"));
            if (expire) await offers.Expire("cascade", "one", T.AddMinutes(10)); else await offers.Reject("cascade", "one", T.AddMinutes(1));
            var offer = db.Collection("turno_ofertas").Document("one");
            await offer.UpdateAsync("AppointmentId", "");
            var before = await offer.GetSnapshotAsync();
            Task Replay() => expire ? offers.Expire("cascade", "one", T.AddMinutes(10)) : offers.Reject("cascade", "one", T.AddMinutes(1));
            await Assert.ThrowsAsync<InvalidOperationException>(Replay);
            Assert.Equal(before.UpdateTime, (await offer.GetSnapshotAsync()).UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task ClaimIsReleasedOnEveryTerminalPathAndReplayPreservesNewOwner()
    {
        foreach (var operation in new[] { "accept", "reject", "expire" })
        {
            var (db, offers) = Setup(); await Seed(db);
            var first = Offer("one");
            await offers.Create(first);
            var claim = db.Collection("turno_ofertas_pendientes").Document("appointment");
            var initial = await claim.GetSnapshotAsync();
            Assert.Equal("one", initial.GetValue<string>("OfferId"));
            Assert.Equal("cascade", initial.GetValue<string>("CascadeId"));
            Task Finish() => operation switch { "accept" => offers.Accept("cascade", "one", T.AddMinutes(1)),
                "reject" => offers.Reject("cascade", "one", T.AddMinutes(1)), _ => offers.Expire("cascade", "one", T.AddMinutes(10)) };
            await Finish();
            Assert.False((await claim.GetSnapshotAsync()).Exists);
            var next = first with { CascadeId = "next", OfferId = "two", ProposedUtc = T.AddHours(12) };
            await offers.Create(next);
            var refs = new[] { claim, db.Collection("turno_ofertas").Document("one"), db.Collection("turno_ofertas").Document("two"),
                db.Collection("turno_cascadas").Document("cascade"), db.Collection("turno_cascadas").Document("next"),
                db.Collection("turno_aceptaciones").Document("one"), db.Collection("turnos").Document("appointment"),
                db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await Finish();
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
            Assert.Equal("two", after[0].GetValue<string>("OfferId"));
            Assert.Equal("next", after[0].GetValue<string>("CascadeId"));
        }
    }

    [LocalFirestoreFact]
    public async Task InvalidClaimsFailClosedWithoutWrites()
    {
        foreach (var operation in new[] { "create", "select", "accept", "reject", "expire" })
        foreach (var corruption in new[] { "malformed", "dangling", "foreign" })
        {
            var (db, offers) = Setup(); await Seed(db);
            var first = Offer("one"); await offers.Create(first);
            var claim = db.Collection("turno_ofertas_pendientes").Document("appointment");
            if (corruption == "malformed") await claim.SetAsync(new { OfferId = 42, CascadeId = "cascade" });
            if (corruption == "dangling") await claim.SetAsync(new { OfferId = "missing", CascadeId = "cascade" });
            if (corruption == "foreign") await claim.SetAsync(new { OfferId = "one", CascadeId = "foreign" });
            string? selection = null;
            if (operation == "select")
            {
                await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T.AddHours(12)));
                await new TurnoService(db).Cancelar("cancelled");
                selection = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents.Where(d => d.Id != "cascade")).Id;
            }
            var refs = new[] { claim, db.Collection("turno_ofertas").Document("one"), db.Collection("turno_ofertas").Document("two"),
                db.Collection("turno_cascadas").Document("cascade"), db.Collection("turnos").Document("appointment"),
                db.Collection("turno_aceptaciones").Document("one"), db.Collection("coordinacion").Document("turnos"),
                db.Collection("coordinacion").Document("empleados") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            var ledgers = await db.Collection("turno_ofertas_terminales").GetSnapshotAsync();
            Task Act() => operation switch { "create" => offers.Create(first with { CascadeId = "other", OfferId = "two" }),
                "select" => offers.SelectAndCreate(selection!, "two", T, T.AddMinutes(10)),
                "accept" => offers.Accept("cascade", "one", T.AddMinutes(1)),
                "reject" => offers.Reject("cascade", "one", T.AddMinutes(1)), _ => offers.Expire("cascade", "one", T.AddMinutes(10)) };
            await Assert.ThrowsAsync<InvalidOperationException>(Act);
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
            Assert.Equal(ledgers.Count, (await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Count);
        }
    }

    [LocalFirestoreFact]
    public async Task CancellationCreateFencesExpectedVacancyAndReplaysAfterAdvance()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        var request = Offer("one") with { CascadeId = cascade.Id, ProposedUtc = T, ExpectedVacancyVersion = 0, ExpectedVacancyStartUtc = T, ExpectedVacancyEndUtc = T.AddMinutes(30) };
        foreach (var stale in new[] { request with { ExpectedVacancyVersion = 1 }, request with { ExpectedVacancyStartUtc = T.AddMinutes(1) }, request with { ExpectedVacancyEndUtc = T.AddMinutes(31) } })
        {
            var before = await cascade.GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(stale));
            Assert.Equal(before.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_adelanto_anclas").GetSnapshotAsync()).Documents);
        }
        await offers.Create(request); await offers.Reject(cascade.Id, "one", T.AddMinutes(1));
        await offers.Create(request);
        await db.Collection("turnos").Document("second").SetAsync(Booking("second", T.AddDays(3)));
        var secondBooking = Booking("second", T.AddDays(3)); secondBooking.ClienteId = "second-client";
        await db.Collection("turnos").Document("second").SetAsync(secondBooking);
        var next = request with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3), CreatedUtc = T.AddMinutes(-30), ExpiresUtc = T.AddMinutes(-20) };
        await offers.Create(next); await offers.Accept(cascade.Id, "two", T.AddMinutes(-29));
        await offers.Create(request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(request with { OfferId = "three" }));
    }

    [LocalFirestoreFact]
    public async Task RejectedScheduleCannotBeOfferedAgainToSameCustomerInCancellationCascade()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("clientes").Document("client").SetAsync(new { Id = "client", Estado = "Activo", Email = "client@example.com" });
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        var request = RootOffer("one", cascade.Id);
        await offers.Create(request);
        await offers.Reject(cascade.Id, "one", T.AddMinutes(1));
        await db.Collection("turnos").Document("second").SetAsync(Booking("second", T.AddDays(3)));
        var before = await cascade.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(request with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3) }));
        Assert.Equal(before.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        Assert.False((await db.Collection("turno_ofertas").Document("two").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task ExpiredScheduleCannotRepeatButDifferentScheduleAndTerminalRetryWork()
    {
        var (db, offers) = Setup(); await Seed(db);
        var first = Offer("one");
        await offers.Create(first);
        await offers.Expire("cascade", "one", T.AddMinutes(10));
        var ledger = Assert.Single((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        var cascade = db.Collection("turno_cascadas").Document("cascade");
        var before = await cascade.GetSnapshotAsync();
        await offers.Expire("cascade", "one", T.AddMinutes(10));
        Assert.Equal(ledger.UpdateTime, (await ledger.Reference.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(first with { OfferId = "two" }));
        Assert.Equal(before.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        Assert.False((await db.Collection("turno_ofertas").Document("two").GetSnapshotAsync()).Exists);
        await offers.Create(first with { OfferId = "two", ProposedUtc = first.ProposedUtc.AddMinutes(15) });
    }

    [LocalFirestoreFact]
    public async Task ForgedSourceOfferWithoutAcceptanceReceiptCannotAuthorizeContinuation()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        await db.Collection("turno_ofertas").Document("forged").SetAsync(new Dictionary<string, object> {
            ["CascadeId"] = cascade.Id, ["Estado"] = "Aceptada", ["VacancyVersion"] = 1L,
            ["AppointmentId"] = "appointment", ["ProposedUtc"] = T, ["AnchorUtc"] = T.AddHours(1),
            ["OriginalUtc"] = T.AddHours(1), ["OriginalEndUtc"] = T.AddHours(1).AddMinutes(30) });
        await cascade.UpdateAsync(new Dictionary<string, object> { ["VacancySourceOfferId"] = "forged", ["VacancyVersion"] = 1L,
            ["VacancyStartUtc"] = T.AddHours(1), ["VacancyEndUtc"] = T.AddHours(1).AddMinutes(30) });
        var before = await cascade.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(Offer("one") with { CascadeId = cascade.Id }));
        Assert.Equal(before.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        Assert.False((await db.Collection("turno_ofertas").Document("one").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task CancellationContinuationRejectsMalformedOrMismatchedSourceReceiptWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        var first = RootOffer("one", cascade.Id) with { CreatedUtc = T.AddMinutes(-30), ExpiresUtc = T.AddMinutes(-20) };
        await offers.Create(first); await offers.Accept(cascade.Id, "one", T.AddMinutes(-29));
        await db.Collection("turnos").Document("second").SetAsync(Booking("second", T.AddDays(3)));
        var receipt = db.Collection("turno_aceptaciones").Document("one");
        var original = (await receipt.GetSnapshotAsync()).ToDictionary();
        foreach (var field in new[] { "CascadeId", "AppointmentId", "OriginalUtc", "OriginalEndUtc", "ProposedUtc", "AnchorUtc" })
        {
            var missing = new Dictionary<string, object>(original); missing.Remove(field);
            await receipt.SetAsync(missing);
            var refs = new[] { cascade, receipt, db.Collection("turno_ofertas").Document("one"), db.Collection("turnos").Document("second"), db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(first with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3), ProposedUtc = T.AddDays(2) }));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
            Assert.False((await db.Collection("turno_ofertas").Document("two").GetSnapshotAsync()).Exists);
            Assert.False((await db.Collection("turno_adelanto_anclas").Document("second").GetSnapshotAsync()).Exists);
        }
        var mismatch = new Dictionary<string, object>(original) { ["AppointmentId"] = "other" };
        await receipt.SetAsync(mismatch);
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(first with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3), ProposedUtc = T.AddDays(2) }));
        await receipt.SetAsync(original);
        await offers.Create(first with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3), ProposedUtc = T.AddDays(2), ExpectedVacancyVersion = 1, ExpectedVacancyStartUtc = T.AddDays(2), ExpectedVacancyEndUtc = T.AddDays(2).AddMinutes(30) });
        Assert.True((await db.Collection("turno_ofertas").Document("two").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task AcceptedCancellationContinuationRecordsPriorIntervalAndAllowsNextOffer()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        var request = RootOffer("one", cascade.Id) with { CreatedUtc = T.AddMinutes(-30), ExpiresUtc = T.AddMinutes(-20) };
        await offers.Create(request);
        await offers.Accept(cascade.Id, "one", T.AddMinutes(-29));
        var after = await cascade.GetSnapshotAsync();
        Assert.Equal(T.AddDays(2), after.GetValue<DateTime>("VacancyStartUtc"));
        Assert.Equal(T.AddDays(2).AddMinutes(30), after.GetValue<DateTime>("VacancyEndUtc"));
        Assert.Equal("cancelled", after.GetValue<string>("CancelledAppointmentId"));
        await db.Collection("turnos").Document("second").SetAsync(Booking("second", T.AddDays(3)));
        await offers.Create(request with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3), ProposedUtc = T.AddDays(2), ExpectedVacancyVersion = 1, ExpectedVacancyStartUtc = T.AddDays(2), ExpectedVacancyEndUtc = T.AddDays(2).AddMinutes(30) });
    }

    [LocalFirestoreFact]
    public async Task AcceptedReplayAfterLinkedContinuationDoesNotWrite()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        var first = RootOffer("one", cascade.Id) with { CreatedUtc = T.AddMinutes(-30), ExpiresUtc = T.AddMinutes(-20) };
        await offers.Create(first); await offers.Accept(cascade.Id, "one", T.AddMinutes(-29));
        await db.Collection("turnos").Document("second").SetAsync(Booking("second", T.AddDays(3)));
        var second = first with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3), ProposedUtc = T.AddDays(2),
            ExpectedVacancyVersion = 1, ExpectedVacancyStartUtc = T.AddDays(2), ExpectedVacancyEndUtc = T.AddDays(2).AddMinutes(30) };
        await offers.Create(second); await offers.Accept(cascade.Id, "two", T.AddMinutes(-28));
        var refs = new[] { cascade, db.Collection("turnos").Document("cancelled"), db.Collection("turnos").Document("appointment"),
            db.Collection("turnos").Document("second"), db.Collection("turno_ofertas").Document("one"), db.Collection("turno_ofertas").Document("two"),
            db.Collection("turno_aceptaciones").Document("one"), db.Collection("turno_aceptaciones").Document("two"),
            db.Collection("turno_adelanto_anclas").Document("appointment"), db.Collection("turno_adelanto_anclas").Document("second"),
            db.Collection("turno_ofertas_pendientes").Document("appointment"), db.Collection("turno_ofertas_pendientes").Document("second"),
            db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        Assert.Equal(2L, before[0].GetValue<long>("VacancyVersion"));
        Assert.Equal("two", before[0].GetValue<string>("VacancySourceOfferId"));
        Assert.Equal(T.AddDays(3), before[0].GetValue<DateTime>("VacancyStartUtc"));
        Assert.Equal(T.AddDays(3).AddMinutes(30), before[0].GetValue<DateTime>("VacancyEndUtc"));
        Assert.Equal(T, before[2].GetValue<DateTime>("InicioUtc"));
        Assert.Equal(T.AddDays(2), before[3].GetValue<DateTime>("InicioUtc"));
        await offers.Accept(cascade.Id, "one", T.AddMinutes(-27));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++)
        {
            Assert.Equal(before[i].Exists, after[i].Exists);
            Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
            if (before[i].Exists) Assert.Equal(before[i].ToDictionary(), after[i].ToDictionary());
        }
    }

    [LocalFirestoreFact]
    public async Task AcceptedReceiptMismatchAndMissingLegacyReceiptFailClosed()
    {
        var (db, offers) = Setup(); await Seed(db);
        await offers.Create(Offer("one")); await offers.Accept("cascade", "one", T.AddMinutes(1));
        var receipt = db.Collection("turno_aceptaciones").Document("one");
        var proof = await receipt.GetSnapshotAsync();
        Assert.True(proof.Exists);
        Assert.Equal("cascade", proof.GetValue<string>("CascadeId"));
        Assert.Equal("appointment", proof.GetValue<string>("AppointmentId"));
        Assert.Equal(T.AddDays(2), proof.GetValue<DateTime>("OriginalUtc"));
        Assert.Equal(T.AddDays(2).AddMinutes(30), proof.GetValue<DateTime>("OriginalEndUtc"));
        await receipt.UpdateAsync("AppointmentId", "other");
        var refs = new[] { receipt, db.Collection("turno_ofertas").Document("one"), db.Collection("turnos").Document("appointment"), db.Collection("turno_cascadas").Document("cascade") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Accept("cascade", "one", T.AddMinutes(2)));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
        await db.Collection("turno_ofertas").Document("legacy").SetAsync(new Dictionary<string, object> { ["CascadeId"] = "cascade", ["AppointmentId"] = "appointment", ["Estado"] = "Aceptada", ["AnchorUtc"] = T.AddDays(2), ["OriginalUtc"] = T.AddDays(2), ["ProposedUtc"] = T.AddDays(1), ["CreatedUtc"] = T, ["ExpiresUtc"] = T.AddMinutes(10) });
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Accept("cascade", "legacy", T.AddMinutes(2)));
        Assert.False((await db.Collection("turno_aceptaciones").Document("legacy").GetSnapshotAsync()).Exists);
    }

    // Direct fixture mutation models a later preference edit; it is not a product integration test.
    [LocalFirestoreFact]
    public async Task AcceptedReplaySurvivesLaterOptOutButPendingAcceptanceDoesNot()
    {
        foreach (var accepted in new[] { false, true })
        {
            var (db, offers) = Setup(); await Seed(db);
            await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
            await new TurnoService(db).Cancelar("cancelled");
            var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
            await offers.Create(RootOffer("one", cascade.Id) with { CreatedUtc = T.AddMinutes(-30), ExpiresUtc = T.AddMinutes(-20) });
            if (accepted) await offers.Accept(cascade.Id, "one", T.AddMinutes(-29));
            var appointment = db.Collection("turnos").Document("appointment");
            await appointment.UpdateAsync("AceptaAdelanto", false);
            var refs = new[] { cascade, appointment, db.Collection("turno_ofertas").Document("one"),
                db.Collection("turno_aceptaciones").Document("one"), db.Collection("turno_adelanto_anclas").Document("appointment"),
                db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            if (accepted) await offers.Accept(cascade.Id, "one", T.AddMinutes(2));
            else await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Accept(cascade.Id, "one", T.AddMinutes(2)));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
        }
    }

    [LocalFirestoreFact]
    public async Task AcceptedReplayRejectsChangedCustomerAndHistoricalRootedEndWithoutWrites()
    {
        foreach (var change in new[] { "customer", "originalEnd" })
        {
            var (db, offers) = Setup(); await Seed(db);
            await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
            await new TurnoService(db).Cancelar("cancelled");
            var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
            var first = RootOffer("one", cascade.Id) with { CreatedUtc = T.AddMinutes(-30), ExpiresUtc = T.AddMinutes(-20) };
            await offers.Create(first); await offers.Accept(cascade.Id, "one", T.AddMinutes(-29));
            await db.Collection("turnos").Document("second").SetAsync(Booking("second", T.AddDays(3)));
            var second = first with { OfferId = "two", AppointmentId = "second", AnchorUtc = T.AddDays(3), ProposedUtc = T.AddDays(2), ExpectedVacancyVersion = 1, ExpectedVacancyStartUtc = T.AddDays(2), ExpectedVacancyEndUtc = T.AddDays(2).AddMinutes(30) };
            await offers.Create(second); await offers.Accept(cascade.Id, "two", T.AddMinutes(-28));
            var appointment = db.Collection("turnos").Document("appointment");
            var offer = db.Collection("turno_ofertas").Document("one");
            // Fixture-only corruption, not a claim about privileged writers or product edits.
            if (change == "customer") await appointment.UpdateAsync("ClienteId", "other");
            else await offer.UpdateAsync("OriginalEndUtc", T.AddDays(2).AddMinutes(45));
            var refs = new[] { cascade, appointment, offer, db.Collection("turno_aceptaciones").Document("one"),
                db.Collection("turno_ofertas").Document("two"), db.Collection("turnos").Document("second"),
                db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Accept(cascade.Id, "one", T.AddMinutes(3)));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
        }
    }

    // State-only later advance, not proof of a linked cancellation cascade.
    [LocalFirestoreFact]
    public async Task AcceptedReplayAfterLaterAdvanceDoesNotWrite()
    {
        var (db, offers) = Setup(); await Seed(db);
        var first = Offer("one");
        await offers.Create(first); await offers.Accept("cascade", "one", T.AddMinutes(1));
        var second = first with { CascadeId = "next", OfferId = "two", ProposedUtc = T.AddHours(12) };
        await offers.Create(second); await offers.Accept("next", "two", T.AddMinutes(2));
        var refs = new[] { db.Collection("turnos").Document("appointment"), db.Collection("turno_ofertas").Document("one"), db.Collection("turno_ofertas").Document("two"), db.Collection("turno_cascadas").Document("cascade"), db.Collection("turno_cascadas").Document("next"), db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        await offers.Accept("cascade", "one", T.AddMinutes(3));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
        Assert.Equal(second.ProposedUtc, after[0].GetValue<DateTime>("InicioUtc"));
    }

    [LocalFirestoreFact]
    public async Task RejectAndExpireReplayAfterNewOfferPreservesContinuation()
    {
        foreach (var expire in new[] { false, true })
        {
            var (db, offers) = Setup(); await Seed(db);
            await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
            await new TurnoService(db).Cancelar("cancelled");
            var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
            var first = RootOffer("one", cascade.Id);
            await offers.Create(first);
            if (expire) await offers.Expire(cascade.Id, "one", T.AddMinutes(10)); else await offers.Reject(cascade.Id, "one", T.AddMinutes(1));
            var secondStart = T.AddDays(3);
            await db.Collection("turnos").Document("second").SetAsync(Booking("second", secondStart));
            var secondBooking = Booking("second", secondStart); secondBooking.ClienteId = "second-client";
            await db.Collection("turnos").Document("second").SetAsync(secondBooking);
            await offers.Create(first with { OfferId = "two", AppointmentId = "second", AnchorUtc = secondStart });
            var refs = new[] { cascade, db.Collection("turno_ofertas").Document("one"), db.Collection("turno_ofertas").Document("two"), db.Collection("turnos").Document("appointment"), db.Collection("coordinacion").Document("turnos") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            if (expire) await offers.Expire(cascade.Id, "one", T.AddMinutes(10)); else await offers.Reject(cascade.Id, "one", T.AddMinutes(1));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
            Assert.Equal(T, after[0].GetValue<DateTime>("VacancyStartUtc"));
            Assert.Equal(T.AddMinutes(30), after[0].GetValue<DateTime>("VacancyEndUtc"));
            Assert.Equal("two", after[0].GetValue<string>("PendingId"));
        }
    }

    [LocalFirestoreFact]
    public async Task OfferCreationPreservesCancellationContinuationAndReplay()
    {
        var (db, s) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        await cascade.UpdateAsync("Extra", "keep");
        var request = RootOffer("one", cascade.Id);
        await s.Create(request);
        var after = await cascade.GetSnapshotAsync();
        Assert.Equal("cancelled", after.GetValue<string>("CancelledAppointmentId"));
        Assert.Equal(T, after.GetValue<DateTime>("VacancyStartUtc"));
        Assert.Equal(T.AddMinutes(30), after.GetValue<DateTime>("VacancyEndUtc"));
        Assert.Equal("Pendiente", after.GetValue<string>("ContinuationState"));
        Assert.Equal("keep", after.GetValue<string>("Extra"));
        Assert.Equal("one", after.GetValue<string>("PendingId"));
        await s.Create(request);
        Assert.Equal(after.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Create(request with { OfferId = "two" }));
        Assert.Equal(after.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CancellationCascadeSurvivesOfferCreationAndReplay()
    {
        var (db, offers) = Setup(); await Seed(db);
        var original = Booking("cancelled", T);
        var appointment = db.Collection("turnos").Document(original.Id);
        await appointment.SetAsync(original);
        var persisted = await appointment.GetSnapshotAsync();
        var cancellation = new TurnoService(db);
        await cancellation.Cancelar(original.Id);
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        var request = RootOffer("one", cascade.Id);
        await offers.Create(request);
        var after = await cascade.Reference.GetSnapshotAsync();
        Assert.Equal(persisted.GetValue<DateTime>("InicioUtc"), after.GetValue<DateTime>("VacancyStartUtc"));
        Assert.Equal(persisted.GetValue<DateTime>("FinUtc"), after.GetValue<DateTime>("VacancyEndUtc"));
        Assert.Equal(original.Id, after.GetValue<string>("CancelledAppointmentId"));
        Assert.Equal("Pendiente", after.GetValue<string>("ContinuationState"));
        Assert.Equal("one", after.GetValue<string>("PendingId"));
        await offers.Create(request);
        await cancellation.Cancelar(original.Id);
        Assert.Equal(after.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task InvalidContinuationStatesFailClosedAcrossOperations()
    {
        foreach (var operation in new[] { "create", "accept", "reject", "expire" })
        foreach (object state in new object[] { "Finalizada", "pendiente", "", 42L })
        {
            var (db, offers) = Setup(); await Seed(db);
            await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
            await new TurnoService(db).Cancelar("cancelled");
            var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
            var request = RootOffer("one", cascade.Id);
            if (operation != "create") await offers.Create(request);
            await cascade.UpdateAsync("ContinuationState", state);
            var refs = new[] { cascade, db.Collection("turnos").Document("cancelled"), db.Collection("turnos").Document("appointment"),
                db.Collection("turno_ofertas").Document("one"), db.Collection("turno_adelanto_anclas").Document("appointment"),
                db.Collection("turno_aceptaciones").Document("one"), db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            Task Act() => operation switch { "create" => offers.Create(request), "accept" => offers.Accept(cascade.Id, "one", T.AddMinutes(1)),
                "reject" => offers.Reject(cascade.Id, "one", T.AddMinutes(1)), _ => offers.Expire(cascade.Id, "one", T.AddMinutes(10)) };
            var error = await Assert.ThrowsAsync<InvalidOperationException>(Act);
            Assert.Equal("Cascada incompatible.", error.Message);
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++)
            {
                Assert.Equal(before[i].Exists, after[i].Exists);
                Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
                if (before[i].Exists) Assert.Equal(before[i].ToDictionary(), after[i].ToDictionary());
            }
            Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task RootlessProvenanceFailsClosedWithoutWrites()
    {
        foreach (var mask in Enumerable.Range(1, 3))
        foreach (var operation in new[] { "create", "accept", "reject", "expire" })
        {
            var (db, offers) = Setup(); await Seed(db);
            if (operation != "create") await offers.Create(Offer("existing"));
            var cascade = db.Collection("turno_cascadas").Document("cascade");
            if (operation == "create") await cascade.SetAsync(new { PendingId = "" });
            var fields = new Dictionary<string, object>();
            if ((mask & 1) != 0) fields["VacancySourceOfferId"] = "source";
            if ((mask & 2) != 0) fields["VacancyVersion"] = 1L;
            await cascade.UpdateAsync(fields);
            var refs = new[] { cascade, db.Collection("turno_ofertas").Document("existing"), db.Collection("turno_ofertas").Document("new"),
                db.Collection("turno_adelanto_anclas").Document("appointment"), db.Collection("turnos").Document("appointment"),
                db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados"),
                db.Collection("turno_aceptaciones").Document("existing") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            Task Act() => operation switch { "create" => offers.Create(Offer("new")), "accept" => offers.Accept("cascade", "existing", T.AddMinutes(1)),
                "reject" => offers.Reject("cascade", "existing", T.AddMinutes(1)), _ => offers.Expire("cascade", "existing", T.AddMinutes(10)) };
            await Assert.ThrowsAsync<InvalidOperationException>(Act);
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); if (before[i].Exists) Assert.Equal(before[i].ToDictionary(), after[i].ToDictionary()); }
            Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task InvalidContinuationFailsClosedButLegacyCascadeIsPreserved()
    {
        var (db, offers) = Setup(); await Seed(db);
        var cascade = db.Collection("turno_cascadas").Document("cascade");
        var cases = new[] {
            new Dictionary<string, object> { ["PendingId"] = "", ["CancelledAppointmentId"] = "cancelled" },
            new Dictionary<string, object> { ["PendingId"] = "", ["CancelledAppointmentId"] = "cancelled", ["VacancyStartUtc"] = T, ["VacancyEndUtc"] = T, ["ContinuationState"] = "Pendiente" },
            new Dictionary<string, object> { ["PendingId"] = "", ["CancelledAppointmentId"] = "cancelled", ["VacancyStartUtc"] = T, ["VacancyEndUtc"] = T.AddMinutes(30), ["ContinuationState"] = "" }
        };
        foreach (var fields in cases)
        {
            await cascade.SetAsync(fields);
            var before = await cascade.GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(Offer("one")));
            Assert.Equal(before.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_adelanto_anclas").GetSnapshotAsync()).Documents);
        }
        await cascade.SetAsync(new { PendingId = "", Extra = "legacy" });
        await offers.Create(Offer("one"));
        var legacy = await cascade.GetSnapshotAsync();
        Assert.Equal("legacy", legacy.GetValue<string>("Extra"));
        Assert.False(legacy.ContainsField("VacancyStartUtc"));
        Assert.False(legacy.ContainsField("VacancyEndUtc"));
    }

    [LocalFirestoreFact]
    public async Task ConflictingCancellationIdentityAndIntervalRejectWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        var original = Booking("cancelled", T);
        await db.Collection("turnos").Document(original.Id).SetAsync(original);
        await new TurnoService(db).Cancelar(original.Id);
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        var request = RootOffer("one", cascade.Id);
        var other = db.Collection("turnos").Document("other");
        var otherBooking = Booking("other", T); otherBooking.Estado = "Cancelado";
        await other.SetAsync(otherBooking);
        foreach (var change in new[] { new Dictionary<string, object> { ["CancelledAppointmentId"] = "other" }, new Dictionary<string, object> { ["CancelledAppointmentId"] = original.Id, ["VacancyStartUtc"] = T.AddMinutes(1) }, new Dictionary<string, object> { ["VacancyStartUtc"] = T, ["VacancyEndUtc"] = T.AddMinutes(31) } })
        {
            await cascade.UpdateAsync(change);
            var before = await cascade.GetSnapshotAsync();
            var appointment = await db.Collection("turnos").Document(original.Id).GetSnapshotAsync();
            var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(request));
            Assert.Equal(before.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
            Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
            Assert.Empty((await db.Collection("turno_adelanto_anclas").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task AcceptRejectsChangedCancellationVacancyWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        await db.Collection("turnos").Document("cancelled").SetAsync(Booking("cancelled", T));
        await new TurnoService(db).Cancelar("cancelled");
        var cascade = Assert.Single((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents).Reference;
        var request = RootOffer("one", cascade.Id);
        await offers.Create(request);
        await cascade.UpdateAsync("VacancyEndUtc", T.AddMinutes(31));
        var refs = new[] { cascade, db.Collection("turno_ofertas").Document("one"), db.Collection("turnos").Document("appointment"), db.Collection("coordinacion").Document("turnos") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Accept(cascade.Id, "one", T.AddMinutes(1)));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task OfferCreateReplayRejectsChangedPendingAppointmentWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        var request = Offer("one"); await offers.Create(request);
        var appointment = db.Collection("turnos").Document("appointment");
        await appointment.UpdateAsync(new Dictionary<string, object> { ["InicioUtc"] = T.AddDays(3), ["FinUtc"] = T.AddDays(3).AddMinutes(30) });
        var refs = new[] { appointment, db.Collection("turno_cascadas").Document("cascade"), db.Collection("turno_ofertas").Document("one") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(request));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task CreateReplayRejectsTamperedCustomerAndUnprovedTerminalWithoutWrites()
    {
        foreach (var terminal in new[] { "Pendiente", "Rechazada", "Expirada" })
        {
            var (db, offers) = Setup(); await Seed(db);
            var request = Offer("one"); await offers.Create(request);
            var offer = db.Collection("turno_ofertas").Document("one");
            var cascade = db.Collection("turno_cascadas").Document("cascade");
            if (terminal == "Pendiente") await offer.UpdateAsync("ClienteId", "other");
            else { await offer.UpdateAsync("Estado", terminal); await cascade.UpdateAsync("PendingId", ""); }
            var refs = new[] { offer, cascade, db.Collection("turnos").Document("appointment"), db.Collection("turno_adelanto_anclas").Document("appointment"), db.Collection("coordinacion").Document("turnos") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(request));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
            Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task CreateTerminalReplayRequiresMatchingLedgerButSurvivesCustomerChange()
    {
        foreach (var expire in new[] { false, true })
        {
            var (db, offers) = Setup(); await Seed(db); var request = Offer("one"); await offers.Create(request);
            if (expire) await offers.Expire("cascade", "one", T.AddMinutes(10)); else await offers.Reject("cascade", "one", T.AddMinutes(1));
            var ledger = Assert.Single((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents).Reference;
            await db.Collection("turnos").Document("appointment").UpdateAsync("ClienteId", "other");
            var before = await ledger.GetSnapshotAsync(); await offers.Create(request);
            Assert.Equal(before.UpdateTime, (await ledger.GetSnapshotAsync()).UpdateTime);
            await ledger.UpdateAsync("OfferId", "different");
            var refs = new[] { ledger, db.Collection("turno_ofertas").Document("one"), db.Collection("turno_cascadas").Document("cascade"), db.Collection("turnos").Document("appointment") };
            var snapshots = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(request));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) Assert.Equal(snapshots[i].UpdateTime, after[i].UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task AcceptedReplayRejectsMalformedCurrentAppointmentWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        await offers.Create(Offer("one")); await offers.Accept("cascade", "one", T.AddMinutes(1));
        var appointment = db.Collection("turnos").Document("appointment");
        // Fixture corruption: no legitimate move can leave an end before its start.
        await appointment.UpdateAsync("FinUtc", T.AddDays(1).AddMinutes(-1));
        var refs = new[] { appointment, db.Collection("turno_ofertas").Document("one"),
            db.Collection("turno_cascadas").Document("cascade"), db.Collection("turno_aceptaciones").Document("one"),
            db.Collection("turno_adelanto_anclas").Document("appointment"), db.Collection("turno_ofertas_pendientes").Document("appointment"),
            db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Accept("cascade", "one", T.AddMinutes(2)));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
    }

    [LocalFirestoreFact]
    public async Task AcceptedReplayRejectsTamperedProposedEndWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        await offers.Create(Offer("one")); await offers.Accept("cascade", "one", T.AddMinutes(1));
        var offer = db.Collection("turno_ofertas").Document("one");
        await offer.UpdateAsync("ProposedEndUtc", T.AddDays(1).AddMinutes(45));
        var refs = new[] { offer, db.Collection("turnos").Document("appointment"), db.Collection("turno_cascadas").Document("cascade"),
            db.Collection("turno_aceptaciones").Document("one"), db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Accept("cascade", "one", T.AddMinutes(2)));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task TerminalReplayAfterBookingChangesUsesPersistedLedgerWithoutWrites()
    {
        foreach (var expire in new[] { false, true })
        {
            var (db, offers) = Setup(); await Seed(db); await offers.Create(Offer("one"));
            if (expire) await offers.Expire("cascade", "one", T.AddMinutes(10));
            else await offers.Reject("cascade", "one", T.AddMinutes(1));
            var appointment = db.Collection("turnos").Document("appointment");
            await appointment.UpdateAsync("ClienteId", "other");
            var ledger = Assert.Single((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
            var refs = new[] { appointment, ledger.Reference, db.Collection("turno_ofertas").Document("one"), db.Collection("turno_cascadas").Document("cascade") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            if (expire) await offers.Expire("cascade", "one", T.AddMinutes(10));
            else await offers.Reject("cascade", "one", T.AddMinutes(1));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) Assert.Equal(before[i].UpdateTime, after[i].UpdateTime);
        }
    }

    [LocalFirestoreFact]
    public async Task PendingCreateReplayRejectsChangedProposedEndWithoutWrites()
    {
        var (db, offers) = Setup(); await Seed(db);
        var request = Offer("one"); await offers.Create(request);
        var offer = db.Collection("turno_ofertas").Document("one");
        await offer.UpdateAsync("ProposedEndUtc", request.ProposedUtc.AddMinutes(45));
        var refs = new[] { offer, db.Collection("turnos").Document("appointment"), db.Collection("turno_cascadas").Document("cascade"),
            db.Collection("turno_adelanto_anclas").Document("appointment"), db.Collection("coordinacion").Document("turnos"),
            db.Collection("coordinacion").Document("empleados") };
        var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => offers.Create(request));
        var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
        for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
        Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task PendingTransitionsRejectTamperedOfferOrStaleBookingWithoutWrites()
    {
        foreach (var operation in new[] { "accept", "reject", "expire" })
        foreach (var change in new[] { "offerCustomer", "offerEnd", "bookingCustomer", "bookingDuration" })
        {
            var (db, offers) = Setup(); await Seed(db); await offers.Create(Offer("one"));
            var offer = db.Collection("turno_ofertas").Document("one");
            var appointment = db.Collection("turnos").Document("appointment");
            if (change == "offerCustomer") await offer.UpdateAsync("ClienteId", "other");
            if (change == "offerEnd") await offer.UpdateAsync("ProposedEndUtc", T.AddDays(1).AddMinutes(45));
            if (change == "bookingCustomer") await appointment.UpdateAsync("ClienteId", "other");
            if (change == "bookingDuration")
            {
                var booking = Booking("appointment", T.AddDays(2));
                booking.FinUtc = booking.InicioUtc.AddMinutes(45);
                booking.Vehiculos[0].Servicios[0].TiempoEstimado = 45;
                await appointment.SetAsync(booking);
            }
            var refs = new[] { offer, appointment, db.Collection("turno_cascadas").Document("cascade"),
                db.Collection("turno_adelanto_anclas").Document("appointment"), db.Collection("turno_aceptaciones").Document("one"),
                db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            Task Act() => operation switch { "accept" => offers.Accept("cascade", "one", T.AddMinutes(1)),
                "reject" => offers.Reject("cascade", "one", T.AddMinutes(1)), _ => offers.Expire("cascade", "one", T.AddMinutes(10)) };
            await Assert.ThrowsAsync<InvalidOperationException>(Act);
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
            Assert.Empty((await db.Collection("turno_ofertas_terminales").GetSnapshotAsync()).Documents);
        }
    }

    [LocalFirestoreFact]
    public async Task AcceptanceRejectsMissingOfferWithoutWrites()
    {
        var (db, s) = Setup();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Accept("cascade", "missing", T));
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turnos").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("coordinacion").GetSnapshotAsync()).Documents);
    }

    [LocalFirestoreFact]
    public async Task CascadeAllowsDifferentCandidatesAndKeepsPerAppointmentSevenDayAnchor()
    {
        var (db, s) = Setup(); await Seed(db);
        var first = Offer("one");
        await s.Create(first);
        await s.Reject("cascade", "one", T.AddMinutes(1));
        var secondStart = T.AddDays(3);
        await db.Collection("turnos").Document("second").SetAsync(Booking("second", secondStart));
        var second = first with { OfferId = "two", AppointmentId = "second", AnchorUtc = secondStart, ProposedUtc = T.AddDays(1).AddMinutes(30) };
        await s.Create(second);
        await s.Reject("cascade", "two", T.AddMinutes(1));
        // A fresh cascade must not reset the first appointment's original anchor.
        var invalid = first with { CascadeId = "next", OfferId = "three", AnchorUtc = T.AddDays(3), ProposedUtc = T.AddDays(1) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Create(invalid));
        Assert.False((await db.Collection("turno_ofertas").Document("three").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task MultiStepAdvanceKeepsOriginalSevenDayLimit()
    {
        var (db, s) = Setup(); await Seed(db);
        var original = T.AddDays(9);
        await db.Collection("turnos").Document("appointment").SetAsync(Booking("appointment", original));
        var first = Offer("one") with { AnchorUtc = original, ProposedUtc = T.AddDays(4) };
        await s.Create(first); await s.Accept("cascade", "one", T.AddMinutes(1));
        var boundary = first with { CascadeId = "next", OfferId = "two", ProposedUtc = T.AddDays(2) };
        await s.Create(boundary); await s.Accept("next", "two", T.AddMinutes(1));
        Assert.Equal(boundary.ProposedUtc, (await db.Collection("turnos").Document("appointment").GetSnapshotAsync()).GetValue<DateTime>("InicioUtc"));
        var beyond = boundary with { CascadeId = "third", OfferId = "three", ProposedUtc = boundary.ProposedUtc.AddTicks(-10) };
        await Assert.ThrowsAsync<ArgumentException>(() => s.Create(beyond));
        Assert.False((await db.Collection("turno_ofertas").Document("three").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task AcceptanceRejectsStaffGridAndOpeningChangesWithoutWrites()
    {
        var (db, s) = Setup(); await Seed(db);
        await s.Create(Offer("one"));
        var config = db.Collection("configuracion").Document("system_config");
        var employee = db.Collection("empleados").Document("one");
        var refs = new[] { db.Collection("turnos").Document("appointment"), db.Collection("turno_ofertas").Document("one"), db.Collection("turno_cascadas").Document("cascade"), db.Collection("turno_adelanto_anclas").Document("appointment"), db.Collection("coordinacion").Document("turnos"), db.Collection("coordinacion").Document("empleados") };
        async Task AssertUnchanged()
        {
            var before = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => s.Accept("cascade", "one", T.AddMinutes(1)));
            var after = await Task.WhenAll(refs.Select(r => r.GetSnapshotAsync()));
            for (var i = 0; i < refs.Length; i++) { Assert.Equal(before[i].Exists, after[i].Exists); Assert.Equal(before[i].UpdateTime, after[i].UpdateTime); }
        }
        await employee.UpdateAsync("Estado", "Inactivo"); await AssertUnchanged();
        await employee.UpdateAsync("Estado", "Activo");
        await config.UpdateAsync("IntervaloInicioTurnosMinutos", 50); await AssertUnchanged();
        await config.UpdateAsync("IntervaloInicioTurnosMinutos", 15);
        await config.UpdateAsync("HorariosOperacion", new Dictionary<string, string> { ["Miércoles"] = "CERRADO" }); await AssertUnchanged();
    }

    [LocalFirestoreFact]
    public async Task AcceptMovesAtomicallyAndReplayDoesNotWrite()
    {
        var (db, s) = Setup(); await Seed(db);
        var request = Offer("one") with { ProposedUtc = T.AddDays(2).AddMinutes(-15) };
        await s.Create(request);
        await s.Accept("cascade", "one", T.AddMinutes(1));
        var appointment = await db.Collection("turnos").Document("appointment").GetSnapshotAsync();
        Assert.Equal(request.ProposedUtc, appointment.GetValue<DateTime>("InicioUtc"));
        Assert.Equal(request.ProposedUtc.AddMinutes(30), appointment.GetValue<DateTime>("FinUtc"));
        Assert.Equal("Aceptada", (await db.Collection("turno_ofertas").Document("one").GetSnapshotAsync()).GetValue<string>("Estado"));
        Assert.Equal("", (await db.Collection("turno_cascadas").Document("cascade").GetSnapshotAsync()).GetValue<string>("PendingId"));
        var capacity = await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync();
        await s.Accept("cascade", "one", T.AddMinutes(2));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(capacity.UpdateTime, (await capacity.Reference.GetSnapshotAsync()).UpdateTime);
    }

    [LocalFirestoreFact]
    public async Task AcceptanceRejectsLostCapacityAndExpiryWithoutWrites()
    {
        var (db, s) = Setup(); await Seed(db);
        var request = Offer("one"); await s.Create(request);
        await db.Collection("turnos").Document("other").SetAsync(Booking("other", request.ProposedUtc));
        var appointment = await db.Collection("turnos").Document("appointment").GetSnapshotAsync();
        var offer = await db.Collection("turno_ofertas").Document("one").GetSnapshotAsync();
        var cascade = await db.Collection("turno_cascadas").Document("cascade").GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Accept("cascade", "one", T.AddMinutes(1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Accept("cascade", "one", T.AddMinutes(10)));
        Assert.Equal(appointment.UpdateTime, (await appointment.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(offer.UpdateTime, (await offer.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.Equal(cascade.UpdateTime, (await cascade.Reference.GetSnapshotAsync()).UpdateTime);
        Assert.False((await db.Collection("coordinacion").Document("turnos").GetSnapshotAsync()).Exists);
    }

    [LocalFirestoreFact]
    public async Task CreateRetryAndTerminalTransitionsAreAtomicAndIdempotent()
    {
        var (db, s) = Setup(); await Seed(db);
        await s.Create(Offer("one"));
        await s.Create(Offer("one"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Create(Offer("two")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Create(Offer("one") with { AppointmentId = "other" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Expire("cascade", "one", T.AddMinutes(9)));
        await s.Reject("cascade", "one", T.AddMinutes(9));
        await s.Reject("cascade", "one", T.AddMinutes(9));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Expire("cascade", "one", T.AddMinutes(10)));
        await s.Create(Offer("two") with { ProposedUtc = T.AddDays(1).AddMinutes(30) });
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reject("cascade", "two", T.AddMinutes(10)));
        await s.Expire("cascade", "two", T.AddMinutes(10));
        await s.Expire("cascade", "two", T.AddMinutes(10));
        var docs = await db.Collection("turno_ofertas").GetSnapshotAsync();
        Assert.Equal(2, docs.Count);
        Assert.Equal("Rechazada", docs.Documents.Single(x => x.Id == "one").GetValue<string>("Estado"));
        Assert.Equal("Expirada", docs.Documents.Single(x => x.Id == "two").GetValue<string>("Estado"));
    }

    [LocalFirestoreFact]
    public async Task PrecisionAndOrphanMarkersFailClosedWithoutWrites()
    {
        var (db, s) = Setup(); await Seed(db);
        await Assert.ThrowsAsync<ArgumentException>(() => s.Create(Offer("precision") with { CreatedUtc = T.AddTicks(1) }));
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        await s.Create(Offer("one"));
        var cascade = db.Collection("turno_cascadas").Document("cascade");
        await cascade.UpdateAsync("PendingId", "other");
        var before = await cascade.GetSnapshotAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Create(Offer("one")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Reject("cascade", "one", T));
        Assert.Equal(before.UpdateTime, (await cascade.GetSnapshotAsync()).UpdateTime);
        Assert.Equal("Pendiente", (await db.Collection("turno_ofertas").Document("one").GetSnapshotAsync()).GetValue<string>("Estado"));
    }

    [LocalFirestoreFact]
    public async Task RejectAndExpireRaceHasOneTerminalWinner()
    {
        var (db, s) = Setup(); await Seed(db);
        await s.Create(Offer("one"));
        async Task<bool> Try(Func<Task> f) { try { await f(); return true; } catch (InvalidOperationException) { return false; } }
        var results = await Task.WhenAll(Try(() => s.Reject("cascade", "one", T.AddMinutes(10).AddTicks(-10))), Try(() => s.Expire("cascade", "one", T.AddMinutes(10)))).WaitAsync(TimeSpan.FromSeconds(45));
        Assert.Single(results, x => x);
        var offer = await db.Collection("turno_ofertas").Document("one").GetSnapshotAsync();
        Assert.Contains(offer.GetValue<string>("Estado"), new[] { "Rechazada", "Expirada" });
        Assert.Equal("", (await db.Collection("turno_cascadas").Document("cascade").GetSnapshotAsync()).GetValue<string>("PendingId"));
    }

    [LocalFirestoreFact]
    public async Task ConcurrentOffersAllowOnlyOneAndInvalidRequestsLeaveNoWrites()
    {
        var (db, s) = Setup(); await Seed(db);
        foreach (var invalid in new[] { Offer("bad") with { CascadeId = "x/y" }, Offer("bad") with { CreatedUtc = DateTime.SpecifyKind(T, DateTimeKind.Unspecified) }, Offer("bad") with { ExpiresUtc = T }, Offer("bad") with { ProposedUtc = T.AddDays(-6) }, Offer("bad") with { ProposedUtc = T.AddDays(2) } })
            await Assert.ThrowsAsync<ArgumentException>(() => s.Create(invalid));
        Assert.Empty((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
        Assert.Empty((await db.Collection("turno_cascadas").GetSnapshotAsync()).Documents);
        async Task<bool> Attempt(string id) { try { await s.Create(Offer(id)); return true; } catch (InvalidOperationException) { return false; } }
        var results = await Task.WhenAll(Attempt("a"), Attempt("b"));
        Assert.Single(results.Where(x => x));
        Assert.Single((await db.Collection("turno_ofertas").GetSnapshotAsync()).Documents);
    }
}
