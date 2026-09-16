namespace Teretana.Api.Domen;

public sealed class Termin
{
    public int Id { get; set; }

    public int TrenerId { get; set; }

    public Korisnik Trener { get; set; } = null!;

    public required string Naziv { get; set; }

    public string? Opis { get; set; }

    public DateTime Pocetak { get; set; }

    public DateTime Kraj { get; set; }

    public int Kapacitet { get; set; }

    /// <summary>
    /// Broj potvrđenih rezervacija. Čuva se u terminu da bi zauzimanje mesta bilo jedan uslovni
    /// UPDATE koji baza izvršava atomski, umesto brojanja redova pa upisa.
    /// </summary>
    public int BrojPotvrdjenih { get; set; }

    public StatusTermina Status { get; set; }

    public DateTime KreiranAt { get; set; }

    public List<Rezervacija> Rezervacije { get; } = [];
}

public enum StatusTermina
{
    Aktivan,
    Otkazan,
}
