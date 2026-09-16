using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.Api.Podaci;
using Teretana.Api.Ugovori.Termini;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Repozitorijumi;

public interface ITerminRepozitorijum
{
    Task<StranicaOdgovor<TerminStavkaOdgovor>> VratiStranicuAsync(TerminiUpit upit, DateTime sada, CancellationToken cancellationToken);

    Task<TerminDetaljOdgovor?> VratiDetaljAsync(int idTermina, int? idClana, CancellationToken cancellationToken);

    Task<PolazniciOdgovor> VratiPolazniceAsync(int idTermina, CancellationToken cancellationToken);

    Task<Termin?> PronadjiAsync(int idTermina, CancellationToken cancellationToken);

    Task<bool> ImaAktivnePrijaveAsync(int idTermina, CancellationToken cancellationToken);

    Task<bool> ImaPrijaveAsync(int idTermina, CancellationToken cancellationToken);

    Task DodajAsync(Termin termin, CancellationToken cancellationToken);

    /// <returns>false ako je termin u međuvremenu otkazan ili je dobio aktivnu prijavu.</returns>
    Task<bool> IzmeniAkoNemaAktivnihPrijavaAsync(int idTermina, IzmenaTermina izmena, CancellationToken cancellationToken);

    /// <returns>false ako termin ima bilo kakvu prijavu.</returns>
    Task<bool> ObrisiAkoNemaPrijavaAsync(int idTermina, CancellationToken cancellationToken);

    /// <returns>false ako termin više nije aktivan ili je u međuvremenu počeo.</returns>
    Task<bool> OtkaziAsync(int idTermina, DateTime sada, CancellationToken cancellationToken);
}

public sealed record IzmenaTermina(string Naziv, string? Opis, DateTime Pocetak, DateTime Kraj, int Kapacitet);

internal sealed class TerminRepozitorijum(TeretanaDbContext db) : ITerminRepozitorijum
{
    private const int SqliteConstraintForeignKey = 787;

    // SQLite sprovodi ON DELETE RESTRICT internim okidačem, pa takvo kršenje prijavljuje kao SQLITE_CONSTRAINT_TRIGGER.
    private const int SqliteConstraintTrigger = 1811;

    private static readonly Expression<Func<Termin, TerminStavkaOdgovor>> UStavku = t => new TerminStavkaOdgovor(
        t.Id,
        t.Naziv,
        t.Pocetak,
        t.Kraj,
        t.Kapacitet,
        t.BrojPotvrdjenih,
        t.Status == StatusTermina.Otkazan ? 0 : t.Kapacitet - t.BrojPotvrdjenih,
        t.Rezervacije.Count(r => r.Status == StatusRezervacije.NaCekanju),
        t.Status,
        new TrenerUkratko(t.Trener.Id, t.Trener.ImePrezime));

    public async Task<StranicaOdgovor<TerminStavkaOdgovor>> VratiStranicuAsync(TerminiUpit upit, DateTime sada, CancellationToken cancellationToken)
    {
        var termini = db.Termini.AsNoTracking();

        if (upit.Od is { } od)
        {
            var odUtc = od.UtcDateTime;
            termini = termini.Where(t => t.Pocetak >= odUtc);
        }

        if (upit.Do is { } @do)
        {
            var doUtc = @do.UtcDateTime;
            termini = termini.Where(t => t.Pocetak < doUtc);
        }

        if (upit.TrenerId is { } idTrenera)
        {
            termini = termini.Where(t => t.TrenerId == idTrenera);
        }

        if (upit.Status is { } status)
        {
            termini = termini.Where(t => t.Status == status);
        }

        if (upit.SamoSlobodni)
        {
            termini = termini.Where(t => t.Status == StatusTermina.Aktivan && t.Pocetak > sada && t.BrojPotvrdjenih < t.Kapacitet);
        }

        var ukupno = await termini.CountAsync(cancellationToken);
        var stavke = await Sortiraj(termini, upit.Sortiranje)
            .ThenBy(t => t.Id)
            .Skip(upit.Preskoci())
            .Take(upit.VelicinaStranice)
            .Select(UStavku)
            .ToListAsync(cancellationToken);

        return new StranicaOdgovor<TerminStavkaOdgovor>(stavke, upit.Stranica, upit.VelicinaStranice, ukupno);
    }

    public async Task<TerminDetaljOdgovor?> VratiDetaljAsync(int idTermina, int? idClana, CancellationToken cancellationToken)
    {
        var termin = db.Termini.AsNoTracking().Where(t => t.Id == idTermina);
        var s = await termin.Select(UStavku).SingleOrDefaultAsync(cancellationToken);
        if (s is null)
        {
            return null;
        }

        var opis = await termin.Select(t => t.Opis).SingleAsync(cancellationToken);
        var mojaPrijava = idClana is { } clan ? await VratiAktivnuPrijavuAsync(idTermina, clan, cancellationToken) : null;
        return new TerminDetaljOdgovor(
            s.Id, s.Naziv, opis, s.Pocetak, s.Kraj, s.Kapacitet, s.BrojPotvrdjenih, s.SlobodnaMesta, s.BrojNaCekanju, s.Status, s.Trener, mojaPrijava);
    }

