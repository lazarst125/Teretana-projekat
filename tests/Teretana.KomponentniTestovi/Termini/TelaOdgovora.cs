namespace Teretana.KomponentniTestovi.Termini;

/// <summary>
/// Testovi čitaju odgovore u sopstvene tipove, a ne u ugovore iz API projekta, da bi promena imena
/// polja u API-ju oborila test umesto da je deserializacija tiho prati.
/// </summary>
internal sealed record TrenerTelo(int Id, string ImePrezime);

internal sealed record MojaPrijavaTelo(int RezervacijaId, string Status, int? PozicijaNaCekanju);

internal sealed record TerminTelo(
    int Id,
    string Naziv,
    string? Opis,
    DateTime Pocetak,
    DateTime Kraj,
    int Kapacitet,
    int BrojPotvrdjenih,
    int SlobodnaMesta,
    int BrojNaCekanju,
    string Status,
    TrenerTelo Trener,
    MojaPrijavaTelo? MojaPrijava);

internal sealed record StranicaTelo(IReadOnlyList<TerminTelo> Stavke, int Stranica, int VelicinaStranice, int UkupnoStavki, int UkupnoStranica);

internal sealed record PotvrdjeniTelo(int RezervacijaId, int ClanId, string ImePrezime, string Email, DateTime? PotvrdjenaAt, bool? Prisustvovao);

internal sealed record NaCekanjuTelo(int RezervacijaId, int ClanId, string ImePrezime, string Email, int Pozicija, DateTime PrijavljenAt);

internal sealed record PolazniciTelo(int TerminId, IReadOnlyList<PotvrdjeniTelo> Potvrdjeni, IReadOnlyList<NaCekanjuTelo> ListaCekanja);
