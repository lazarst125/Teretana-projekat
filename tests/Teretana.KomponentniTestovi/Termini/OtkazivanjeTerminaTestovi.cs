using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Termini;

public sealed class OtkazivanjeTerminaTestovi : KomponentniTest
{
    [Test]
    public async Task Otkazivanje_AktivanTerminSaPrijavama_OtkazujeTerminISvePrijaveIBrojacJeNula()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener, kapacitet: 1);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.NaCekanju);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/otkazivanje", content: null);

        var otkazan = await odgovor.Content.ReadFromJsonAsync<TerminTelo>();
        var prijave = await SaBazomAsync(db => db.Rezervacije.Where(r => r.TerminId == termin.Id).ToListAsync());
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(otkazan?.Status, Is.EqualTo("Otkazan"));
            Assert.That(otkazan?.BrojPotvrdjenih, Is.Zero);
            Assert.That(prijave, Has.Count.EqualTo(2).And.All.Matches<Rezervacija>(r =>
                r.Status == StatusRezervacije.Otkazana && r.OtkazanaAt == TestniEntiteti.Sada));
        });
    }

    [Test]
    public async Task Otkazivanje_VecOtkazanTermin_Vraca409TerminOtkazan()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener);
        await PrijaviSeKaoAsync(trener);
        using var prvoOtkazivanje = await Klijent.PostAsync($"/api/termini/{termin.Id}/otkazivanje", content: null);

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/otkazivanje", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "termin-otkazan");
    }

    [Test]
    public async Task Otkazivanje_TerminKojiJePoceo_Vraca422TerminJePoceo()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddMinutes(-10));
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/otkazivanje", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.UnprocessableEntity, "termin-je-poceo");
    }

    [Test]
    public async Task Otkazivanje_TudjiTermin_Vraca403TudjiTermin()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/otkazivanje", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Forbidden, "tudji-termin");
    }
}
