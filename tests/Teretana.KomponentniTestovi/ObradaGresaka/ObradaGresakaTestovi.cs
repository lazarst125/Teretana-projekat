using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.ObradaGresaka;

public sealed class ObradaGresakaTestovi : KomponentniTest
{
    private const string PutanjaKojaBacaIzuzetak = "/test/neobradjen-izuzetak";
    private const string TajnaPorukaIzuzetka = "interni-detalj-koji-ne-sme-da-procuri";

    [Test]
    public async Task NepostojecaPutanja_Vraca404KaoProblemDetails()
    {
        using var odgovor = await Klijent.GetAsync("/api/ne-postoji");

        var problem = await odgovor.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(odgovor.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(problem?.Status, Is.EqualTo(404));
            Assert.That(problem?.Extensions, Does.ContainKey("traceId"));
        });
    }

    [Test]
    public async Task NeobradjenIzuzetak_Vraca500ProblemDetailsBezDetaljaIzuzetka()
    {
        using var odgovor = await Klijent.GetAsync(PutanjaKojaBacaIzuzetak);

        var telo = await odgovor.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(odgovor.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(odgovor.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(telo, Does.Contain("Došlo je do neočekivane greške."));
            Assert.That(telo, Does.Not.Contain(TajnaPorukaIzuzetka));
        });
    }

    protected override void PodesiTestneServise(IServiceCollection servisi) =>
        servisi.AddSingleton<IStartupFilter, IzuzetakStartupFilter>();

    /// <summary>
    /// Dodaje putanju koja baca izuzetak na kraj pipeline-a aplikacije, tako da izuzetak prolazi
    /// kroz istu obradu grešaka kao i pravi zahtevi.
    /// </summary>
    private sealed class IzuzetakStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(PutanjaKojaBacaIzuzetak, grana => grana.Run(_ => throw new InvalidOperationException(TajnaPorukaIzuzetka)));
        };
    }
}
