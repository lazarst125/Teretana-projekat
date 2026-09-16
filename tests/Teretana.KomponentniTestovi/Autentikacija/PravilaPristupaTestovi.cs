using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Autentikacija;

/// <summary>
/// Matrica pravila pristupa: svaka zaštićena operacija bez tokena vraća 401, a svaka operacija namenjena drugoj
/// ulozi vraća 403. Zahtevi gađaju termin i rezervaciju koji postoje, pa test dokazuje i da odbijeni zahtev
/// ništa ne menja. Arhitekturni test proverava da atribut postoji; ovi testovi da se pravilo zaista primenjuje.
/// </summary>
public sealed class PravilaPristupaTestovi : KomponentniTest
{
    [TestCase("GET", "/api/auth/ja")]
    [TestCase("PUT", "/api/auth/ja")]
    [TestCase("DELETE", "/api/auth/ja")]
    [TestCase("GET", "/api/termini")]
    [TestCase("GET", "/api/termini/{termin}")]
    [TestCase("POST", "/api/termini")]
    [TestCase("PUT", "/api/termini/{termin}")]
    [TestCase("DELETE", "/api/termini/{termin}")]
    [TestCase("POST", "/api/termini/{termin}/otkazivanje")]
    [TestCase("GET", "/api/termini/{termin}/polaznici")]
    [TestCase("POST", "/api/termini/{termin}/rezervacije")]
    [TestCase("POST", "/api/termini/{termin}/lista-cekanja")]
    [TestCase("GET", "/api/rezervacije/moje")]
    [TestCase("GET", "/api/rezervacije/{rezervacija}")]
    [TestCase("PUT", "/api/rezervacije/{rezervacija}/prisustvo")]
    [TestCase("DELETE", "/api/rezervacije/{rezervacija}")]
    public async Task ZasticenaOperacija_BezTokena_Vraca401IPodaciOstajuIsti(string metod, string sablonPutanje)
    {
        var (termin, rezervacija) = await PostojeciTerminSaRezervacijomAsync();
        using var zahtev = Zahtev(metod, sablonPutanje, termin, rezervacija);

        using var odgovor = await Klijent.SendAsync(zahtev);

        await OcekujOdbijanjePolitikomAsync(odgovor, HttpStatusCode.Unauthorized);
        await OcekujNepromenjenePodatkeAsync(termin, rezervacija);
    }

    [TestCase(Uloga.Clan, "POST", "/api/termini")]
    [TestCase(Uloga.Clan, "PUT", "/api/termini/{termin}")]
    [TestCase(Uloga.Clan, "DELETE", "/api/termini/{termin}")]
    [TestCase(Uloga.Clan, "POST", "/api/termini/{termin}/otkazivanje")]
    [TestCase(Uloga.Clan, "GET", "/api/termini/{termin}/polaznici")]
    [TestCase(Uloga.Clan, "PUT", "/api/rezervacije/{rezervacija}/prisustvo")]
    [TestCase(Uloga.Trener, "POST", "/api/termini/{termin}/rezervacije")]
    [TestCase(Uloga.Trener, "POST", "/api/termini/{termin}/lista-cekanja")]
    [TestCase(Uloga.Trener, "GET", "/api/rezervacije/moje")]
    [TestCase(Uloga.Trener, "GET", "/api/rezervacije/{rezervacija}")]
    [TestCase(Uloga.Trener, "DELETE", "/api/rezervacije/{rezervacija}")]
    public async Task OperacijaNamenjenaDrugojUlozi_Vraca403IPodaciOstajuIsti(Uloga uloga, string metod, string sablonPutanje)
    {
        var (termin, rezervacija) = await PostojeciTerminSaRezervacijomAsync();
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(uloga));
        using var zahtev = Zahtev(metod, sablonPutanje, termin, rezervacija);

        using var odgovor = await Klijent.SendAsync(zahtev);

        await OcekujOdbijanjePolitikomAsync(odgovor, HttpStatusCode.Forbidden);
        await OcekujNepromenjenePodatkeAsync(termin, rezervacija);
    }

    /// <summary>
    /// Zahtev mora da odbije politika pristupa, pre servisa. Servis za tuđi termin takođe vraća 403, ali sa kodom
    /// greške, pa bi bez provere odsustva koda test prošao i kada politika na trenerskoj operaciji nedostaje.
    /// </summary>
    private static async Task OcekujOdbijanjePolitikomAsync(HttpResponseMessage odgovor, HttpStatusCode status)
    {
        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(status));
            Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo((int)status));
            Assert.That(problem.TryGetProperty("code", out _), Is.False, "Odbijanje politikom nema kod domenske greške.");
        });
    }

    private async Task<(Termin Termin, Rezervacija Rezervacija)> PostojeciTerminSaRezervacijomAsync()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), naziv: "Termin pod zaštitom");
        var rezervacija = await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        return (termin, rezervacija);
    }

    private async Task OcekujNepromenjenePodatkeAsync(Termin termin, Rezervacija rezervacija)
    {
        var (brojTermina, terminNepromenjen, rezervacijaNepromenjena) = await SaBazomAsync(async db => (
            await db.Termini.CountAsync(),
            await db.Termini.AnyAsync(t => t.Id == termin.Id && t.Naziv == termin.Naziv && t.Status == StatusTermina.Aktivan),
            await db.Rezervacije.AnyAsync(r => r.Id == rezervacija.Id && r.Status == StatusRezervacije.Potvrdjena && r.Prisustvovao == null)));

        Assert.Multiple(() =>
        {
            Assert.That(brojTermina, Is.EqualTo(1), "Odbijen zahtev ne sme da kreira termin.");
            Assert.That(terminNepromenjen, Is.True, "Odbijen zahtev ne sme da izmeni, obriše ni otkaže termin.");
            Assert.That(rezervacijaNepromenjena, Is.True, "Odbijen zahtev ne sme da otkaže rezervaciju ni upiše prisustvo.");
        });
    }

    private static HttpRequestMessage Zahtev(string metod, string sablonPutanje, Termin termin, Rezervacija rezervacija)
    {
        var putanja = sablonPutanje
            .Replace("{termin}", termin.Id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{rezervacija}", rezervacija.Id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        object ispravnoTelo = sablonPutanje switch
        {
            "/api/auth/ja" => new { imePrezime = "Pokušaj bez prava" },
            var putanjaSablona when putanjaSablona.EndsWith("/prisustvo", StringComparison.Ordinal) => new { prisustvovao = true },
            _ => new { naziv = "Pokušaj bez prava", pocetak = TestniEntiteti.Sada.AddDays(5), kraj = TestniEntiteti.Sada.AddDays(5).AddHours(1), kapacitet = 5 },
        };

        return new HttpRequestMessage(new HttpMethod(metod), putanja)
        {
            Content = metod is "POST" or "PUT" ? JsonContent.Create(ispravnoTelo) : null,
        };
    }
}
