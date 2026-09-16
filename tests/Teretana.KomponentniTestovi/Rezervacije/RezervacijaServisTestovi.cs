using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Teretana.Api.Domen;
using Teretana.Api.Repozitorijumi;
using Teretana.Api.Servisi;

namespace Teretana.KomponentniTestovi.Rezervacije;

/// <summary>
/// Servis sa lažnim repozitorijumima, bez baze: proverava da se pravilo roka primenjuje pre nego što
/// otkazivanje uopšte stigne do baze.
/// </summary>
[Parallelizable(ParallelScope.All)]
[Category("Unit")]
public sealed class RezervacijaServisTestovi
{
    private const int IdClana = 3;
    private const int IdTrenera = 2;
    private const int IdRezervacije = 7;
    private const int IdTermina = 11;
    private static readonly DateTime Sada = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly IRezervacijaRepozitorijum _rezervacije = Substitute.For<IRezervacijaRepozitorijum>();
    private readonly ITerminRepozitorijum _termini = Substitute.For<ITerminRepozitorijum>();
    private readonly RezervacijaServis _servis;

    public RezervacijaServisTestovi() =>
        _servis = new RezervacijaServis(
            _rezervacije,
            _termini,
            new PolitikaOtkazivanja(TimeSpan.FromHours(2)),
            new FakeTimeProvider(new DateTimeOffset(Sada)),
            NullLogger<RezervacijaServis>.Instance);

    [Test]
    public void Otkazivanje_PotvrdjenaRezervacijaPosleIstekaRoka_OdbijaSeIRepozitorijumNeOtkazuje()
    {
        _rezervacije.VratiAsync(IdRezervacije, Arg.Any<CancellationToken>()).Returns(PotvrdjenaRezervacija(pocetakTermina: Sada.AddHours(1)));

        var greska = Assert.ThrowsAsync<DomenskaGreska>(() => _servis.OtkaziAsync(IdClana, IdRezervacije, CancellationToken.None));

        Assert.That(greska?.Kod, Is.EqualTo("rok-za-otkazivanje-istekao"));
        _ = _rezervacije.DidNotReceiveWithAnyArgs().OtkaziAsync(default, default, default);
    }

    [Test]
    public async Task Otkazivanje_PotvrdjenaRezervacijaPreIstekaRoka_PredajeOtkazivanjeRepozitorijumuSaTrenutnimVremenom()
    {
        _rezervacije.VratiAsync(IdRezervacije, Arg.Any<CancellationToken>()).Returns(PotvrdjenaRezervacija(pocetakTermina: Sada.AddDays(1)));
        _rezervacije.OtkaziAsync(IdRezervacije, Sada, Arg.Any<CancellationToken>()).Returns(new RezultatOtkazivanja(true, null));

        await _servis.OtkaziAsync(IdClana, IdRezervacije, CancellationToken.None);

        await _rezervacije.Received(1).OtkaziAsync(IdRezervacije, Sada, Arg.Any<CancellationToken>());
    }

    [Test]
    public void Otkazivanje_RezervacijaOtkazanaPosleProvere_OdbijaSeSaRezervacijaOtkazana()
    {
        _rezervacije.VratiAsync(IdRezervacije, Arg.Any<CancellationToken>()).Returns(PotvrdjenaRezervacija(pocetakTermina: Sada.AddDays(1)));
        _rezervacije.OtkaziAsync(IdRezervacije, Sada, Arg.Any<CancellationToken>()).Returns(new RezultatOtkazivanja(false, null));

        var greska = Assert.ThrowsAsync<DomenskaGreska>(() => _servis.OtkaziAsync(IdClana, IdRezervacije, CancellationToken.None));

        Assert.That(greska?.Kod, Is.EqualTo("rezervacija-otkazana"));
    }

    [Test]
    public void Prisustvo_RezervacijaOtkazanaPosleProvere_OdbijaSeSaRezervacijaNijePotvrdjena()
    {
        _rezervacije.VratiAsync(IdRezervacije, Arg.Any<CancellationToken>()).Returns(PotvrdjenaRezervacija(pocetakTermina: Sada.AddHours(-1)));
        _rezervacije.EvidentirajPrisustvoAsync(IdRezervacije, true, Arg.Any<CancellationToken>()).Returns(false);

        var greska = Assert.ThrowsAsync<DomenskaGreska>(() => _servis.EvidentirajPrisustvoAsync(IdTrenera, IdRezervacije, true, CancellationToken.None));

        Assert.That(greska?.Kod, Is.EqualTo("rezervacija-nije-potvrdjena"));
    }

    [Test]
    public void Rezervacija_IstiClanUpisanPosleProvere_OdbijaSeSaVecPrijavljen()
    {
        _termini.PronadjiAsync(IdTermina, Arg.Any<CancellationToken>()).Returns(new Termin
        {
            Id = IdTermina,
            TrenerId = IdTrenera,
            Naziv = "Joga",
            Pocetak = Sada.AddDays(1),
            Kraj = Sada.AddDays(1).AddHours(1),
            Kapacitet = 10,
            Status = StatusTermina.Aktivan,
        });
        _rezervacije.ImaAktivnuPrijavuAsync(IdTermina, IdClana, Arg.Any<CancellationToken>()).Returns(false);
        _rezervacije.RezervisiAsync(IdTermina, IdClana, Sada, Arg.Any<CancellationToken>()).Returns(new RezultatUpisa(IshodUpisa.VecPrijavljen, null));

        var greska = Assert.ThrowsAsync<DomenskaGreska>(() => _servis.RezervisiAsync(IdClana, IdTermina, CancellationToken.None));

        Assert.That(greska?.Kod, Is.EqualTo("vec-prijavljen"));
    }

    private static RezervacijaPodaci PotvrdjenaRezervacija(DateTime pocetakTermina) => new(
        IdRezervacije,
        IdClana,
        StatusRezervacije.Potvrdjena,
        Sada.AddDays(-1),
        Sada.AddDays(-1),
        null,
        null,
        IdTermina,
        "Joga",
        pocetakTermina,
        pocetakTermina.AddHours(1),
        StatusTermina.Aktivan,
        IdTrenera,
        "Testni Trener");
}
