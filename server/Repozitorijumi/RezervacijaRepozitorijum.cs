using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Teretana.Api.Domen;
using Teretana.Api.Podaci;
using Teretana.Api.Ugovori.Rezervacije;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Repozitorijumi;

public interface IRezervacijaRepozitorijum
{
    Task<RezervacijaPodaci?> VratiAsync(int idRezervacije, CancellationToken cancellationToken);

    Task<StranicaOdgovor<RezervacijaPodaci>> VratiMojeAsync(int idClana, MojeRezervacijeUpit upit, CancellationToken cancellationToken);

    Task<bool> ImaAktivnuPrijavuAsync(int idTermina, int idClana, CancellationToken cancellationToken);

    /// <summary>Zauzima mesto i upisuje potvrđenu rezervaciju u jednoj transakciji.</summary>
    Task<RezultatUpisa> RezervisiAsync(int idTermina, int idClana, DateTime sada, CancellationToken cancellationToken);

    /// <summary>Upisuje prijavu na listu čekanja samo ako je termin i dalje pun, u jednoj transakciji.</summary>
    Task<RezultatUpisa> UpisiNaListuCekanjaAsync(int idTermina, int idClana, DateTime sada, CancellationToken cancellationToken);

    /// <summary>
    /// Otkazuje aktivnu prijavu. Ako je bila potvrđena, oslobođeno mesto u istoj transakciji dobija prvi sa
    /// liste čekanja, a ako takvog nema, smanjuje se broj potvrđenih.
    /// </summary>
    Task<RezultatOtkazivanja> OtkaziAsync(int idRezervacije, DateTime sada, CancellationToken cancellationToken);

    /// <returns>false ako rezervacija u međuvremenu više nije potvrđena.</returns>
    Task<bool> EvidentirajPrisustvoAsync(int idRezervacije, bool prisustvovao, CancellationToken cancellationToken);
}

public enum IshodUpisa
{
    Upisano,
    UslovNijeIspunjen,
    VecPrijavljen,
}

public sealed record RezultatUpisa(IshodUpisa Ishod, int? IdRezervacije);

public sealed record RezultatOtkazivanja(bool Otkazano, int? IdUnapredjeneRezervacije);

public sealed record RezervacijaPodaci(
    int Id,
    int ClanId,
    StatusRezervacije Status,
    DateTime KreiranaAt,
    DateTime? PotvrdjenaAt,
    DateTime? OtkazanaAt,
    bool? Prisustvovao,
    int TerminId,
    string NazivTermina,
    DateTime PocetakTermina,
    DateTime KrajTermina,
    StatusTermina StatusTermina,
    int TrenerId,
    string ImePrezimeTrenera)
{
    public int? PozicijaNaCekanju { get; init; }
}

internal sealed class RezervacijaRepozitorijum(TeretanaDbContext db) : IRezervacijaRepozitorijum
{
    private const int SqliteConstraintUnique = 2067;

    private static readonly Expression<Func<Rezervacija, RezervacijaPodaci>> UPodatke = r => new RezervacijaPodaci(
        r.Id,
        r.ClanId,
        r.Status,
        r.KreiranaAt,
        r.PotvrdjenaAt,
        r.OtkazanaAt,
        r.Prisustvovao,
        r.TerminId,
        r.Termin.Naziv,
        r.Termin.Pocetak,
        r.Termin.Kraj,
        r.Termin.Status,
        r.Termin.TrenerId,
        r.Termin.Trener.ImePrezime);

    public async Task<RezervacijaPodaci?> VratiAsync(int idRezervacije, CancellationToken cancellationToken)
    {
        var podaci = await db.Rezervacije.AsNoTracking()
            .Where(r => r.Id == idRezervacije)
            .Select(UPodatke)
            .SingleOrDefaultAsync(cancellationToken);

        return podaci is null ? null : (await SaPozicijamaNaCekanjuAsync([podaci], cancellationToken))[0];
    }

    public async Task<StranicaOdgovor<RezervacijaPodaci>> VratiMojeAsync(int idClana, MojeRezervacijeUpit upit, CancellationToken cancellationToken)
    {
        var rezervacije = db.Rezervacije.AsNoTracking().Where(r => r.ClanId == idClana);
        if (upit.Status is { } status)
        {
            rezervacije = rezervacije.Where(r => r.Status == status);
        }

        var ukupno = await rezervacije.CountAsync(cancellationToken);
        var sortirano = upit.Sortiranje switch
        {
            SortiranjeRezervacija.PoPocetkuTermina => rezervacije.OrderBy(r => r.Termin.Pocetak),
            SortiranjeRezervacija.PoPocetkuTerminaOpadajuce => rezervacije.OrderByDescending(r => r.Termin.Pocetak),
            _ => throw new ArgumentOutOfRangeException(nameof(upit), upit.Sortiranje, "Nepodržano sortiranje rezervacija."),
        };
        var stavke = await sortirano
            .ThenBy(r => r.Id)
            .Skip(upit.Preskoci())
            .Take(upit.VelicinaStranice)
            .Select(UPodatke)
            .ToListAsync(cancellationToken);

        return new StranicaOdgovor<RezervacijaPodaci>(
            await SaPozicijamaNaCekanjuAsync(stavke, cancellationToken), upit.Stranica, upit.VelicinaStranice, ukupno);
    }

    public Task<bool> ImaAktivnuPrijavuAsync(int idTermina, int idClana, CancellationToken cancellationToken) =>
        db.Rezervacije.AnyAsync(
            r => r.TerminId == idTermina && r.ClanId == idClana && r.Status != StatusRezervacije.Otkazana,
            cancellationToken);

