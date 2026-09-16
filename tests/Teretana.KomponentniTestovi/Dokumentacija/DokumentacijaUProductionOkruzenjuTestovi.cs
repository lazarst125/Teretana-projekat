using System.Net;
using Teretana.KomponentniTestovi.Infrastruktura;

namespace Teretana.KomponentniTestovi.Dokumentacija;

public sealed class DokumentacijaUProductionOkruzenjuTestovi : KomponentniTest
{
    protected override string Okruzenje => "Production";

    [Test]
    public async Task OpenApiDokumentISwaggerUi_UProductionOkruzenju_NisuDostupni()
    {
        using var dokument = await Klijent.GetAsync("/openapi/v1.json");
        using var swaggerUi = await Klijent.GetAsync("/swagger/index.html");

        Assert.Multiple(() =>
        {
            Assert.That(dokument.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(swaggerUi.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }
}
