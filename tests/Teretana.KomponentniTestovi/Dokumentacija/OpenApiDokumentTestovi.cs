using System.Net.Http.Json;
using System.Text.Json;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Dokumentacija;

/// <summary>
/// OpenAPI dokument mora da opisuje isti ugovor koji proveravaju ostali testovi: istu listu operacija i iste
/// status kodove po operaciji, pa promena API-ja bez dopune dokumentacije obara ovaj test.
/// </summary>
public sealed class OpenApiDokumentTestovi : KomponentniTest
{
    private const string PutanjaDokumenta = "/openapi/v1.json";

    private static readonly Dictionary<string, string[]> UgovorOperacija = new()
    {
        ["POST /api/auth/registracija"] = ["201", "400", "409"],
        ["POST /api/auth/prijava"] = ["200", "400", "401"],
        ["GET /api/auth/ja"] = ["200", "401"],
        ["PUT /api/auth/ja"] = ["200", "400", "401"],
        ["DELETE /api/auth/ja"] = ["204", "401", "409"],
        ["GET /api/termini"] = ["200", "400", "401"],
        ["POST /api/termini"] = ["201", "400", "401", "403", "422"],
        ["GET /api/termini/{id}"] = ["200", "401", "404"],
        ["PUT /api/termini/{id}"] = ["200", "400", "401", "403", "404", "409", "422"],
        ["DELETE /api/termini/{id}"] = ["204", "401", "403", "404", "409"],
        ["POST /api/termini/{id}/otkazivanje"] = ["200", "401", "403", "404", "409", "422"],
        ["GET /api/termini/{id}/polaznici"] = ["200", "401", "403", "404"],
        ["POST /api/termini/{idTermina}/rezervacije"] = ["201", "401", "403", "404", "409", "422"],
        ["POST /api/termini/{idTermina}/lista-cekanja"] = ["201", "401", "403", "404", "409", "422"],
        ["GET /api/rezervacije/moje"] = ["200", "400", "401", "403"],
        ["GET /api/rezervacije/{id}"] = ["200", "401", "403", "404"],
        ["PUT /api/rezervacije/{id}/prisustvo"] = ["200", "400", "401", "403", "404", "409", "422"],
        ["DELETE /api/rezervacije/{id}"] = ["204", "401", "403", "404", "409", "422"],
    };

    [Test]
    public async Task Dokument_ZaSvakuOperaciju_DokumentujeTacnoStatusKodoveIzUgovora()
    {
        var dokument = await DokumentAsync();

        var operacije = Operacije(dokument).ToDictionary(o => o.Kljuc, o => o.Kodovi);

        Assert.Multiple(() =>
        {
            Assert.That(operacije.Keys, Is.EquivalentTo(UgovorOperacija.Keys));
            foreach (var (kljuc, kodovi) in UgovorOperacija)
            {
                Assert.That(operacije.GetValueOrDefault(kljuc), Is.EquivalentTo(kodovi), kljuc);
            }
        });
    }

    [Test]
    public async Task Dokument_ZasticeneOperacije_ZahtevajuBearerTokenAAnonimneNe()
    {
        var dokument = await DokumentAsync();

        var sema = dokument.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Multiple(() =>
        {
            Assert.That(sema.GetProperty("type").GetString(), Is.EqualTo("http"));
            Assert.That(sema.GetProperty("scheme").GetString(), Is.EqualTo("bearer"));
            Assert.That(TraziBearer(Operacija(dokument, "/api/termini", "post")), Is.True);
            Assert.That(TraziBearer(Operacija(dokument, "/api/rezervacije/{id}", "delete")), Is.True);
            Assert.That(TraziBearer(Operacija(dokument, "/api/auth/prijava", "post")), Is.False);
        });
    }

    [Test]
    public async Task Dokument_ZahtevZaTermin_ImaOpisIPrimerAStatusTerminaJeTekstualniEnum()
    {
        var dokument = await DokumentAsync();

        var seme = dokument.GetProperty("components").GetProperty("schemas");
        var naziv = seme.GetProperty("TerminZahtev").GetProperty("properties").GetProperty("naziv");
        var status = Razresi(dokument, seme.GetProperty("TerminStavkaOdgovor").GetProperty("properties").GetProperty("status"));
        Assert.Multiple(() =>
        {
            Assert.That(naziv.TryGetProperty("description", out _), Is.True, "Polje naziv nema opis.");
            Assert.That(Primer(naziv), Is.EqualTo("Joga za početnike"));
            Assert.That(status.GetProperty("enum").EnumerateArray().Select(v => v.GetString()), Is.EquivalentTo(new[] { "Aktivan", "Otkazan" }));
        });
    }

    private async Task<JsonElement> DokumentAsync() =>
        await Klijent.GetFromJsonAsync<JsonElement>(PutanjaDokumenta);

    private static IEnumerable<(string Kljuc, string[] Kodovi)> Operacije(JsonElement dokument) =>
        dokument.GetProperty("paths").EnumerateObject()
            .Where(putanja => putanja.Name.StartsWith("/api/", StringComparison.Ordinal))
            .SelectMany(putanja => putanja.Value.EnumerateObject()
                .Where(metod => metod.Value.TryGetProperty("responses", out _))
                .Select(metod => (
                    $"{metod.Name.ToUpperInvariant()} {putanja.Name}",
                    metod.Value.GetProperty("responses").EnumerateObject().Select(odgovor => odgovor.Name).ToArray())));

    private static JsonElement Operacija(JsonElement dokument, string putanja, string metod) =>
        dokument.GetProperty("paths").GetProperty(putanja).GetProperty(metod);

    private static bool TraziBearer(JsonElement operacija) =>
        operacija.TryGetProperty("security", out var zahtevi)
        && zahtevi.EnumerateArray().Any(zahtev => zahtev.TryGetProperty("Bearer", out _));

    private static string? Primer(JsonElement svojstvo) =>
        svojstvo.TryGetProperty("example", out var primer) ? primer.GetString()
        : svojstvo.TryGetProperty("examples", out var primeri) ? primeri.EnumerateArray().First().GetString()
        : null;

    /// <summary>Prati $ref (i omotače allOf/anyOf/oneOf) do stvarne šeme u components.</summary>
    private static JsonElement Razresi(JsonElement dokument, JsonElement sema)
    {
        if (sema.TryGetProperty("$ref", out var referenca))
        {
            var naziv = referenca.GetString()!.Split('/')[^1];
            return Razresi(dokument, dokument.GetProperty("components").GetProperty("schemas").GetProperty(naziv));
        }

        foreach (var omotac in new[] { "allOf", "anyOf", "oneOf" })
        {
            if (sema.TryGetProperty(omotac, out var delovi))
            {
                return Razresi(dokument, delovi.EnumerateArray().First(deo => deo.TryGetProperty("$ref", out _) || deo.TryGetProperty("enum", out _)));
            }
        }

        return sema;
    }
}
