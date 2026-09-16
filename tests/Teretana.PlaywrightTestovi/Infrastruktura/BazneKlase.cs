using NUnit.Framework.Interfaces;

namespace Teretana.PlaywrightTestovi.Infrastruktura;

[Category("API")]
public abstract class ApiTest : PlaywrightTest
{
    private HostovanaAplikacija _aplikacija = null!;

    protected IAPIRequestContext Api { get; private set; } = null!;

    protected TestniPodaci Podaci { get; private set; } = null!;

    [OneTimeSetUp]
    public void PokreniAplikaciju()
    {
        _aplikacija = new HostovanaAplikacija();
        _aplikacija.Pokreni();
    }

    [OneTimeTearDown]
    public async Task ZaustaviAplikaciju() => await _aplikacija.DisposeAsync();

    [SetUp]
    public async Task NapraviApiKontekst()
    {
        Api = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = _aplikacija.Adresa });
        Podaci = new TestniPodaci(_aplikacija, Api);
    }

    [TearDown]
    public async Task ZatvoriApiKontekst() => await Api.DisposeAsync();
}

/// <summary>
/// Osnova E2E testova: aplikacija po klasi, novi browser context po testu, podaci po testu kroz API, a trace,
/// screenshot i video se čuvaju samo kad test padne.
/// </summary>
[Category("E2E")]
public abstract class E2ETest : PageTest
{
    private const string KljucSesije = "teretana.sesija";
    private static readonly string FolderZaVideo = Path.Combine(Path.GetTempPath(), "teretana-playwright-video");

    private readonly List<IBrowserContext> _dodatniKonteksti = [];
    private HostovanaAplikacija _aplikacija = null!;
    private IAPIRequestContext _api = null!;

    protected TestniPodaci Podaci { get; private set; } = null!;

    [OneTimeSetUp]
    public void PokreniAplikaciju()
    {
        _aplikacija = new HostovanaAplikacija();
        _aplikacija.Pokreni();
    }

    [OneTimeTearDown]
    public async Task ZaustaviAplikaciju() => await _aplikacija.DisposeAsync();

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = _aplikacija.Adresa,
        RecordVideoDir = FolderZaVideo,
        ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
    };

    [SetUp]
    public async Task PripremiTest()
    {
        _api = await Playwright.APIRequest.NewContextAsync(new() { BaseURL = _aplikacija.Adresa });
        Podaci = new TestniPodaci(_aplikacija, _api);
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
    }

    [TearDown]
    public async Task SacuvajArtefakteAkoJeTestPao()
    {
        foreach (var kontekst in _dodatniKonteksti)
        {
            await kontekst.CloseAsync();
        }

        _dodatniKonteksti.Clear();
        await _api.DisposeAsync();

        var testJePao = TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed;
        var folder = FolderArtefakata();

        if (testJePao)
        {
            await Page.ScreenshotAsync(new() { Path = Path.Combine(folder, "screenshot.png"), FullPage = true });
            await Context.Tracing.StopAsync(new() { Path = Path.Combine(folder, "trace.zip") });
        }
        else
        {
            await Context.Tracing.StopAsync();
        }

        var video = Page.Video;
        await Context.CloseAsync();
        if (video is null)
        {
            return;
        }

        if (testJePao)
        {
            await video.SaveAsAsync(Path.Combine(folder, "video.webm"));
        }

        await video.DeleteAsync();
    }

    /// <summary>
    /// Upisuje sesiju korisnika prijavljenog kroz API u browser, da testovi kojima prijava nije predmet
    /// ne prolaze svaki put kroz formu. Sama forma za prijavu ima sopstvene testove.
    /// </summary>
    protected async Task PrijaviSeAsync(PrijavljenKorisnik korisnik, IPage? strana = null)
    {
        var cilj = strana ?? Page;
        await cilj.GotoAsync("/");
        await cilj.EvaluateAsync(
            "([kljuc, sesija]) => localStorage.setItem(kljuc, JSON.stringify(sesija))",
            new object[]
            {
                KljucSesije,
                new
                {
                    token = korisnik.Token,
                    istice = korisnik.Istice,
                    korisnik = new { id = korisnik.Id, email = korisnik.Email, imePrezime = korisnik.ImePrezime, uloga = korisnik.Uloga },
                },
            });
        await cilj.ReloadAsync();
        await Expect(cilj.GetByTestId("profil-ime")).ToHaveTextAsync(korisnik.ImePrezime);
    }

    /// <summary>Strana drugog korisnika u zasebnom browser kontekstu (sopstveni localStorage); zatvara se posle testa.</summary>
    protected async Task<IPage> NovaStranaZaAsync(PrijavljenKorisnik korisnik)
    {
        var kontekst = await Browser.NewContextAsync(new() { BaseURL = _aplikacija.Adresa });
        _dodatniKonteksti.Add(kontekst);
        var strana = await kontekst.NewPageAsync();
        await PrijaviSeAsync(korisnik, strana);
        return strana;
    }

    private static string FolderArtefakata()
    {
        var imeTesta = string.Concat(TestContext.CurrentContext.Test.FullName.Select(z => Path.GetInvalidFileNameChars().Contains(z) ? '_' : z));
        return Path.Combine(TestContext.CurrentContext.WorkDirectory, "playwright-artefakti", imeTesta);
    }
}
