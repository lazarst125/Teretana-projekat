using Microsoft.AspNetCore.Identity;
using Teretana.Api.Domen;
using Teretana.Api.Infrastruktura.Autentikacija;
using Teretana.Api.Repozitorijumi;
using Teretana.Api.Ugovori.Autentikacija;

namespace Teretana.Api.Servisi;

public interface IAutentikacijaServis
{
    Task<KorisnikOdgovor> RegistrujClanaAsync(RegistracijaZahtev zahtev, CancellationToken cancellationToken);

    Task<PrijavaOdgovor> PrijaviAsync(PrijavaZahtev zahtev, CancellationToken cancellationToken);

    Task<KorisnikOdgovor> VratiKorisnikaAsync(int idKorisnika, CancellationToken cancellationToken);

    Task<KorisnikOdgovor> IzmeniProfilAsync(int idKorisnika, ProfilZahtev zahtev, CancellationToken cancellationToken);

    Task ObrisiNalogAsync(int idKorisnika, CancellationToken cancellationToken);
}

internal sealed partial class AutentikacijaServis(
    IKorisnikRepozitorijum korisnici,
    IPasswordHasher<Korisnik> hasher,
    ITokenServis tokeni,
    TimeProvider vreme,
    ILogger<AutentikacijaServis> logger) : IAutentikacijaServis
{
    // Prijava sa nepostojećim email-om svejedno proverava lozinku nad ovim hešom, da trajanje odgovora
    // ne bi otkrilo koji email-ovi imaju nalog.
    private static readonly Korisnik LazniKorisnik = new() { Email = string.Empty, ImePrezime = string.Empty };
    private static readonly string LazniHes = new PasswordHasher<Korisnik>().HashPassword(LazniKorisnik, Guid.NewGuid().ToString());

    public async Task<KorisnikOdgovor> RegistrujClanaAsync(RegistracijaZahtev zahtev, CancellationToken cancellationToken)
    {
        var email = zahtev.Email.Trim();
        if (await korisnici.PostojiEmailAsync(email, cancellationToken))
        {
            throw Greske.EmailZauzet();
        }

        var korisnik = new Korisnik
        {
            Email = email,
            ImePrezime = zahtev.ImePrezime.Trim(),
            Uloga = Uloga.Clan,
            KreiranAt = vreme.GetUtcNow().UtcDateTime,
        };
        korisnik.LozinkaHash = hasher.HashPassword(korisnik, zahtev.Lozinka);

        await korisnici.DodajAsync(korisnik, cancellationToken);
        LogRegistrovanClan(logger, korisnik.Id);

        return KorisnikOdgovor.Iz(korisnik);
    }

    public async Task<PrijavaOdgovor> PrijaviAsync(PrijavaZahtev zahtev, CancellationToken cancellationToken)
    {
        var korisnik = await korisnici.PronadjiPoEmailuAsync(zahtev.Email.Trim(), cancellationToken);
        if (korisnik is null)
        {
            _ = hasher.VerifyHashedPassword(LazniKorisnik, LazniHes, zahtev.Lozinka);
            LogNeuspesnaPrijava(logger);
            throw Greske.NeispravniKredencijali();
        }

        if (hasher.VerifyHashedPassword(korisnik, korisnik.LozinkaHash, zahtev.Lozinka) == PasswordVerificationResult.Failed)
        {
            LogNeuspesnaPrijava(logger);
            throw Greske.NeispravniKredencijali();
        }

        var token = tokeni.Izdaj(korisnik);
        LogUspesnaPrijava(logger, korisnik.Id, korisnik.Uloga);

        return new PrijavaOdgovor(token.Token, token.Istice, KorisnikOdgovor.Iz(korisnik));
    }

    public async Task<KorisnikOdgovor> VratiKorisnikaAsync(int idKorisnika, CancellationToken cancellationToken)
    {
        var korisnik = await korisnici.PronadjiPoIdAsync(idKorisnika, cancellationToken)
            ?? throw Greske.KorisnikIzTokenaNePostoji();

        return KorisnikOdgovor.Iz(korisnik);
    }

    public async Task<KorisnikOdgovor> IzmeniProfilAsync(int idKorisnika, ProfilZahtev zahtev, CancellationToken cancellationToken)
    {
        var korisnik = await korisnici.PronadjiPoIdAsync(idKorisnika, cancellationToken)
            ?? throw Greske.KorisnikIzTokenaNePostoji();

        var imePrezime = zahtev.ImePrezime.Trim();
        var lozinkaHash = zahtev.Lozinka is null ? null : hasher.HashPassword(korisnik, zahtev.Lozinka);

        if (!await korisnici.AzurirajAsync(korisnik.Id, imePrezime, lozinkaHash, cancellationToken))
        {
            throw Greske.KorisnikIzTokenaNePostoji();
        }

        LogIzmenjenProfil(logger, korisnik.Id, lozinkaHash is not null);

        return new KorisnikOdgovor(korisnik.Id, korisnik.Email, imePrezime, korisnik.Uloga);
    }

    public async Task ObrisiNalogAsync(int idKorisnika, CancellationToken cancellationToken)
    {
        var korisnik = await korisnici.PronadjiPoIdAsync(idKorisnika, cancellationToken)
            ?? throw Greske.KorisnikIzTokenaNePostoji();

        if (await korisnici.ImaVezanePodatkeAsync(korisnik.Id, cancellationToken))
        {
            throw Greske.NalogImaPodatke();
        }

        // Termin ili prijava je mogla da nastane posle provere; tada brisanje odbija strani ključ.
        if (!await korisnici.ObrisiAsync(korisnik.Id, cancellationToken))
        {
            throw Greske.NalogImaPodatke();
        }

        LogObrisanNalog(logger, korisnik.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Izmenjen je profil korisnika {KorisnikId} (lozinka promenjena: {LozinkaPromenjena})")]
    private static partial void LogIzmenjenProfil(ILogger logger, int korisnikId, bool lozinkaPromenjena);

    [LoggerMessage(Level = LogLevel.Information, Message = "Obrisan je nalog korisnika {KorisnikId}")]
    private static partial void LogObrisanNalog(ILogger logger, int korisnikId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registrovan je novi član {KorisnikId}")]
    private static partial void LogRegistrovanClan(ILogger logger, int korisnikId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uspešna prijava korisnika {KorisnikId} sa ulogom {Uloga}")]
    private static partial void LogUspesnaPrijava(ILogger logger, int korisnikId, Uloga uloga);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Neuspešna prijava: pogrešan email ili lozinka")]
    private static partial void LogNeuspesnaPrijava(ILogger logger);
}
