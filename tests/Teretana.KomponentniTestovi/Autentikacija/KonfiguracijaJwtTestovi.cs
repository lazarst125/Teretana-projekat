using Teretana.KomponentniTestovi.Infrastruktura;
using Teretana.Testovi.Zajednicko;

namespace Teretana.KomponentniTestovi.Autentikacija;

[Parallelizable(ParallelScope.All)]
[Category("Komponentni")]
public sealed class KonfiguracijaJwtTestovi
{
    [Test]
    public async Task Pokretanje_VanDevelopmentaBezJwtKljuca_NeUspevaSaJasnomPorukom()
    {
        await using var aplikacija = new TeretanaAplikacija(
            "Production",
            konfiguracija => konfiguracija.Remove(TestnaKonfiguracija.KljucJwtKljuca),
            _ => { });

        var greska = Assert.Catch(() => aplikacija.CreateClient());

        Assert.That(greska?.ToString(), Does.Contain("Jwt:Kljuc"));
    }
}
