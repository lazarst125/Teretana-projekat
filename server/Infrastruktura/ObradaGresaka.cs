using Microsoft.AspNetCore.Diagnostics;
using Teretana.Api.Domen;

namespace Teretana.Api.Infrastruktura.ObradaGresaka;

internal static class ProblemDetailsPodesavanja
{
    public const string KodValidacije = "validacija";

    /// <summary>
    /// Validacione greške dobijaju isti kod kao i domenske, da bi klijent razlikovao vrstu greške
    /// po jednom polju, bez obzira na to da li je zahtev odbio model binding ili servis.
    /// </summary>
    public static void DodajKodZaValidaciju(ProblemDetailsContext kontekst)
    {
        if (kontekst.ProblemDetails is HttpValidationProblemDetails)
        {
            kontekst.ProblemDetails.Extensions.TryAdd(DomenskaGreskaHandler.PoljeKodaGreske, KodValidacije);
        }
    }
}

internal sealed partial class DomenskaGreskaHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomenskaGreskaHandler> logger) : IExceptionHandler
{
    public const string PoljeKodaGreske = "code";

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomenskaGreska greska)
        {
            return ValueTask.FromResult(false);
        }

        var status = greska.Vrsta switch
        {
            VrstaGreske.NijeAutorizovan => StatusCodes.Status401Unauthorized,
            VrstaGreske.Zabranjeno => StatusCodes.Status403Forbidden,
            VrstaGreske.NijePronadjeno => StatusCodes.Status404NotFound,
            VrstaGreske.Konflikt => StatusCodes.Status409Conflict,
            VrstaGreske.PoslovnoPravilo => StatusCodes.Status422UnprocessableEntity,
            _ => throw new ArgumentOutOfRangeException(nameof(exception), greska.Vrsta, "Nepoznata vrsta domenske greške."),
        };

        LogDomenskaGreska(logger, greska.Kod, status, httpContext.Request.Method, httpContext.Request.Path.Value);

        httpContext.Response.StatusCode = status;
        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Title = greska.Message,
                Extensions = { [PoljeKodaGreske] = greska.Kod },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Zahtev {Metod} {Putanja} odbijen: {KodGreske} ({Status})")]
    private static partial void LogDomenskaGreska(ILogger logger, string kodGreske, int status, string metod, string? putanja);
}

/// <summary>
/// Poslednja linija odbrane: svaki izuzetak koji nijedan sloj nije obradio postaje 500 ProblemDetails
/// bez poruke i stack trace-a, a detalji ostaju samo u logu.
/// </summary>
internal sealed partial class NeobradjenIzuzetakHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<NeobradjenIzuzetakHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogNeobradjenIzuzetak(logger, httpContext.Request.Method, httpContext.Request.Path.Value, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Došlo je do neočekivane greške.",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Neobrađen izuzetak pri obradi zahteva {Metod} {Putanja}")]
    private static partial void LogNeobradjenIzuzetak(ILogger logger, string metod, string? putanja, Exception exception);
}
