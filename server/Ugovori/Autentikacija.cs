using System.ComponentModel.DataAnnotations;
using Teretana.Api.Domen;

namespace Teretana.Api.Ugovori.Autentikacija;

public sealed record RegistracijaZahtev
{
    /// <summary>Email koji služi za prijavu; velika i mala slova se ne razlikuju.</summary>
    /// <example>ana.anic@primer.rs</example>
    [Required(ErrorMessage = "Email je obavezan.")]
    [EmailAddress(ErrorMessage = "Email nije ispravan.")]
    [StringLength(254, ErrorMessage = "Email može imati najviše 254 znaka.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>Ime i prezime člana.</summary>
    /// <example>Ana Anić</example>
    [Required(ErrorMessage = "Ime i prezime su obavezni.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Ime i prezime moraju imati između 2 i 100 znakova.")]
    public string ImePrezime { get; init; } = string.Empty;

    /// <summary>Lozinka od 8 do 100 znakova.</summary>
    /// <example>Lozinka123!</example>
    [Required(ErrorMessage = "Lozinka je obavezna.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Lozinka mora imati između 8 i 100 znakova.")]
    public string Lozinka { get; init; } = string.Empty;
}

public sealed record PrijavaZahtev
{
    /// <summary>Email naloga.</summary>
    /// <example>trener1@teretana.local</example>
    [Required(ErrorMessage = "Email je obavezan.")]
    [StringLength(254, ErrorMessage = "Email može imati najviše 254 znaka.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>Lozinka naloga.</summary>
    /// <example>Trener123!</example>
    [Required(ErrorMessage = "Lozinka je obavezna.")]
    [StringLength(100, ErrorMessage = "Lozinka može imati najviše 100 znakova.")]
    public string Lozinka { get; init; } = string.Empty;
}

public sealed record ProfilZahtev
{
    /// <summary>Novo ime i prezime.</summary>
    /// <example>Ana Anić Petrović</example>
    [Required(ErrorMessage = "Ime i prezime su obavezni.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Ime i prezime moraju imati između 2 i 100 znakova.")]
    public string ImePrezime { get; init; } = string.Empty;

    /// <summary>Nova lozinka; izostavite polje da bi postojeća lozinka ostala nepromenjena.</summary>
    /// <example>NovaLozinka123!</example>
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Lozinka mora imati između 8 i 100 znakova.")]
    public string? Lozinka { get; init; }
}

public sealed record KorisnikOdgovor(int Id, string Email, string ImePrezime, Uloga Uloga)
{
    public static KorisnikOdgovor Iz(Korisnik korisnik) => new(korisnik.Id, korisnik.Email, korisnik.ImePrezime, korisnik.Uloga);
}

public sealed record PrijavaOdgovor(string Token, DateTime Istice, KorisnikOdgovor Korisnik);
