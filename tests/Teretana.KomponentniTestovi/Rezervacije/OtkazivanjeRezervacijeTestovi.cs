using System.Net;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Rezervacije;

/// <summary>
/// Rok za otkazivanje je 2 sata pre početka. Sat testa stoji na <see cref="TestniEntiteti.Sada"/>, pa se granica
/// pomera postavljanjem početka termina tačno 2 sata od sada, uz pomak od jedne sekunde.
/// </summary>
public sealed class OtkazivanjeRezervacijeTestovi : KomponentniTest
{
    [TestCase(1, TestName = "Otkazivanje_SekundPreIstekaRoka_Vraca204")]
    [TestCase(0, TestName = "Otkazivanje_TacnoUTrenutkuIstekaRoka_Vraca204")]
    public async Task Otkazivanje_DoIstekaRoka_Vraca204(int sekundiDoIstekaRoka)
    {
        var termin = await NoviTerminUBaziAsync(
            await NoviKorisnikUBaziAsync(Uloga.Trener),
            pocetak: TestniEntiteti.Sada.AddHours(2).AddSeconds(sekundiDoIstekaRoka));
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var rezervacija = await NovaPrijavaUBaziAsync(termin, ja, StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(ja);

        using var odgovor = await Klijent.DeleteAsync($"/api/rezervacije/{rezervacija.Id}");

        Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Otkazivanje_SekundPosleIstekaRoka_Vraca422RokZaOtkazivanjeIstekao()
    {
        var termin = await NoviTerminUBaziAsync(
            await NoviKorisnikUBaziAsync(Uloga.Trener),
            pocetak: TestniEntiteti.Sada.AddHours(2).AddSeconds(-1));
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var rezervacija = await NovaPrijavaUBaziAsync(termin, ja, StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(ja);

        using var odgovor = await Klijent.DeleteAsync($"/api/rezervacije/{rezervacija.Id}");

        await OcekujProblemAsync(odgovor, HttpStatusCode.UnprocessableEntity, "rok-za-otkazivanje-istekao");
    }

    [Test]
    public async Task Otkazivanje_PotvrdjenaRezervacijaSaListomCekanja_PrviSaListePostajePotvrdjenADrugiIDaljeCeka()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var mojaRezervacija = await NovaPrijavaUBaziAsync(termin, ja, StatusRezervacije.Potvrdjena);
        var prijavljenKasnije = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.NaCekanju, TestniEntiteti.Sada.AddMinutes(-10));
        var prijavljenRanije = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.NaCekanju, TestniEntiteti.Sada.AddMinutes(-50));
        await PrijaviSeKaoAsync(ja);

        using var odgovor = await Klijent.DeleteAsync($"/api/rezervacije/{mojaRezervacija.Id}");

        var statusi = await SaBazomAsync(db => db.Rezervacije.Where(r => r.TerminId == termin.Id).ToDictionaryAsync(r => r.Id, r => r.Status));
        var brojPotvrdjenih = await SaBazomAsync(db => db.Termini.Where(t => t.Id == termin.Id).Select(t => t.BrojPotvrdjenih).SingleAsync());
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(statusi[mojaRezervacija.Id], Is.EqualTo(StatusRezervacije.Otkazana));
            Assert.That(statusi[prijavljenRanije.Id], Is.EqualTo(StatusRezervacije.Potvrdjena));
            Assert.That(statusi[prijavljenKasnije.Id], Is.EqualTo(StatusRezervacije.NaCekanju));
            Assert.That(brojPotvrdjenih, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Otkazivanje_PotvrdjenaRezervacijaBezListeCekanja_SmanjujeBrojPotvrdjenih()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 2);
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var mojaRezervacija = await NovaPrijavaUBaziAsync(termin, ja, StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(ja);

        using var odgovor = await Klijent.DeleteAsync($"/api/rezervacije/{mojaRezervacija.Id}");

        var brojPotvrdjenih = await SaBazomAsync(db => db.Termini.Where(t => t.Id == termin.Id).Select(t => t.BrojPotvrdjenih).SingleAsync());
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(brojPotvrdjenih, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Otkazivanje_TudjaRezervacija_Vraca404RezervacijaNePostoji()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        var tudja = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.DeleteAsync($"/api/rezervacije/{tudja.Id}");

        await OcekujProblemAsync(odgovor, HttpStatusCode.NotFound, "rezervacija-ne-postoji");
    }

    [Test]
    public async Task Otkazivanje_VecOtkazanaRezervacija_Vraca409RezervacijaOtkazana()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var otkazana = await NovaPrijavaUBaziAsync(termin, ja, StatusRezervacije.Otkazana);
        await PrijaviSeKaoAsync(ja);

        using var odgovor = await Klijent.DeleteAsync($"/api/rezervacije/{otkazana.Id}");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "rezervacija-otkazana");
    }
}
