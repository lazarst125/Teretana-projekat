using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.Api.Podaci;

namespace Teretana.Api.Repozitorijumi;

public interface IKorisnikRepozitorijum
{
    Task<bool> PostojiEmailAsync(string email, CancellationToken cancellationToken);

    Task<Korisnik?> PronadjiPoEmailuAsync(string email, CancellationToken cancellationToken);

    Task<Korisnik?> PronadjiPoIdAsync(int id, CancellationToken cancellationToken);

    Task DodajAsync(Korisnik korisnik, CancellationToken cancellationToken);

    Task<bool> ImaVezanePodatkeAsync(int id, CancellationToken cancellationToken);

    /// <returns>false ako korisnik u međuvremenu više ne postoji.</returns>
    Task<bool> AzurirajAsync(int id, string imePrezime, string? lozinkaHash, CancellationToken cancellationToken);

    /// <returns>false ako je brisanje odbijeno jer nalog ipak ima termine ili prijave.</returns>
    Task<bool> ObrisiAsync(int id, CancellationToken cancellationToken);
}

internal sealed class KorisnikRepozitorijum(TeretanaDbContext db) : IKorisnikRepozitorijum
{
    private const int SqliteConstraintUnique = 2067;

    private const int SqliteConstraintForeignKey = 787;

    // SQLite sprovodi ON DELETE RESTRICT internim okidačem, pa takvo kršenje prijavljuje kao SQLITE_CONSTRAINT_TRIGGER.
    private const int SqliteConstraintTrigger = 1811;

    public Task<bool> PostojiEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Korisnici.AnyAsync(k => k.Email == email, cancellationToken);

    public Task<Korisnik?> PronadjiPoEmailuAsync(string email, CancellationToken cancellationToken) =>
        db.Korisnici.SingleOrDefaultAsync(k => k.Email == email, cancellationToken);

    public Task<Korisnik?> PronadjiPoIdAsync(int id, CancellationToken cancellationToken) =>
        db.Korisnici.AsNoTracking().SingleOrDefaultAsync(k => k.Id == id, cancellationToken);

    public async Task DodajAsync(Korisnik korisnik, CancellationToken cancellationToken)
    {
        db.Korisnici.Add(korisnik);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException greska) when (greska.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique })
        {
            // Dve istovremene registracije istog email-a prođu proveru u servisu; jedinstveni indeks odlučuje.
            throw Greske.EmailZauzet();
        }
    }

    public async Task<bool> ImaVezanePodatkeAsync(int id, CancellationToken cancellationToken) =>
        await db.Termini.AnyAsync(t => t.TrenerId == id, cancellationToken)
        || await db.Rezervacije.AnyAsync(r => r.ClanId == id, cancellationToken);

    public async Task<bool> AzurirajAsync(int id, string imePrezime, string? lozinkaHash, CancellationToken cancellationToken)
    {
        var korisnik = db.Korisnici.Where(k => k.Id == id);
        // Lozinka se upisuje samo kada je zadata nova, da izmena imena ne bi dirala heš.
        var izmenjeno = lozinkaHash is null
            ? await korisnik.ExecuteUpdateAsync(s => s.SetProperty(k => k.ImePrezime, imePrezime), cancellationToken)
            : await korisnik.ExecuteUpdateAsync(
                s => s.SetProperty(k => k.ImePrezime, imePrezime).SetProperty(k => k.LozinkaHash, lozinkaHash),
                cancellationToken);

        return izmenjeno == 1;
    }

    public async Task<bool> ObrisiAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await db.Korisnici.Where(k => k.Id == id).ExecuteDeleteAsync(cancellationToken) == 1;
        }
        catch (SqliteException greska) when (greska.SqliteExtendedErrorCode is SqliteConstraintForeignKey or SqliteConstraintTrigger)
        {
            // Termin ili prijava je nastala posle provere u servisu; strani ključ sa Restrict čuva istoriju.
            return false;
        }
    }
}
