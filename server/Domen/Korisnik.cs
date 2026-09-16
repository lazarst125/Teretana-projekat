namespace Teretana.Api.Domen;

public sealed class Korisnik
{
    public int Id { get; set; }

    public required string Email { get; set; }

    public required string ImePrezime { get; set; }

    public string LozinkaHash { get; set; } = string.Empty;

    public Uloga Uloga { get; set; }

    public DateTime KreiranAt { get; set; }
}

public enum Uloga
{
    Clan,
    Trener,
}
