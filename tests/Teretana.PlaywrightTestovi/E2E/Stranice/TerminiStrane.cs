using System.Globalization;

namespace Teretana.PlaywrightTestovi.E2E.Stranice;

/// <summary>Raspored termina i trenerov spisak sopstvenih termina (isti ekran sa filterom po treneru).</summary>
public sealed class RasporedStrana(IPage stranica)
{
    public ILocator Kartice => stranica.GetByTestId("termin-kartica");

    public ILocator NaziviTermina => Kartice.GetByTestId("termin-detalji");

    public ILocator InfoStranice => stranica.GetByTestId("termini-stranicenje-info");

    public ILocator Kartica(int idTermina) =>
        stranica.Locator($"[data-testid='termin-kartica'][data-termin-id='{idTermina}']");

    public ILocator Stanje(int idTermina) => Kartica(idTermina).GetByTestId("termin-stanje");

    public ILocator Zauzetost(int idTermina) => Kartica(idTermina).GetByTestId("termin-zauzetost");

    public ILocator PraznoStanje => stranica.GetByTestId("termini-prazno");

    public ILocator StanjeGreske => stranica.GetByTestId("termini-greska");

    public ILocator DugmePokusajPonovo => stranica.GetByTestId("termini-ponovo");

    public async Task OtvoriAsync() => await stranica.GotoAsync("/#/termini");

    public async Task OtvoriMojeTermineAsync() => await stranica.GotoAsync("/#/moji-termini");

    public async Task OtvoriDetaljAsync(int idTermina) => await Kartica(idTermina).GetByTestId("termin-detalji").ClickAsync();

