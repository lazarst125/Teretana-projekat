using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Termini;

public sealed class KreiranjeTerminaTestovi : KomponentniTest
{
    [Test]
    public async Task Kreiranje_TrenerSaIspravnimPodacima_Vraca201SaNovimPraznimTerminom()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.PostAsJsonAsync("/api/termini", IspravanZahtev());

        var termin = await odgovor.Content.ReadFromJsonAsync<TerminTelo>();
        Assert.That(termin, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(odgovor.Headers.Location?.ToString(), Does.EndWith($"/api/termini/{termin!.Id}"));
            Assert.That(termin.Trener.Id, Is.EqualTo(trener.Id));
            Assert.That(termin.Status, Is.EqualTo("Aktivan"));
            Assert.That(termin.BrojPotvrdjenih, Is.Zero);
            Assert.That(termin.SlobodnaMesta, Is.EqualTo(8));
            Assert.That(termin.Pocetak, Is.EqualTo(TestniEntiteti.Sada.AddDays(2)));
        });
    }

    [Test]
    public async Task Kreiranje_KapacitetNulaIKrajPrePocetka_Vraca400SaGreskomZaObaPolja()
    {
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        var pocetak = TestniEntiteti.Sada.AddDays(2);

        using var odgovor = await Klijent.PostAsJsonAsync("/api/termini", new { naziv = "Joga", pocetak, kraj = pocetak.AddMinutes(-1), kapacitet = 0 });

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem.GetProperty("errors").EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "kapacitet", "kraj" }));
        });
    }

    [Test]
    public async Task Kreiranje_PocetakUProslosti_Vraca422PocetakUProslosti()
    {
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        var pocetak = TestniEntiteti.Sada.AddMinutes(-1);

        using var odgovor = await Klijent.PostAsJsonAsync("/api/termini", new { naziv = "Joga", pocetak, kraj = pocetak.AddHours(1), kapacitet = 8 });

        await OcekujProblemAsync(odgovor, HttpStatusCode.UnprocessableEntity, "pocetak-u-proslosti");
    }

    [Test]
    public async Task Kreiranje_TeloSaServerskimPoljima_IgnoriseTrenerStatusIBrojPotvrdjenih()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var drugiTrener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        await PrijaviSeKaoAsync(trener);
        var pocetak = TestniEntiteti.Sada.AddDays(2);

        using var odgovor = await Klijent.PostAsJsonAsync("/api/termini", new
        {
            naziv = "Joga",
            pocetak,
            kraj = pocetak.AddHours(1),
            kapacitet = 8,
            trenerId = drugiTrener.Id,
            status = "Otkazan",
            brojPotvrdjenih = 5,
        });

        var id = (await odgovor.Content.ReadFromJsonAsync<TerminTelo>())!.Id;
        var sacuvan = await SaBazomAsync(db => db.Termini.SingleAsync(t => t.Id == id));
        Assert.Multiple(() =>
        {
            Assert.That(sacuvan.TrenerId, Is.EqualTo(trener.Id));
            Assert.That(sacuvan.Status, Is.EqualTo(StatusTermina.Aktivan));
            Assert.That(sacuvan.BrojPotvrdjenih, Is.Zero);
        });
    }

    private static object IspravanZahtev()
    {
        var pocetak = TestniEntiteti.Sada.AddDays(2);
        return new { naziv = "Joga za početnike", opis = "Lagan uvod.", pocetak, kraj = pocetak.AddHours(1), kapacitet = 8 };
    }
}