    public async Task<RezultatUpisa> RezervisiAsync(int idTermina, int idClana, DateTime sada, CancellationToken cancellationToken)
    {
        // Microsoft.Data.Sqlite otvara transakciju kao BEGIN IMMEDIATE: upisi se serijalizuju od početka transakcije.
        await using var transakcija = await db.Database.BeginTransactionAsync(cancellationToken);

        // Provera kapaciteta i zauzimanje mesta su jedan SQL iskaz, pa dva zahteva za poslednje mesto ne mogu
        // oba da prođu, bez obzira na to šta je servis pročitao pre toga. CHECK na brojaču je poslednja odbrana.
        var zauzeto = await db.Termini
            .Where(t => t.Id == idTermina
                && t.Status == StatusTermina.Aktivan
                && t.Pocetak > sada
                && t.BrojPotvrdjenih < t.Kapacitet)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.BrojPotvrdjenih, t => t.BrojPotvrdjenih + 1), cancellationToken);
        if (zauzeto == 0)
        {
            return new RezultatUpisa(IshodUpisa.UslovNijeIspunjen, null);
        }

        return await UpisiPrijavuAsync(
            transakcija,
            new Rezervacija { TerminId = idTermina, ClanId = idClana, Status = StatusRezervacije.Potvrdjena, KreiranaAt = sada, PotvrdjenaAt = sada },
            cancellationToken);
    }

    public async Task<RezultatUpisa> UpisiNaListuCekanjaAsync(int idTermina, int idClana, DateTime sada, CancellationToken cancellationToken)
    {
        await using var transakcija = await db.Database.BeginTransactionAsync(cancellationToken);

        // Unutar iste IMMEDIATE transakcije niko ne može da oslobodi mesto između provere i upisa.
        var terminJePun = await db.Termini.AnyAsync(
            t => t.Id == idTermina
                && t.Status == StatusTermina.Aktivan
                && t.Pocetak > sada
                && t.BrojPotvrdjenih >= t.Kapacitet,
            cancellationToken);
        if (!terminJePun)
        {
            return new RezultatUpisa(IshodUpisa.UslovNijeIspunjen, null);
        }

        return await UpisiPrijavuAsync(
            transakcija,
            new Rezervacija { TerminId = idTermina, ClanId = idClana, Status = StatusRezervacije.NaCekanju, KreiranaAt = sada },
            cancellationToken);
    }

    public async Task<RezultatOtkazivanja> OtkaziAsync(int idRezervacije, DateTime sada, CancellationToken cancellationToken)
    {
        await using var transakcija = await db.Database.BeginTransactionAsync(cancellationToken);

        var prijava = await db.Rezervacije.AsNoTracking()
            .Where(r => r.Id == idRezervacije && r.Status != StatusRezervacije.Otkazana)
            .Select(r => new { r.TerminId, r.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (prijava is null)
        {
            return new RezultatOtkazivanja(false, null);
        }

        await db.Rezervacije
            .Where(r => r.Id == idRezervacije)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, StatusRezervacije.Otkazana).SetProperty(r => r.OtkazanaAt, sada),
                cancellationToken);

        int? unapredjena = null;
        if (prijava.Status == StatusRezervacije.Potvrdjena)
        {
            unapredjena = await db.Rezervacije
                .Where(r => r.TerminId == prijava.TerminId && r.Status == StatusRezervacije.NaCekanju)
                .PoReduCekanja()
                .Select(r => (int?)r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (unapredjena is { } idUnapredjene)
            {
                // Mesto prelazi na prvog sa liste, pa se broj potvrđenih ne menja.
                await db.Rezervacije
                    .Where(r => r.Id == idUnapredjene)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(r => r.Status, StatusRezervacije.Potvrdjena).SetProperty(r => r.PotvrdjenaAt, sada),
                        cancellationToken);
            }
            else
            {
                await db.Termini
                    .Where(t => t.Id == prijava.TerminId)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.BrojPotvrdjenih, t => t.BrojPotvrdjenih - 1), cancellationToken);
            }
        }

        await transakcija.CommitAsync(cancellationToken);
        return new RezultatOtkazivanja(true, unapredjena);
    }

    public async Task<bool> EvidentirajPrisustvoAsync(int idRezervacije, bool prisustvovao, CancellationToken cancellationToken) =>
        await db.Rezervacije
            .Where(r => r.Id == idRezervacije && r.Status == StatusRezervacije.Potvrdjena)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Prisustvovao, prisustvovao), cancellationToken) == 1;

    private async Task<RezultatUpisa> UpisiPrijavuAsync(IDbContextTransaction transakcija, Rezervacija prijava, CancellationToken cancellationToken)
    {
        db.Rezervacije.Add(prijava);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException greska) when (greska.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique })
        {
            // Isti član je u međuvremenu upisao drugu aktivnu prijavu; poništava se i već zauzeto mesto.
            await transakcija.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new RezultatUpisa(IshodUpisa.VecPrijavljen, null);
        }

        await transakcija.CommitAsync(cancellationToken);
        return new RezultatUpisa(IshodUpisa.Upisano, prijava.Id);
    }

    private async Task<List<RezervacijaPodaci>> SaPozicijamaNaCekanjuAsync(List<RezervacijaPodaci> stavke, CancellationToken cancellationToken)
    {
        var terminiSaCekanjem = stavke.Where(s => s.Status == StatusRezervacije.NaCekanju).Select(s => s.TerminId).Distinct().ToList();
        if (terminiSaCekanjem.Count == 0)
        {
            return stavke;
        }

        var pozicije = await db.Rezervacije.AsNoTracking().PozicijeNaCekanjuAsync(terminiSaCekanjem, cancellationToken);
        return [.. stavke.Select(s => s.Status == StatusRezervacije.NaCekanju ? s with { PozicijaNaCekanju = pozicije[s.Id] } : s)];
    }
}
