using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teretana.Api.Domen;
using Teretana.Api.Podaci;
using Teretana.Api.Repozitorijumi;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Rezervacije;

public sealed class RezervisanjeTestovi : KomponentniTest
{
    private const int SqliteConstraintUnique = 2067;

    [Test]
    public async Task Rezervacija_SlobodnoMesto_Vraca201SaPotvrdjenomRezervacijomIUvecavaBrojPotvrdjenih()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 2);
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/rezervacije", content: null);

        var rezervacija = await odgovor.Content.ReadFromJsonAsync<RezervacijaTelo>();
        var brojPotvrdjenih = await BrojPotvrdjenihAsync(termin.Id);
        Assert.That(rezervacija, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(odgovor.Headers.Location?.ToString(), Does.EndWith($"/api/rezervacije/{rezervacija!.Id}"));
            Assert.That(rezervacija.Status, Is.EqualTo("Potvrdjena"));
            Assert.That(rezervacija.Termin.Id, Is.EqualTo(termin.Id));
            Assert.That(brojPotvrdjenih, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Rezervacija_ClanVecImaPotvrdjenuRezervaciju_Vraca409VecPrijavljenIBrojacSeNeMenja()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 5);
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await NovaPrijavaUBaziAsync(termin, clan, StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(clan);

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/rezervacije", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "vec-prijavljen");
        Assert.That(await BrojPotvrdjenihAsync(termin.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task DrugaPotvrdjenaRezervacijaIstogClanaMimoServisa_BazaOdbijaJedinstvenimIndeksom()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 5);
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await NovaPrijavaUBaziAsync(termin, clan, StatusRezervacije.Potvrdjena);

        var greska = await UpisKojiBazaOdbijaAsync(db =>
        {
            db.Rezervacije.Add(new Rezervacija { TerminId = termin.Id, ClanId = clan.Id, Status = StatusRezervacije.Potvrdjena, KreiranaAt = TestniEntiteti.Sada });
            return Task.CompletedTask;
        });

        Assert.That(greska.SqliteExtendedErrorCode, Is.EqualTo(SqliteConstraintUnique));
    }

    [Test]
    public async Task Rezervacija_PunTermin_Vraca409TerminPopunjen()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/rezervacije", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "termin-popunjen");
    }

    [Test]
    public async Task DvaIstovremenaZahtevaZaPoslednjeMesto_TacnoJedanDobijaMestoADrugiDobija409TerminPopunjen()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        using var prvi = await NoviKlijentZaAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));
        using var drugi = await NoviKlijentZaAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var zahtevi = new[] { prvi, drugi }
            .Select(async klijent =>
            {
                await start.Task;
                return await klijent.PostAsync($"/api/termini/{termin.Id}/rezervacije", content: null);
            })
            .ToArray();

        start.SetResult();
        var odgovori = await Task.WhenAll(zahtevi);

        try
        {
            var brojPotvrdjenih = await BrojPotvrdjenihAsync(termin.Id);
            var potvrdjenihRezervacija = await SaBazomAsync(db =>
                db.Rezervacije.CountAsync(r => r.TerminId == termin.Id && r.Status == StatusRezervacije.Potvrdjena));
            Assert.Multiple(() =>
            {
                Assert.That(odgovori.Select(o => o.StatusCode), Is.EquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
                Assert.That(brojPotvrdjenih, Is.EqualTo(1));
                Assert.That(potvrdjenihRezervacija, Is.EqualTo(1));
            });
            await OcekujProblemAsync(odgovori.First(o => o.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, "termin-popunjen");
        }
        finally
        {
            foreach (var odgovor in odgovori)
            {
                odgovor.Dispose();
            }
        }
    }

    [Test]
    public async Task DvaKontekstaKojaObaVideSlobodnoMesto_UslovnoZauzimanjePropustaSamoPrvog()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        var prviClan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var drugiClan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await using var prviScope = Aplikacija.Services.CreateAsyncScope();
        await using var drugiScope = Aplikacija.Services.CreateAsyncScope();
        var prviVidiSlobodnoMesto = await ImaSlobodnoMestoAsync(prviScope, termin.Id);
        var drugiVidiSlobodnoMesto = await ImaSlobodnoMestoAsync(drugiScope, termin.Id);

        var prvi = await Repozitorijum(prviScope).RezervisiAsync(termin.Id, prviClan.Id, TestniEntiteti.Sada, CancellationToken.None);
        var drugi = await Repozitorijum(drugiScope).RezervisiAsync(termin.Id, drugiClan.Id, TestniEntiteti.Sada, CancellationToken.None);

        var brojPotvrdjenih = await BrojPotvrdjenihAsync(termin.Id);
        Assert.Multiple(() =>
        {
            Assert.That(prviVidiSlobodnoMesto && drugiVidiSlobodnoMesto, Is.True, "Oba konteksta moraju pre upisa da vide slobodno mesto.");
            Assert.That(prvi.Ishod, Is.EqualTo(IshodUpisa.Upisano));
            Assert.That(drugi.Ishod, Is.EqualTo(IshodUpisa.UslovNijeIspunjen));
            Assert.That(brojPotvrdjenih, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Rezervacija_TerminKojiJePoceo_Vraca422TerminJePoceo()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), pocetak: TestniEntiteti.Sada.AddMinutes(-10));
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/rezervacije", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.UnprocessableEntity, "termin-je-poceo");
    }

    [Test]
    public async Task Rezervacija_OtkazanTermin_Vraca409TerminOtkazan()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await SaBazomAsync(db => db.Termini.Where(t => t.Id == termin.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusTermina.Otkazan)));
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/rezervacije", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "termin-otkazan");
    }

    /// <summary>
    /// Simulira trku: servis nije video prijavu, a isti član je u međuvremenu upisan na listu čekanja. Mesto je slobodno,
    /// pa uslovno zauzimanje prolazi, a jedinstveni indeks odbija upis i transakcija vraća zauzeto mesto.
    /// </summary>
    [Test]
    public async Task RezervacijaURepozitorijumu_ClanVecImaAktivnuPrijavu_VracaVecPrijavljenIPonistavaZauzetoMesto()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 2);
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await NovaPrijavaUBaziAsync(termin, clan, StatusRezervacije.NaCekanju);
        await using var scope = Aplikacija.Services.CreateAsyncScope();

        var rezultat = await Repozitorijum(scope).RezervisiAsync(termin.Id, clan.Id, TestniEntiteti.Sada, CancellationToken.None);

        var brojPotvrdjenih = await BrojPotvrdjenihAsync(termin.Id);
        Assert.Multiple(() =>
        {
            Assert.That(rezultat.Ishod, Is.EqualTo(IshodUpisa.VecPrijavljen));
            Assert.That(brojPotvrdjenih, Is.Zero);
        });
    }

    private static Task<bool> ImaSlobodnoMestoAsync(AsyncServiceScope scope, int idTermina) =>
        scope.ServiceProvider.GetRequiredService<TeretanaDbContext>().Termini
            .Where(t => t.Id == idTermina)
            .Select(t => t.BrojPotvrdjenih < t.Kapacitet)
            .SingleAsync();

    private static IRezervacijaRepozitorijum Repozitorijum(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IRezervacijaRepozitorijum>();

    private Task<int> BrojPotvrdjenihAsync(int idTermina) =>
        SaBazomAsync(db => db.Termini.Where(t => t.Id == idTermina).Select(t => t.BrojPotvrdjenih).SingleAsync());
}
