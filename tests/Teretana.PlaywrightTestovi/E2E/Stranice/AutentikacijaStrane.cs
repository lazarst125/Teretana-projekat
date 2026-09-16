namespace Teretana.PlaywrightTestovi.E2E.Stranice;

public sealed class Zaglavlje(IPage stranica)
{
    public ILocator ImeKorisnika => stranica.GetByTestId("profil-ime");

    public ILocator Uloga => stranica.GetByTestId("profil-uloga");

    public ILocator Obavestenje => stranica.GetByTestId("obavestenje");

    public ILocator NavigacijaNoviTermin => stranica.GetByTestId("nav-novi-termin");

    public async Task OdjaviSeAsync() => await stranica.GetByTestId("odjava").ClickAsync();
}

public sealed class PocetnaStrana(IPage stranica)
{
    public ILocator StatusSistemaRezultat => stranica.GetByTestId("status-sistema-rezultat");

    private ILocator ProveriStatusSistemaDugme => stranica.GetByTestId("status-sistema-proveri");

    public async Task OtvoriAsync() => await stranica.GotoAsync("/");

    public async Task ProveriStatusSistemaAsync() => await ProveriStatusSistemaDugme.ClickAsync();
}

public sealed class PrijavaStrana(IPage stranica)
{
    public ILocator GreskaForme => stranica.GetByTestId("prijava-greska");

    public ILocator GreskaPolja(string naziv) => stranica.GetByTestId($"prijava-{naziv}-greska");

    public async Task OtvoriAsync() => await stranica.GotoAsync("/#/prijava");

    public async Task PrijaviSeAsync(string email, string lozinka)
    {
        await stranica.GetByTestId("prijava-email").FillAsync(email);
        await stranica.GetByTestId("prijava-lozinka").FillAsync(lozinka);
        await stranica.GetByTestId("prijava-potvrdi").ClickAsync();
    }

    /// <summary>Prijava bez upotrebe miša: kucanje, Tab do lozinke i Enter umesto klika na dugme.</summary>
    public async Task PrijaviSeTastaturomAsync(string email, string lozinka)
    {
        await stranica.GetByTestId("prijava-email").FocusAsync();
        await stranica.Keyboard.TypeAsync(email);
        await stranica.Keyboard.PressAsync("Tab");
        await stranica.Keyboard.TypeAsync(lozinka);
        await stranica.Keyboard.PressAsync("Enter");
    }
}

public sealed class ProfilStrana(IPage stranica)
{
    public ILocator Email => stranica.GetByTestId("profil-email");

    public ILocator GreskaForme => stranica.GetByTestId("profil-forma-greska");

    /// <param name="polje">Deo test id-ja polja: ime-prezime ili lozinka.</param>
    public ILocator GreskaPolja(string polje) => stranica.GetByTestId($"profil-forma-{polje}-greska");

    public ILocator GreskaBrisanja => stranica.GetByTestId("profil-brisanje-greska");

    public async Task OtvoriAsync() => await stranica.GotoAsync("/#/profil");

    /// <param name="imePrezime">Vrednost koja se upisuje u polje imena i prezimena.</param>
    /// <param name="novaLozinka">Izostavljena vrednost ostavlja polje prazno, što znači „zadrži postojeću lozinku“.</param>
    public async Task SacuvajAsync(string imePrezime, string? novaLozinka = null)
    {
        await stranica.GetByTestId("profil-forma-ime-prezime").FillAsync(imePrezime);
        await stranica.GetByTestId("profil-forma-lozinka").FillAsync(novaLozinka ?? string.Empty);
        await stranica.GetByTestId("profil-forma-sacuvaj").ClickAsync();
    }

    public async Task ObrisiNalogAsync()
    {
        await stranica.GetByTestId("profil-obrisi").ClickAsync();
        await new DijalogPotvrde(stranica).PotvrdiAsync();
    }
}

public sealed class RegistracijaStrana(IPage stranica)
{
    public ILocator GreskaForme => stranica.GetByTestId("registracija-greska");

    /// <param name="polje">Deo test id-ja polja: ime-prezime, email ili lozinka.</param>
    public ILocator GreskaPolja(string polje) => stranica.GetByTestId($"registracija-{polje}-greska");

    public async Task OtvoriAsync() => await stranica.GotoAsync("/#/registracija");

    public async Task RegistrujAsync(string imePrezime, string email, string lozinka)
    {
        await stranica.GetByTestId("registracija-ime-prezime").FillAsync(imePrezime);
        await stranica.GetByTestId("registracija-email").FillAsync(email);
        await stranica.GetByTestId("registracija-lozinka").FillAsync(lozinka);
        await stranica.GetByTestId("registracija-potvrdi").ClickAsync();
    }
}
