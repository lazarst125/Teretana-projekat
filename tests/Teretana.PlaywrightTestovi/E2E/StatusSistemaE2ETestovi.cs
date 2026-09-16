using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

public sealed class StatusSistemaE2ETestovi : E2ETest
{
    [Test]
    public async Task ProveraStatusa_KadaJeSistemIspravan_PrikazujeDaSistemRadi()
    {
        var pocetna = new PocetnaStrana(Page);
        await pocetna.OtvoriAsync();

        await pocetna.ProveriStatusSistemaAsync();

        await Expect(pocetna.StatusSistemaRezultat).ToHaveAttributeAsync("data-stanje", "ispravan");
        await Expect(pocetna.StatusSistemaRezultat).ToHaveTextAsync("Sistem radi ispravno.");
    }
}
