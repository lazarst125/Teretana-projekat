namespace Teretana.Api.Domen;

public enum VrstaGreske
{
    NijeAutorizovan,
    Zabranjeno,
    NijePronadjeno,
    Konflikt,
    PoslovnoPravilo,
}

/// <summary>
/// Očekivano odbijanje zahteva po poslovnom pravilu. Servisi je bacaju, a obrada grešaka je na jednom
/// mestu prevodi u ProblemDetails sa odgovarajućim statusom i stabilnim kodom greške.
/// </summary>
public sealed class DomenskaGreska(VrstaGreske vrsta, string kod, string poruka) : Exception(poruka)
{
    public VrstaGreske Vrsta { get; } = vrsta;

    public string Kod { get; } = kod;
}

public static class Greske
{
    public static DomenskaGreska EmailZauzet() =>
        new(VrstaGreske.Konflikt, "email-zauzet", "Nalog sa ovim email-om već postoji.");

    public static DomenskaGreska NeispravniKredencijali() =>
        new(VrstaGreske.NijeAutorizovan, "neispravni-kredencijali", "Email ili lozinka nisu ispravni.");

    public static DomenskaGreska KorisnikIzTokenaNePostoji() =>
        new(VrstaGreske.NijeAutorizovan, "korisnik-ne-postoji", "Korisnik iz tokena više ne postoji.");

    public static DomenskaGreska NalogImaPodatke() =>
        new(VrstaGreske.Konflikt, "nalog-ima-podatke", "Nalog ima termine ili prijave i ne može da se obriše; istorija prijava se čuva.");

    public static DomenskaGreska TerminNePostoji() =>
        new(VrstaGreske.NijePronadjeno, "termin-ne-postoji", "Termin ne postoji.");

    public static DomenskaGreska TudjiTermin() =>
        new(VrstaGreske.Zabranjeno, "tudji-termin", "Trener može da upravlja samo svojim terminima.");

    public static DomenskaGreska TerminOtkazan() =>
        new(VrstaGreske.Konflikt, "termin-otkazan", "Termin je otkazan.");

    public static DomenskaGreska TerminImaPrijave() =>
        new(VrstaGreske.Konflikt, "termin-ima-prijave", "Termin ima prijave i ne može da se menja ni briše; umesto toga ga otkažite.");

    public static DomenskaGreska TerminJePoceo() =>
        new(VrstaGreske.PoslovnoPravilo, "termin-je-poceo", "Termin je već počeo.");

    public static DomenskaGreska PocetakUProslosti() =>
        new(VrstaGreske.PoslovnoPravilo, "pocetak-u-proslosti", "Početak termina mora biti u budućnosti.");

    public static DomenskaGreska TerminPopunjen() =>
        new(VrstaGreske.Konflikt, "termin-popunjen", "Termin je popunjen. Možete se prijaviti na listu čekanja.");

    public static DomenskaGreska TerminImaSlobodnihMesta() =>
        new(VrstaGreske.Konflikt, "termin-ima-slobodnih-mesta", "Termin ima slobodnih mesta; rezervišite mesto umesto prijave na listu čekanja.");

    public static DomenskaGreska VecPrijavljen() =>
        new(VrstaGreske.Konflikt, "vec-prijavljen", "Već imate prijavu za ovaj termin.");

    public static DomenskaGreska RezervacijaNePostoji() =>
        new(VrstaGreske.NijePronadjeno, "rezervacija-ne-postoji", "Rezervacija ne postoji.");

    public static DomenskaGreska RezervacijaOtkazana() =>
        new(VrstaGreske.Konflikt, "rezervacija-otkazana", "Rezervacija je već otkazana.");

    public static DomenskaGreska RokZaOtkazivanjeIstekao() =>
        new(VrstaGreske.PoslovnoPravilo, "rok-za-otkazivanje-istekao", "Rok za otkazivanje rezervacije je istekao.");

    public static DomenskaGreska RezervacijaNijePotvrdjena() =>
        new(VrstaGreske.Konflikt, "rezervacija-nije-potvrdjena", "Prisustvo se evidentira samo za potvrđene rezervacije.");

    public static DomenskaGreska TerminNijePoceo() =>
        new(VrstaGreske.PoslovnoPravilo, "termin-nije-poceo", "Prisustvo može da se evidentira tek kada termin počne.");
}
