using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.Api;

/// <summary>
/// Ono što se vidi tek kada aplikacija stoji na pravom serveru: isti proces servira i frontend iz foldera
/// <c>client/</c> i dokumentaciju API-ja. Sadržaj OpenAPI dokumenta proveravaju komponentni testovi; ovde je
/// pitanje samo da li se do njega i do stranica dolazi preko HTTP-a.
/// </summary>
public sealed class HostingApiTestovi : ApiTest
{
    [Test]
    public async Task KorenAdresa_VracaPocetnuStranuFrontenda_SaPratecimStilomISkriptom()
    {
        var strana = await Api.GetAsync("/");
        var html = await strana.TextAsync();
        var stil = await Api.GetAsync("/css/app.css");
        var skripta = await Api.GetAsync("/js/app.js");

        Assert.Multiple(() =>
        {
            Assert.That(strana.Status, Is.EqualTo(200));
            Assert.That(strana.Headers["content-type"], Does.StartWith("text/html"));
            Assert.That(html, Does.Contain("js/app.js"), "Početna strana ne povezuje skriptu aplikacije.");
            Assert.That(stil.Status, Is.EqualTo(200));
            Assert.That(stil.Headers["content-type"], Does.StartWith("text/css"));
            Assert.That(skripta.Status, Is.EqualTo(200));
            Assert.That(skripta.Headers["content-type"], Does.StartWith("text/javascript"));
        });
    }

    [Test]
    public async Task SwaggerUiIOpenApiDokument_VanProductionOkruzenja_OdgovarajuNaSvojimAdresama()
    {
        var swagger = await Api.GetAsync("/swagger/index.html");
        var dokument = await Api.GetAsync("/openapi/v1.json");
        var telo = (await dokument.JsonAsync())!.Value;

        Assert.Multiple(() =>
        {
            Assert.That(swagger.Status, Is.EqualTo(200));
            Assert.That(swagger.Headers["content-type"], Does.StartWith("text/html"));
            Assert.That(dokument.Status, Is.EqualTo(200));
            Assert.That(telo.GetProperty("openapi").GetString(), Does.StartWith("3."));
            Assert.That(telo.GetProperty("paths").TryGetProperty("/api/termini", out _), Is.True);
        });
    }
}
