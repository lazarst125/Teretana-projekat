using System.Text.RegularExpressions;
using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

/// <summary>
/// Filteri rasporeda koje korisnik unosi kroz polja za datum i padajuću listu sortiranja, i ponašanje ekrana
/// kada lista termina ne stigne. Sve tri provere zahtevaju browser: vrednosti se unose u polja, a prekid veze
/// se pravi presretanjem mrežnog poziva.
/// </summary>
public sealed class FilteriRasporedaE2ETestovi : E2ETest
{
    [Test]
    public async Task FilterOdDoDatuma_OstavljaSamoTerminUOpsegu_APonistavanjeVracaOstale()
    {
        var trener = await Podaci.NoviTrenerAsync("Trener Datuma");
        var danas = DateOnly.FromDateTime(DateTime.Now);
        var trazeniDan = danas.AddDays(5);
        var idRanijeg = await Podaci.NoviTerminAsync(trener, pocetak: LokalnoPodne(danas.AddDays(2)), naziv: "Pre opsega");
        await Podaci.NoviTerminAsync(trener, pocetak: LokalnoPodne(trazeniDan), naziv: "U opsegu");
        await Podaci.NoviTerminAsync(trener, pocetak: LokalnoPodne(danas.AddDays(9)), naziv: "Posle opsega");
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var raspored = new RasporedStrana(Page);
        await raspored.OtvoriAsync();

        await raspored.FiltrirajAsync(idTrenera: trener.Id, odDatuma: trazeniDan, doDatuma: trazeniDan);

        await Expect(raspored.NaziviTermina).ToHaveTextAsync(["U opsegu"]);

        await raspored.PonistiFiltereAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("#/termini$"));
        await Expect(raspored.Kartica(idRanijeg)).ToBeVisibleAsync();
    }

    [Test]
    public async Task SortiranjePoSlobodnimMestima_RedjaTermineOdNajvisePremaNajmanjeSlobodnih()
    {
        var trener = await Podaci.NoviTrenerAsync("Trener Sortiranja");
        await Podaci.NoviTerminAsync(trener, kapacitet: 2, naziv: "Mali");
        var idVelikog = await Podaci.NoviTerminAsync(trener, kapacitet: 9, naziv: "Veliki");
        await Podaci.NoviTerminAsync(trener, kapacitet: 5, naziv: "Srednji");
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var raspored = new RasporedStrana(Page);
        await raspored.OtvoriAsync();

        await raspored.FiltrirajAsync(idTrenera: trener.Id, sortiranje: "-slobodnaMesta");

        await Expect(raspored.NaziviTermina).ToHaveTextAsync(["Veliki", "Srednji", "Mali"]);
        await Expect(raspored.Zauzetost(idVelikog)).ToHaveTextAsync("Zauzeto 0 od 9");
    }

    [Test]
    public async Task ListaTerminaNeStigne_PrikazujeStanjeGreske_APokusajPonovoUcitavaTermine()
    {
        var trener = await Podaci.NoviTrenerAsync("Trener Oporavka");
        var idTermina = await Podaci.NoviTerminAsync(trener, naziv: "Vidljiv posle oporavka");
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var raspored = new RasporedStrana(Page);
        // Isti obrazac hvata i poziv za spisak trenera, pa ekran ostaje bez ijednog podatka sa servera.
        var pozivListe = new Regex(@"/api/termini(\?|$)");
        await Page.RouteAsync(pozivListe, ruta => ruta.AbortAsync());

        await raspored.OtvoriAsync();
        // Prijava već ostavlja stranu na rasporedu, pa tek ponovno učitavanje šalje presretnuti zahtev.
        await Page.ReloadAsync();

        await Expect(raspored.StanjeGreske).ToContainTextAsync("Server nije dostupan");

        await Page.UnrouteAsync(pozivListe);
        await raspored.DugmePokusajPonovo.ClickAsync();

        await Expect(raspored.Kartica(idTermina)).ToBeVisibleAsync();
        await Expect(raspored.StanjeGreske).ToHaveCountAsync(0);
    }

    /// <summary>Podne u zoni računara na kom test radi, da dan u polju za datum odgovara danu termina.</summary>
    private static DateTimeOffset LokalnoPodne(DateOnly dan) =>
        new(DateTime.SpecifyKind(dan.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Local));
}
