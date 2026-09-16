using Teretana.KomponentniTestovi.Termini;

namespace Teretana.KomponentniTestovi.Rezervacije;

internal sealed record TerminUkratkoTelo(int Id, string Naziv, DateTime Pocetak, DateTime Kraj, string Status, TrenerTelo Trener);

internal sealed record RezervacijaTelo(
    int Id,
    string Status,
    int? PozicijaNaCekanju,
    DateTime KreiranaAt,
    DateTime? PotvrdjenaAt,
    DateTime? OtkazanaAt,
    bool? Prisustvovao,
    DateTime RokZaOtkazivanje,
    bool MozeDaSeOtkaze,
    TerminUkratkoTelo Termin);

internal sealed record StranicaRezervacijaTelo(IReadOnlyList<RezervacijaTelo> Stavke, int Stranica, int VelicinaStranice, int UkupnoStavki, int UkupnoStranica);
