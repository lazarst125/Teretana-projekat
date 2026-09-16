using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;

namespace Teretana.Api.Podaci;

/// <summary>
/// Demonstracioni podaci. Vremena su relativna u odnosu na trenutak upisa, pa svaki ivični slučaj
/// (pun termin, lista čekanja, prošao rok za otkazivanje, završen i otkazan termin) važi odmah posle upisa.
/// </summary>
public static class PocetniPodaci
{
    public const string LozinkaTrenera = "Trener123!";
    public const string LozinkaClana = "Clan123!";

    public static class Nalozi
    {
        public const string Trener1 = "trener1@teretana.local";
        public const string Trener2 = "trener2@teretana.local";
        public const string Clan1 = "clan1@teretana.local";
        public const string Clan2 = "clan2@teretana.local";
        public const string Clan3 = "clan3@teretana.local";
        public const string Clan4 = "clan4@teretana.local";
        public const string Clan5 = "clan5@teretana.local";
        public const string Clan6 = "clan6@teretana.local";
    }

    public static class Termini
    {
        public const string BezPrijava = "Funkcionalni trening (bez prijava)";
        public const string SlobodnaMesta = "Joga (slobodna mesta)";
        public const string Pun = "Crossfit (pun termin)";
        public const string SaListomCekanja = "Spinning (lista čekanja)";
        public const string RokZaOtkazivanjeProsao = "Pilates (počinje za 90 minuta)";
        public const string Zavrsen = "Boks (završen termin)";
        public const string Otkazan = "Zumba (otkazan termin)";
    }

    public static async Task UpisiAkoJeBazaPraznaAsync(TeretanaDbContext db, TimeProvider vreme, CancellationToken cancellationToken = default)
    {
        if (await db.Korisnici.AnyAsync(cancellationToken))
        {
            return;
        }

        await UpisiAsync(db, vreme, cancellationToken);
    }

    public static async Task UpisiAsync(TeretanaDbContext db, TimeProvider vreme, CancellationToken cancellationToken = default)
    {
        var utc = vreme.GetUtcNow().UtcDateTime;
        var sada = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
        var hasher = new PasswordHasher<Korisnik>();

        Korisnik NoviKorisnik(string email, string imePrezime, Uloga uloga, string lozinka)
        {
            var korisnik = new Korisnik { Email = email, ImePrezime = imePrezime, Uloga = uloga, KreiranAt = sada.AddDays(-30) };
            korisnik.LozinkaHash = hasher.HashPassword(korisnik, lozinka);
            return korisnik;
        }

        Termin NoviTermin(Korisnik trener, string naziv, string opis, DateTime pocetak, int kapacitet, StatusTermina status, params Prijava[] prijave)
        {
            var termin = new Termin
            {
                Trener = trener,
                Naziv = naziv,
                Opis = opis,
                Pocetak = pocetak,
                Kraj = pocetak.AddHours(1),
                Kapacitet = kapacitet,
                BrojPotvrdjenih = prijave.Count(p => p.Status == StatusRezervacije.Potvrdjena),
                Status = status,
                KreiranAt = sada.AddDays(-7),
            };

            for (var i = 0; i < prijave.Length; i++)
            {
                var kreirana = termin.KreiranAt.AddHours(i + 1);
                termin.Rezervacije.Add(new Rezervacija
                {
                    Clan = prijave[i].Clan,
                    Status = prijave[i].Status,
                    KreiranaAt = kreirana,
                    PotvrdjenaAt = prijave[i].Status == StatusRezervacije.Potvrdjena ? kreirana : null,
                    OtkazanaAt = prijave[i].Status == StatusRezervacije.Otkazana ? sada : null,
                    Prisustvovao = prijave[i].Prisustvovao,
                });
            }

            return termin;
        }

        var jelena = NoviKorisnik(Nalozi.Trener1, "Jelena Petrović", Uloga.Trener, LozinkaTrenera);
        var nikola = NoviKorisnik(Nalozi.Trener2, "Nikola Jovanović", Uloga.Trener, LozinkaTrenera);
        var ana = NoviKorisnik(Nalozi.Clan1, "Ana Marković", Uloga.Clan, LozinkaClana);
        var stefan = NoviKorisnik(Nalozi.Clan2, "Stefan Ilić", Uloga.Clan, LozinkaClana);
        var milica = NoviKorisnik(Nalozi.Clan3, "Milica Đorđević", Uloga.Clan, LozinkaClana);
        var luka = NoviKorisnik(Nalozi.Clan4, "Luka Stojanović", Uloga.Clan, LozinkaClana);
        var teodora = NoviKorisnik(Nalozi.Clan5, "Teodora Pavlović", Uloga.Clan, LozinkaClana);
        var vuk = NoviKorisnik(Nalozi.Clan6, "Vuk Nikolić", Uloga.Clan, LozinkaClana);

        db.Korisnici.AddRange(jelena, nikola, ana, stefan, milica, luka, teodora, vuk);

        db.Termini.AddRange(
            NoviTermin(jelena, Termini.BezPrijava, "Trening celog tela sa sopstvenom težinom.", sada.AddDays(4), 12, StatusTermina.Aktivan),
            NoviTermin(nikola, Termini.SlobodnaMesta, "Hatha joga za sve nivoe.", sada.AddDays(2), 10, StatusTermina.Aktivan,
                Potvrdjena(teodora), Potvrdjena(vuk)),
            NoviTermin(jelena, Termini.Pun, "Intervalni trening visokog intenziteta.", sada.AddDays(1), 3, StatusTermina.Aktivan,
                Potvrdjena(ana), Potvrdjena(stefan), Potvrdjena(milica)),
            NoviTermin(jelena, Termini.SaListomCekanja, "Vožnja sobnog bicikla uz muziku.", sada.AddDays(3), 2, StatusTermina.Aktivan,
                Potvrdjena(ana), Potvrdjena(stefan), NaCekanju(milica), NaCekanju(luka)),
            NoviTermin(jelena, Termini.RokZaOtkazivanjeProsao, "Jačanje dubokih mišića trupa.", sada.AddMinutes(90), 8, StatusTermina.Aktivan,
                Potvrdjena(teodora), Potvrdjena(luka)),
            NoviTermin(nikola, Termini.Zavrsen, "Tehnika udaraca i kondicija.", sada.AddDays(-1), 6, StatusTermina.Aktivan,
                Potvrdjena(ana, prisustvovao: true), Potvrdjena(stefan, prisustvovao: false), Potvrdjena(teodora)),
            NoviTermin(jelena, Termini.Otkazan, "Plesni kardio trening.", sada.AddDays(5), 10, StatusTermina.Otkazan,
                Otkazana(vuk), Otkazana(milica)));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Prijava Potvrdjena(Korisnik clan, bool? prisustvovao = null) => new(clan, StatusRezervacije.Potvrdjena, prisustvovao);

    private static Prijava NaCekanju(Korisnik clan) => new(clan, StatusRezervacije.NaCekanju, null);

    private static Prijava Otkazana(Korisnik clan) => new(clan, StatusRezervacije.Otkazana, null);

    private sealed record Prijava(Korisnik Clan, StatusRezervacije Status, bool? Prisustvovao);
}
