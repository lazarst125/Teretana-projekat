using Microsoft.Extensions.Options;
using Teretana.Api.Domen;
using Teretana.Api.Repozitorijumi;

namespace Teretana.Api.Servisi;

public static class ServisiRegistracija
{
    public static IServiceCollection DodajAplikacioneServise(this IServiceCollection servisi)
    {
        servisi.AddOptions<RezervacijePodesavanja>()
            .BindConfiguration(RezervacijePodesavanja.Sekcija)
            .Validate(podesavanja => podesavanja.RokZaOtkazivanje > TimeSpan.Zero, "Rezervacije:RokZaOtkazivanje mora biti pozitivan.")
            .ValidateOnStart();

        servisi.AddSingleton(provajder =>
            new PolitikaOtkazivanja(provajder.GetRequiredService<IOptions<RezervacijePodesavanja>>().Value.RokZaOtkazivanje));

        return servisi
            .AddScoped<IKorisnikRepozitorijum, KorisnikRepozitorijum>()
            .AddScoped<ITerminRepozitorijum, TerminRepozitorijum>()
            .AddScoped<IRezervacijaRepozitorijum, RezervacijaRepozitorijum>()
            .AddScoped<IAutentikacijaServis, AutentikacijaServis>()
            .AddScoped<ITerminServis, TerminServis>()
            .AddScoped<IRezervacijaServis, RezervacijaServis>();
    }
}