    public async Task<PolazniciOdgovor> VratiPolazniceAsync(int idTermina, CancellationToken cancellationToken)
    {
        var prijave = await db.Rezervacije.AsNoTracking()
            .Where(r => r.TerminId == idTermina && r.Status != StatusRezervacije.Otkazana)
            .PoReduCekanja()
            .Select(r => new { r.Id, r.ClanId, r.Clan.ImePrezime, r.Clan.Email, r.Status, r.KreiranaAt, r.PotvrdjenaAt, r.Prisustvovao })
            .ToListAsync(cancellationToken);

        var potvrdjeni = prijave
            .Where(p => p.Status == StatusRezervacije.Potvrdjena)
            .Select(p => new PotvrdjeniPolaznikOdgovor(p.Id, p.ClanId, p.ImePrezime, p.Email, p.PotvrdjenaAt, p.Prisustvovao))
            .ToList();
        var listaCekanja = prijave
            .Where(p => p.Status == StatusRezervacije.NaCekanju)
            .Select((p, indeks) => new PolaznikNaCekanjuOdgovor(p.Id, p.ClanId, p.ImePrezime, p.Email, indeks + 1, p.KreiranaAt))
            .ToList();

        return new PolazniciOdgovor(idTermina, potvrdjeni, listaCekanja);
    }

    public Task<Termin?> PronadjiAsync(int idTermina, CancellationToken cancellationToken) =>
        db.Termini.AsNoTracking().SingleOrDefaultAsync(t => t.Id == idTermina, cancellationToken);

    public Task<bool> ImaAktivnePrijaveAsync(int idTermina, CancellationToken cancellationToken) =>
        db.Rezervacije.AnyAsync(r => r.TerminId == idTermina && r.Status != StatusRezervacije.Otkazana, cancellationToken);

    public Task<bool> ImaPrijaveAsync(int idTermina, CancellationToken cancellationToken) =>
        db.Rezervacije.AnyAsync(r => r.TerminId == idTermina, cancellationToken);

    public async Task DodajAsync(Termin termin, CancellationToken cancellationToken)
    {
        db.Termini.Add(termin);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IzmeniAkoNemaAktivnihPrijavaAsync(int idTermina, IzmenaTermina izmena, CancellationToken cancellationToken)
    {
        // Provera i izmena su jedan SQL iskaz, pa prijava upisana posle provere u servisu ne može da prođe neprimećeno.
        var izmenjeno = await db.Termini
            .Where(t => t.Id == idTermina
                && t.Status == StatusTermina.Aktivan
                && !t.Rezervacije.Any(r => r.Status != StatusRezervacije.Otkazana))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.Naziv, izmena.Naziv)
                    .SetProperty(t => t.Opis, izmena.Opis)
                    .SetProperty(t => t.Pocetak, izmena.Pocetak)
                    .SetProperty(t => t.Kraj, izmena.Kraj)
                    .SetProperty(t => t.Kapacitet, izmena.Kapacitet),
                cancellationToken);

        return izmenjeno == 1;
    }

    public async Task<bool> ObrisiAkoNemaPrijavaAsync(int idTermina, CancellationToken cancellationToken)
    {
        try
        {
            return await db.Termini.Where(t => t.Id == idTermina).ExecuteDeleteAsync(cancellationToken) == 1;
        }
        catch (SqliteException greska) when (greska.SqliteExtendedErrorCode is SqliteConstraintForeignKey or SqliteConstraintTrigger)
        {
            // Prijava je upisana posle provere u servisu; strani ključ sa Restrict čuva istoriju prijava.
            return false;
        }
    }

    public async Task<bool> OtkaziAsync(int idTermina, DateTime sada, CancellationToken cancellationToken)
    {
        await using var transakcija = await db.Database.BeginTransactionAsync(cancellationToken);

        var otkazano = await db.Termini
            .Where(t => t.Id == idTermina && t.Status == StatusTermina.Aktivan && t.Pocetak > sada)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.Status, StatusTermina.Otkazan).SetProperty(t => t.BrojPotvrdjenih, 0),
                cancellationToken);
        if (otkazano == 0)
        {
            return false;
        }

        await db.Rezervacije
            .Where(r => r.TerminId == idTermina && r.Status != StatusRezervacije.Otkazana)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, StatusRezervacije.Otkazana).SetProperty(r => r.OtkazanaAt, sada),
                cancellationToken);

        await transakcija.CommitAsync(cancellationToken);
        return true;
    }

    private static IOrderedQueryable<Termin> Sortiraj(IQueryable<Termin> termini, string sortiranje) => sortiranje switch
    {
        SortiranjeTermina.PoPocetku => termini.OrderBy(t => t.Pocetak),
        SortiranjeTermina.PoPocetkuOpadajuce => termini.OrderByDescending(t => t.Pocetak),
        SortiranjeTermina.PoNazivu => termini.OrderBy(t => t.Naziv),
        SortiranjeTermina.PoNazivuOpadajuce => termini.OrderByDescending(t => t.Naziv),
        SortiranjeTermina.PoSlobodnimMestima => termini.OrderBy(t => t.Kapacitet - t.BrojPotvrdjenih),
        SortiranjeTermina.PoSlobodnimMestimaOpadajuce => termini.OrderByDescending(t => t.Kapacitet - t.BrojPotvrdjenih),
        _ => throw new ArgumentOutOfRangeException(nameof(sortiranje), sortiranje, "Nepodržano sortiranje termina."),
    };

    private async Task<MojaPrijavaOdgovor?> VratiAktivnuPrijavuAsync(int idTermina, int idClana, CancellationToken cancellationToken)
    {
        var prijava = await db.Rezervacije.AsNoTracking()
            .Where(r => r.TerminId == idTermina && r.ClanId == idClana && r.Status != StatusRezervacije.Otkazana)
            .Select(r => new { r.Id, r.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (prijava is null)
        {
            return null;
        }

        if (prijava.Status != StatusRezervacije.NaCekanju)
        {
            return new MojaPrijavaOdgovor(prijava.Id, prijava.Status, null);
        }

        var pozicije = await db.Rezervacije.AsNoTracking().PozicijeNaCekanjuAsync([idTermina], cancellationToken);
        return new MojaPrijavaOdgovor(prijava.Id, prijava.Status, pozicije[prijava.Id]);
    }
}
