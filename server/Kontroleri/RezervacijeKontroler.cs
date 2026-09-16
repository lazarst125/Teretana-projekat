using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Teretana.Api.Infrastruktura.Autentikacija;
using Teretana.Api.Infrastruktura.Dokumentacija;
using Teretana.Api.Servisi;
using Teretana.Api.Ugovori.Rezervacije;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Kontroleri;

/// <summary>Rezervacije i lista čekanja za članove, evidencija prisustva za trenere.</summary>
[ApiController]
[Route("api")]
[Tags("Rezervacije")]
public sealed class RezervacijeKontroler(IRezervacijaServis servis) : ControllerBase
{
    /// <summary>Rezerviše mesto na terminu.</summary>
    /// <remarks>Kada dva člana istovremeno traže poslednje mesto, samo jedan ga dobija; drugi dobija 409 termin-popunjen.</remarks>
    /// <response code="201">Potvrđena rezervacija; zaglavlje Location vodi na detalj rezervacije.</response>
    /// <response code="404">Termin ne postoji (code: termin-ne-postoji).</response>
    /// <response code="409">Termin je popunjen (code: termin-popunjen), otkazan (code: termin-otkazan) ili član već ima prijavu (code: vec-prijavljen).</response>
    /// <response code="422">Termin je već počeo (code: termin-je-poceo).</response>
    [HttpPost("termini/{idTermina:int}/rezervacije")]
    [Authorize(Policy = Politike.Clan)]
    [ProducesResponseType<RezervacijaOdgovor>(StatusCodes.Status201Created)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    [ProblemOdgovor(StatusCodes.Status409Conflict)]
    [ProblemOdgovor(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RezervacijaOdgovor>> Rezervisi(int idTermina, CancellationToken cancellationToken)
    {
        var rezervacija = await servis.RezervisiAsync(User.IdKorisnika(), idTermina, cancellationToken);
        return CreatedAtAction(nameof(Detalj), new { id = rezervacija.Id }, rezervacija);
    }

    /// <summary>Prijavljuje člana na listu čekanja popunjenog termina.</summary>
    /// <response code="201">Prijava na listu čekanja sa pozicijom; zaglavlje Location vodi na detalj prijave.</response>
    /// <response code="404">Termin ne postoji (code: termin-ne-postoji).</response>
    /// <response code="409">Termin ima slobodnih mesta (code: termin-ima-slobodnih-mesta), otkazan je (code: termin-otkazan) ili član već ima prijavu (code: vec-prijavljen).</response>
    /// <response code="422">Termin je već počeo (code: termin-je-poceo).</response>
    [HttpPost("termini/{idTermina:int}/lista-cekanja")]
    [Authorize(Policy = Politike.Clan)]
    [ProducesResponseType<RezervacijaOdgovor>(StatusCodes.Status201Created)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    [ProblemOdgovor(StatusCodes.Status409Conflict)]
    [ProblemOdgovor(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RezervacijaOdgovor>> PrijaviNaListuCekanja(int idTermina, CancellationToken cancellationToken)
    {
        var prijava = await servis.PrijaviNaListuCekanjaAsync(User.IdKorisnika(), idTermina, cancellationToken);
        return CreatedAtAction(nameof(Detalj), new { id = prijava.Id }, prijava);
    }

    /// <summary>Vraća stranicu rezervacija i prijava na čekanje prijavljenog člana.</summary>
    /// <response code="200">Stranica rezervacija.</response>
    /// <response code="400">Neispravni parametri upita (code: validacija).</response>
    [HttpGet("rezervacije/moje")]
    [Authorize(Policy = Politike.Clan)]
    [ProducesResponseType<StranicaOdgovor<RezervacijaOdgovor>>(StatusCodes.Status200OK)]
    [ValidacioniProblemOdgovor]
    public async Task<ActionResult<StranicaOdgovor<RezervacijaOdgovor>>> Moje([FromQuery] MojeRezervacijeUpit upit, CancellationToken cancellationToken) =>
        await servis.VratiMojeAsync(User.IdKorisnika(), upit, cancellationToken);

    /// <summary>Vraća sopstvenu rezervaciju sa rokom za otkazivanje.</summary>
    /// <response code="200">Rezervacija.</response>
    /// <response code="404">Rezervacija ne postoji ili pripada drugom članu (code: rezervacija-ne-postoji).</response>
    [HttpGet("rezervacije/{id:int}")]
    [Authorize(Policy = Politike.Clan)]
    [ProducesResponseType<RezervacijaOdgovor>(StatusCodes.Status200OK)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RezervacijaOdgovor>> Detalj(int id, CancellationToken cancellationToken) =>
        await servis.VratiAsync(User.IdKorisnika(), id, cancellationToken);

    /// <summary>Evidentira da li je član došao na termin.</summary>
    /// <response code="200">Rezervacija sa evidentiranim prisustvom.</response>
    /// <response code="400">Polje prisustvovao nedostaje (code: validacija).</response>
    /// <response code="403">Termin pripada drugom treneru (code: tudji-termin) ili korisnik nije trener.</response>
    /// <response code="404">Rezervacija ne postoji (code: rezervacija-ne-postoji).</response>
    /// <response code="409">Rezervacija nije potvrđena (code: rezervacija-nije-potvrdjena).</response>
    /// <response code="422">Termin još nije počeo (code: termin-nije-poceo).</response>
    [HttpPut("rezervacije/{id:int}/prisustvo")]
    [Authorize(Policy = Politike.Trener)]
    [ProducesResponseType<RezervacijaOdgovor>(StatusCodes.Status200OK)]
    [ValidacioniProblemOdgovor]
    [ProblemOdgovor(StatusCodes.Status403Forbidden)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    [ProblemOdgovor(StatusCodes.Status409Conflict)]
    [ProblemOdgovor(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RezervacijaOdgovor>> EvidentirajPrisustvo(int id, PrisustvoZahtev zahtev, CancellationToken cancellationToken) =>
        await servis.EvidentirajPrisustvoAsync(User.IdKorisnika(), id, zahtev.Prisustvovao!.Value, cancellationToken);

    /// <summary>Otkazuje sopstvenu rezervaciju ili povlači prijavu sa liste čekanja.</summary>
    /// <remarks>Potvrđena rezervacija može da se otkaže najkasnije 2 sata pre početka. Oslobođeno mesto automatski dobija prvi sa liste čekanja.</remarks>
    /// <response code="204">Rezervacija je otkazana.</response>
    /// <response code="404">Rezervacija ne postoji ili pripada drugom članu (code: rezervacija-ne-postoji).</response>
    /// <response code="409">Rezervacija je već otkazana (code: rezervacija-otkazana).</response>
    /// <response code="422">Rok za otkazivanje je istekao (code: rok-za-otkazivanje-istekao) ili je termin počeo (code: termin-je-poceo).</response>
    [HttpDelete("rezervacije/{id:int}")]
    [Authorize(Policy = Politike.Clan)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    [ProblemOdgovor(StatusCodes.Status409Conflict)]
    [ProblemOdgovor(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Otkazi(int id, CancellationToken cancellationToken)
    {
        await servis.OtkaziAsync(User.IdKorisnika(), id, cancellationToken);
        return NoContent();
    }
}
