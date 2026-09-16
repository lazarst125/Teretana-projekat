using System.Text.RegularExpressions;
using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

public sealed class AutentikacijaE2ETestovi : E2ETest
{
    [Test]
    public async Task Prijava_PogresnaLozinka_PrikazujePorukuServeraIOstajeNaPrijavi()
    {
        var clan = await Podaci.NoviClanAsync();
        var prijava = new PrijavaStrana(Page);
        await prijava.OtvoriAsync();

        await prijava.PrijaviSeAsync(clan.Email, "PogresnaLozinka1!");

        await Expect(prijava.GreskaForme).ToHaveTextAsync("Email ili lozinka nisu ispravni.");
        await Expect(Page).ToHaveURLAsync(new Regex("#/prijava$"));
    }

    [Test]
    public async Task Registracija_KratkaLozinkaPaZauzetEmail_PrikazujePorukeServeraIOstajeNaRegistraciji()
    {
        var postojeciClan = await Podaci.NoviClanAsync();
        var registracija = new RegistracijaStrana(Page);
        await registracija.OtvoriAsync();
        await registracija.RegistrujAsync("Novi Član", postojeciClan.Email, "kratka");
        await Expect(registracija.GreskaPolja("lozinka")).ToHaveTextAsync("Lozinka mora imati između 8 i 100 znakova.");

        await registracija.RegistrujAsync("Novi Član", postojeciClan.Email.ToUpperInvariant(), TestniPodaci.Lozinka);

        await Expect(registracija.GreskaForme).ToHaveTextAsync("Nalog sa ovim email-om već postoji.");
        await Expect(registracija.GreskaPolja("lozinka")).ToBeHiddenAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("#/registracija$"));
    }

    [Test]
    public async Task ZasticenaAdresaBezPrijave_PosleUspesnePrijave_OtvaraTrazenuStranu()
    {
        var clan = await Podaci.NoviClanAsync("Ana Povratak");
        await Page.GotoAsync("/#/rezervacije");
        await Expect(Page).ToHaveURLAsync(new Regex("#/prijava\\?povratak=%2Frezervacije$"));

        await new PrijavaStrana(Page).PrijaviSeAsync(clan.Email, TestniPodaci.Lozinka);

        await Expect(Page).ToHaveURLAsync(new Regex("#/rezervacije$"));
        await Expect(new Zaglavlje(Page).ImeKorisnika).ToHaveTextAsync("Ana Povratak");
    }

    [Test]
    public async Task ClanNaTrenerskojAdresi_VidiNematePristupINemaTrenerskuNavigaciju()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync());

        await Page.GotoAsync("/#/termini/novi");

        await Expect(Page.GetByTestId("nemate-pristup")).ToBeVisibleAsync();
        await Expect(new Zaglavlje(Page).NavigacijaNoviTermin).ToHaveCountAsync(0);
    }

    [Test]
    public async Task AdresaKojuRuterNePrepoznaje_PrikazujeStranuKojaNePostojiIZadrzavaZaglavlje()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync("Zalutali Clan"));

        await Page.GotoAsync("/#/termini/5/nepostojeci-ekran");

        await Expect(Page.GetByTestId("stranica-ne-postoji")).ToContainTextAsync("Adresa koju ste otvorili ne postoji u aplikaciji.");
        await Expect(new Zaglavlje(Page).ImeKorisnika).ToHaveTextAsync("Zalutali Clan");
    }

    [Test]
    public async Task TokenKojiServerViseNePrihvata_VracaClanaNaPrijavuSaPorukomOIsteklojSesiji()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var zaglavlje = new Zaglavlje(Page);
        // Server ne može da posluži istekao token bez čekanja na istek, pa odgovor zamenjuje presretač;
        // predmet provere je šta aplikacija radi kada joj API odbije sačuvani token.
        await Page.RouteAsync(new Regex("/api/"), ruta => ruta.FulfillAsync(new()
        {
            Status = 401,
            ContentType = "application/problem+json",
            Body = "{\"status\":401,\"title\":\"Unauthorized\"}",
        }));

        await Page.GotoAsync("/#/rezervacije");

        await Expect(zaglavlje.Obavestenje).ToHaveTextAsync("Sesija je istekla. Prijavite se ponovo.");
        await Expect(Page).ToHaveURLAsync(new Regex("#/prijava$"));
        await Expect(zaglavlje.ImeKorisnika).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Odjava_PrijavljenClan_VracaNaPrijavuIZasticeneStraneTraziPonovnuPrijavu()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var zaglavlje = new Zaglavlje(Page);

        await zaglavlje.OdjaviSeAsync();

        await Expect(zaglavlje.Obavestenje).ToHaveTextAsync("Odjavljeni ste.");
        await Page.GotoAsync("/#/rezervacije");
        await Expect(Page).ToHaveURLAsync(new Regex("#/prijava\\?povratak=%2Frezervacije$"));
    }
}
