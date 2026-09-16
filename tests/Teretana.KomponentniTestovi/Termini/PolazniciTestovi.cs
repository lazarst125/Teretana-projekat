using System.Net;
using System.Net.Http.Json;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Termini;

public sealed class PolazniciTestovi : KomponentniTest
{
    [Test]
    public async Task Polaznici_TerminSaPotvrdjenimICekanjem_VracaPotvrdjeneIListuCekanjaPoVremenuPrijave()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener, kapacitet: 1);
        var potvrdjen = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var prijavljenKasnije = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var prijavljenRanije = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await NovaPrijavaUBaziAsync(termin, potvrdjen, StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(termin, prijavljenKasnije, StatusRezervacije.NaCekanju, TestniEntiteti.Sada.AddMinutes(-5));
        await NovaPrijavaUBaziAsync(termin, prijavljenRanije, StatusRezervacije.NaCekanju, TestniEntiteti.Sada.AddMinutes(-50));
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Otkazana);
        await PrijaviSeKaoAsync(trener);

        var polaznici = await Klijent.GetFromJsonAsync<PolazniciTelo>($"/api/termini/{termin.Id}/polaznici");

        Assert.That(polaznici, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(polaznici!.Potvrdjeni.Select(p => p.ClanId), Is.EqualTo(new[] { potvrdjen.Id }));
            Assert.That(
                polaznici.ListaCekanja.Select(p => (p.ClanId, p.Pozicija)),
                Is.EqualTo(new[] { (prijavljenRanije.Id, 1), (prijavljenKasnije.Id, 2) }));
        });
    }

    [Test]
    public async Task Polaznici_TudjiTermin_Vraca403TudjiTermin()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));

        using var odgovor = await Klijent.GetAsync($"/api/termini/{termin.Id}/polaznici");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Forbidden, "tudji-termin");
    }
}
