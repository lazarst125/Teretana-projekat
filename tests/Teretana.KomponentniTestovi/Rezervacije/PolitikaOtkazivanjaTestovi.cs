using Teretana.Api.Domen;

namespace Teretana.KomponentniTestovi.Rezervacije;

[Parallelizable(ParallelScope.All)]
[Category("Unit")]
public sealed class PolitikaOtkazivanjaTestovi
{
    private static readonly DateTime PocetakTermina = new(2026, 9, 20, 18, 0, 0, DateTimeKind.Utc);

    private readonly PolitikaOtkazivanja _politika = new(TimeSpan.FromHours(2));

    [TestCase(-1, ExpectedResult = false, TestName = "RokJeIstekao_SekundPreIstekaRoka_JosNije")]
    [TestCase(0, ExpectedResult = false, TestName = "RokJeIstekao_TacnoUTrenutkuIstekaRoka_JosNije")]
    [TestCase(1, ExpectedResult = true, TestName = "RokJeIstekao_SekundPosleIstekaRoka_Jeste")]
    public bool RokJeIstekao_OkoGraniceRoka(int sekundiPosleIstekaRoka) =>
        _politika.RokJeIstekao(PocetakTermina, PocetakTermina.AddHours(-2).AddSeconds(sekundiPosleIstekaRoka));

    [Test]
    public void Konstruktor_RokKojiNijePozitivan_Odbija() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PolitikaOtkazivanja(TimeSpan.Zero));
}
