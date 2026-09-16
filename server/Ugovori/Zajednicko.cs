using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Teretana.Api.Ugovori.Zajednicko;

/// <summary>
/// Straničenje liste iz query string-a. Imena parametara su eksplicitna jer MVC ključ validacione greške
/// za query parametre gradi iz imena za binding, a klijent treba da dobije isto ime koje je poslao.
/// </summary>
public abstract record StranicenjeUpit
{
    public const int NajvecaVelicinaStranice = 100;

    [FromQuery(Name = "stranica")]
    [Range(1, 100_000, ErrorMessage = "Stranica mora biti između 1 i 100000.")]
    public int Stranica { get; init; } = 1;

    [FromQuery(Name = "velicinaStranice")]
    [Range(1, NajvecaVelicinaStranice, ErrorMessage = "Veličina stranice mora biti između 1 i 100.")]
    public int VelicinaStranice { get; init; } = 20;

    public int Preskoci() => (Stranica - 1) * VelicinaStranice;
}

public sealed record StranicaOdgovor<T>(IReadOnlyList<T> Stavke, int Stranica, int VelicinaStranice, int UkupnoStavki)
{
    public int UkupnoStranica => (int)Math.Ceiling(UkupnoStavki / (double)VelicinaStranice);
}
