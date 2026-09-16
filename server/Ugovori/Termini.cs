using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Teretana.Api.Domen;
using Teretana.Api.Ugovori.Validacija;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Ugovori.Termini;

public sealed record TerminZahtev
{
    /// <summary>Naziv termina.</summary>
    /// <example>Joga za početnike</example>
    [Required(ErrorMessage = "Naziv je obavezan.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Naziv mora imati između 2 i 100 znakova.")]
    public string Naziv { get; init; } = string.Empty;

    /// <summary>Opcioni opis termina.</summary>
    /// <example>Lagan uvod u disanje i osnovne položaje.</example>
    [StringLength(500, ErrorMessage = "Opis može imati najviše 500 znakova.")]
    public string? Opis { get; init; }

    /// <summary>Početak termina sa vremenskom zonom; mora biti u budućnosti i čuva se kao UTC.</summary>
    /// <example>2026-10-01T18:00:00+02:00</example>
    [Required(ErrorMessage = "Početak je obavezan.")]
    public DateTimeOffset? Pocetak { get; init; }

    /// <summary>Kraj termina sa vremenskom zonom; mora biti posle početka.</summary>
    /// <example>2026-10-01T19:00:00+02:00</example>
    [Required(ErrorMessage = "Kraj je obavezan.")]
    [Posle(nameof(Pocetak), ErrorMessage = "Kraj mora biti posle početka.")]
    public DateTimeOffset? Kraj { get; init; }

    /// <summary>Najveći broj potvrđenih rezervacija, od 1 do 100.</summary>
    /// <example>12</example>
    [Range(1, 100, ErrorMessage = "Kapacitet mora biti između 1 i 100.")]
    public int Kapacitet { get; init; }
}

public sealed record TerminiUpit : StranicenjeUpit
{
    /// <summary>Termini koji počinju u ovom trenutku ili kasnije.</summary>
    [FromQuery(Name = "od")]
    public DateTimeOffset? Od { get; init; }

    /// <summary>Termini koji počinju pre ovog trenutka.</summary>
    [FromQuery(Name = "do")]
    [Posle(nameof(Od), ErrorMessage = "Kraj opsega mora biti posle početka opsega.")]
    public DateTimeOffset? Do { get; init; }

    [FromQuery(Name = "trenerId")]
    public int? TrenerId { get; init; }

    /// <summary>Samo termini koji mogu da se rezervišu: aktivni, još nisu počeli i imaju bar jedno slobodno mesto.</summary>
    [FromQuery(Name = "samoSlobodni")]
    public bool SamoSlobodni { get; init; }

    [FromQuery(Name = "status")]
    public StatusTermina? Status { get; init; }

    [FromQuery(Name = "sortiranje")]
    [AllowedValues(
        SortiranjeTermina.PoPocetku, SortiranjeTermina.PoPocetkuOpadajuce,
        SortiranjeTermina.PoNazivu, SortiranjeTermina.PoNazivuOpadajuce,
        SortiranjeTermina.PoSlobodnimMestima, SortiranjeTermina.PoSlobodnimMestimaOpadajuce,
        ErrorMessage = "Sortiranje mora biti: pocetak, naziv ili slobodnaMesta, uz opcioni prefiks '-' za opadajući redosled.")]
    public string Sortiranje { get; init; } = SortiranjeTermina.PoPocetku;
}

public static class SortiranjeTermina
{
    public const string PoPocetku = "pocetak";
    public const string PoPocetkuOpadajuce = "-pocetak";
    public const string PoNazivu = "naziv";
    public const string PoNazivuOpadajuce = "-naziv";
    public const string PoSlobodnimMestima = "slobodnaMesta";
    public const string PoSlobodnimMestimaOpadajuce = "-slobodnaMesta";
}

public sealed record TrenerUkratko(int Id, string ImePrezime);

public sealed record TerminStavkaOdgovor(
    int Id,
    string Naziv,
    DateTime Pocetak,
    DateTime Kraj,
    int Kapacitet,
    int BrojPotvrdjenih,
    int SlobodnaMesta,
    int BrojNaCekanju,
    StatusTermina Status,
    TrenerUkratko Trener);

public sealed record TerminDetaljOdgovor(
    int Id,
    string Naziv,
    string? Opis,
    DateTime Pocetak,
    DateTime Kraj,
    int Kapacitet,
    int BrojPotvrdjenih,
    int SlobodnaMesta,
    int BrojNaCekanju,
    StatusTermina Status,
    TrenerUkratko Trener,
    MojaPrijavaOdgovor? MojaPrijava);

/// <summary>Aktivna prijava člana koji gleda termin; za trenera i za člana bez prijave je null.</summary>
public sealed record MojaPrijavaOdgovor(int RezervacijaId, StatusRezervacije Status, int? PozicijaNaCekanju);

public sealed record PolazniciOdgovor(
    int TerminId,
    IReadOnlyList<PotvrdjeniPolaznikOdgovor> Potvrdjeni,
    IReadOnlyList<PolaznikNaCekanjuOdgovor> ListaCekanja);

public sealed record PotvrdjeniPolaznikOdgovor(int RezervacijaId, int ClanId, string ImePrezime, string Email, DateTime? PotvrdjenaAt, bool? Prisustvovao);

public sealed record PolaznikNaCekanjuOdgovor(int RezervacijaId, int ClanId, string ImePrezime, string Email, int Pozicija, DateTime PrijavljenAt);
