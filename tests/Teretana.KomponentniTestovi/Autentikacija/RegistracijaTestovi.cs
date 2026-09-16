using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teretana.Api.Domen;
using Teretana.Api.Repozitorijumi;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Autentikacija;

public sealed class RegistracijaTestovi : KomponentniTest
{
    [Test]
    public async Task Registracija_IspravniPodaci_Vraca201IKreiraClanaSaHesovanomLozinkom()
    {
        var email = NoviEmail();

        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/registracija", new { email, imePrezime = "Ana Anić", lozinka = TestnaLozinka });

        var telo = await odgovor.Content.ReadFromJsonAsync<KorisnikTelo>();
        var sacuvan = await SaBazomAsync(db => db.Korisnici.SingleAsync(k => k.Email == email));
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(odgovor.Headers.Location?.ToString(), Does.EndWith("/api/auth/ja"));
            Assert.That(telo, Is.EqualTo(new KorisnikTelo(sacuvan.Id, email, "Ana Anić", "Clan")));
            Assert.That(sacuvan.LozinkaHash, Is.Not.EqualTo(TestnaLozinka));
            Assert.That(
                new PasswordHasher<Korisnik>().VerifyHashedPassword(sacuvan, sacuvan.LozinkaHash, TestnaLozinka),
                Is.Not.EqualTo(PasswordVerificationResult.Failed));
        });
    }

    [Test]
    public async Task Registracija_NeispravanEmailIKratkaLozinka_Vraca400SaGreskomZaSvakoPolje()
    {
        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/registracija", new { email = "nije-email", imePrezime = "Ana Anić", lozinka = "kratka" });

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        var poljaSaGreskom = problem.GetProperty("errors").EnumerateObject().Select(p => p.Name);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("validacija"));
            Assert.That(poljaSaGreskom, Is.EquivalentTo(new[] { "email", "lozinka" }));
        });
    }

    [Test]
    public async Task Registracija_EmailVecPostojiSaDrugacijimVelikimSlovima_Vraca409EmailZauzet()
    {
        var postojeci = await NoviKorisnikUBaziAsync(Uloga.Clan);

        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/registracija", new { email = postojeci.Email.ToUpperInvariant(), imePrezime = "Drugi Nalog", lozinka = TestnaLozinka });

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("email-zauzet"));
        });
    }

    [Test]
    public async Task Registracija_TeloTraziUloguTrener_KreiraClana()
    {
        var email = NoviEmail();

        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/registracija", new { email, imePrezime = "Pokušaj Trener", lozinka = TestnaLozinka, uloga = "Trener" });

        var uloga = await SaBazomAsync(db => db.Korisnici.Where(k => k.Email == email).Select(k => k.Uloga).SingleAsync());
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(uloga, Is.EqualTo(Uloga.Clan));
        });
    }

    [Test]
    public async Task DodavanjeKorisnika_KadaIstiEmailProdjeProveruUServisu_BazaOdlucujeIVracaEmailZauzet()
    {
        var postojeci = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var duplikat = TestniEntiteti.NoviClan();
        duplikat.Email = postojeci.Email.ToUpperInvariant();

        await using var scope = Aplikacija.Services.CreateAsyncScope();
        var repozitorijum = scope.ServiceProvider.GetRequiredService<IKorisnikRepozitorijum>();

        var greska = Assert.ThrowsAsync<DomenskaGreska>(() => repozitorijum.DodajAsync(duplikat, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(greska?.Vrsta, Is.EqualTo(VrstaGreske.Konflikt));
            Assert.That(greska?.Kod, Is.EqualTo("email-zauzet"));
        });
    }

    private sealed record KorisnikTelo(int Id, string Email, string ImePrezime, string Uloga);
}
