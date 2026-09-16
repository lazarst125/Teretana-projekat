using Teretana.Api.Domen;
using Teretana.Api.Repozitorijumi;
using Teretana.Api.Ugovori.Rezervacije;
using Teretana.Api.Ugovori.Termini;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Servisi;

public interface IRezervacijaServis
{
    Task<RezervacijaOdgovor> RezervisiAsync(int idClana, int idTermina, CancellationToken cancellationToken);

    Task<RezervacijaOdgovor> PrijaviNaListuCekanjaAsync(int idClana, int idTermina, CancellationToken cancellationToken);

    Task<StranicaOdgovor<RezervacijaOdgovor>> VratiMojeAsync(int idClana, MojeRezervacijeUpit upit, CancellationToken cancellationToken);

    Task<RezervacijaOdgovor> VratiAsync(int idClana, int idRezervacije, CancellationToken cancellationToken);

    Task<RezervacijaOdgovor> EvidentirajPrisustvoAsync(int idTrenera, int idRezervacije, bool prisustvovao, CancellationToken cancellationToken);

    Task OtkaziAsync(int idClana, int idRezervacije, CancellationToken cancellationToken);
}

internal sealed partial class RezervacijaServis(
    IRezervacijaRepozitorijum rezervacije,
    ITerminRepozitorijum termini,
    PolitikaOtkazivanja politika,
    TimeProvider vreme,
    ILogger<RezervacijaServis> logger) : IRezervacijaServis
{
    public async Task<RezervacijaOdgovor> RezervisiAsync(int idClana, int idTermina, CancellationToken cancellationToken)
    {
        await ProveriDaSePrijavaMozeUpisatiAsync(idClana, idTermina, cancellationToken);

        var rezultat = await rezervacije.RezervisiAsync(idTermina, idClana, Sada(), cancellationToken);
        if (rezultat.Ishod == IshodUpisa.UslovNijeIspunjen)
        {
            // Mesto je zauzeo neko drugi, ili je termin u međuvremenu otkazan ili počeo.
            await ProveriDaSePrijavaMozeUpisatiAsync(idClana, idTermina, cancellationToken);
            throw Greske.TerminPopunjen();
        }

        var rezervacija = await VratiUpisanuAsync(rezultat, cancellationToken);
        LogRezervisano(logger, rezervacija.Id, idTermina, idClana);
        return rezervacija;
    }

    public async Task<RezervacijaOdgovor> PrijaviNaListuCekanjaAsync(int idClana, int idTermina, CancellationToken cancellationToken)
    {
        await ProveriDaSePrijavaMozeUpisatiAsync(idClana, idTermina, cancellationToken);

        var rezultat = await rezervacije.UpisiNaListuCekanjaAsync(idTermina, idClana, Sada(), cancellationToken);
        if (rezultat.Ishod == IshodUpisa.UslovNijeIspunjen)
        {
            await ProveriDaSePrijavaMozeUpisatiAsync(idClana, idTermina, cancellationToken);
            throw Greske.TerminImaSlobodnihMesta();
        }

        var prijava = await VratiUpisanuAsync(rezultat, cancellationToken);
        LogNaListiCekanja(logger, prijava.Id, idTermina, idClana, prijava.PozicijaNaCekanju);
        return prijava;
    }

    public async Task<StranicaOdgovor<RezervacijaOdgovor>> VratiMojeAsync(int idClana, MojeRezervacijeUpit upit, CancellationToken cancellationToken)
    {
        var stranica = await rezervacije.VratiMojeAsync(idClana, upit, cancellationToken);
        return new StranicaOdgovor<RezervacijaOdgovor>(
            [.. stranica.Stavke.Select(UOdgovor)], stranica.Stranica, stranica.VelicinaStranice, stranica.UkupnoStavki);
    }

    public async Task<RezervacijaOdgovor> VratiAsync(int idClana, int idRezervacije, CancellationToken cancellationToken) =>
        UOdgovor(await SopstvenaRezervacijaAsync(idClana, idRezervacije, cancellationToken));

    public async Task<RezervacijaOdgovor> EvidentirajPrisustvoAsync(int idTrenera, int idRezervacije, bool prisustvovao, CancellationToken cancellationToken)
    {
        var rezervacija = await rezervacije.VratiAsync(idRezervacije, cancellationToken) ?? throw Greske.RezervacijaNePostoji();
        if (rezervacija.TrenerId != idTrenera)
        {
            throw Greske.TudjiTermin();
        }

        if (rezervacija.Status != StatusRezervacije.Potvrdjena)
        {
            throw Greske.RezervacijaNijePotvrdjena();
        }

        if (rezervacija.PocetakTermina > Sada())
        {
            throw Greske.TerminNijePoceo();
        }

        if (!await rezervacije.EvidentirajPrisustvoAsync(idRezervacije, prisustvovao, cancellationToken))
        {
            throw Greske.RezervacijaNijePotvrdjena();
        }

        LogPrisustvo(logger, idRezervacije, prisustvovao, idTrenera);
        return UOdgovor(await rezervacije.VratiAsync(idRezervacije, cancellationToken) ?? throw Greske.RezervacijaNePostoji());
    }

    public async Task OtkaziAsync(int idClana, int idRezervacije, CancellationToken cancellationToken)
    {
        var rezervacija = await SopstvenaRezervacijaAsync(idClana, idRezervacije, cancellationToken);
        var sada = Sada();
        if (RazlogZaOdbijanjeOtkazivanja(rezervacija, sada) is { } razlog)
        {
            throw razlog;
        }

        var rezultat = await rezervacije.OtkaziAsync(idRezervacije, sada, cancellationToken);
        if (!rezultat.Otkazano)
        {
            throw Greske.RezervacijaOtkazana();
        }

        LogOtkazano(logger, idRezervacije, rezervacija.TerminId, idClana);
        if (rezultat.IdUnapredjeneRezervacije is { } unapredjena)
        {
            LogUnapredjenoSaListeCekanja(logger, unapredjena, rezervacija.TerminId);
        }
    }

    private async Task ProveriDaSePrijavaMozeUpisatiAsync(int idClana, int idTermina, CancellationToken cancellationToken)
    {
        var termin = await termini.PronadjiAsync(idTermina, cancellationToken) ?? throw Greske.TerminNePostoji();
        if (termin.Status == StatusTermina.Otkazan)
        {
            throw Greske.TerminOtkazan();
        }

        if (termin.Pocetak <= Sada())
        {
            throw Greske.TerminJePoceo();
        }

        if (await rezervacije.ImaAktivnuPrijavuAsync(idTermina, idClana, cancellationToken))
        {
            throw Greske.VecPrijavljen();
        }
    }

    private async Task<RezervacijaOdgovor> VratiUpisanuAsync(RezultatUpisa rezultat, CancellationToken cancellationToken)
    {
        if (rezultat.Ishod == IshodUpisa.VecPrijavljen)
        {
            throw Greske.VecPrijavljen();
        }

        var podaci = await rezervacije.VratiAsync(rezultat.IdRezervacije!.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Upisana rezervacija {rezultat.IdRezervacije} nije pronađena.");
        return UOdgovor(podaci);
    }

    /// <summary>Tuđa rezervacija se prijavljuje kao nepostojeća, da član ne bi saznao koji Id-jevi postoje.</summary>
    private async Task<RezervacijaPodaci> SopstvenaRezervacijaAsync(int idClana, int idRezervacije, CancellationToken cancellationToken)
    {
        var rezervacija = await rezervacije.VratiAsync(idRezervacije, cancellationToken);
        return rezervacija is not null && rezervacija.ClanId == idClana ? rezervacija : throw Greske.RezervacijaNePostoji();
    }

    /// <summary>
    /// Jedno mesto za pravila otkazivanja: koristi ga i samo otkazivanje i polje MozeDaSeOtkaze u odgovoru.
    /// Rok važi samo za potvrđene rezervacije; sa liste čekanja član može da se povuče do početka termina.
    /// </summary>
    private DomenskaGreska? RazlogZaOdbijanjeOtkazivanja(RezervacijaPodaci rezervacija, DateTime sada)
    {
        if (rezervacija.Status == StatusRezervacije.Otkazana)
        {
            return Greske.RezervacijaOtkazana();
        }

        if (rezervacija.PocetakTermina <= sada)
        {
            return Greske.TerminJePoceo();
        }

        return rezervacija.Status == StatusRezervacije.Potvrdjena && politika.RokJeIstekao(rezervacija.PocetakTermina, sada)
            ? Greske.RokZaOtkazivanjeIstekao()
            : null;
    }

    private RezervacijaOdgovor UOdgovor(RezervacijaPodaci podaci) => new(
        podaci.Id,
        podaci.Status,
        podaci.PozicijaNaCekanju,
        podaci.KreiranaAt,
        podaci.PotvrdjenaAt,
        podaci.OtkazanaAt,
        podaci.Prisustvovao,
        politika.PoslednjiTrenutakZaOtkazivanje(podaci.PocetakTermina),
        RazlogZaOdbijanjeOtkazivanja(podaci, Sada()) is null,
        new TerminUkratkoOdgovor(
            podaci.TerminId,
            podaci.NazivTermina,
            podaci.PocetakTermina,
            podaci.KrajTermina,
            podaci.StatusTermina,
            new TrenerUkratko(podaci.TrenerId, podaci.ImePrezimeTrenera)));

    private DateTime Sada() => vreme.GetUtcNow().UtcDateTime;

    [LoggerMessage(Level = LogLevel.Information, Message = "Član {ClanId} je rezervisao mesto {RezervacijaId} na terminu {TerminId}")]
    private static partial void LogRezervisano(ILogger logger, int rezervacijaId, int terminId, int clanId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Član {ClanId} je na listi čekanja za termin {TerminId} (prijava {RezervacijaId}, pozicija {Pozicija})")]
    private static partial void LogNaListiCekanja(ILogger logger, int rezervacijaId, int terminId, int clanId, int? pozicija);

    [LoggerMessage(Level = LogLevel.Information, Message = "Član {ClanId} je otkazao prijavu {RezervacijaId} na terminu {TerminId}")]
    private static partial void LogOtkazano(ILogger logger, int rezervacijaId, int terminId, int clanId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Prijava {RezervacijaId} je automatski prešla sa liste čekanja u potvrđenu rezervaciju za termin {TerminId}")]
    private static partial void LogUnapredjenoSaListeCekanja(ILogger logger, int rezervacijaId, int terminId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Trener {TrenerId} je evidentirao prisustvo {Prisustvovao} za rezervaciju {RezervacijaId}")]
    private static partial void LogPrisustvo(ILogger logger, int rezervacijaId, bool prisustvovao, int trenerId);
}

public sealed class RezervacijePodesavanja
{
    public const string Sekcija = "Rezervacije";

    public TimeSpan RokZaOtkazivanje { get; set; }
}
