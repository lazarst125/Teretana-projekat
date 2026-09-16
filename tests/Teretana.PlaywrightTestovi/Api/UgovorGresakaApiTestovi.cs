using Teretana.PlaywrightTestovi.Infrastruktura;
using static Teretana.PlaywrightTestovi.Infrastruktura.TestniPodaci;

namespace Teretana.PlaywrightTestovi.Api;

/// <summary>
/// Pojedinačni slučajevi grešaka i njihovi kodovi pokriveni su komponentnim testovima. Ovde se jednom, preko pravog
/// Kestrel servera, proverava da svaka vrsta greške stiže do klijenta u istom obliku: application/problem+json,
/// status u telu i kod greške.
/// </summary>
public sealed class UgovorGresakaApiTestovi : ApiTest
{
    [Test]
    public async Task SvakaVrstaGreske_PrekoHttp_StizeKaoProblemDetailsSaStatusomIKodom()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync();
        var idPunogTermina = await Podaci.NoviTerminAsync(trener, kapacitet: 1);
        await Podaci.RezervisiAsync(clan, idPunogTermina);
        var sutra = DateTimeOffset.UtcNow.AddDays(1);
        var proslost = DateTimeOffset.UtcNow.AddHours(-1);

        var bezTokena = await Api.GetAsync("/api/rezervacije/moje");
        var slucajevi = new (string Opis, IAPIResponse Odgovor, int Status, string? Kod)[]
        {
            ("400 nevalidan zahtev", await Api.PostAsync("/api/termini", SaTokenom(trener, new { naziv = "", kapacitet = 0 })), 400, "validacija"),
            ("401 bez tokena", bezTokena, 401, null),
            ("403 pogrešna uloga", await Api.PostAsync("/api/termini", SaTokenom(clan, new { naziv = "Joga", pocetak = sutra, kraj = sutra.AddHours(1), kapacitet = 5 })), 403, null),
            ("404 nepostojeći termin", await Api.GetAsync("/api/termini/999999", SaTokenom(clan)), 404, "termin-ne-postoji"),
            ("409 popunjen termin", await Api.PostAsync($"/api/termini/{idPunogTermina}/rezervacije", SaTokenom(await Podaci.NoviClanAsync())), 409, "termin-popunjen"),
            ("422 početak u prošlosti", await Api.PostAsync("/api/termini", SaTokenom(trener, new { naziv = "Joga", pocetak = proslost, kraj = proslost.AddHours(1), kapacitet = 5 })), 422, "pocetak-u-proslosti"),
        };

        var rezultati = new List<(string Opis, int Status, int OcekivaniStatus, string TipSadrzaja, int StatusUTelu, string? Tip, string? TraceId, string? Kod, string? OcekivaniKod)>();
        foreach (var (opis, odgovor, ocekivaniStatus, ocekivaniKod) in slucajevi)
        {
            var telo = (await odgovor.JsonAsync())!.Value;
            string? Polje(string naziv) => telo.TryGetProperty(naziv, out var polje) ? polje.GetString() : null;
            rezultati.Add((opis, odgovor.Status, ocekivaniStatus, odgovor.Headers["content-type"], telo.GetProperty("status").GetInt32(), Polje("type"), Polje("traceId"), Polje("code"), ocekivaniKod));
        }

        Assert.Multiple(() =>
        {
            foreach (var rezultat in rezultati)
            {
                Assert.That(rezultat.Status, Is.EqualTo(rezultat.OcekivaniStatus), rezultat.Opis);
                Assert.That(rezultat.TipSadrzaja, Does.StartWith("application/problem+json"), rezultat.Opis);
                Assert.That(rezultat.StatusUTelu, Is.EqualTo(rezultat.OcekivaniStatus), rezultat.Opis);
                Assert.That(rezultat.Tip, Does.StartWith("https://"), rezultat.Opis);
                Assert.That(rezultat.TraceId, Is.Not.Empty, rezultat.Opis);
                if (rezultat.OcekivaniKod is not null)
                {
                    Assert.That(rezultat.Kod, Is.EqualTo(rezultat.OcekivaniKod), rezultat.Opis);
                }
            }

            Assert.That(bezTokena.Headers["www-authenticate"], Does.StartWith("Bearer"), "401 mora da najavi Bearer šemu.");
        });
    }
}
