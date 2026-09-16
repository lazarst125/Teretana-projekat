using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Autentikacija;

/// <summary>
/// Izmena sopstvenog profila. Lozinka se menja samo kada je zadata, pa svaki test proverava i šta se
/// dogodilo sa hešom u bazi, a ne samo odgovor.
/// </summary>
public sealed class IzmenaProfilaTestovi : KomponentniTest
{
    [Test]
    public async Task Izmena_SamoImePrezime_Vraca200IStaraLozinkaIDaljeVazi()
    {
        var korisnik = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(korisnik);

        using var odgovor = await Klijent.PutAsJsonAsync("/api/auth/ja", new { imePrezime = "  Novo Ime  " });

        var profil = await odgovor.Content.ReadFromJsonAsync<KorisnikTelo>();
        var uBazi = await SaBazomAsync(db => db.Korisnici.AsNoTracking().SingleAsync(k => k.Id == korisnik.Id));
        using var prijava = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = korisnik.Email, lozinka = TestnaLozinka });
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(profil!.ImePrezime, Is.EqualTo("Novo Ime"), "Ime se čuva bez suvišnih razmaka.");
            Assert.That(profil.Email, Is.EqualTo(korisnik.Email), "Email se ne menja.");
            Assert.That(profil.Uloga, Is.EqualTo("Clan"), "Uloga se ne menja.");
            Assert.That(uBazi.ImePrezime, Is.EqualTo("Novo Ime"));
            Assert.That(uBazi.LozinkaHash, Is.EqualTo(korisnik.LozinkaHash), "Izmena imena ne sme da dira heš lozinke.");
            Assert.That(prijava.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task Izmena_SaNovomLozinkom_Vraca200IPrijavaRadiSamoNovomLozinkom()
    {
        var korisnik = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(korisnik);
        const string NovaLozinka = "NovaLozinka123!";

        using var odgovor = await Klijent.PutAsJsonAsync("/api/auth/ja", new { imePrezime = korisnik.ImePrezime, lozinka = NovaLozinka });

        using var novom = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = korisnik.Email, lozinka = NovaLozinka });
        using var starom = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = korisnik.Email, lozinka = TestnaLozinka });
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(novom.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(starom.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        });
    }

    [Test]
    public async Task Izmena_PraznoImeIPrekratkaLozinka_Vraca400SaGreskamaZaObaPolja()
    {
        var korisnik = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(korisnik);

        using var odgovor = await Klijent.PutAsJsonAsync("/api/auth/ja", new { imePrezime = "", lozinka = "kratka" });

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        var uBazi = await SaBazomAsync(db => db.Korisnici.AsNoTracking().SingleAsync(k => k.Id == korisnik.Id));
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("validacija"));
            Assert.That(problem.GetProperty("errors").EnumerateObject().Select(polje => polje.Name), Is.EquivalentTo(new[] { "imePrezime", "lozinka" }));
            Assert.That(uBazi.ImePrezime, Is.EqualTo(korisnik.ImePrezime), "Odbijen zahtev ne sme ništa da promeni.");
            Assert.That(uBazi.LozinkaHash, Is.EqualTo(korisnik.LozinkaHash));
        });
    }

    [Test]
    public async Task Izmena_KorisnikIzTokenaJeObrisan_Vraca401KorisnikNePostoji()
    {
        var korisnik = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(korisnik);
        await SaBazomAsync(db => db.Korisnici.Where(k => k.Id == korisnik.Id).ExecuteDeleteAsync());

        using var odgovor = await Klijent.PutAsJsonAsync("/api/auth/ja", new { imePrezime = "Bilo koje ime" });

        await OcekujProblemAsync(odgovor, HttpStatusCode.Unauthorized, "korisnik-ne-postoji");
    }

    private sealed record KorisnikTelo(int Id, string Email, string ImePrezime, string Uloga);
}
