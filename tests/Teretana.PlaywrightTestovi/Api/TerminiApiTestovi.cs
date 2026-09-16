using System.Text.Json;
using Teretana.PlaywrightTestovi.Infrastruktura;
using static Teretana.PlaywrightTestovi.Infrastruktura.TestniPodaci;

namespace Teretana.PlaywrightTestovi.Api;

/// <summary>
/// Pravila i status kodovi za termine pokriveni su komponentnim testovima; ovde se proverava tok između
/// dve role preko pravog HTTP-a, uključujući vreme sa zonom u query string-u.
/// </summary>
public sealed class TerminiApiTestovi : ApiTest
{
    [Test]
    public async Task TrenerKreiraIOtkazujeTermin_ClanGaNalaziFilteromIVidiOtkazivanje_PrekoHttp()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var clan = await Podaci.NoviClanAsync();
        // Offset +02:00 u query string-u proverava da se znak '+' enkoduje i da server poredi UTC trenutke.
        var pocetak = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(10), TimeSpan.Zero).ToOffset(TimeSpan.FromHours(2));

        var kreiranje = await Api.PostAsync("/api/termini", SaTokenom(trener, new { naziv = "Api termin", pocetak, kraj = pocetak.AddHours(1), kapacitet = 5 }));
        var idTermina = (await kreiranje.JsonAsync())!.Value.GetProperty("id").GetInt32();
        var lista = await Api.GetAsync("/api/termini", SaTokenom(clan, parametri: new()
        {
            ["trenerId"] = trener.Id,
            ["od"] = pocetak.AddMinutes(-1).ToString("O"),
            ["do"] = pocetak.AddMinutes(1).ToString("O"),
            ["samoSlobodni"] = true,
        }));
        var teloListe = (await lista.JsonAsync())!.Value;
        var otkazivanje = await Api.PostAsync($"/api/termini/{idTermina}/otkazivanje", SaTokenom(trener));
        var detalj = await Api.GetAsync($"/api/termini/{idTermina}", SaTokenom(clan));
        var teloDetalja = (await detalj.JsonAsync())!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(kreiranje.Status, Is.EqualTo(201));
            Assert.That(lista.Status, Is.EqualTo(200));
            Assert.That(teloListe.GetProperty("stavke").EnumerateArray().Select(t => t.GetProperty("id").GetInt32()), Is.EqualTo(new[] { idTermina }));
            Assert.That(otkazivanje.Status, Is.EqualTo(200));
            Assert.That(teloDetalja.GetProperty("status").GetString(), Is.EqualTo("Otkazan"));
            Assert.That(teloDetalja.GetProperty("slobodnaMesta").GetInt32(), Is.Zero);
            Assert.That(teloDetalja.GetProperty("pocetak").GetDateTimeOffset(), Is.EqualTo(pocetak));
            Assert.That(teloDetalja.GetProperty("mojaPrijava").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task ListaTermina_SaStranicomIVelicinomStraniceUQueryStringu_VracaTrazeniDeoIBrojeveStranica()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var prviPocetak = DateTimeOffset.UtcNow.AddDays(3);
        for (var redniBroj = 1; redniBroj <= 3; redniBroj++)
        {
            await Podaci.NoviTerminAsync(trener, pocetak: prviPocetak.AddHours(redniBroj), naziv: $"Termin {redniBroj}");
        }

        var odgovor = await Api.GetAsync("/api/termini", SaTokenom(trener, parametri: new()
        {
            ["trenerId"] = trener.Id,
            ["stranica"] = 2,
            ["velicinaStranice"] = 2,
            ["sortiranje"] = "pocetak",
        }));
        var telo = (await odgovor.JsonAsync())!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(odgovor.Status, Is.EqualTo(200));
            Assert.That(telo.GetProperty("stranica").GetInt32(), Is.EqualTo(2));
            Assert.That(telo.GetProperty("velicinaStranice").GetInt32(), Is.EqualTo(2));
            Assert.That(telo.GetProperty("ukupnoStavki").GetInt32(), Is.EqualTo(3));
            Assert.That(telo.GetProperty("ukupnoStranica").GetInt32(), Is.EqualTo(2));
            Assert.That(
                telo.GetProperty("stavke").EnumerateArray().Select(termin => termin.GetProperty("naziv").GetString()),
                Is.EqualTo(new[] { "Termin 3" }));
        });
    }

    [Test]
    public async Task ListaTermina_SaPrevelikomVelicinomStranice_Vraca400SaImenomParametraIzQueryStringa()
    {
        var trener = await Podaci.NoviTrenerAsync();

        var odgovor = await Api.GetAsync("/api/termini", SaTokenom(trener, parametri: new() { ["velicinaStranice"] = 101 }));
        var telo = (await odgovor.JsonAsync())!.Value;
        var imaKljucParametra = telo.GetProperty("errors").TryGetProperty("velicinaStranice", out var poruke);
        var tekstPoruka = imaKljucParametra ? string.Join(" ", poruke.EnumerateArray().Select(poruka => poruka.GetString())) : string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(odgovor.Status, Is.EqualTo(400));
            Assert.That(odgovor.Headers["content-type"], Does.StartWith("application/problem+json"));
            Assert.That(telo.GetProperty("code").GetString(), Is.EqualTo("validacija"));
            Assert.That(imaKljucParametra, Is.True, "Greška mora da nosi ime parametra onako kako ga je klijent poslao.");
            Assert.That(tekstPoruka, Does.Contain("između 1 i 100"));
        });
    }

    [Test]
    public async Task TrenerMenjaPaBriseTerminBezPrijava_PosleBrisanjaTerminViseNePostoji_PrekoHttp()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener, kapacitet: 5);
        var noviPocetak = DateTimeOffset.UtcNow.AddDays(4);

        var izmena = await Api.PutAsync($"/api/termini/{idTermina}", SaTokenom(trener, new { naziv = "Izmenjen preko HTTP-a", pocetak = noviPocetak, kraj = noviPocetak.AddHours(2), kapacitet = 7 }));
        var teloIzmene = (await izmena.JsonAsync())!.Value;
        var brisanje = await Api.DeleteAsync($"/api/termini/{idTermina}", SaTokenom(trener));
        var posleBrisanja = await Api.GetAsync($"/api/termini/{idTermina}", SaTokenom(trener));
        var problem = (await posleBrisanja.JsonAsync())!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(izmena.Status, Is.EqualTo(200));
            Assert.That(teloIzmene.GetProperty("naziv").GetString(), Is.EqualTo("Izmenjen preko HTTP-a"));
            Assert.That(teloIzmene.GetProperty("kapacitet").GetInt32(), Is.EqualTo(7));
            Assert.That(brisanje.Status, Is.EqualTo(204));
            Assert.That(posleBrisanja.Status, Is.EqualTo(404));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo("termin-ne-postoji"));
        });
    }
}
