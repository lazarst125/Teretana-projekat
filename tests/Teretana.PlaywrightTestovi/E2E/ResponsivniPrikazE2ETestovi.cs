using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

public sealed class ResponsivniPrikazE2ETestovi : E2ETest
{
    private const int SirinaTelefona = 390;

    [Test]
    public async Task EkranTelefona_RasporedIPolaznici_NemajuHorizontalniSkrolStranice()
    {
        await Page.SetViewportSizeAsync(SirinaTelefona, 844);
        var trener = await Podaci.NoviTrenerAsync();
        var idTermina = await Podaci.NoviTerminAsync(trener);
        await Podaci.RezervisiAsync(await Podaci.NoviClanAsync(), idTermina);
        await PrijaviSeAsync(trener);
        var raspored = new RasporedStrana(Page);
        var polaznici = new PolazniciStrana(Page);

        await raspored.OtvoriAsync();
        await Expect(raspored.Kartica(idTermina)).ToBeVisibleAsync();
        var sirinaRasporeda = await SirinaSadrzajaAsync();
        await polaznici.OtvoriAsync(idTermina);
        await Expect(polaznici.ImenaPotvrdjenih).ToHaveCountAsync(1);
        var sirinaPolaznika = await SirinaSadrzajaAsync();

        Assert.Multiple(() =>
        {
            Assert.That(sirinaRasporeda, Is.LessThanOrEqualTo(SirinaTelefona), "Raspored ima horizontalni skrol.");
            Assert.That(sirinaPolaznika, Is.LessThanOrEqualTo(SirinaTelefona), "Polaznici imaju horizontalni skrol.");
        });
    }

    private Task<int> SirinaSadrzajaAsync() => Page.EvaluateAsync<int>("() => document.documentElement.scrollWidth");
}
