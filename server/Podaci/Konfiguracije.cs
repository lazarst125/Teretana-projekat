using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teretana.Api.Domen;

namespace Teretana.Api.Podaci.Konfiguracije;

internal sealed class KorisnikKonfiguracija : IEntityTypeConfiguration<Korisnik>
{
    public void Configure(EntityTypeBuilder<Korisnik> builder)
    {
        builder.ToTable("Korisnici", tabela =>
            tabela.HasCheckConstraint("CK_Korisnici_Uloga", "\"Uloga\" IN ('Clan', 'Trener')"));

        // NOCASE čini jedinstveni indeks neosetljivim na velika i mala slova (Ana@x i ana@x su isti nalog).
        builder.Property(k => k.Email).HasMaxLength(254).UseCollation("NOCASE");
        builder.HasIndex(k => k.Email).IsUnique();

        builder.Property(k => k.ImePrezime).HasMaxLength(100);
        builder.Property(k => k.LozinkaHash).HasMaxLength(200);
        builder.Property(k => k.Uloga).HasConversion<string>().HasMaxLength(20);
    }
}

internal sealed class TerminKonfiguracija : IEntityTypeConfiguration<Termin>
{
    public void Configure(EntityTypeBuilder<Termin> builder)
    {
        builder.ToTable("Termini", tabela =>
        {
            tabela.HasCheckConstraint("CK_Termini_Kapacitet", "\"Kapacitet\" > 0");
            tabela.HasCheckConstraint("CK_Termini_BrojPotvrdjenih", "\"BrojPotvrdjenih\" >= 0 AND \"BrojPotvrdjenih\" <= \"Kapacitet\"");
            tabela.HasCheckConstraint("CK_Termini_KrajPoslePocetka", "\"Kraj\" > \"Pocetak\"");
            tabela.HasCheckConstraint("CK_Termini_Status", "\"Status\" IN ('Aktivan', 'Otkazan')");
        });

        builder.Property(t => t.Naziv).HasMaxLength(100);
        builder.Property(t => t.Opis).HasMaxLength(500);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.Trener)
            .WithMany()
            .HasForeignKey(t => t.TrenerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Pocetak);
    }
}

internal sealed class RezervacijaKonfiguracija : IEntityTypeConfiguration<Rezervacija>
{
    public void Configure(EntityTypeBuilder<Rezervacija> builder)
    {
        builder.ToTable("Rezervacije", tabela =>
            tabela.HasCheckConstraint("CK_Rezervacije_Status", "\"Status\" IN ('Potvrdjena', 'NaCekanju', 'Otkazana')"));

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // Restrict: termin ili član sa prijavama ne može da se obriše ni kad se servis zaobiđe.
        builder.HasOne(r => r.Termin)
            .WithMany(t => t.Rezervacije)
            .HasForeignKey(r => r.TerminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Clan)
            .WithMany()
            .HasForeignKey(r => r.ClanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Otkazane prijave su van indeksa, pa član posle otkazivanja može ponovo da se prijavi.
        builder.HasIndex(r => new { r.TerminId, r.ClanId })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Potvrdjena', 'NaCekanju')")
            .HasDatabaseName("UX_Rezervacije_AktivnaPrijava");

        builder.HasIndex(r => new { r.TerminId, r.Status, r.KreiranaAt, r.Id })
            .HasDatabaseName("IX_Rezervacije_RedCekanja");
    }
}
