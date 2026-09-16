using System.ComponentModel.DataAnnotations;

namespace Teretana.Api.Ugovori.Validacija;

/// <summary>
/// Vrednost svojstva mora biti strogo posle vrednosti drugog svojstva istog objekta. Atribut je na svojstvu
/// (a ne u IValidatableObject), da bi ključ greške bio isti kao naziv JSON polja.
/// Kad bilo koja od dve vrednosti nedostaje, proveru preskače; obaveznost proverava [Required].
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PosleAttribute(string nazivPrethodnogSvojstva) : ValidationAttribute
{
    public string NazivPrethodnogSvojstva { get; } = nazivPrethodnogSvojstva;

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var prethodno = validationContext.ObjectType.GetProperty(NazivPrethodnogSvojstva)?.GetValue(validationContext.ObjectInstance);
        if (value is not IComparable vrednost || prethodno is null || vrednost.CompareTo(prethodno) > 0)
        {
            return ValidationResult.Success;
        }

        return new ValidationResult(
            FormatErrorMessage(validationContext.DisplayName),
            validationContext.MemberName is null ? null : [validationContext.MemberName]);
    }
}
