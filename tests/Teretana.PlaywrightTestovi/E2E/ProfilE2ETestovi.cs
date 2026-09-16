using System.Text.RegularExpressions;
using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

/// <summary>Izmena i brisanje sopstvenog naloga kroz interfejs, uključujući razlog kada brisanje nije dozvoljeno.</summary>
public sealed class ProfilE2ETestovi : E2ETest
{
    private const string NovaLozinka = "NovaLozinka123!";

    [Test]
    public async Task IzmenaProfila_NovoImeINovaLozinka_MenjaZaglavljeAPrijavaRadiNovomLozinkom()
    {
        var clan = await Podaci.NoviClanAsync("Staro Ime");
        await PrijaviSeAsync(clan);
        var profil = new ProfilStrana(Page);
        var zaglavlje = new Zaglavlje(Page);
        await profil.OtvoriAsync();
        await Expect(profil.Email).ToHaveTextAsync(clan.Email);

        await profil.SacuvajAsync("Novo Ime", NovaLozinka);

        await Expect(zaglavlje.Obavestenje).ToHaveTextAsync("Profil je sačuvan.");
        await Expect(zaglavlje.ImeKorisnika).ToHaveTextAsync("Novo Ime");

        await zaglavlje.OdjaviSeAsync();
        await new PrijavaStrana(Page).PrijaviSeAsync(clan.Email, NovaLozinka);

        await Expect(zaglavlje.ImeKorisnika).ToHaveTextAsync("Novo Ime");
    }

    [Test]
    public async Task IzmenaProfila_PraznoIme_PrikazujePorukuIspodPoljaIImeOstaje()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync("Nepromenjeno Ime"));
        var profil = new ProfilStrana(Page);
        await profil.OtvoriAsync();

        await profil.SacuvajAsync(string.Empty);

        await Expect(profil.GreskaPolja("ime-prezime")).ToHaveTextAsync("Ime i prezime su obavezni.");
        await Expect(new Zaglavlje(Page).ImeKorisnika).ToHaveTextAsync("Nepromenjeno Ime");
    }

    [Test]
    public async Task BrisanjeNaloga_ClanSaRezervacijom_PrikazujeRazlogINalogOstaje()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync("Član Sa Prijavom");
        await Podaci.RezervisiAsync(clan, await Podaci.NoviTerminAsync(trener));
        await PrijaviSeAsync(clan);
        var profil = new ProfilStrana(Page);
        await profil.OtvoriAsync();

        await profil.ObrisiNalogAsync();

        await Expect(profil.GreskaBrisanja).ToContainTextAsync("Nalog ima termine ili prijave");
        await Expect(new Zaglavlje(Page).ImeKorisnika).ToHaveTextAsync("Član Sa Prijavom");
    }

    [Test]
    public async Task BrisanjeNaloga_ClanBezPrijava_VracaNaPrijavuAStariNalogViseNeRadi()
    {
        var clan = await Podaci.NoviClanAsync();
        await PrijaviSeAsync(clan);
        var profil = new ProfilStrana(Page);
        var prijava = new PrijavaStrana(Page);
        await profil.OtvoriAsync();

        await profil.ObrisiNalogAsync();

        await Expect(new Zaglavlje(Page).Obavestenje).ToHaveTextAsync("Nalog je obrisan.");
        await Expect(Page).ToHaveURLAsync(new Regex("#/prijava$"));

        await prijava.PrijaviSeAsync(clan.Email, TestniPodaci.Lozinka);

        await Expect(prijava.GreskaForme).ToHaveTextAsync("Email ili lozinka nisu ispravni.");
    }
}
