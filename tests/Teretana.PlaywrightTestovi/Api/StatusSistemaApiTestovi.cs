using System.Text.Json;
using Teretana.PlaywrightTestovi.Infrastruktura;

namespace Teretana.PlaywrightTestovi.Api;

public sealed class StatusSistemaApiTestovi : ApiTest
{
    [Test]
    public async Task GetHealth_BezTokena_Vraca200SaStatusomHealthy()
    {
        var odgovor = await Api.GetAsync("/health");

        var telo = await odgovor.JsonAsync();

        Assert.Multiple(() =>
        {
            Assert.That(odgovor.Status, Is.EqualTo(200));
            Assert.That(odgovor.Headers["content-type"], Does.StartWith("application/json"));
            Assert.That(telo?.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
            Assert.That(telo?.GetProperty("provere").ValueKind, Is.EqualTo(JsonValueKind.Array));
        });
    }
}
