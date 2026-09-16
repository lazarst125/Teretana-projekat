using System.Text.RegularExpressions;
using Teretana.PlaywrightTestovi.E2E.Stranice;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.E2E;

/// <summary>Rad bez miša: preskakanje navigacije i popunjavanje forme prijave samo tastaturom.</summary>
public sealed class PristupacnostE2ETestovi : E2ETest
{
    [Test]
    public async Task PrviTabNaStrani_OtvaraPrecicuKojaPomeraFokusNaGlavniSadrzaj()
    {
        await PrijaviSeAsync(await Podaci.NoviClanAsync());
        var precica = Page.GetByTestId("preskoci-na-sadrzaj");

        await Page.Keyboard.PressAsync("Tab");

        await Expect(precica).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Enter");

        // Glavni sadržaj nema data-testid, pa se fokus čita iz dokumenta umesto preko lokatora.
        var fokusiraniElement = await Page.EvaluateAsync<string>("() => document.activeElement.id");
        Assert.That(fokusiraniElement, Is.EqualTo("sadrzaj"));
    }

    [Test]
    public async Task FormaPrijave_PopunjenaSamoTastaturom_PrijavljujeKorisnika()
    {
        var clan = await Podaci.NoviClanAsync("Tastatura Korisnik");
        var prijava = new PrijavaStrana(Page);
        await prijava.OtvoriAsync();

        await prijava.PrijaviSeTastaturomAsync(clan.Email, TestniPodaci.Lozinka);

        await Expect(Page).ToHaveURLAsync(new Regex("#/termini$"));
        await Expect(new Zaglavlje(Page).ImeKorisnika).ToHaveTextAsync("Tastatura Korisnik");
    }
}
