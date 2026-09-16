using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Teretana.Api.Podaci.Migracije
{
    /// <inheritdoc />
    public partial class Pocetna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Korisnici",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false, collation: "NOCASE"),
                    ImePrezime = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LozinkaHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Uloga = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    KreiranAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Korisnici", x => x.Id);
                    table.CheckConstraint("CK_Korisnici_Uloga", "\"Uloga\" IN ('Clan', 'Trener')");
                });

            migrationBuilder.CreateTable(
                name: "Termini",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrenerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Opis = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Pocetak = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kraj = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kapacitet = table.Column<int>(type: "INTEGER", nullable: false),
                    BrojPotvrdjenih = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    KreiranAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Termini", x => x.Id);
                    table.CheckConstraint("CK_Termini_BrojPotvrdjenih", "\"BrojPotvrdjenih\" >= 0 AND \"BrojPotvrdjenih\" <= \"Kapacitet\"");
                    table.CheckConstraint("CK_Termini_Kapacitet", "\"Kapacitet\" > 0");
                    table.CheckConstraint("CK_Termini_KrajPoslePocetka", "\"Kraj\" > \"Pocetak\"");
                    table.CheckConstraint("CK_Termini_Status", "\"Status\" IN ('Aktivan', 'Otkazan')");
                    table.ForeignKey(
                        name: "FK_Termini_Korisnici_TrenerId",
                        column: x => x.TrenerId,
                        principalTable: "Korisnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rezervacije",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TerminId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    KreiranaAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PotvrdjenaAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OtkazanaAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Prisustvovao = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rezervacije", x => x.Id);
                    table.CheckConstraint("CK_Rezervacije_Status", "\"Status\" IN ('Potvrdjena', 'NaCekanju', 'Otkazana')");
                    table.ForeignKey(
                        name: "FK_Rezervacije_Korisnici_ClanId",
                        column: x => x.ClanId,
                        principalTable: "Korisnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rezervacije_Termini_TerminId",
                        column: x => x.TerminId,
                        principalTable: "Termini",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Korisnici_Email",
                table: "Korisnici",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rezervacije_ClanId",
                table: "Rezervacije",
                column: "ClanId");

            migrationBuilder.CreateIndex(
                name: "IX_Rezervacije_RedCekanja",
                table: "Rezervacije",
                columns: new[] { "TerminId", "Status", "KreiranaAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_Rezervacije_AktivnaPrijava",
                table: "Rezervacije",
                columns: new[] { "TerminId", "ClanId" },
                unique: true,
                filter: "\"Status\" IN ('Potvrdjena', 'NaCekanju')");

            migrationBuilder.CreateIndex(
                name: "IX_Termini_Pocetak",
                table: "Termini",
                column: "Pocetak");

            migrationBuilder.CreateIndex(
                name: "IX_Termini_TrenerId",
                table: "Termini",
                column: "TrenerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rezervacije");

            migrationBuilder.DropTable(
                name: "Termini");

            migrationBuilder.DropTable(
                name: "Korisnici");
        }
    }
}
