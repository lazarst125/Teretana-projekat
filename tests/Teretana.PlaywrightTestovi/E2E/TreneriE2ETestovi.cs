using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

public sealed class TreneriE2ETestovi : E2ETest
{
    private const string PorukaTerminImaPrijave = "Termin ima prijave i ne može da se menja ni briše; umesto toga ga otkažite.";

    [Test]
    public async Task NoviTermin_PraznaFormaPaIspravniPodaci_PrvoPrikazujePorukeIspodPoljaPaKreiraTermin()
    {
        await PrijaviSeAsync(await Podaci.NoviTrenerAsync());
        var forma = new TerminFormaStrana(Page);
        await forma.OtvoriNovuAsync();
        await forma.SacuvajAsync();
        await Expect(forma.GreskaPolja("naziv")).ToHaveTextAsync("Naziv je obavezan.");
        await Expect(forma.GreskaPolja("pocetak")).ToHaveTextAsync("Početak je obavezan.");
        await Expect(forma.GreskaPolja("kraj")).ToHaveTextAsync("Kraj je obavezan.");
        await Expect(forma.GreskaPolja("kapacitet")).ToHaveTextAsync("Kapacitet mora biti između 1 i 100.");
        await Expect(forma.Naziv).ToBeFocusedAsync();
        var pocetak = DateTime.Now.Date.AddDays(3).AddHours(18);

        await forma.PopuniAsync("Pilates E2E", pocetak, pocetak.AddHours(1), kapacitet: 8);
        await forma.SacuvajAsync();

        var detalj = new TerminDetaljStrana(Page);
        await Expect(detalj.Naziv).ToHaveTextAsync("Pilates E2E");
        await Expect(detalj.Kapacitet).ToHaveTextAsync("8");
    }

    [Test]
    public async Task BrisanjeTerminaSaRezervacijom_PrikazujeKonfliktITerminOstaje()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener, naziv: "Crossfit E2E");
        await Podaci.RezervisiAsync(await Podaci.NoviClanAsync(), idTermina);
        await PrijaviSeAsync(trener);
        var detalj = new TerminDetaljStrana(Page);
        await detalj.OtvoriAsync(idTermina);

        await detalj.ObrisiAsync();

        await Expect(detalj.GreskaAkcije).ToHaveTextAsync(PorukaTerminImaPrijave);
        await Page.ReloadAsync();
        await Expect(detalj.Naziv).ToHaveTextAsync("Crossfit E2E");
    }

    [Test]
    public async Task IzmenaTerminaSaRezervacijom_PrikazujeKonfliktUFormi()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener, kapacitet: 5);
        await Podaci.RezervisiAsync(await Podaci.NoviClanAsync(), idTermina);
        await PrijaviSeAsync(trener);
        var detalj = new TerminDetaljStrana(Page);
        var forma = new TerminFormaStrana(Page);
        await detalj.OtvoriAsync(idTermina);
        await detalj.OtvoriIzmenuAsync();

        await forma.PostaviKapacitetAsync(10);
        await forma.SacuvajAsync();

        await Expect(forma.GreskaForme).ToHaveTextAsync(PorukaTerminImaPrijave);
    }

    [Test]
    public async Task OtkazivanjeTermina_ClanSaRezervacijomVidiDaJeTerminOtkazan()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener);
        var idRezervacije = await Podaci.RezervisiAsync(clan, idTermina);
        await PrijaviSeAsync(trener);
        var detalj = new TerminDetaljStrana(Page);
        await detalj.OtvoriAsync(idTermina);

        await detalj.OtkaziTerminAsync();

        await Expect(detalj.Stanje).ToHaveAttributeAsync("data-stanje", "otkazan");
        var stranaClana = await NovaStranaZaAsync(clan);
        var mojeRezervacije = new MojeRezervacijeStrana(stranaClana);
        await mojeRezervacije.OtvoriAsync();
        await Expect(mojeRezervacije.Status(idRezervacije)).ToHaveAttributeAsync("data-status", "Otkazana");
        await Expect(mojeRezervacije.TerminOtkazan(idRezervacije)).ToBeVisibleAsync();
        var detaljZaClana = new TerminDetaljStrana(stranaClana);
        await detaljZaClana.OtvoriAsync(idTermina);
        await Expect(detaljZaClana.Napomena).ToHaveTextAsync("Termin je otkazan i ne prima prijave.");
    }

    [Test]
    public async Task EvidencijaPrisustva_NaTerminuKojiJePoceo_OznacavaDolazakClana()
    {
        const string ImeClana = "Marko Dolazi";
        var trener = await Podaci.NoviTrenerAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener);
        await Podaci.RezervisiAsync(await Podaci.NoviClanAsync(ImeClana), idTermina);
        await Podaci.PomeriTerminAsync(idTermina, DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(30));
        await PrijaviSeAsync(trener);
        var polaznici = new PolazniciStrana(Page);
        await polaznici.OtvoriAsync(idTermina);
        await Expect(polaznici.Prisustvo(ImeClana)).ToHaveAttributeAsync("data-prisustvo", "neevidentirano");

        await polaznici.EvidentirajDolazakAsync(ImeClana);

        await Expect(polaznici.Prisustvo(ImeClana)).ToHaveAttributeAsync("data-prisustvo", "dosao");
    }

    [Test]
    public async Task NazivTerminaSaHtmlom_PrikazujeSeKaoObicanTekst()
    {
        const string Naziv = "<img src=x onerror=alert(1)> Joga 'E2E'";
        var trener = await Podaci.NoviTrenerAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener, naziv: Naziv);
        await PrijaviSeAsync(trener);
        var raspored = new RasporedStrana(Page);

        await raspored.OtvoriAsync();
        await Expect(raspored.Kartica(idTermina)).ToContainTextAsync(Naziv);
        await raspored.OtvoriDetaljAsync(idTermina);

        await Expect(new TerminDetaljStrana(Page).Naziv).ToHaveTextAsync(Naziv);
        await Expect(Page.Locator("img")).ToHaveCountAsync(0);
    }
}
