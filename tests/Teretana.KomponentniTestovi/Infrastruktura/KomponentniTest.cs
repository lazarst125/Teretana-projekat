using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Teretana.Api.Domen;
using Teretana.Api.Podaci;
using Teretana.Testovi.Zajednicko;

// Klase se izvršavaju paralelno, a KomponentniTest paralelizuje i metode unutar klase.
// Nova instanca klase po testu garantuje da testovi ne dele polja (aplikaciju, klijenta, bazu).
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]

namespace Teretana.KomponentniTestovi.Infrastruktura;

/// <summary>
/// Svaki test dobija sopstvenu instancu aplikacije, sopstvenu SQLite bazu i sopstveni sat postavljen na
/// <see cref="TestniEntiteti.Sada"/>, pa testovi mogu da se izvršavaju paralelno, bilo kojim redosledom
/// i ne zavise od stvarnog datuma.
/// </summary>
[Parallelizable(ParallelScope.All)]
[Category("Komponentni")]
public abstract class KomponentniTest
{
    protected const string TestnaLozinka = "Lozinka123!";

    protected TeretanaAplikacija Aplikacija { get; private set; } = null!;

    protected HttpClient Klijent { get; private set; } = null!;

    protected FakeTimeProvider Vreme { get; } = new(new DateTimeOffset(TestniEntiteti.Sada));

    protected virtual string Okruzenje => "Testing";

    [SetUp]
    public void PokreniAplikaciju()
    {
        Aplikacija = new TeretanaAplikacija(Okruzenje, PodesiKonfiguraciju, servisi =>
        {
            servisi.AddSingleton<TimeProvider>(Vreme);
            PodesiTestneServise(servisi);
        });
        Klijent = Aplikacija.CreateClient();
    }

    [TearDown]
    public async Task ZaustaviAplikaciju()
    {
        Klijent.Dispose();
        await Aplikacija.DisposeAsync();
    }

    protected static string NoviEmail() => $"{Guid.NewGuid():N}@test.local";

    protected static async Task<JsonElement> ProblemIzOdgovoraAsync(HttpResponseMessage odgovor)
    {
        Assert.That(odgovor.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        return await odgovor.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Odgovor je ProblemDetails sa očekivanim statusom i, ako je zadat, kodom greške.</summary>
    protected static async Task OcekujProblemAsync(HttpResponseMessage odgovor, HttpStatusCode status, string? kod = null)
    {
        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(status));
            Assert.That(problem.GetProperty("status").GetInt32(), Is.EqualTo((int)status));
            if (kod is not null)
            {
                Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(kod));
            }
        });
    }

    protected virtual void PodesiKonfiguraciju(IDictionary<string, string?> konfiguracija)
    {
    }

    protected virtual void PodesiTestneServise(IServiceCollection servisi)
    {
    }

    protected async Task SaBazomAsync(Func<TeretanaDbContext, Task> akcija)
    {
        await using var scope = Aplikacija.Services.CreateAsyncScope();
        await akcija(scope.ServiceProvider.GetRequiredService<TeretanaDbContext>());
    }

    protected async Task<T> SaBazomAsync<T>(Func<TeretanaDbContext, Task<T>> upit)
    {
        await using var scope = Aplikacija.Services.CreateAsyncScope();
        return await upit(scope.ServiceProvider.GetRequiredService<TeretanaDbContext>());
    }

    /// <summary>
    /// Izvršava izmenu direktno nad bazom, mimo servisa, i vraća grešku kojom je baza odbila upis.
    /// </summary>
    protected async Task<SqliteException> UpisKojiBazaOdbijaAsync(Func<TeretanaDbContext, Task> izmena)
    {
        await using var scope = Aplikacija.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TeretanaDbContext>();
        await izmena(db);

        var izuzetak = Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        return izuzetak?.InnerException as SqliteException
            ?? throw new AssertionException($"Očekivana je SQLite greška, a dobijeno je: {izuzetak?.InnerException}");
    }

    /// <summary>
    /// Upisuje korisnika sa lozinkom <see cref="TestnaLozinka"/> direktno u bazu; tako nastaju i treneri,
    /// jer registracija kroz API pravi samo članove.
    /// </summary>
    protected async Task<Korisnik> NoviKorisnikUBaziAsync(Uloga uloga)
    {
        var korisnik = uloga == Uloga.Trener ? TestniEntiteti.NoviTrener() : TestniEntiteti.NoviClan();
        korisnik.LozinkaHash = new PasswordHasher<Korisnik>().HashPassword(korisnik, TestnaLozinka);
        await SaBazomAsync(db =>
        {
            db.Korisnici.Add(korisnik);
            return db.SaveChangesAsync();
        });
        return korisnik;
    }

    protected async Task<Termin> NoviTerminUBaziAsync(Korisnik trener, int kapacitet = 10, DateTime? pocetak = null, string naziv = "Testni termin")
    {
        var pocetakTermina = pocetak ?? TestniEntiteti.Sada.AddDays(1);
        var termin = new Termin
        {
            TrenerId = trener.Id,
            Naziv = naziv,
            Pocetak = pocetakTermina,
            Kraj = pocetakTermina.AddHours(1),
            Kapacitet = kapacitet,
            Status = StatusTermina.Aktivan,
            KreiranAt = TestniEntiteti.Sada.AddDays(-1),
        };
        await SaBazomAsync(db =>
        {
            db.Termini.Add(termin);
            return db.SaveChangesAsync();
        });
        return termin;
    }

    /// <summary>
    /// Upisuje prijavu direktno u bazu; za potvrđenu prijavu uvećava i brojač termina, kao što to radi servis.
    /// </summary>
    protected async Task<Rezervacija> NovaPrijavaUBaziAsync(Termin termin, Korisnik clan, StatusRezervacije status, DateTime? kreiranaAt = null)
    {
        var kreirana = kreiranaAt ?? TestniEntiteti.Sada.AddHours(-1);
        var prijava = new Rezervacija
        {
            TerminId = termin.Id,
            ClanId = clan.Id,
            Status = status,
            KreiranaAt = kreirana,
            PotvrdjenaAt = status == StatusRezervacije.Potvrdjena ? kreirana : null,
        };
        await SaBazomAsync(async db =>
        {
            db.Rezervacije.Add(prijava);
            await db.SaveChangesAsync();
            if (status == StatusRezervacije.Potvrdjena)
            {
                await db.Termini.Where(t => t.Id == termin.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.BrojPotvrdjenih, t => t.BrojPotvrdjenih + 1));
            }
        });
        return prijava;
    }

    protected async Task PrijaviSeKaoAsync(Korisnik korisnik) =>
        Klijent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenZaAsync(korisnik));

    /// <summary>Poseban klijent sa tokenom korisnika, za testove u kojima više korisnika šalje zahteve istovremeno.</summary>
    protected async Task<HttpClient> NoviKlijentZaAsync(Korisnik korisnik)
    {
        var klijent = Aplikacija.CreateClient();
        klijent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenZaAsync(korisnik));
        return klijent;
    }

    private async Task<string> TokenZaAsync(Korisnik korisnik)
    {
        using var odgovor = await Klijent.PostAsJsonAsync("/api/auth/prijava", new { email = korisnik.Email, lozinka = TestnaLozinka });
        odgovor.EnsureSuccessStatusCode();
        var telo = await odgovor.Content.ReadFromJsonAsync<JsonElement>();
        return telo.GetProperty("token").GetString()!;
    }
}

