using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Teretana.Api.Domen;
using Teretana.Api.Ugovori.Termini;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Ugovori.Rezervacije;

public sealed record MojeRezervacijeUpit : StranicenjeUpit
{
    [FromQuery(Name = "status")]
    public StatusRezervacije? Status { get; init; }

    [FromQuery(Name = "sortiranje")]
    [AllowedValues(
        SortiranjeRezervacija.PoPocetkuTermina, SortiranjeRezervacija.PoPocetkuTerminaOpadajuce,
        ErrorMessage = "Sortiranje mora biti: pocetak ili -pocetak.")]
    public string Sortiranje { get; init; } = SortiranjeRezervacija.PoPocetkuTermina;
}

public static class SortiranjeRezervacija
{
    public const string PoPocetkuTermina = "pocetak";
    public const string PoPocetkuTerminaOpadajuce = "-pocetak";
}

public sealed record PrisustvoZahtev
{
    /// <summary>Da li je član došao na termin.</summary>
    /// <example>true</example>
    [Required(ErrorMessage = "Polje prisustvovao je obavezno.")]
    public bool? Prisustvovao { get; init; }
}

public sealed record TerminUkratkoOdgovor(int Id, string Naziv, DateTime Pocetak, DateTime Kraj, StatusTermina Status, TrenerUkratko Trener);

/// <summary>
/// Rezervacija ili mesto na listi čekanja. RokZaOtkazivanje je poslednji trenutak u kome potvrđena rezervacija
/// može da se otkaže, a MozeDaSeOtkaze kaže da li bi otkazivanje sada prošlo, da klijent ne bi sam računao pravila.
/// </summary>
public sealed record RezervacijaOdgovor(
    int Id,
    StatusRezervacije Status,
    int? PozicijaNaCekanju,
    DateTime KreiranaAt,
    DateTime? PotvrdjenaAt,
    DateTime? OtkazanaAt,
    bool? Prisustvovao,
    DateTime RokZaOtkazivanje,
    bool MozeDaSeOtkaze,
    TerminUkratkoOdgovor Termin);
