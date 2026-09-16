using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Rezervacije;

public sealed class PrisustvoTestovi : KomponentniTest
{
    [Test]
    public async Task Prisustvo_TrenerPoslePocetkaSvogTermina_Vraca200ISnimaPrisustvo()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddMinutes(-30));
        var rezervacija = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.PutAsJsonAsync($"/api/rezervacije/{rezervacija.Id}/prisustvo", new { prisustvovao = true });

        var telo = await odgovor.Content.ReadFromJsonAsync<RezervacijaTelo>();
        var sacuvano = await SaBazomAsync(db => db.Rezervacije.Where(r => r.Id == rezervacija.Id).Select(r => r.Prisustvovao).SingleAsync());
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(telo?.Prisustvovao, Is.True);
            Assert.That(sacuvano, Is.True);
        });
    }

    [Test]
    public async Task Prisustvo_PrePocetkaTermina_Vraca422TerminNijePoceo()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddMinutes(30));
        var rezervacija = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.PutAsJsonAsync($"/api/rezervacije/{rezervacija.Id}/prisustvo", new { prisustvovao = true });

        await OcekujProblemAsync(odgovor, HttpStatusCode.UnprocessableEntity, "termin-nije-poceo");
    }

    [Test]
    public async Task Prisustvo_RezervacijaNaTudjemTerminu_Vraca403TudjiTermin()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), pocetak: TestniEntiteti.Sada.AddMinutes(-30));
        var rezervacija = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));

        using var odgovor = await Klijent.PutAsJsonAsync($"/api/rezervacije/{rezervacija.Id}/prisustvo", new { prisustvovao = true });

        await OcekujProblemAsync(odgovor, HttpStatusCode.Forbidden, "tudji-termin");
    }

    [Test]
    public async Task Prisustvo_OtkazanaRezervacija_Vraca409RezervacijaNijePotvrdjena()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddMinutes(-30));
        var rezervacija = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Otkazana);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.PutAsJsonAsync($"/api/rezervacije/{rezervacija.Id}/prisustvo", new { prisustvovao = false });

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "rezervacija-nije-potvrdjena");
    }
}
