using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Teretana.Api.Domen;

namespace Teretana.Api.Infrastruktura.Autentikacija;

public static partial class AutentikacijaRegistracija
{
    private const int MinimalnaDuzinaKljucaUBajtovima = 32;

    public static IServiceCollection DodajAutentikaciju(this IServiceCollection servisi)
    {
        servisi.AddOptions<JwtPodesavanja>()
            .BindConfiguration(JwtPodesavanja.Sekcija)
            .PostConfigure<IHostEnvironment>((jwt, okruzenje) =>
            {
                // Aplikacija mora da se pokrene iz čistog klona bez tajni u repozitorijumu, pa se u Development-u
                // ključ generiše pri startu. Posledica: tokeni ne važe posle restarta.
                if (okruzenje.IsDevelopment() && string.IsNullOrEmpty(jwt.Kljuc))
                {
                    jwt.Kljuc = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                }
            })
            .Validate(
                jwt => Encoding.UTF8.GetByteCount(jwt.Kljuc) >= MinimalnaDuzinaKljucaUBajtovima,
                $"Jwt:Kljuc mora da ima najmanje {MinimalnaDuzinaKljucaUBajtovima} bajta. Van Development okruženja postavite ga kroz promenljivu okruženja Jwt__Kljuc.")
            .Validate(
                jwt => !string.IsNullOrWhiteSpace(jwt.Izdavac) && !string.IsNullOrWhiteSpace(jwt.Publika) && jwt.TrajanjeTokena > TimeSpan.Zero,
                "Jwt:Izdavac, Jwt:Publika i Jwt:TrajanjeTokena moraju biti podešeni.")
            .ValidateOnStart();

        servisi.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        servisi.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtPodesavanja>, TimeProvider>((opcije, podesavanja, vreme) =>
            {
                var jwt = podesavanja.Value;
                opcije.MapInboundClaims = false;
                opcije.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Izdavac,
                    ValidAudience = jwt.Publika,
                    IssuerSigningKey = jwt.KljucZaPotpis(),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    NameClaimType = JwtRegisteredClaimNames.Name,
                    RoleClaimType = TokenServis.TipClaimaUloge,
                    // Trajanje tokena se proverava prema istom TimeProvider-u kojim je token izdat,
                    // tako da istek može deterministički da se testira.
                    LifetimeValidator = (vaziOd, vaziDo, _, _) =>
                    {
                        var sada = vreme.GetUtcNow().UtcDateTime;
                        return (vaziOd is null || vaziOd <= sada) && vaziDo is not null && sada < vaziDo;
                    },
                };
            });

        servisi.AddAuthorizationBuilder()
            .AddPolicy(Politike.Clan, politika => politika.RequireRole(nameof(Uloga.Clan)))
            .AddPolicy(Politike.Trener, politika => politika.RequireRole(nameof(Uloga.Trener)));
        servisi.AddSingleton<IPasswordHasher<Korisnik>, PasswordHasher<Korisnik>>();
        servisi.AddSingleton<ITokenServis, TokenServis>();

        return servisi;
    }

    public static void UpozoriAkoJeJwtKljucPrivremen(WebApplication app)
    {
        if (app.Environment.IsDevelopment() && string.IsNullOrEmpty(app.Configuration[$"{JwtPodesavanja.Sekcija}:{nameof(JwtPodesavanja.Kljuc)}"]))
        {
            LogPrivremeniJwtKljuc(app.Logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jwt:Kljuc nije podešen; koristi se privremeni ključ, pa izdati tokeni ne važe posle restarta aplikacije.")]
    private static partial void LogPrivremeniJwtKljuc(ILogger logger);
}

public static class Politike
{
    public const string Clan = nameof(Uloga.Clan);

    public const string Trener = nameof(Uloga.Trener);
}

internal static class KorisnikIzTokena
{
    public static int IdKorisnika(this ClaimsPrincipal korisnik)
    {
        var sub = korisnik.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? throw new InvalidOperationException("Autentifikovani korisnik nema 'sub' claim.");
        return int.Parse(sub, CultureInfo.InvariantCulture);
    }
}

public sealed class JwtPodesavanja
{
    public const string Sekcija = "Jwt";

    public string Izdavac { get; set; } = string.Empty;

    public string Publika { get; set; } = string.Empty;

    public string Kljuc { get; set; } = string.Empty;

    public TimeSpan TrajanjeTokena { get; set; }

    public SymmetricSecurityKey KljucZaPotpis() => new(Encoding.UTF8.GetBytes(Kljuc));
}

public interface ITokenServis
{
    IzdatToken Izdaj(Korisnik korisnik);
}

public sealed record IzdatToken(string Token, DateTime Istice);

internal sealed class TokenServis(IOptions<JwtPodesavanja> podesavanja, TimeProvider vreme) : ITokenServis
{
    public const string TipClaimaUloge = "role";

    private readonly JsonWebTokenHandler _handler = new();

    public IzdatToken Izdaj(Korisnik korisnik)
    {
        var jwt = podesavanja.Value;
        var sada = vreme.GetUtcNow().UtcDateTime;
        var istice = sada.Add(jwt.TrajanjeTokena);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Izdavac,
            Audience = jwt.Publika,
            IssuedAt = sada,
            NotBefore = sada,
            Expires = istice,
            SigningCredentials = new SigningCredentials(jwt.KljucZaPotpis(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = korisnik.Id.ToString(CultureInfo.InvariantCulture),
                [JwtRegisteredClaimNames.Email] = korisnik.Email,
                [JwtRegisteredClaimNames.Name] = korisnik.ImePrezime,
                [TipClaimaUloge] = korisnik.Uloga.ToString(),
            },
        });

        return new IzdatToken(token, istice);
    }
}
