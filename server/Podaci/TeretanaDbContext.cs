using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Teretana.Api.Domen;

namespace Teretana.Api.Podaci;

public sealed class TeretanaDbContext(DbContextOptions<TeretanaDbContext> options) : DbContext(options)
{
    public DbSet<Korisnik> Korisnici => Set<Korisnik>();

    public DbSet<Termin> Termini => Set<Termin>();

    public DbSet<Rezervacija> Rezervacije => Set<Rezervacija>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeretanaDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeKonverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeKonverter>();
    }
}

/// <summary>
/// SQLite ne čuva vremensku zonu, a sva vremena u aplikaciji su UTC. Pri upisu se lokalno vreme
/// prevodi u UTC, a pri čitanju se vraća <see cref="DateTimeKind.Utc"/> da se poređenja ne bi pomerila.
/// </summary>
public sealed class UtcDateTimeKonverter() : ValueConverter<DateTime, DateTime>(
    vreme => vreme.Kind == DateTimeKind.Local ? vreme.ToUniversalTime() : DateTime.SpecifyKind(vreme, DateTimeKind.Utc),
    vreme => DateTime.SpecifyKind(vreme, DateTimeKind.Utc));
