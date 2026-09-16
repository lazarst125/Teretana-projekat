using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.Api.Podaci;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.BazaPodataka;

public sealed class PocetniPodaciTestovi : KomponentniTest
{
    private static readonly DateTime Sada = TestniEntiteti.Sada;

    [Test]
    public async Task Seed_PunTermin_ImaPotvrdjenihRezervacijaKolikoJeKapacitet()
    {
        var termini = await UpisiIProcitajTermineAsync();

        var pun = termini.Single(t => t.Naziv == PocetniPodaci.Termini.Pun);
        Assert.Multiple(() =>
        {
            Assert.That(pun.BrojPotvrdjenih, Is.EqualTo(pun.Kapacitet));
            Assert.That(pun.Status, Is.EqualTo(StatusTermina.Aktivan));
            Assert.That(pun.Pocetak, Is.GreaterThan(Sada));
        });
    }

    [Test]
    public async Task Seed_TerminSaListomCekanja_JePunIClanoviCekajuPoRedosleduPrijave()
    {
        var termini = await UpisiIProcitajTermineAsync();

        var termin = termini.Single(t => t.Naziv == PocetniPodaci.Termini.SaListomCekanja);
        var redCekanja = termin.Rezervacije
            .Where(r => r.Status == StatusRezervacije.NaCekanju)
            .OrderBy(r => r.KreiranaAt).ThenBy(r => r.Id)
            .Select(r => r.Clan.Email);
        Assert.Multiple(() =>
        {
            Assert.That(termin.BrojPotvrdjenih, Is.EqualTo(termin.Kapacitet));
            Assert.That(redCekanja, Is.EqualTo(new[] { PocetniPodaci.Nalozi.Clan3, PocetniPodaci.Nalozi.Clan4 }));
        });
    }

    [Test]
    public async Task Seed_TerminKojiPocinjeUskoro_JosNijePoceoAliPocinjeZaManjeOdDvaSata()
    {
        var termini = await UpisiIProcitajTermineAsync();

        var termin = termini.Single(t => t.Naziv == PocetniPodaci.Termini.RokZaOtkazivanjeProsao);
        Assert.Multiple(() =>
        {
            Assert.That(termin.Pocetak, Is.GreaterThan(Sada));
            Assert.That(termin.Pocetak - Sada, Is.LessThan(TimeSpan.FromHours(2)));
            Assert.That(termin.Rezervacije, Has.Some.Matches<Rezervacija>(r => r.Status == StatusRezervacije.Potvrdjena));
        });
    }

    [Test]
    public async Task Seed_ZavrsenTermin_ImaEvidentiranoINeevidentiranoPrisustvo()
    {
        var termini = await UpisiIProcitajTermineAsync();

        var termin = termini.Single(t => t.Naziv == PocetniPodaci.Termini.Zavrsen);
        Assert.Multiple(() =>
        {
            Assert.That(termin.Kraj, Is.LessThan(Sada));
            Assert.That(termin.Rezervacije, Has.Some.Matches<Rezervacija>(r => r.Prisustvovao != null));
            Assert.That(termin.Rezervacije, Has.Some.Matches<Rezervacija>(r => r.Status == StatusRezervacije.Potvrdjena && r.Prisustvovao == null));
        });
    }

    [Test]
    public async Task Seed_OtkazanTermin_NemaAktivnihPrijava()
    {
        var termini = await UpisiIProcitajTermineAsync();

        var termin = termini.Single(t => t.Naziv == PocetniPodaci.Termini.Otkazan);
        Assert.Multiple(() =>
        {
            Assert.That(termin.Status, Is.EqualTo(StatusTermina.Otkazan));
            Assert.That(termin.BrojPotvrdjenih, Is.Zero);
            Assert.That(termin.Rezervacije, Is.Not.Empty.And.All.Matches<Rezervacija>(r => r.Status == StatusRezervacije.Otkazana));
        });
    }

    [Test]
    public async Task Seed_TerminBezPrijava_NemaRezervacija()
    {
        var termini = await UpisiIProcitajTermineAsync();

        var termin = termini.Single(t => t.Naziv == PocetniPodaci.Termini.BezPrijava);
        Assert.That(termin.Rezervacije, Is.Empty);
    }

    [Test]
    public async Task Seed_TerminSaSlobodnimMestima_PripadaDrugomTreneruIImaPrijave()
    {
        var termini = await UpisiIProcitajTermineAsync();

        var termin = termini.Single(t => t.Naziv == PocetniPodaci.Termini.SlobodnaMesta);
        Assert.Multiple(() =>
        {
            Assert.That(termin.Trener.Email, Is.EqualTo(PocetniPodaci.Nalozi.Trener2));
            Assert.That(termin.BrojPotvrdjenih, Is.GreaterThan(0).And.LessThan(termin.Kapacitet));
        });
    }

    [Test]
    public async Task Seed_ZaSvakiTermin_BrojPotvrdjenihOdgovaraPotvrdjenimRezervacijama()
    {
        var termini = await UpisiIProcitajTermineAsync();

        Assert.That(termini, Is.Not.Empty.And.All.Matches<Termin>(t =>
            t.BrojPotvrdjenih == t.Rezervacije.Count(r => r.Status == StatusRezervacije.Potvrdjena)));
    }

    [Test]
    public async Task UpisAkoJeBazaPrazna_PonovljenPoziv_NeDupliraPodatke()
    {
        await SaBazomAsync(db => PocetniPodaci.UpisiAkoJeBazaPraznaAsync(db, Vreme));

        await SaBazomAsync(db => PocetniPodaci.UpisiAkoJeBazaPraznaAsync(db, Vreme));

        var brojTermina = await SaBazomAsync(db => db.Termini.CountAsync());
        Assert.That(brojTermina, Is.EqualTo(7));
    }

    [Test]
    public async Task ResetBaze_PosleIzmenaPodataka_VracaSamoPocetnePodatke()
    {
        await SaBazomAsync(db => PocetniPodaci.UpisiAsync(db, Vreme));
        var dodatiClan = await NoviKorisnikUBaziAsync(Uloga.Clan);

        await PripremaBaze.ResetujAsync(Aplikacija.Services);

        var emailovi = await SaBazomAsync(db => db.Korisnici.Select(k => k.Email).ToListAsync());
        Assert.Multiple(() =>
        {
            Assert.That(emailovi, Has.Count.EqualTo(8));
            Assert.That(emailovi, Does.Not.Contain(dodatiClan.Email));
        });
    }

    [Test]
    public async Task Pokretanje_VanDevelopmentOkruzenja_NeUpisujePocetnePodatke()
    {
        var brojKorisnika = await SaBazomAsync(db => db.Korisnici.CountAsync());

        Assert.That(brojKorisnika, Is.Zero);
    }

    private Task<List<Termin>> UpisiIProcitajTermineAsync() => SaBazomAsync(async db =>
    {
        await PocetniPodaci.UpisiAsync(db, Vreme);
        db.ChangeTracker.Clear();
        return await db.Termini
            .Include(t => t.Trener)
            .Include(t => t.Rezervacije).ThenInclude(r => r.Clan)
            .AsSplitQuery()
            .ToListAsync();
    });
}
