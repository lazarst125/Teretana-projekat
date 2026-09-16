using System.Globalization;
using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

/// <summary>Parametri liste termina uneti kroz UI: trener, samo slobodni, sortiranje, veličina strane i straničenje.</summary>
public sealed class RasporedE2ETestovi : E2ETest
{
    [Test]
    public async Task FilteriTreneraISlobodnihMestaSaSortiranjemPoNazivu_PrikazujuSamoOdgovarajuceTermineUTrazenomRedosledu()
    {
        var trener = await Podaci.NoviTrenerAsync("Trener Filtera");
        var clan = await Podaci.NoviClanAsync();
        await Podaci.NoviTerminAsync(trener, kapacitet: 5, naziv: "B joga");
        await Podaci.NoviTerminAsync(trener, kapacitet: 5, naziv: "A pilates");
        await Podaci.RezervisiAsync(clan, await Podaci.NoviTerminAsync(trener, kapacitet: 1, naziv: "C pun termin"));
        await Podaci.NoviTerminAsync(await Podaci.NoviTrenerAsync("Drugi Trener"), naziv: "D tuđi termin");
        await PrijaviSeAsync(clan);
        var raspored = new RasporedStrana(Page);
        await raspored.OtvoriAsync();

        await raspored.FiltrirajAsync(idTrenera: trener.Id, samoSlobodni: true, sortiranje: "naziv");

        await Expect(raspored.NaziviTermina).ToHaveTextAsync(["A pilates", "B joga"]);
    }

    [Test]
    public async Task MojiTerminiSaDesetPoStrani_SledecaStrana_PrikazujePreostaliTermin()
    {
        var trener = await Podaci.NoviTrenerAsync();
        var prviPocetak = DateTimeOffset.UtcNow.AddDays(3);
        for (var redniBroj = 1; redniBroj <= 11; redniBroj++)
        {
            await Podaci.NoviTerminAsync(trener, pocetak: prviPocetak.AddHours(redniBroj), naziv: $"Termin {redniBroj.ToString("00", CultureInfo.InvariantCulture)}");
        }

        await PrijaviSeAsync(trener);
        var raspored = new RasporedStrana(Page);
        await raspored.OtvoriMojeTermineAsync();
        await raspored.FiltrirajAsync(velicinaStranice: 10);
        await Expect(raspored.Kartice).ToHaveCountAsync(10);
        await Expect(raspored.InfoStranice).ToHaveTextAsync("Strana 1 od 2 (ukupno 11)");

        await raspored.SledecaStranaAsync();

        await Expect(raspored.NaziviTermina).ToHaveTextAsync(["Termin 11"]);
        await Expect(raspored.InfoStranice).ToHaveTextAsync("Strana 2 od 2 (ukupno 11)");
    }
}
