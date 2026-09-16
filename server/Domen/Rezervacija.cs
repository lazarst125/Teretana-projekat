namespace Teretana.Api.Domen;

/// <summary>
/// Prijava člana na termin: potvrđena rezervacija, mesto na listi čekanja ili otkazana prijava.
/// Jedna tabela omogućava da baza jednim indeksom garantuje da član nema dve aktivne prijave na isti termin.
/// </summary>
public sealed class Rezervacija
{
    public int Id { get; set; }

    public int TerminId { get; set; }

    public Termin Termin { get; set; } = null!;

    public int ClanId { get; set; }

    public Korisnik Clan { get; set; } = null!;

    public StatusRezervacije Status { get; set; }

    public DateTime KreiranaAt { get; set; }

    public DateTime? PotvrdjenaAt { get; set; }

    public DateTime? OtkazanaAt { get; set; }

    public bool? Prisustvovao { get; set; }
}

public enum StatusRezervacije
{
    Potvrdjena,
    NaCekanju,
    Otkazana,
}
