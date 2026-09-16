using System.Net;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Autentikacija;

/// <summary>
/// Brisanje sopstvenog naloga. Pravilo je isto kao za brisanje termina: sve što ima prijave ili termine
/// ostaje, da bi istorija bila sačuvana, a strani ključ sa Restrict to sprovodi i kad se servis zaobiđe.
/// </summary>
public sealed class BrisanjeNalogaTestovi : KomponentniTest
{
    private const int SqliteConstraint = 19;

    [Test]
    public async Task Brisanje_ClanBezPrijava_Vraca204INalogViseNePostoji()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await PrijaviSeKaoAsync(clan);

        using var odgovor = await Klijent.DeleteAsync("/api/auth/ja");

        var postoji = await SaBazomAsync(db => db.Korisnici.AnyAsync(k => k.Id == clan.Id));
        using var profilStarimTokenom = await Klijent.GetAsync("/api/auth/ja");
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(postoji, Is.False);
        });
        await OcekujProblemAsync(profilStarimTokenom, HttpStatusCode.Unauthorized, "korisnik-ne-postoji");
    }

    [Test]
    public async Task Brisanje_ClanSaPotvrdjenomRezervacijom_Vraca409NalogImaPodatkeINalogOstaje()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await NovaPrijavaUBaziAsync(termin, clan, StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(clan);

        using var odgovor = await Klijent.DeleteAsync("/api/auth/ja");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "nalog-ima-podatke");
        Assert.That(await SaBazomAsync(db => db.Korisnici.AnyAsync(k => k.Id == clan.Id)), Is.True);
    }

    [Test]
    public async Task Brisanje_ClanSaSamoOtkazanomPrijavom_Vraca409JerSeIstorijaCuva()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await NovaPrijavaUBaziAsync(termin, clan, StatusRezervacije.Otkazana);
        await PrijaviSeKaoAsync(clan);

        using var odgovor = await Klijent.DeleteAsync("/api/auth/ja");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "nalog-ima-podatke");
        Assert.That(await SaBazomAsync(db => db.Korisnici.AnyAsync(k => k.Id == clan.Id)), Is.True);
    }

    [Test]
    public async Task Brisanje_TrenerSaTerminom_Vraca409INiNalogNiTerminNeNestaju()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.DeleteAsync("/api/auth/ja");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "nalog-ima-podatke");
        var (nalogPostoji, terminPostoji) = await SaBazomAsync(async db => (
            await db.Korisnici.AnyAsync(k => k.Id == trener.Id),
            await db.Termini.AnyAsync(t => t.Id == termin.Id)));
        Assert.Multiple(() =>
        {
            Assert.That(nalogPostoji, Is.True);
            Assert.That(terminPostoji, Is.True);
        });
    }

    [Test]
    public async Task BrisanjeMimoServisa_NalogSaPrijavom_BazaOdbijaStranimKljucem()
    {
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await NovaPrijavaUBaziAsync(termin, clan, StatusRezervacije.Potvrdjena);

        var greska = await UpisKojiBazaOdbijaAsync(async db => db.Korisnici.Remove(await db.Korisnici.SingleAsync(k => k.Id == clan.Id)));

        Assert.Multiple(() =>
        {
            Assert.That(greska.SqliteErrorCode, Is.EqualTo(SqliteConstraint));
            Assert.That(greska.Message, Does.Contain("FOREIGN KEY constraint failed"));
        });
    }
}
