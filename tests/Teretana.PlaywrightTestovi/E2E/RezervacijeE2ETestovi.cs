using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

public sealed class RezervacijeE2ETestovi : E2ETest
{
    [Test]
    public async Task NoviClan_RegistrujeSeRezervisePaOtkazujePreRoka_MestoSeOslobadja()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener, kapacitet: 5, naziv: "Joga E2E");
        var zaglavlje = new Zaglavlje(Page);
        var detaljTermina = new TerminDetaljStrana(Page);
        var rezervacija = new RezervacijaDetaljStrana(Page);
        var registracija = new RegistracijaStrana(Page);
        await registracija.OtvoriAsync();
        await registracija.RegistrujAsync("Nova Članica", $"{Guid.NewGuid():N}@primer.rs", TestniPodaci.Lozinka);
        await Expect(zaglavlje.ImeKorisnika).ToHaveTextAsync("Nova Članica");

        await new RasporedStrana(Page).OtvoriDetaljAsync(idTermina);
        await detaljTermina.RezervisiAsync();
        await Expect(rezervacija.Status).ToHaveAttributeAsync("data-status", "Potvrdjena");
        await rezervacija.OtkaziAsync();

        await Expect(rezervacija.Status).ToHaveAttributeAsync("data-status", "Otkazana");
        await Expect(zaglavlje.Obavestenje).ToHaveTextAsync("Rezervacija je otkazana.");
        await detaljTermina.OtvoriAsync(idTermina);
        await Expect(detaljTermina.SlobodnaMesta).ToHaveTextAsync("5");
    }

    [Test]
    public async Task PotvrdjenaRezervacija_PosleIstekaRoka_NemaOtkazivanjaIObjasnjavaZasto()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener, pocetak: DateTimeOffset.UtcNow.AddMinutes(90));
        var idRezervacije = await Podaci.RezervisiAsync(clan, idTermina);
        await PrijaviSeAsync(clan);
        var rezervacija = new RezervacijaDetaljStrana(Page);

        await rezervacija.OtvoriAsync(idRezervacije);

        await Expect(rezervacija.OtkazivanjeNedostupno).ToContainTextAsync("Rok za otkazivanje je istekao");
        await Expect(rezervacija.DugmeOtkazi).ToHaveCountAsync(0);
    }

    [Test]
    public async Task PopunjenTermin_KadaPotvrdjeniClanOtkaze_PrviSaListeCekanjaAutomatskiDobijaMesto()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var potvrdjeni = await Podaci.NoviClanAsync("Prvi Potvrđeni");
        var cekac = await Podaci.NoviClanAsync("Drugi Čeka");
        var idTermina = await Podaci.NoviTerminAsync(trener, kapacitet: 1);
        var idRezervacije = await Podaci.RezervisiAsync(potvrdjeni, idTermina);
        var stranaCekaca = await NovaStranaZaAsync(cekac);
        var detaljZaCekaca = new TerminDetaljStrana(stranaCekaca);
        var prijavaCekaca = new RezervacijaDetaljStrana(stranaCekaca);
        await detaljZaCekaca.OtvoriAsync(idTermina);
        await detaljZaCekaca.PrijaviNaListuCekanjaAsync();
        await Expect(prijavaCekaca.Status).ToHaveTextAsync("Na čekanju, pozicija 1");

        await PrijaviSeAsync(potvrdjeni);
        var rezervacija = new RezervacijaDetaljStrana(Page);
        await rezervacija.OtvoriAsync(idRezervacije);
        await rezervacija.OtkaziAsync();
        await Expect(rezervacija.Status).ToHaveAttributeAsync("data-status", "Otkazana");

        await stranaCekaca.ReloadAsync();
        await Expect(prijavaCekaca.Status).ToHaveAttributeAsync("data-status", "Potvrdjena");
    }

    [Test]
    public async Task TerminNaKomClanVecImaRezervaciju_NeNudiNovuRezervacijuVecPostojecuPrijavu()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener, kapacitet: 5);
        await Podaci.RezervisiAsync(clan, idTermina);
        await PrijaviSeAsync(clan);
        var detalj = new TerminDetaljStrana(Page);

        await detalj.OtvoriAsync(idTermina);

        await Expect(detalj.MojaPrijava).ToHaveAttributeAsync("data-status", "Potvrdjena");
        await Expect(detalj.DugmeRezervisi).ToHaveCountAsync(0);
        await Expect(detalj.DugmeListaCekanja).ToHaveCountAsync(0);
    }

    [Test]
    public async Task NoviClanBezRezervacija_MojeRezervacije_PrikazujuPraznoStanje()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var mojeRezervacije = new MojeRezervacijeStrana(Page);

        await mojeRezervacije.OtvoriAsync();

        await Expect(mojeRezervacije.PraznoStanje).ToHaveTextAsync("Nemate rezervacija koje odgovaraju filterima.");
    }

    [Test]
    public async Task MojeRezervacije_FilterStatusa_PrikazujeOdvojenoOtkazaneIPotvrdjene()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync();
        var idPotvrdjene = await Podaci.RezervisiAsync(clan, await Podaci.NoviTerminAsync(trener, naziv: "Ostaje potvrđena"));
        var idOtkazane = await Podaci.RezervisiAsync(clan, await Podaci.NoviTerminAsync(trener, naziv: "Biće otkazana"));
        await Podaci.OtkaziRezervacijuAsync(clan, idOtkazane);
        await PrijaviSeAsync(clan);
        var mojeRezervacije = new MojeRezervacijeStrana(Page);
        await mojeRezervacije.OtvoriAsync();
        await Expect(mojeRezervacije.Kartice).ToHaveCountAsync(2);

        await mojeRezervacije.FiltrirajAsync("Otkazana");

        await Expect(mojeRezervacije.Kartice).ToHaveCountAsync(1);
        await Expect(mojeRezervacije.Status(idOtkazane)).ToHaveAttributeAsync("data-status", "Otkazana");

        await mojeRezervacije.FiltrirajAsync("Potvrdjena");

        await Expect(mojeRezervacije.Kartice).ToHaveCountAsync(1);
        await Expect(mojeRezervacije.Status(idPotvrdjene)).ToHaveAttributeAsync("data-status", "Potvrdjena");
    }

    [Test]
    public async Task NepostojeciTermin_PrikazujeStanjeGreskeSaPonovnimPokusajem()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var detalj = new TerminDetaljStrana(Page);

        await detalj.OtvoriAsync(999_999);

        await Expect(detalj.StanjeGreske).ToContainTextAsync("Termin ne postoji.");
        await Expect(detalj.DugmePokusajPonovo).ToBeVisibleAsync();
    }

    [Test]
    public async Task OtkazivanjeRezervacije_EscapeZatvaraDijalog_RezervacijaOstajeAFokusSeVracaNaDugme()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync();
        var idRezervacije = await Podaci.RezervisiAsync(clan, await Podaci.NoviTerminAsync(trener));
        await PrijaviSeAsync(clan);
        var rezervacija = new RezervacijaDetaljStrana(Page);
        var dijalog = new DijalogPotvrde(Page);
        await rezervacija.OtvoriAsync(idRezervacije);
        await rezervacija.ZapocniOtkazivanjeAsync();
        await Expect(dijalog.Dijalog).ToBeVisibleAsync();

        await dijalog.ZatvoriTasteromEscapeAsync();

        await Expect(dijalog.Dijalog).ToHaveCountAsync(0);
        await Expect(rezervacija.DugmeOtkazi).ToBeFocusedAsync();
        await Page.ReloadAsync();
        await Expect(rezervacija.Status).ToHaveAttributeAsync("data-status", "Potvrdjena");
    }
}
