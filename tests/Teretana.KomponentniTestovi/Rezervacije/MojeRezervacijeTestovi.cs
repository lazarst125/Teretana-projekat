using System.Net.Http.Json;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Rezervacije;

public sealed class MojeRezervacijeTestovi : KomponentniTest
{
    [Test]
    public async Task Moje_DrugaStranicaPoPocetkuTermina_VracaSamoSopstvenePrijaveIUkupanBroj()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var drugiClan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var raniji = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(1));
        var kasniji = await NoviTerminUBaziAsync(trener, kapacitet: 1, pocetak: TestniEntiteti.Sada.AddDays(2));
        await NovaPrijavaUBaziAsync(raniji, ja, StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(raniji, drugiClan, StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(kasniji, drugiClan, StatusRezervacije.Potvrdjena);
        var mojaNaKasnijem = await NovaPrijavaUBaziAsync(kasniji, ja, StatusRezervacije.NaCekanju);
        await PrijaviSeKaoAsync(ja);

        var stranica = await Klijent.GetFromJsonAsync<StranicaRezervacijaTelo>("/api/rezervacije/moje?stranica=2&velicinaStranice=1");

        Assert.That(stranica, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stranica!.Stavke.Select(r => r.Id), Is.EqualTo(new[] { mojaNaKasnijem.Id }));
            Assert.That(stranica.UkupnoStavki, Is.EqualTo(2));
            Assert.That(stranica.UkupnoStranica, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Moje_FilterNaCekanju_VracaSamoPrijaveSaListeCekanjaSaPozicijom()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var slobodan = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(1));
        var pun = await NoviTerminUBaziAsync(trener, kapacitet: 1, pocetak: TestniEntiteti.Sada.AddDays(2));
        await NovaPrijavaUBaziAsync(slobodan, ja, StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(pun, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        var naCekanju = await NovaPrijavaUBaziAsync(pun, ja, StatusRezervacije.NaCekanju);
        await PrijaviSeKaoAsync(ja);

        var stranica = await Klijent.GetFromJsonAsync<StranicaRezervacijaTelo>("/api/rezervacije/moje?status=NaCekanju");

        Assert.That(
            stranica?.Stavke.Select(r => (r.Id, r.Status, r.PozicijaNaCekanju)),
            Is.EqualTo(new[] { (naCekanju.Id, "NaCekanju", (int?)1) }));
    }

    [Test]
    public async Task Moje_SortiranjePoPocetkuOpadajuce_VracaNajkasnijiTerminPrvi()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var ja = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var naRanijem = await NovaPrijavaUBaziAsync(await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(1)), ja, StatusRezervacije.Potvrdjena);
        var naKasnijem = await NovaPrijavaUBaziAsync(await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(2)), ja, StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(ja);

        var stranica = await Klijent.GetFromJsonAsync<StranicaRezervacijaTelo>("/api/rezervacije/moje?sortiranje=-pocetak");

        Assert.That(stranica?.Stavke.Select(r => r.Id), Is.EqualTo(new[] { naKasnijem.Id, naRanijem.Id }));
    }
}
