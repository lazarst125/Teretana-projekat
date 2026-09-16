using System.Net;
using System.Net.Http.Json;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Termini;

public sealed class DetaljTerminaTestovi : KomponentniTest
{
    [Test]
    public async Task Detalj_ClanDrugiNaListiCekanja_VracaZauzetostBrojNaCekanjuIMojuPoziciju()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.NaCekanju, TestniEntiteti.Sada.AddMinutes(-30));
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var mojaPrijava = await NovaPrijavaUBaziAsync(termin, ja, StatusRezervacije.NaCekanju, TestniEntiteti.Sada.AddMinutes(-10));
        await PrijaviSeKaoAsync(ja);

        var detalj = await Klijent.GetFromJsonAsync<TerminTelo>($"/api/termini/{termin.Id}");

        Assert.That(detalj, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(detalj!.SlobodnaMesta, Is.Zero);
            Assert.That(detalj.BrojNaCekanju, Is.EqualTo(2));
            Assert.That(detalj.MojaPrijava, Is.EqualTo(new MojaPrijavaTelo(mojaPrijava.Id, "NaCekanju", 2)));
        });
    }

    [Test]
    public async Task Detalj_NepostojeciTermin_Vraca404TerminNePostoji()
    {
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.GetAsync("/api/termini/999999");

        await OcekujProblemAsync(odgovor, HttpStatusCode.NotFound, "termin-ne-postoji");
    }
}
