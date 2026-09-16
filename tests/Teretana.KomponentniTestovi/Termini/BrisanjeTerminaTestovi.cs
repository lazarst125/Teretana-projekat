using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Teretana.Api.Domen;
using Teretana.Api.Repozitorijumi;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Termini;

public sealed class BrisanjeTerminaTestovi : KomponentniTest
{
    private const int SqliteConstraint = 19;

    [Test]
    public async Task Brisanje_SopstveniTerminBezPrijava_Vraca204ITerminViseNePostoji()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.DeleteAsync($"/api/termini/{termin.Id}");

        var postoji = await SaBazomAsync(db => db.Termini.AnyAsync(t => t.Id == termin.Id));
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(postoji, Is.False);
        });
    }

    [Test]
    public async Task Brisanje_TerminSaOtkazanomPrijavom_Vraca409ITerminOstaje()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termin = await NoviTerminUBaziAsync(trener);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Otkazana);
        await PrijaviSeKaoAsync(trener);

        using var odgovor = await Klijent.DeleteAsync($"/api/termini/{termin.Id}");

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "termin-ima-prijave");
        Assert.That(await SaBazomAsync(db => db.Termini.AnyAsync(t => t.Id == termin.Id)), Is.True);
    }

    [Test]
    public async Task BrisanjeMimoServisa_TerminSaPrijavom_BazaOdbijaStranimKljucem()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);

        var greska = await UpisKojiBazaOdbijaAsync(async db => db.Termini.Remove(await db.Termini.SingleAsync(t => t.Id == termin.Id)));

        Assert.Multiple(() =>
        {
            Assert.That(greska.SqliteErrorCode, Is.EqualTo(SqliteConstraint));
            Assert.That(greska.Message, Does.Contain("FOREIGN KEY constraint failed"));
        });
    }

    [Test]
    public async Task BrisanjeURepozitorijumu_PrijavaNastalaPosleProvereUServisu_VracaFalseITerminOstaje()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener));
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await using var scope = Aplikacija.Services.CreateAsyncScope();
        var repozitorijum = scope.ServiceProvider.GetRequiredService<ITerminRepozitorijum>();

        var obrisan = await repozitorijum.ObrisiAkoNemaPrijavaAsync(termin.Id, CancellationToken.None);

        var terminPostoji = await SaBazomAsync(db => db.Termini.AnyAsync(t => t.Id == termin.Id));
        Assert.Multiple(() =>
        {
            Assert.That(obrisan, Is.False);
            Assert.That(terminPostoji, Is.True);
        });
    }
}
