namespace Teretana.PlaywrightTestovi.E2E.Stranice;

public sealed class MojeRezervacijeStrana(IPage stranica)
{
    public ILocator PraznoStanje => stranica.GetByTestId("rezervacije-prazno");

    public ILocator Kartice => stranica.GetByTestId("rezervacija-kartica");

    public ILocator Statusi => Kartice.GetByTestId("rezervacija-status");

    public ILocator Kartica(int idRezervacije) =>
        stranica.Locator($"[data-testid='rezervacija-kartica'][data-rezervacija-id='{idRezervacije}']");

    public ILocator Status(int idRezervacije) => Kartica(idRezervacije).GetByTestId("rezervacija-status");

    public ILocator TerminOtkazan(int idRezervacije) => Kartica(idRezervacije).GetByTestId("rezervacija-termin-otkazan");

    public async Task OtvoriAsync() => await stranica.GotoAsync("/#/rezervacije");

    /// <param name="status">Vrednost iz padajuće liste: prazno za sve, Potvrdjena, NaCekanju ili Otkazana.</param>
    public async Task FiltrirajAsync(string status)
    {
        await stranica.GetByTestId("rezervacije-filter-status").SelectOptionAsync(status);
        await stranica.GetByTestId("rezervacije-filter-primeni").ClickAsync();
    }
}

public sealed class RezervacijaDetaljStrana(IPage stranica)
{
    public ILocator Status => stranica.GetByTestId("rezervacija-detalj").GetByTestId("rezervacija-status");

    public ILocator DugmeOtkazi => stranica.GetByTestId("rezervacija-otkazi");

    public ILocator OtkazivanjeNedostupno => stranica.GetByTestId("rezervacija-otkazivanje-nedostupno");

    public async Task OtvoriAsync(int idRezervacije) => await stranica.GotoAsync($"/#/rezervacije/{idRezervacije}");

    /// <summary>Otvara dijalog potvrde bez potvrđivanja.</summary>
    public async Task ZapocniOtkazivanjeAsync() => await DugmeOtkazi.ClickAsync();

    public async Task OtkaziAsync()
    {
        await ZapocniOtkazivanjeAsync();
        await new DijalogPotvrde(stranica).PotvrdiAsync();
    }
}

public sealed class DijalogPotvrde(IPage stranica)
{
    public ILocator Dijalog => stranica.GetByTestId("potvrda-dijalog");

    public async Task PotvrdiAsync() => await stranica.GetByTestId("potvrda-da").ClickAsync();

    public async Task OdustaniAsync() => await stranica.GetByTestId("potvrda-ne").ClickAsync();

    public async Task ZatvoriTasteromEscapeAsync() => await stranica.Keyboard.PressAsync("Escape");
}
