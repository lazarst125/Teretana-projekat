namespace Teretana.Api.Domen;

/// <summary>
/// Potvrđena rezervacija može da se otkaže najkasnije <see cref="RokPrePocetka"/> pre početka termina,
/// uključujući i sam taj trenutak.
/// </summary>
public sealed class PolitikaOtkazivanja(TimeSpan rokPrePocetka)
{
    public TimeSpan RokPrePocetka { get; } = rokPrePocetka > TimeSpan.Zero
        ? rokPrePocetka
        : throw new ArgumentOutOfRangeException(nameof(rokPrePocetka), rokPrePocetka, "Rok za otkazivanje mora biti pozitivan.");

    public DateTime PoslednjiTrenutakZaOtkazivanje(DateTime pocetakTermina) => pocetakTermina - RokPrePocetka;

    public bool RokJeIstekao(DateTime pocetakTermina, DateTime sada) => sada > PoslednjiTrenutakZaOtkazivanje(pocetakTermina);
}