public sealed class TeretanaAplikacija(
    string okruzenje,
    Action<IDictionary<string, string?>> podesiKonfiguraciju,
    Action<IServiceCollection> podesiTestneServise) : WebApplicationFactory<Program>
{
    private readonly IzolovanaBaza _baza = new();

    public string PutanjaBaze => _baza.Putanja;

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _baza.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var konfiguracija = TestnaKonfiguracija.Osnovna(_baza);
        podesiKonfiguraciju(konfiguracija);

        builder.UseEnvironment(okruzenje);
        builder.ConfigureAppConfiguration((_, izvori) => izvori.AddInMemoryCollection(konfiguracija));
        builder.ConfigureTestServices(podesiTestneServise);
    }
}

/// <summary>
/// Entiteti sa ispravnim podrazumevanim vrednostima; test menja samo ono što proverava.
/// Email je jedinstven po pozivu, pa testovi ne zavise jedan od drugog.
/// </summary>
internal static class TestniEntiteti
{
    public static readonly DateTime Sada = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    public static Korisnik NoviTrener() => NoviKorisnik(Uloga.Trener);

    public static Korisnik NoviClan() => NoviKorisnik(Uloga.Clan);

    public static Termin NoviTermin(Korisnik trener, int kapacitet = 10) => new()
    {
        Trener = trener,
        Naziv = "Testni termin",
        Pocetak = Sada.AddDays(1),
        Kraj = Sada.AddDays(1).AddHours(1),
        Kapacitet = kapacitet,
        Status = StatusTermina.Aktivan,
        KreiranAt = Sada,
    };

    public static Rezervacija NovaRezervacija(Termin termin, Korisnik clan, StatusRezervacije status) => new()
    {
        Termin = termin,
        Clan = clan,
        Status = status,
        KreiranaAt = Sada,
    };

    private static Korisnik NoviKorisnik(Uloga uloga) => new()
    {
        Email = $"{Guid.NewGuid():N}@test.local",
        ImePrezime = "Testni Korisnik",
        Uloga = uloga,
        LozinkaHash = "nije-bitno-za-test",
        KreiranAt = Sada,
    };
}
