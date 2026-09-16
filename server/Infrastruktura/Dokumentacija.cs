using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace Teretana.Api.Infrastruktura.Dokumentacija;

public static class OpenApiRegistracija
{
    public const string PutanjaDokumenta = "/openapi/v1.json";

    private const string NazivBearerSeme = "Bearer";

    public static IServiceCollection DodajOpenApiDokumentaciju(this IServiceCollection servisi) =>
        servisi.AddOpenApi(opcije =>
        {
            opcije.AddDocumentTransformer((dokument, _, _) =>
            {
                dokument.Info = new OpenApiInfo
                {
                    Title = "Teretana API",
                    Version = "v1",
                    Description = "Rezervacija termina u teretani: termini sa ograničenim kapacitetom, lista čekanja, "
                        + "otkazivanje sa rokom i role član i trener. Greške se vraćaju kao ProblemDetails sa poljem code.",
                };
                dokument.AddComponent(NazivBearerSeme, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Token iz odgovora POST /api/auth/prijava.",
                });
                return Task.CompletedTask;
            });

            // 401 i 403 proizilaze iz [Authorize] metapodataka, pa se dodaju ovde umesto na svakoj akciji.
            opcije.AddOperationTransformer((operacija, kontekst, _) =>
            {
                var metapodaci = kontekst.Description.ActionDescriptor.EndpointMetadata;
                var pravila = metapodaci.OfType<IAuthorizeData>().ToList();
                if (pravila.Count == 0 || metapodaci.OfType<IAllowAnonymous>().Any())
                {
                    return Task.CompletedTask;
                }

                operacija.Security ??= [];
                operacija.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(NazivBearerSeme, kontekst.Document)] = [],
                });

                operacija.Responses ??= [];
                operacija.Responses.TryAdd("401", new OpenApiResponse { Description = "Token nedostaje, neispravan je ili je istekao." });
                if (pravila.Any(pravilo => pravilo.Policy is not null))
                {
                    operacija.Responses.TryAdd("403", new OpenApiResponse { Description = "Uloga prijavljenog korisnika nema pristup ovoj operaciji." });
                }

                return Task.CompletedTask;
            });
        });
}

/// <summary>Dokumentuje odgovor greške kao ProblemDetails sa poljem code.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProblemOdgovorAttribute(int statusCode)
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), statusCode, "application/problem+json");

/// <summary>Dokumentuje 400 odgovor sa greškama po polju.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidacioniProblemOdgovorAttribute()
    : ProducesResponseTypeAttribute(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json");
