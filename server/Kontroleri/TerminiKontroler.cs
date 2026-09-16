using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Teretana.Api.Domen;
using Teretana.Api.Infrastruktura.Autentikacija;
using Teretana.Api.Infrastruktura.Dokumentacija;
using Teretana.Api.Servisi;
using Teretana.Api.Ugovori.Termini;
using Teretana.Api.Ugovori.Zajednicko;

namespace Teretana.Api.Kontroleri;

/// <summary>Pregled termina za sve prijavljene korisnike i upravljanje terminima za trenere.</summary>
[ApiController]
[Route("api/termini")]
[Tags("Termini")]
public sealed class TerminiKontroler(ITerminServis servis) : ControllerBase
{
    /// <summary>Vraća stranicu termina uz filtere i sortiranje.</summary>
    /// <response code="200">Stranica termina; prazna lista ima isti omotač sa <c>stavke: []</c>.</response>
    /// <response code="400">Neispravni parametri upita, npr. velicinaStranice veća od 100 (code: validacija).</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<StranicaOdgovor<TerminStavkaOdgovor>>(StatusCodes.Status200OK)]
    [ValidacioniProblemOdgovor]
    public async Task<ActionResult<StranicaOdgovor<TerminStavkaOdgovor>>> Lista([FromQuery] TerminiUpit upit, CancellationToken cancellationToken) =>
        await servis.VratiStranicuAsync(upit, cancellationToken);

    /// <summary>Vraća detalj termina sa slobodnim mestima, brojem na čekanju i prijavom člana koji gleda termin.</summary>
    /// <response code="200">Detalj termina.</response>
    /// <response code="404">Termin ne postoji (code: termin-ne-postoji).</response>
    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType<TerminDetaljOdgovor>(StatusCodes.Status200OK)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerminDetaljOdgovor>> Detalj(int id, CancellationToken cancellationToken)
    {
        var idClana = User.IsInRole(nameof(Uloga.Clan)) ? User.IdKorisnika() : (int?)null;
        return await servis.VratiDetaljAsync(id, idClana, cancellationToken);
    }

    /// <summary>Kreira termin prijavljenog trenera.</summary>
    /// <response code="201">Termin je kreiran; zaglavlje Location vodi na detalj.</response>
    /// <response code="400">Neispravni podaci, npr. kapacitet van opsega 1–100 ili kraj pre početka (code: validacija).</response>
    /// <response code="422">Početak termina nije u budućnosti (code: pocetak-u-proslosti).</response>
    [HttpPost]
    [Authorize(Policy = Politike.Trener)]
    [ProducesResponseType<TerminDetaljOdgovor>(StatusCodes.Status201Created)]
    [ValidacioniProblemOdgovor]
    [ProblemOdgovor(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TerminDetaljOdgovor>> Kreiraj(TerminZahtev zahtev, CancellationToken cancellationToken)
    {
        var termin = await servis.KreirajAsync(User.IdKorisnika(), zahtev, cancellationToken);
        return CreatedAtAction(nameof(Detalj), new { id = termin.Id }, termin);
    }

    /// <summary>Menja sopstveni termin koji nema aktivnih prijava.</summary>
    /// <response code="200">Izmenjeni termin.</response>
    /// <response code="400">Neispravni podaci (code: validacija).</response>
    /// <response code="403">Termin pripada drugom treneru (code: tudji-termin) ili korisnik nije trener.</response>
    /// <response code="404">Termin ne postoji (code: termin-ne-postoji).</response>
    /// <response code="409">Termin ima aktivne prijave (code: termin-ima-prijave) ili je otkazan (code: termin-otkazan).</response>
    /// <response code="422">Novi početak nije u budućnosti (code: pocetak-u-proslosti).</response>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politike.Trener)]
    [ProducesResponseType<TerminDetaljOdgovor>(StatusCodes.Status200OK)]
    [ValidacioniProblemOdgovor]
    [ProblemOdgovor(StatusCodes.Status403Forbidden)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    [ProblemOdgovor(StatusCodes.Status409Conflict)]
    [ProblemOdgovor(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TerminDetaljOdgovor>> Izmeni(int id, TerminZahtev zahtev, CancellationToken cancellationToken) =>
        await servis.IzmeniAsync(User.IdKorisnika(), id, zahtev, cancellationToken);

    /// <summary>Briše sopstveni termin koji nikada nije imao prijave.</summary>
    /// <remarks>Termin sa bilo kakvom prijavom, i otkazanom, ne briše se da bi istorija ostala sačuvana; umesto toga se otkazuje.</remarks>
    /// <response code="204">Termin je obrisan.</response>
    /// <response code="403">Termin pripada drugom treneru (code: tudji-termin) ili korisnik nije trener.</response>
    /// <response code="404">Termin ne postoji (code: termin-ne-postoji).</response>
    /// <response code="409">Termin ima prijave (code: termin-ima-prijave).</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Politike.Trener)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProblemOdgovor(StatusCodes.Status403Forbidden)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    [ProblemOdgovor(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Obrisi(int id, CancellationToken cancellationToken)
    {
        await servis.ObrisiAsync(User.IdKorisnika(), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Otkazuje sopstveni termin i sve njegove prijave.</summary>
    /// <response code="200">Otkazani termin.</response>
    /// <response code="403">Termin pripada drugom treneru (code: tudji-termin) ili korisnik nije trener.</response>
    /// <response code="404">Termin ne postoji (code: termin-ne-postoji).</response>
    /// <response code="409">Termin je već otkazan (code: termin-otkazan).</response>
    /// <response code="422">Termin je već počeo (code: termin-je-poceo).</response>
    [HttpPost("{id:int}/otkazivanje")]
    [Authorize(Policy = Politike.Trener)]
    [ProducesResponseType<TerminDetaljOdgovor>(StatusCodes.Status200OK)]
    [ProblemOdgovor(StatusCodes.Status403Forbidden)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    [ProblemOdgovor(StatusCodes.Status409Conflict)]
    [ProblemOdgovor(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TerminDetaljOdgovor>> Otkazi(int id, CancellationToken cancellationToken) =>
        await servis.OtkaziAsync(User.IdKorisnika(), id, cancellationToken);

    /// <summary>Vraća potvrđene polaznike i listu čekanja sopstvenog termina.</summary>
    /// <response code="200">Potvrđeni polaznici i lista čekanja po redosledu prijave.</response>
    /// <response code="403">Termin pripada drugom treneru (code: tudji-termin) ili korisnik nije trener.</response>
    /// <response code="404">Termin ne postoji (code: termin-ne-postoji).</response>
    [HttpGet("{id:int}/polaznici")]
    [Authorize(Policy = Politike.Trener)]
    [ProducesResponseType<PolazniciOdgovor>(StatusCodes.Status200OK)]
    [ProblemOdgovor(StatusCodes.Status403Forbidden)]
    [ProblemOdgovor(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolazniciOdgovor>> Polaznici(int id, CancellationToken cancellationToken) =>
        await servis.VratiPolazniceAsync(User.IdKorisnika(), id, cancellationToken);
}
