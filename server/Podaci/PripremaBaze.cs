using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Teretana.Api.Podaci;

public static class BazaPodatakaRegistracija
{
    public const string NazivConnectionStringa = "Teretana";

    public static IServiceCollection DodajBazuPodataka(this IServiceCollection servisi) =>
        // Konfiguracija se čita pri kreiranju konteksta, a ne pri registraciji, da bi testovi mogli
        // da podmetnu sopstvenu bazu kroz konfiguraciju.
        servisi.AddDbContext<TeretanaDbContext>((provajder, opcije) => opcije.UseSqlite(
            NapraviConnectionString(
                provajder.GetRequiredService<IConfiguration>(),
                provajder.GetRequiredService<IHostEnvironment>())));

    private static string NapraviConnectionString(IConfiguration konfiguracija, IHostEnvironment okruzenje)
    {
        var connectionString = konfiguracija.GetConnectionString(NazivConnectionStringa)
            ?? throw new InvalidOperationException($"Nedostaje connection string '{NazivConnectionStringa}'.");

        var sqlite = new SqliteConnectionStringBuilder(connectionString);
        if (!Path.IsPathRooted(sqlite.DataSource))
        {
            // Relativna putanja se vezuje za folder aplikacije, a ne za folder iz kog je pokrenut `dotnet run`.
            sqlite.DataSource = Path.Combine(okruzenje.ContentRootPath, sqlite.DataSource);
        }

        return sqlite.ToString();
    }
}

public static partial class PripremaBaze
{
    /// <summary>
    /// Primenjuje migracije pri svakom pokretanju; demonstracioni podaci se upisuju samo u Development
    /// okruženju i samo u praznu bazu.
    /// </summary>
    public static async Task PripremiAsync(IServiceProvider servisi, CancellationToken cancellationToken = default)
    {
        await using var scope = servisi.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TeretanaDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        if (scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            var vreme = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            await PocetniPodaci.UpisiAkoJeBazaPraznaAsync(db, vreme, cancellationToken);
        }
    }

    public static async Task ResetujAsync(IServiceProvider servisi, CancellationToken cancellationToken = default)
    {
        await using var scope = servisi.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TeretanaDbContext>();
        var vreme = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await PocetniPodaci.UpisiAsync(db, vreme, cancellationToken);
    }

    /// <summary>
    /// Obrađuje argument <c>--reset-db</c>. Dozvoljeno samo u Development okruženju, da se
    /// produkciona baza ne bi slučajno obrisala.
    /// </summary>
    /// <returns>Izlazni kod procesa.</returns>
    public static async Task<int> ResetujIzKomandneLinijeAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            LogResetOdbijen(app.Logger, app.Environment.EnvironmentName);
            return 1;
        }

        await ResetujAsync(app.Services);
        LogResetZavrsen(app.Logger);
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Reset baze je dozvoljen samo u Development okruženju (trenutno: {Okruzenje}).")]
    private static partial void LogResetOdbijen(ILogger logger, string okruzenje);

    [LoggerMessage(Level = LogLevel.Information, Message = "Baza je vraćena na početno stanje sa demonstracionim podacima.")]
    private static partial void LogResetZavrsen(ILogger logger);
}
