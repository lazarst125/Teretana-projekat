// Svaka klasa ima sopstvenu hostovanu aplikaciju, pa klase idu paralelno; testovi unutar klase
// dele browser/aplikaciju i izvršavaju se redom, kako Playwright preporučuje za NUnit.
[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Teretana.PlaywrightTestovi;

/// <summary>
/// Instalira Chromium pre prvog testa, tako da celina radi iz čistog klona bez ručnog koraka.
/// Ako je browser već instaliran, instalacija se završava odmah.
/// </summary>
[SetUpFixture]
public sealed class InstalacijaBrowsera
{
    [OneTimeSetUp]
    public void InstalirajChromium()
    {
        var izlazniKod = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (izlazniKod != 0)
        {
            throw new InvalidOperationException($"Instalacija Chromium browsera nije uspela (izlazni kod {izlazniKod}).");
        }
    }
}
