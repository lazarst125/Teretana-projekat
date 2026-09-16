using System.Net;
using System.Net.Http.Json;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Rezervacije;

public sealed class RezervacijaPoIdTestovi : KomponentniTest
{
    [Test]
    public async Task Detalj_SopstvenaPotvrdjenaRezervacija_VracaRokZaOtkazivanjeIDaMozeDaSeOtkaze()
    {
        var pocetak = TestniEntiteti.Sada.AddDays(1);
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), pocetak: pocetak, naziv: "Joga");
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var rezervacija = await NovaPrijavaUBaziAsync(termin, ja, StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(ja);

        var detalj = await Klijent.GetFromJsonAsync<RezervacijaTelo>($"/api/rezervacije/{rezervacija.Id}");

        Assert.That(detalj, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(detalj!.Status, Is.EqualTo("Potvrdjena"));
            Assert.That(detalj.RokZaOtkazivanje, Is.EqualTo(pocetak.AddHours(-2)));
            Assert.That(detalj.MozeDaSeOtkaze, Is.True);
            Assert.That(detalj.Termin.Naziv, Is.EqualTo("Joga"));
        });
    }

    [Test]
    public async Task Detalj_TudjaRezervacija_Vraca404RezervacijaNePostoji()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        var tudja = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.GetAsync($"/api/rezervacije/{tudja.Id}");

        await OcekujProblemAsync(odgovor, HttpStatusCode.NotFound, "rezervacija-ne-postoji");
    }

    [Test]
    public async Task Detalj_NepostojecaRezervacija_Vraca404RezervacijaNePostoji()
    {
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.GetAsync("/api/rezervacije/999999");

        await OcekujProblemAsync(odgovor, HttpStatusCode.NotFound, "rezervacija-ne-postoji");
    }
}
