using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Rezervacije;

public sealed class ListaCekanjaTestovi : KomponentniTest
{
    [Test]
    public async Task PrijavaNaListuCekanja_PunTermin_Vraca201NaCekanjuIzaRanijePrijavljenih()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await NovaPrijavaUBaziAsync(termin, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.NaCekanju, TestniEntiteti.Sada.AddHours(-1));
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/lista-cekanja", content: null);

        var prijava = await odgovor.Content.ReadFromJsonAsync<RezervacijaTelo>();
        var brojPotvrdjenih = await SaBazomAsync(db => db.Termini.Where(t => t.Id == termin.Id).Select(t => t.BrojPotvrdjenih).SingleAsync());
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(odgovor.Headers.Location?.ToString(), Does.EndWith($"/api/rezervacije/{prijava?.Id}"));
            Assert.That(prijava?.Status, Is.EqualTo("NaCekanju"));
            Assert.That(prijava?.PozicijaNaCekanju, Is.EqualTo(2));
            Assert.That(brojPotvrdjenih, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task PrijavaNaListuCekanja_TerminSaSlobodnimMestima_Vraca409TerminImaSlobodnihMesta()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 2);
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/lista-cekanja", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "termin-ima-slobodnih-mesta");
    }

    [Test]
    public async Task PrijavaNaListuCekanja_ClanVecImaPotvrdjenuRezervaciju_Vraca409VecPrijavljen()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        await NovaPrijavaUBaziAsync(termin, clan, StatusRezervacije.Potvrdjena);
        await PrijaviSeKaoAsync(clan);

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/lista-cekanja", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "vec-prijavljen");
    }

    [Test]
    public async Task PrijavaNaListuCekanja_OtkazanTermin_Vraca409TerminOtkazan()
    {
        var termin = await NoviTerminUBaziAsync(await NoviKorisnikUBaziAsync(Uloga.Trener), kapacitet: 1);
        await SaBazomAsync(db => db.Termini.Where(t => t.Id == termin.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusTermina.Otkazan)));
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.PostAsync($"/api/termini/{termin.Id}/lista-cekanja", content: null);

        await OcekujProblemAsync(odgovor, HttpStatusCode.Conflict, "termin-otkazan");
    }
}
