using Microsoft.EntityFrameworkCore;
using Teretana.Api.Domen;

namespace Teretana.Api.Podaci;

/// <summary>
/// Jedino mesto koje određuje redosled liste čekanja: ko se ranije prijavio je ispred, a pri istom vremenu
/// prijave odlučuje Id. Koriste ga spisak polaznika, pozicija na čekanju i automatsko unapređenje.
/// </summary>
public static class RedCekanja
{
    public static IOrderedQueryable<Rezervacija> PoReduCekanja(this IQueryable<Rezervacija> rezervacije) =>
        rezervacije.OrderBy(r => r.KreiranaAt).ThenBy(r => r.Id);

    /// <returns>Pozicija (od 1) svake prijave na čekanju u zadatim terminima, po Id-ju prijave.</returns>
    public static async Task<Dictionary<int, int>> PozicijeNaCekanjuAsync(
        this IQueryable<Rezervacija> rezervacije,
        IReadOnlyCollection<int> idTermina,
        CancellationToken cancellationToken)
    {
        var naCekanju = await rezervacije
            .Where(r => idTermina.Contains(r.TerminId) && r.Status == StatusRezervacije.NaCekanju)
            .PoReduCekanja()
            .Select(r => new { r.Id, r.TerminId })
            .ToListAsync(cancellationToken);

        return naCekanju
            .GroupBy(r => r.TerminId)
            .SelectMany(termin => termin.Select((prijava, indeks) => (prijava.Id, Pozicija: indeks + 1)))
            .ToDictionary(p => p.Id, p => p.Pozicija);
    }
}
