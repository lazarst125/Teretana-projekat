using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Autentikacija;

public sealed class ProfilTestovi : KomponentniTest
{
    [Test]
    public async Task Ja_SaVazecimTokenom_VracaPodatkeKorisnikaBezLozinke()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(clan);

        using var odgovor = await Klijent.GetAsync("/api/auth/ja");

        var telo = await odgovor.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(telo.GetProperty("id").GetInt32(), Is.EqualTo(clan.Id));
            Assert.That(telo.GetProperty("email").GetString(), Is.EqualTo(clan.Email));
            Assert.That(telo.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "email", "imePrezime", "uloga" }));
        });
    }

    [Test]
    public async Task Ja_BezTokena_Vraca401KaoProblemDetails()
    {
        using var odgovor = await Klijent.GetAsync("/api/auth/ja");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Unauthorized);
        Assert.That(odgovor.Headers.WwwAuthenticate.Select(z => z.Scheme), Does.Contain("Bearer"));
    }

    [Test]
    public async Task Ja_TokenPotpisanDrugimKljucem_Vraca401()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        Klijent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenPotpisanDrugimKljucem(clan));

        using var odgovor = await Klijent.GetAsync("/api/auth/ja");

        Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Ja_TokenKomeJeIstekloTrajanje_Vraca401()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(clan);
        Vreme.Advance(TimeSpan.FromHours(8));

        using var odgovor = await Klijent.GetAsync("/api/auth/ja");

        Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Ja_KorisnikIzTokenaObrisanPosleIzdavanja_Vraca401KorisnikNePostoji()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(clan);
        await SaBazomAsync(db => db.Korisnici.Where(k => k.Id == clan.Id).ExecuteDeleteAsync());

        using var odgovor = await Klijent.GetAsync("/api/auth/ja");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Unauthorized, "korisnik-ne-postoji");
    }

    private string TokenPotpisanDrugimKljucem(Korisnik korisnik)
    {
        var sada = Vreme.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Teretana",
            Audience = "Teretana",
            IssuedAt = sada,
            NotBefore = sada,
            Expires = sada.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("potpuno-drugi-kljuc-koji-aplikacija-ne-poznaje-0123456789")),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = korisnik.Id.ToString(CultureInfo.InvariantCulture),
                ["role"] = korisnik.Uloga.ToString(),
            },
        });
    }
}
