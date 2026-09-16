using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Teretana.Api.Infrastruktura.StatusSistema;

internal sealed record StatusSistemaOdgovor(string Status, IReadOnlyList<ProveraStatusa> Provere)
{
    public static Task UpisiAsync(HttpContext httpContext, HealthReport izvestaj)
    {
        var odgovor = new StatusSistemaOdgovor(
            izvestaj.Status.ToString(),
            [.. izvestaj.Entries.Select(p => new ProveraStatusa(p.Key, p.Value.Status.ToString(), p.Value.Description))]);

        return httpContext.Response.WriteAsJsonAsync(odgovor, httpContext.RequestAborted);
    }
}

internal sealed record ProveraStatusa(string Naziv, string Status, string? Opis);
