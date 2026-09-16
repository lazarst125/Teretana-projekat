using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Teretana.Api.Domen;
using Teretana.Api.Repozitorijumi;
using Teretana.Api.Servisi;
using Teretana.Api.Ugovori.Termini;

namespace Teretana.KomponentniTestovi.Termini;

/// <summary>
/// Grane u kojima je provera u servisu prošla, a uslovni upis u bazi nije uspeo jer je drugi zahtev u međuvremenu
/// promenio termin. Kroz HTTP se taj redosled ne može pouzdano izazvati, pa repozitorijum vraća stanje posle trke.
/// </summary>
[Parallelizable(ParallelScope.All)]
[Category("Unit")]
public sealed class TerminServisTestovi
{
    private const int IdTrenera = 2;
    private const int IdTermina = 11;
    private static readonly DateTime Sada = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly ITerminRepozitorijum _termini = Substitute.For<ITerminRepozitorijum>();
    private readonly TerminServis _servis;

    public TerminServisTestovi() =>
        _servis = new TerminServis(_termini, new FakeTimeProvider(new DateTimeOffset(Sada)), NullLogger<TerminServis>.Instance);

    [TestCase(StatusTermina.Aktivan, "termin-ima-prijave", TestName = "Izmena_PrijavaNastalaPosleProvere_OdbijaSeSaTerminImaPrijave")]
    [TestCase(StatusTermina.Otkazan, "termin-otkazan", TestName = "Izmena_TerminOtkazanPosleProvere_OdbijaSeSaTerminOtkazan")]
    public void Izmena_UslovnaIzmenaNijeUspela_VracaGreskuPremaStanjuPosleTrke(StatusTermina statusPosleTrke, string ocekivaniKod)
    {
        _termini.PronadjiAsync(IdTermina, Arg.Any<CancellationToken>()).Returns(NoviTermin(StatusTermina.Aktivan), NoviTermin(statusPosleTrke));
        _termini.ImaAktivnePrijaveAsync(IdTermina, Arg.Any<CancellationToken>()).Returns(false);
        _termini.IzmeniAkoNemaAktivnihPrijavaAsync(IdTermina, Arg.Any<IzmenaTermina>(), Arg.Any<CancellationToken>()).Returns(false);

        var greska = Assert.ThrowsAsync<DomenskaGreska>(() => _servis.IzmeniAsync(IdTrenera, IdTermina, IspravanZahtev(), CancellationToken.None));

        Assert.That(greska?.Kod, Is.EqualTo(ocekivaniKod));
    }

    [TestCase(false, "termin-otkazan", TestName = "Otkazivanje_TerminOtkazanPosleProvere_OdbijaSeSaTerminOtkazan")]
    [TestCase(true, "termin-je-poceo", TestName = "Otkazivanje_TerminPoceoPosleProvere_OdbijaSeSaTerminJePoceo")]
    public void Otkazivanje_UslovnoOtkazivanjeNijeUspelo_VracaGreskuPremaStanjuPosleTrke(bool poceoUMedjuvremenu, string ocekivaniKod)
    {
        var posleTrke = poceoUMedjuvremenu
            ? NoviTermin(StatusTermina.Aktivan, pocetak: Sada.AddMinutes(-1))
            : NoviTermin(StatusTermina.Otkazan);
        _termini.PronadjiAsync(IdTermina, Arg.Any<CancellationToken>()).Returns(NoviTermin(StatusTermina.Aktivan), posleTrke);
        _termini.OtkaziAsync(IdTermina, Sada, Arg.Any<CancellationToken>()).Returns(false);

        var greska = Assert.ThrowsAsync<DomenskaGreska>(() => _servis.OtkaziAsync(IdTrenera, IdTermina, CancellationToken.None));

        Assert.That(greska?.Kod, Is.EqualTo(ocekivaniKod));
    }

    private static Termin NoviTermin(StatusTermina status, DateTime? pocetak = null)
    {
        var pocetakTermina = pocetak ?? Sada.AddDays(1);
        return new Termin
        {
            Id = IdTermina,
            TrenerId = IdTrenera,
            Naziv = "Joga",
            Pocetak = pocetakTermina,
            Kraj = pocetakTermina.AddHours(1),
            Kapacitet = 10,
            Status = status,
        };
    }

    private static TerminZahtev IspravanZahtev() => new()
    {
        Naziv = "Joga",
        Pocetak = new DateTimeOffset(Sada.AddDays(2)),
        Kraj = new DateTimeOffset(Sada.AddDays(2).AddHours(1)),
        Kapacitet = 10,
    };
}
