using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teretana.Api.Domen;
using Teretana.Api.Podaci;

namespace Teretana.PlaywrightTestovi.Infrastruktura;

/// <summary>
/// Pravi podatke za jedan test kroz API, kao pravi klijent, sa jedinstvenim email-ovima, pa testovi ne zavise
/// od demonstracionih podataka ni jedan od drugog. Direktno u bazu hostovane aplikacije ide samo ono što API
/// ne dozvoljava: trenerski nalog i termin koji je već počeo.
/// </summary>
public sealed class TestniPodaci(HostovanaAplikacija aplikacija, IAPIRequestContext api)
{
    public const string Lozinka = "Lozinka123!";

    public static APIRequestContextOptions SaTokenom(
        PrijavljenKorisnik korisnik,
        object? telo = null,
        Dictionary<string, object>? parametri = null) => new()
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {korisnik.Token}" },
            DataObject = telo,
            Params = parametri,
        };

    public async Task<PrijavljenKorisnik> NoviTrenerAsync(string imePrezime = "Test Trener")
    {
        var trener = new Korisnik { Email = NoviEmail(), ImePrezime = imePrezime, Uloga = Uloga.Trener, KreiranAt = DateTime.UtcNow };
        trener.LozinkaHash = new PasswordHasher<Korisnik>().HashPassword(trener, Lozinka);
        await SaBazomAsync(db =>
        {
            db.Korisnici.Add(trener);
            return db.SaveChangesAsync();
        });

        return await PrijaviAsync(trener.Email);
    }

    public async Task<PrijavljenKorisnik> NoviClanAsync(string imePrezime = "Test Član")
    {
        var email = NoviEmail();
        var odgovor = await api.PostAsync("/api/auth/registracija", new() { DataObject = new { email, imePrezime, lozinka = Lozinka } });
        Assert.That(odgovor.Status, Is.EqualTo(201), "Registracija člana u pripremi testa nije uspela.");
        return await PrijaviAsync(email);
    }

    public async Task<int> NoviTerminAsync(PrijavljenKorisnik trener, int kapacitet = 10, DateTimeOffset? pocetak = null, string naziv = "Test termin")
    {
        var pocetakTermina = pocetak ?? DateTimeOffset.UtcNow.AddDays(3);
        var odgovor = await api.PostAsync("/api/termini", SaTokenom(trener, new { naziv, pocetak = pocetakTermina, kraj = pocetakTermina.AddHours(1), kapacitet }));
        Assert.That(odgovor.Status, Is.EqualTo(201), "Kreiranje termina u pripremi testa nije uspelo.");
        return (await odgovor.JsonAsync())!.Value.GetProperty("id").GetInt32();
    }

    public Task<int> RezervisiAsync(PrijavljenKorisnik clan, int idTermina) =>
        PrijaviNaTerminAsync(clan, $"/api/termini/{idTermina}/rezervacije");

    public Task<int> UpisiNaListuCekanjaAsync(PrijavljenKorisnik clan, int idTermina) =>
        PrijaviNaTerminAsync(clan, $"/api/termini/{idTermina}/lista-cekanja");

    public async Task OtkaziRezervacijuAsync(PrijavljenKorisnik clan, int idRezervacije)
    {
        var odgovor = await api.DeleteAsync($"/api/rezervacije/{idRezervacije}", SaTokenom(clan));
        Assert.That(odgovor.Status, Is.EqualTo(204), "Otkazivanje rezervacije u pripremi testa nije uspelo.");
    }

    /// <summary>Termin koji je već počeo ne može da se napravi kroz API, jer početak mora biti u budućnosti.</summary>
    public Task PomeriTerminAsync(int idTermina, DateTime pocetakUtc, DateTime krajUtc) =>
        SaBazomAsync(db => db.Termini
            .Where(t => t.Id == idTermina)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Pocetak, pocetakUtc).SetProperty(t => t.Kraj, krajUtc)));

    private async Task<int> PrijaviNaTerminAsync(PrijavljenKorisnik clan, string putanja)
    {
        var odgovor = await api.PostAsync(putanja, SaTokenom(clan));
        Assert.That(odgovor.Status, Is.EqualTo(201), $"Prijava na termin ({putanja}) u pripremi testa nije uspela.");
        return (await odgovor.JsonAsync())!.Value.GetProperty("id").GetInt32();
    }

    private async Task<PrijavljenKorisnik> PrijaviAsync(string email)
    {
        var odgovor = await api.PostAsync("/api/auth/prijava", new() { DataObject = new { email, lozinka = Lozinka } });
        Assert.That(odgovor.Status, Is.EqualTo(200), "Prijava u pripremi testa nije uspela.");
        var telo = (await odgovor.JsonAsync())!.Value;
        var korisnik = telo.GetProperty("korisnik");
        return new PrijavljenKorisnik(
            korisnik.GetProperty("id").GetInt32(),
            email,
            korisnik.GetProperty("imePrezime").GetString()!,
            korisnik.GetProperty("uloga").GetString()!,
            telo.GetProperty("token").GetString()!,
            telo.GetProperty("istice").GetString()!);
    }

    private async Task SaBazomAsync(Func<TeretanaDbContext, Task> akcija)
    {
        await using var scope = aplikacija.Services.CreateAsyncScope();
        await akcija(scope.ServiceProvider.GetRequiredService<TeretanaDbContext>());
    }

    private static string NoviEmail() => $"{Guid.NewGuid():N}@test.local";
}

public sealed record PrijavljenKorisnik(int Id, string Email, string ImePrezime, string Uloga, string Token, string Istice);
