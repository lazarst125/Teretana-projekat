using System.ComponentModel.DataAnnotations;
using Teretana.Api.Ugovori.Termini;

namespace Teretana.KomponentniTestovi.Validacija;

[Parallelizable(ParallelScope.All)]
[Category("Unit")]
public sealed class ValidacijaTerminaTestovi
{
    private static readonly DateTimeOffset Pocetak = new(2026, 9, 20, 18, 0, 0, TimeSpan.FromHours(2));

    [Test]
    public void TerminZahtev_KrajPrePocetka_NijeIspravanIGreskaJeNaPoljuKraj()
    {
        var zahtev = new TerminZahtev { Naziv = "Joga", Pocetak = Pocetak, Kraj = Pocetak.AddMinutes(-1), Kapacitet = 8 };

        var greske = Validiraj(zahtev);

        Assert.That(greske.SelectMany(g => g.MemberNames), Is.EquivalentTo(new[] { nameof(TerminZahtev.Kraj) }));
    }

    [Test]
    public void TerminZahtev_KrajJednakPocetku_NijeIspravan()
    {
        var zahtev = new TerminZahtev { Naziv = "Joga", Pocetak = Pocetak, Kraj = Pocetak, Kapacitet = 8 };

        var greske = Validiraj(zahtev);

        Assert.That(greske, Has.Count.EqualTo(1));
    }

    [Test]
    public void TerminZahtev_KrajUDrugojVremenskojZoniAliPosleUTC_JeIspravan()
    {
        var krajUUtc = Pocetak.ToOffset(TimeSpan.Zero).AddMinutes(1);
        var zahtev = new TerminZahtev { Naziv = "Joga", Pocetak = Pocetak, Kraj = krajUUtc, Kapacitet = 8 };

        var greske = Validiraj(zahtev);

        Assert.That(greske, Is.Empty);
    }

    [Test]
    public void TerminiUpit_KrajOpsegaPrePocetkaOpsega_NijeIspravan()
    {
        var upit = new TerminiUpit { Od = Pocetak, Do = Pocetak.AddDays(-1) };

        var greske = Validiraj(upit);

        Assert.That(greske.SelectMany(g => g.MemberNames), Is.EquivalentTo(new[] { nameof(TerminiUpit.Do) }));
    }

    private static List<ValidationResult> Validiraj(object objekat)
    {
        var greske = new List<ValidationResult>();
        Validator.TryValidateObject(objekat, new ValidationContext(objekat), greske, validateAllProperties: true);
        return greske;
    }
}
