using Teretana.Api.Domen;
using Teretana.Api.Repozitorijumi;
using Teretana.Api.Ugovori.Termini;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Servisi;

public interface ITerminServis
{
    Task<StranicaOdgovor<TerminStavkaOdgovor>> VratiStranicuAsync(TerminiUpit upit, CancellationToken cancellationToken);

    Task<TerminDetaljOdgovor> VratiDetaljAsync(int idTermina, int? idClana, CancellationToken cancellationToken);

    Task<TerminDetaljOdgovor> KreirajAsync(int idTrenera, TerminZahtev zahtev, CancellationToken cancellationToken);

    Task<TerminDetaljOdgovor> IzmeniAsync(int idTrenera, int idTermina, TerminZahtev zahtev, CancellationToken cancellationToken);

    Task ObrisiAsync(int idTrenera, int idTermina, CancellationToken cancellationToken);

    Task<TerminDetaljOdgovor> OtkaziAsync(int idTrenera, int idTermina, CancellationToken cancellationToken);

    Task<PolazniciOdgovor> VratiPolazniceAsync(int idTrenera, int idTermina, CancellationToken cancellationToken);
}

internal sealed partial class TerminServis(
    ITerminRepozitorijum termini,
    TimeProvider vreme,
    ILogger<TerminServis> logger) : ITerminServis
{
    public Task<StranicaOdgovor<TerminStavkaOdgovor>> VratiStranicuAsync(TerminiUpit upit, CancellationToken cancellationToken) =>
        termini.VratiStranicuAsync(upit, Sada(), cancellationToken);

    public async Task<TerminDetaljOdgovor> VratiDetaljAsync(int idTermina, int? idClana, CancellationToken cancellationToken) =>
        await termini.VratiDetaljAsync(idTermina, idClana, cancellationToken) ?? throw Greske.TerminNePostoji();

    public async Task<TerminDetaljOdgovor> KreirajAsync(int idTrenera, TerminZahtev zahtev, CancellationToken cancellationToken)
    {
        var sada = Sada();
        var podaci = Podaci(zahtev);
        if (podaci.Pocetak <= sada)
        {
            throw Greske.PocetakUProslosti();
        }

        var termin = new Termin
        {
            TrenerId = idTrenera,
            Naziv = podaci.Naziv,
            Opis = podaci.Opis,
            Pocetak = podaci.Pocetak,
            Kraj = podaci.Kraj,
            Kapacitet = podaci.Kapacitet,
            Status = StatusTermina.Aktivan,
            KreiranAt = sada,
        };
        await termini.DodajAsync(termin, cancellationToken);
        LogTerminKreiran(logger, termin.Id, idTrenera);

        return await VratiDetaljAsync(termin.Id, null, cancellationToken);
    }

    public async Task<TerminDetaljOdgovor> IzmeniAsync(int idTrenera, int idTermina, TerminZahtev zahtev, CancellationToken cancellationToken)
    {
        var termin = await TerminKojimTrenerUpravljaAsync(idTrenera, idTermina, cancellationToken);
        if (termin.Status == StatusTermina.Otkazan)
        {
            throw Greske.TerminOtkazan();
        }

        if (await termini.ImaAktivnePrijaveAsync(idTermina, cancellationToken))
        {
            throw Greske.TerminImaPrijave();
        }

        var podaci = Podaci(zahtev);
        if (podaci.Pocetak <= Sada())
        {
            throw Greske.PocetakUProslosti();
        }

        if (!await termini.IzmeniAkoNemaAktivnihPrijavaAsync(idTermina, podaci, cancellationToken))
        {
            var trenutno = await termini.PronadjiAsync(idTermina, cancellationToken);
            throw trenutno?.Status == StatusTermina.Otkazan ? Greske.TerminOtkazan() : Greske.TerminImaPrijave();
        }

        LogTerminIzmenjen(logger, idTermina, idTrenera);
        return await VratiDetaljAsync(idTermina, null, cancellationToken);
    }

    public async Task ObrisiAsync(int idTrenera, int idTermina, CancellationToken cancellationToken)
    {
        await TerminKojimTrenerUpravljaAsync(idTrenera, idTermina, cancellationToken);

        if (await termini.ImaPrijaveAsync(idTermina, cancellationToken)
            || !await termini.ObrisiAkoNemaPrijavaAsync(idTermina, cancellationToken))
        {
            throw Greske.TerminImaPrijave();
        }

        LogTerminObrisan(logger, idTermina, idTrenera);
    }

    public async Task<TerminDetaljOdgovor> OtkaziAsync(int idTrenera, int idTermina, CancellationToken cancellationToken)
    {
        var termin = await TerminKojimTrenerUpravljaAsync(idTrenera, idTermina, cancellationToken);
        ProveriDaSeTerminMozeOtkazati(termin, Sada());

        var sada = Sada();
        if (!await termini.OtkaziAsync(idTermina, sada, cancellationToken))
        {
            var trenutno = await termini.PronadjiAsync(idTermina, cancellationToken) ?? throw Greske.TerminNePostoji();
            ProveriDaSeTerminMozeOtkazati(trenutno, sada);
            throw Greske.TerminOtkazan();
        }

        LogTerminOtkazan(logger, idTermina, idTrenera);
        return await VratiDetaljAsync(idTermina, null, cancellationToken);
    }

    public async Task<PolazniciOdgovor> VratiPolazniceAsync(int idTrenera, int idTermina, CancellationToken cancellationToken)
    {
        await TerminKojimTrenerUpravljaAsync(idTrenera, idTermina, cancellationToken);
        return await termini.VratiPolazniceAsync(idTermina, cancellationToken);
    }

    private static void ProveriDaSeTerminMozeOtkazati(Termin termin, DateTime sada)
    {
        if (termin.Status == StatusTermina.Otkazan)
        {
            throw Greske.TerminOtkazan();
        }

        if (termin.Pocetak <= sada)
        {
            throw Greske.TerminJePoceo();
        }
    }

    private static IzmenaTermina Podaci(TerminZahtev zahtev) => new(
        zahtev.Naziv.Trim(),
        string.IsNullOrWhiteSpace(zahtev.Opis) ? null : zahtev.Opis.Trim(),
        zahtev.Pocetak!.Value.UtcDateTime,
        zahtev.Kraj!.Value.UtcDateTime,
        zahtev.Kapacitet);

    private async Task<Termin> TerminKojimTrenerUpravljaAsync(int idTrenera, int idTermina, CancellationToken cancellationToken)
    {
        var termin = await termini.PronadjiAsync(idTermina, cancellationToken) ?? throw Greske.TerminNePostoji();
        return termin.TrenerId == idTrenera ? termin : throw Greske.TudjiTermin();
    }

    private DateTime Sada() => vreme.GetUtcNow().UtcDateTime;

    [LoggerMessage(Level = LogLevel.Information, Message = "Trener {TrenerId} je kreirao termin {TerminId}")]
    private static partial void LogTerminKreiran(ILogger logger, int terminId, int trenerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trener {TrenerId} je izmenio termin {TerminId}")]
    private static partial void LogTerminIzmenjen(ILogger logger, int terminId, int trenerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trener {TrenerId} je obrisao termin {TerminId}")]
    private static partial void LogTerminObrisan(ILogger logger, int terminId, int trenerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trener {TrenerId} je otkazao termin {TerminId} i sve njegove prijave")]
    private static partial void LogTerminOtkazan(ILogger logger, int terminId, int trenerId);
}
