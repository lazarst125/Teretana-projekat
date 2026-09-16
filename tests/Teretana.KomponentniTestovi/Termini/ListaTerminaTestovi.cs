using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Termini;

public sealed class ListaTerminaTestovi : KomponentniTest
{
    [Test]
    public async Task Lista_DrugaStranica_VracaTrazeneStavkeIUkupanBroj()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var termini = new List<Termin>();
        for (var i = 1; i <= 5; i++)
        {
            termini.Add(await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(i)));
        }

        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        var stranica = await Klijent.GetFromJsonAsync<StranicaTelo>("/api/termini?stranica=2&velicinaStranice=2");

        Assert.That(stranica, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(stranica!.Stavke.Select(t => t.Id), Is.EqualTo(new[] { termini[2].Id, termini[3].Id }));
            Assert.That(stranica.UkupnoStavki, Is.EqualTo(5));
            Assert.That(stranica.UkupnoStranica, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Lista_SamoSlobodniUOpseguDatuma_VracaSamoTermineKojiMoguDaSeRezervisu()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var clan = await NoviKorisnikUBaziAsync(Uloga.Clan);
        var slobodanUOpsegu = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(2));
        var punUOpsegu = await NoviTerminUBaziAsync(trener, kapacitet: 1, pocetak: TestniEntiteti.Sada.AddDays(2));
        await NovaPrijavaUBaziAsync(punUOpsegu, clan, StatusRezervacije.Potvrdjena);
        await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(10));
        await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddHours(-1));
        var otkazanUOpsegu = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(3));
        await SaBazomAsync(db => db.Termini.Where(t => t.Id == otkazanUOpsegu.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusTermina.Otkazan)));
        await PrijaviSeKaoAsync(clan);

        var od = Uri.EscapeDataString(TestniEntiteti.Sada.AddDays(-1).ToString("O"));
        var @do = Uri.EscapeDataString(TestniEntiteti.Sada.AddDays(5).ToString("O"));
        var stranica = await Klijent.GetFromJsonAsync<StranicaTelo>($"/api/termini?samoSlobodni=true&od={od}&do={@do}");

        Assert.That(stranica?.Stavke.Select(t => t.Id), Is.EqualTo(new[] { slobodanUOpsegu.Id }));
    }

    [Test]
    public async Task Lista_SortiranjePoPocetkuOpadajuce_VracaNajkasnijiTerminPrvi()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var sutra = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(1));
        var zaTriDana = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(3));
        var prekosutra = await NoviTerminUBaziAsync(trener, pocetak: TestniEntiteti.Sada.AddDays(2));
        await PrijaviSeKaoAsync(trener);

        var stranica = await Klijent.GetFromJsonAsync<StranicaTelo>("/api/termini?sortiranje=-pocetak");

        Assert.That(stranica?.Stavke.Select(t => t.Id), Is.EqualTo(new[] { zaTriDana.Id, prekosutra.Id, sutra.Id }));
    }

    [Test]
    public async Task Lista_VelicinaStraniceVecaOdDozvoljene_Vraca400SaGreskomZaVelicinuStranice()
    {
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.GetAsync("/api/termini?velicinaStranice=101");

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem.GetProperty("errors").EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "velicinaStranice" }));
        });
    }

    [Test]
    public async Task Lista_BezIjednogTermina_VracaPraznuListuUIstomOmotacu()
    {
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        var stranica = await Klijent.GetFromJsonAsync<StranicaTelo>("/api/termini");

        Assert.That(stranica, Is.EqualTo(new StranicaTelo([], 1, 20, 0, 0)).Using<StranicaTelo>((a, b) =>
            a.Stavke.Count == b.Stavke.Count && a.Stranica == b.Stranica && a.VelicinaStranice == b.VelicinaStranice
            && a.UkupnoStavki == b.UkupnoStavki && a.UkupnoStranica == b.UkupnoStranica));
    }

    [TestCase("naziv", "A,B,C")]
    [TestCase("-naziv", "C,B,A")]
    [TestCase("slobodnaMesta", "C,B,A")]
    [TestCase("-slobodnaMesta", "A,B,C")]
    public async Task Lista_Sortiranje_VracaTermineUTrazenomRedosledu(string sortiranje, string ocekivaniRedosled)
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        var saCetiriSlobodna = await NoviTerminUBaziAsync(trener, kapacitet: 5, pocetak: TestniEntiteti.Sada.AddDays(1), naziv: "B");
        await NovaPrijavaUBaziAsync(saCetiriSlobodna, await NoviKorisnikUBaziAsync(Uloga.Clan), StatusRezervacije.Potvrdjena);
        await NoviTerminUBaziAsync(trener, kapacitet: 10, pocetak: TestniEntiteti.Sada.AddDays(2), naziv: "A");
        await NoviTerminUBaziAsync(trener, kapacitet: 2, pocetak: TestniEntiteti.Sada.AddDays(3), naziv: "C");
        await PrijaviSeKaoAsync(trener);

        var stranica = await Klijent.GetFromJsonAsync<StranicaTelo>($"/api/termini?sortiranje={Uri.EscapeDataString(sortiranje)}");

        Assert.That(stranica?.Stavke.Select(t => t.Naziv), Is.EqualTo(ocekivaniRedosled.Split(',')));
    }

    [Test]
    public async Task Lista_FilterStatusOtkazan_VracaSamoOtkazaneTermine()
    {
        var trener = await NoviKorisnikUBaziAsync(Uloga.Trener);
        await NoviTerminUBaziAsync(trener);
        var otkazan = await NoviTerminUBaziAsync(trener);
        await SaBazomAsync(db => db.Termini.Where(t => t.Id == otkazan.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusTermina.Otkazan)));
        await PrijaviSeKaoAsync(trener);

        var stranica = await Klijent.GetFromJsonAsync<StranicaTelo>("/api/termini?status=Otkazan");

        Assert.That(stranica?.Stavke.Select(t => t.Id), Is.EqualTo(new[] { otkazan.Id }));
    }

    [Test]
    public async Task Lista_NepodrzanoSortiranje_Vraca400SaGreskomZaSortiranje()
    {
        await PrijaviSeKaoAsync(await NoviKorisnikUBaziAsync(Uloga.Clan));

        using var odgovor = await Klijent.GetAsync("/api/termini?sortiranje=kapacitet");

        var problem = await ProblemIzOdgovoraAsync(odgovor);
        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem.GetProperty("errors").EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "sortiranje" }));
        });
    }
}
