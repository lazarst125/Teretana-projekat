using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Autentikacija;

public sealed class PrijavaTestovi : KomponentniTest
{
    [Test]
    public async Task Prijava_IspravniKredencijali_Vraca200SaTokenomKojiNosiIdIUlogu()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);

        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = trener.Email, lozinka = TestnaLozinka });

        var telo = await odgovor.Content.ReadFromJsonAsync<JsonElement>();
        var token = new JsonWebToken(telo.GetProperty("token").GetString());
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(token.Subject, Is.EqualTo(trener.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(token.GetClaim("role").Value, Is.EqualTo("Trener"));
            Assert.That(telo.GetProperty("korisnik").GetProperty("uloga").GetString(), Is.EqualTo("Trener"));
        });
    }

    [Test]
    public async Task Prijava_PogresnaLozinka_Vraca401NeispravniKredencijali()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);

        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = clan.Email, lozinka = "PogresnaLozinka1!" });

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("neispravni-kredencijali"));
        });
    }

    [Test]
    public async Task Prijava_NepostojeciEmail_Vraca401IstiOdgovorKaoZaPogresnuLozinku()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        using var pogresnaLozinka = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = clan.Email, lozinka = "PogresnaLozinka1!" });

        using var nepostojeciEmail = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = NoviEmail(), lozinka = TestnaLozinka });

        var ocekivano = await ProblemIzOdgovoraAsync(pogresnaLozinka);
        var dobijeno = await ProblemIzOdgovoraAsync(nepostojeciEmail);
        Assert.Multiple(() =>
        {
            Assert.That(nepostojeciEmail.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(dobijeno.GetProperty("code").GetString(), Is.EqualTo(ocekivano.GetProperty("code").GetString()));
            Assert.That(dobijeno.GetProperty("title").GetString(), Is.EqualTo(ocekivano.GetProperty("title").GetString()));
        });
    }

    [Test]
    public async Task Prijava_TeloBezPolja_Vraca400SaGreskamaZaEmailILozinku()
    {
        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { });

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem.GetProperty("errors").EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "email", "lozinka" }));
        });
    }
}