    /// <summary>Postavlja samo zadate filtere (ostali zadržavaju trenutnu vrednost) i primenjuje ih.</summary>
    public async Task FiltrirajAsync(
        int? idTrenera = null,
        bool samoSlobodni = false,
        string? sortiranje = null,
        int? velicinaStranice = null,
        DateOnly? odDatuma = null,
        DateOnly? doDatuma = null)
    {
        if (odDatuma is { } od)
        {
            await stranica.GetByTestId("filter-od").FillAsync(od.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (doDatuma is { } doKrajnjeg)
        {
            await stranica.GetByTestId("filter-do").FillAsync(doKrajnjeg.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (idTrenera is { } trener)
        {
            await stranica.GetByTestId("filter-trener").SelectOptionAsync(trener.ToString(CultureInfo.InvariantCulture));
        }

        if (samoSlobodni)
        {
            await stranica.GetByTestId("filter-samo-slobodni").CheckAsync();
        }

        if (sortiranje is not null)
        {
            await stranica.GetByTestId("filter-sortiranje").SelectOptionAsync(sortiranje);
        }

        if (velicinaStranice is { } velicina)
        {
            await stranica.GetByTestId("filter-velicina-stranice").SelectOptionAsync(velicina.ToString(CultureInfo.InvariantCulture));
        }

        await stranica.GetByTestId("filter-primeni").ClickAsync();
    }

    public async Task SledecaStranaAsync() => await stranica.GetByTestId("termini-stranicenje-sledeca").ClickAsync();

    /// <summary>Vraća listu na adresu bez ijednog filtera.</summary>
    public async Task PonistiFiltereAsync() => await stranica.GetByTestId("filter-ponisti").ClickAsync();
}

public sealed class TerminDetaljStrana(IPage stranica)
{
    private ILocator Detalj => stranica.GetByTestId("termin-detalj");

    public ILocator Naziv => stranica.GetByTestId("termin-naziv");

    public ILocator Stanje => Detalj.GetByTestId("termin-stanje");

    public ILocator Kapacitet => stranica.GetByTestId("termin-kapacitet");

    public ILocator SlobodnaMesta => stranica.GetByTestId("termin-slobodna-mesta");

    public ILocator MojaPrijava => stranica.GetByTestId("termin-moja-prijava");

    public ILocator Napomena => stranica.GetByTestId("termin-napomena");

    public ILocator GreskaAkcije => stranica.GetByTestId("termin-akcija-greska");

    public ILocator StanjeGreske => stranica.GetByTestId("termin-greska");

    public ILocator DugmePokusajPonovo => stranica.GetByTestId("termin-ponovo");

    public ILocator DugmeRezervisi => stranica.GetByTestId("termin-rezervisi");

    public ILocator DugmeListaCekanja => stranica.GetByTestId("termin-lista-cekanja");

    public async Task OtvoriAsync(int idTermina) => await stranica.GotoAsync($"/#/termini/{idTermina}");

    public async Task RezervisiAsync() => await DugmeRezervisi.ClickAsync();

    public async Task PrijaviNaListuCekanjaAsync() => await DugmeListaCekanja.ClickAsync();

    public async Task OtvoriIzmenuAsync() => await stranica.GetByTestId("termin-izmeni").ClickAsync();

    public async Task OtvoriPolazniceAsync() => await stranica.GetByTestId("termin-polaznici").ClickAsync();

    public async Task OtkaziTerminAsync()
    {
        await stranica.GetByTestId("termin-otkazi").ClickAsync();
        await new DijalogPotvrde(stranica).PotvrdiAsync();
    }

    public async Task ObrisiAsync()
    {
        await stranica.GetByTestId("termin-obrisi").ClickAsync();
        await new DijalogPotvrde(stranica).PotvrdiAsync();
    }
}

public sealed class TerminFormaStrana(IPage stranica)
{
    private const string FormatLokalnogVremena = "yyyy-MM-dd'T'HH:mm";

    public ILocator Naziv => stranica.GetByTestId("termin-forma-naziv");

    public ILocator GreskaForme => stranica.GetByTestId("termin-forma-greska");

    public ILocator GreskaPolja(string polje) => stranica.GetByTestId($"termin-forma-{polje}-greska");

    public async Task OtvoriNovuAsync() => await stranica.GotoAsync("/#/termini/novi");

    /// <summary>Početak i kraj su lokalna vremena, kao što ih korisnik unosi u polja datuma i vremena.</summary>
    public async Task PopuniAsync(string naziv, DateTime pocetak, DateTime kraj, int kapacitet)
    {
        await Naziv.FillAsync(naziv);
        await stranica.GetByTestId("termin-forma-pocetak").FillAsync(pocetak.ToString(FormatLokalnogVremena, CultureInfo.InvariantCulture));
        await stranica.GetByTestId("termin-forma-kraj").FillAsync(kraj.ToString(FormatLokalnogVremena, CultureInfo.InvariantCulture));
        await PostaviKapacitetAsync(kapacitet);
    }

    public async Task PostaviKapacitetAsync(int kapacitet) =>
        await stranica.GetByTestId("termin-forma-kapacitet").FillAsync(kapacitet.ToString(CultureInfo.InvariantCulture));

    public async Task SacuvajAsync() => await stranica.GetByTestId("termin-forma-sacuvaj").ClickAsync();
}

public sealed class PolazniciStrana(IPage stranica)
{
    public ILocator ImenaPotvrdjenih => stranica.GetByTestId("polaznik-ime");

    public ILocator ImenaNaCekanju => stranica.GetByTestId("cekanje-ime");

    public ILocator RedPolaznika(string imePrezime) =>
        stranica.GetByTestId("polaznik-red").Filter(new() { HasText = imePrezime });

    public ILocator Prisustvo(string imePrezime) => RedPolaznika(imePrezime).GetByTestId("polaznik-prisustvo");

    public async Task OtvoriAsync(int idTermina) => await stranica.GotoAsync($"/#/termini/{idTermina}/polaznici");

    public async Task EvidentirajDolazakAsync(string imePrezime) =>
        await RedPolaznika(imePrezime).GetByTestId("polaznik-dosao").ClickAsync();
}
