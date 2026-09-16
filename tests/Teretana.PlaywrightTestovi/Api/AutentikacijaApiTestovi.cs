using System.Text.Json;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.Api;

/// <summary>
/// Pojedinačni slučajevi (validacija, zauzet email, pogrešna lozinka, istekao token) pokriveni su
/// komponentnim testovima; ovde se proverava da tok radi preko pravog HTTP-a sa Bearer zaglavljem.
/// </summary>
public sealed class AutentikacijaApiTestovi : ApiTest
{
    [Test]
    public async Task RegistracijaPrijavaIPregledProfila_PrekoHttp_VracajuDokumentovaneStrukture()
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        const string Lozinka = "Lozinka123!";

        var registracija = await Api.PostAsync("/api/auth/registracija", new() { DataObject = new { email, imePrezime = "Api Test", lozinka = Lozinka } });
        var prijava = await Api.PostAsync("/api/auth/prijava", new() { DataObject = new { email, lozinka = Lozinka } });
        var teloPrijave = (await prijava.JsonAsync())!.Value;
        var profil = await Api.GetAsync(registracija.Headers["location"], new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {teloPrijave.GetProperty("token").GetString()}" },
        });
        var teloProfila = (await profil.JsonAsync())!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(registracija.Status, Is.EqualTo(201));
            Assert.That(prijava.Status, Is.EqualTo(200));
            Assert.That(teloPrijave.GetProperty("token").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(teloPrijave.GetProperty("istice").TryGetDateTimeOffset(out _), Is.True);
            Assert.That(profil.Status, Is.EqualTo(200));
            Assert.That(teloProfila.GetProperty("id").ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(teloProfila.GetProperty("email").GetString(), Is.EqualTo(email));
            Assert.That(teloProfila.GetProperty("uloga").GetString(), Is.EqualTo("Clan"));
            Assert.That(teloProfila.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "email", "imePrezime", "uloga" }));
        });
    }

    [Test]
    public async Task ZahtevSaPokvarenimBearerTokenom_Vraca401SaRazlogomUZaglavlju()
    {
        var odgovor = await Api.GetAsync("/api/rezervacije/moje", new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer ovo.nije.token" },
        });

        Assert.Multiple(() =>
        {
            Assert.That(odgovor.Status, Is.EqualTo(401));
            // Zahtev bez tokena i zahtev sa pokvarenim tokenom oba daju 401, ali samo drugi nosi razlog odbijanja.
            Assert.That(odgovor.Headers["www-authenticate"], Does.Contain("invalid_token"));
        });
    }
}
