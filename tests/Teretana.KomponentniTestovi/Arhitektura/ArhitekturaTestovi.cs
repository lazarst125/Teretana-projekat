using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Teretana.KomponentniTestovi.Arhitektura;

[Parallelizable(ParallelScope.All)]
[Category("Unit")]
public sealed class ArhitekturaTestovi
{
    private const string NamespaceRepozitorijuma = "Teretana.Api.Repozitorijumi";

    private static readonly Type[] Kontroleri = [.. typeof(Program).Assembly.GetTypes()
        .Where(tip => typeof(ControllerBase).IsAssignableFrom(tip) && !tip.IsAbstract)];

    [Test]
    public void SvakaAkcijaKontrolera_ImaEksplicitnoPraviloPristupa()
    {
        var akcijeBezPravila = Kontroleri
            .SelectMany(kontroler => kontroler.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(akcija => !ImaPraviloPristupa(kontroler) && !ImaPraviloPristupa(akcija))
                .Select(akcija => $"{kontroler.Name}.{akcija.Name}"));

        Assert.Multiple(() =>
        {
            Assert.That(Kontroleri, Is.Not.Empty);
            Assert.That(akcijeBezPravila, Is.Empty, "Svaka akcija mora imati [Authorize] ili [AllowAnonymous].");
        });
    }

    [Test]
    public void Kontroleri_NeZavisePoBaziNiRepozitorijumima()
    {
        var zabranjeneZavisnosti = Kontroleri
            .SelectMany(kontroler => kontroler.GetConstructors()
                .SelectMany(konstruktor => konstruktor.GetParameters())
                .Where(parametar => typeof(DbContext).IsAssignableFrom(parametar.ParameterType)
                    || parametar.ParameterType.Namespace == NamespaceRepozitorijuma)
                .Select(parametar => $"{kontroler.Name}({parametar.ParameterType.Name})"));

        Assert.Multiple(() =>
        {
            Assert.That(Kontroleri, Is.Not.Empty);
            Assert.That(zabranjeneZavisnosti, Is.Empty, "Kontroleri pozivaju servise, a ne bazu ili repozitorijume.");
        });
    }

    private static bool ImaPraviloPristupa(MemberInfo clan) =>
        clan.GetCustomAttributes(inherit: true).Any(atribut => atribut is IAuthorizeData or IAllowAnonymous);
}
