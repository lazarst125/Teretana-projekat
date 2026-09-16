using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Teretana.Api.Infrastruktura.Autentikacija;
using Teretana.Api.Infrastruktura.Dokumentacija;
using Teretana.Api.Infrastruktura.ObradaGresaka;
using Teretana.Api.Infrastruktura.StatusSistema;
using Teretana.Api.Podaci;
using Teretana.Api.Servisi;

const string ArgumentZaResetBaze = "--reset-db";

// Frontend stoji u client/ pored server/, pa se statički fajlovi serviraju odatle umesto iz wwwroot.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [.. args.Where(argument => argument != ArgumentZaResetBaze)],
    WebRootPath = Path.Combine("..", "client"),
});

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(opcije =>
{
    opcije.IncludeScopes = true;
    opcije.UseUtcTimestamp = true;
    opcije.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails(opcije => opcije.CustomizeProblemDetails = ProblemDetailsPodesavanja.DodajKodZaValidaciju);
builder.Services.AddExceptionHandler<DomenskaGreskaHandler>();
builder.Services.AddExceptionHandler<NeobradjenIzuzetakHandler>();
builder.Services
    .AddControllers(opcije => opcije.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(opcije => opcije.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Odgovori kontrolera koriste MVC JSON podešavanja, a generator OpenAPI šema opšta HTTP JSON podešavanja;
// oba moraju da serijalizuju enum kao tekst da bi dokument opisivao ono što API zaista vraća.
builder.Services.ConfigureHttpJsonOptions(opcije => opcije.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.DodajOpenApiDokumentaciju();
builder.Services.DodajBazuPodataka();
builder.Services.DodajAutentikaciju();
builder.Services.DodajAplikacioneServise();
builder.Services.AddHealthChecks().AddDbContextCheck<TeretanaDbContext>("baza");

var app = builder.Build();

if (args.Contains(ArgumentZaResetBaze))
{
    Environment.ExitCode = await PripremaBaze.ResetujIzKomandneLinijeAsync(app);
    return;
}

AutentikacijaRegistracija.UpozoriAkoJeJwtKljucPrivremen(app);
await PripremaBaze.PripremiAsync(app.Services);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();
app.UseStaticFiles();

if (!app.Environment.IsProduction())
{
    app.UseSwaggerUI(opcije =>
    {
        opcije.SwaggerEndpoint(OpenApiRegistracija.PutanjaDokumenta, "Teretana API v1");
        opcije.RoutePrefix = "swagger";
        opcije.DocumentTitle = "Teretana API";
    });
}

app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi(OpenApiRegistracija.PutanjaDokumenta);
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = StatusSistemaOdgovor.UpisiAsync,
});
app.MapControllers();

app.Run();
