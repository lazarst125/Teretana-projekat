using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Podaci;
using Teretana.KomponentniTestovi.Infrastruktura;
using Teretana.Testovi.Zajednicko;

namespace Teretana.KomponentniTestovi.BazaPodataka;

/// <summary>
/// Pokretanje kao iz čistog klona: Development okruženje, bez JWT ključa u konfiguraciji.
/// </summary>
public sealed class PokretanjeUDevelopmentOkruzenjuTestovi : KomponentniTest
{
    protected override string Okruzenje => "Development";

    [Test]
    public async Task Pokretanje_UDevelopmentOkruzenju_UpisujeSveDemonstracioneTermine()
    {
        var nazivi = await SaBazomAsync(db => db.Termini.Select(t => t.Naziv).ToListAsync());

        Assert.That(nazivi, Is.EquivalentTo(new[]
        {
            PocetniPodaci.Termini.BezPrijava,
            PocetniPodaci.Termini.SlobodnaMesta,
            PocetniPodaci.Termini.Pun,
            PocetniPodaci.Termini.SaListomCekanja,
            PocetniPodaci.Termini.RokZaOtkazivanjeProsao,
            PocetniPodaci.Termini.Zavrsen,
            PocetniPodaci.Termini.Otkazan,
        }));
    }

    [Test]
    public async Task PrijavaDemoTrenera_BezPodesenogJwtKljuca_UspevaSaPrivremenimKljucem()
    {
        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = PocetniPodaci.Nalozi.Trener1, lozinka = PocetniPodaci.LozinkaTrenera });

        Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    protected override void PodesiKonfiguraciju(IDictionary<string, string?> konfiguracija) =>
        konfiguracija.Remove(TestnaKonfiguracija.KljucJwtKljuca);
}
