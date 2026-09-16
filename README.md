# Teretana — rezervacija termina

Web aplikacija za rezervaciju termina u teretani: termini sa ograničenim kapacitetom, otkazivanje sa rokom,
lista čekanja koja se automatski pomera i role **član** i **trener**.

Član pregleda raspored, rezerviše mesto ili se prijavljuje na listu čekanja i otkazuje rezervaciju do 2 sata
pre početka. Trener pravi, menja, otkazuje i briše svoje termine, vidi polaznike i evidentira prisustvo.
Svaka API operacija dostupna je i kroz korisnički interfejs.

## Dokumentacija

| Dokument | Sadržaj |
|---|---|
| `README.md` | pokretanje aplikacije i testova, test nalozi, demonstracioni podaci |
| [`docs/ARHITEKTURA.md`](docs/ARHITEKTURA.md) | struktura, tok zahteva, model podataka, rešenje konkurentnosti, obrazložene odluke |
| [`docs/TEST-PLAN.md`](docs/TEST-PLAN.md) | nivoi testiranja, pokrivenost poslovnih pravila i API operacija, izveštaji |

## Preduslovi

- .NET SDK **10** (projekat je proveren sa verzijom 10.0.401)
- Za rad u Visual Studio-u: **Visual Studio 2026** (Visual Studio 2022 ne podržava .NET 10)
- Internet konekcija pri prvom pokretanju (NuGet paketi, a za Playwright testove i Chromium)

Baza se ne instalira posebno: koristi se SQLite, a fajl baze pravi aplikacija pri prvom pokretanju.

Na Windows-u klonirajte projekat u kratku putanju (npr. `C:\Projekti\Teretana`). Paket Playwright sadrži
duboko ugnježdene fajlove, pa build Playwright testova pada sa greškom `MSB3021` ako putanja pređe 260 znakova,
osim ako su u sistemu uključene dugačke putanje (`LongPathsEnabled`).

## Struktura projekta

Projekat se sastoji iz tri celine koje se pokreću nezavisno:

| Celina | Folder | Opis |
|---|---|---|
| Web aplikacija | `server/` i `client/` | ASP.NET Core API (`server/`) koji servira i frontend (`client/`) |
| Komponentni testovi (NUnit) | `tests/Teretana.KomponentniTestovi` | testovi nad aplikacijom u procesu, svaki test sa sopstvenom bazom |
| E2E i API testovi (Playwright) | `tests/Teretana.PlaywrightTestovi` | testovi preko pravog HTTP-a i browsera |

```
client/     index.html, css/, js/ (ekrani, ruter, API klijent)
server/     Kontroleri/, Servisi/, Repozitorijumi/, Domen/, Ugovori/, Podaci/, Infrastruktura/
tests/      Teretana.KomponentniTestovi/, Teretana.PlaywrightTestovi/, Zajednicko/
docs/       ARHITEKTURA.md, TEST-PLAN.md
```

Rešenje `Teretana.sln` u korenu otvara sva tri projekta u Visual Studio-u ili Rider-u.

## Pokretanje aplikacije

```bash
dotnet run --project server
```

Aplikacija je dostupna na <http://localhost:5080>, a provera statusa sistema na <http://localhost:5080/health>.

API dokumentacija (van Production okruženja):

- **Swagger UI:** <http://localhost:5080/swagger> — dugme *Authorize* prima token iz `POST /api/auth/prijava`
- **OpenAPI dokument:** <http://localhost:5080/openapi/v1.json>

Pri pokretanju se primenjuju migracije. U Development okruženju (podrazumevano za `dotnet run`) prazna baza
se popunjava demonstracionim podacima. Fajl baze je `server/teretana.db`.

## Vraćanje baze na početno stanje

```bash
dotnet run --project server -- --reset-db
```

Komanda briše bazu, ponovo primenjuje migracije, upisuje demonstracione podatke i završava rad (ne pokreće server).
Dozvoljena je samo u Development okruženju.

## Test nalozi

| Email | Lozinka | Uloga | Ime |
|---|---|---|---|
| `trener1@teretana.local` | `Trener123!` | trener | Jelena Petrović |
| `trener2@teretana.local` | `Trener123!` | trener | Nikola Jovanović |
| `clan1@teretana.local` | `Clan123!` | član | Ana Marković |
| `clan2@teretana.local` | `Clan123!` | član | Stefan Ilić |
| `clan3@teretana.local` | `Clan123!` | član | Milica Đorđević |
| `clan4@teretana.local` | `Clan123!` | član | Luka Stojanović |
| `clan5@teretana.local` | `Clan123!` | član | Teodora Pavlović |
| `clan6@teretana.local` | `Clan123!` | član | Vuk Nikolić |

Nalozi i lozinke postoje samo u demonstracionim podacima za Development okruženje.

### JWT ključ

U Development okruženju aplikacija sama generiše privremeni ključ za potpisivanje tokena, pa radi bez
ikakvog podešavanja; posledica je da tokeni ne važe posle restarta. Van Development okruženja aplikacija
se ne pokreće bez ključa od najmanje 32 bajta, koji se zadaje kroz promenljivu okruženja:

```bash
Jwt__Kljuc="<nasumičan niz od najmanje 32 znaka>" dotnet run --project server --no-launch-profile
```

## Demonstracioni podaci

Vremena termina računaju se u odnosu na trenutak upisa podataka, pa svaki ivični slučaj važi odmah posle
pokretanja ili reset-a baze. Rok za otkazivanje je 2 sata pre početka termina.

| Termin | Trener | Stanje posle upisa | Šta se na njemu demonstrira |
|---|---|---|---|
| **Funkcionalni trening (bez prijava)** | trener1 | počinje za 4 dana; 0/12 | izmena i brisanje termina koji nema prijava (dozvoljeno) |
| **Joga (slobodna mesta)** | trener2 | počinje za 2 dana; 2/10 (clan5, clan6) | rezervacija slobodnog mesta; izmena i brisanje termina sa prijavama se odbija (409); trener1 ne može da menja tuđi termin (403) |
| **Crossfit (pun termin)** | trener1 | počinje za 1 dan; 3/3 (clan1, clan2, clan3) | rezervacija punog termina se odbija (409); prijava na listu čekanja |
| **Spinning (lista čekanja)** | trener1 | počinje za 3 dana; 2/2 (clan1, clan2); na čekanju clan3, pa clan4 | automatsko pomeranje liste: kad clan1 ili clan2 otkaže, clan3 postaje potvrđen, a clan4 ostaje prvi na čekanju |
| **Pilates (počinje za 90 minuta)** | trener1 | počinje za 90 minuta; 2/8 (clan5, clan4) | rok za otkazivanje je prošao: otkazivanje se odbija (422), a rezervacija slobodnog mesta je i dalje moguća |
| **Boks (završen termin)** | trener2 | počeo i završio se juče; 3/6; prisustvo: clan1 došao, clan2 nije došao, clan5 neevidentirano | rezervacija prošlog termina se odbija (422); trener evidentira prisustvo za clan5 |
| **Zumba (otkazan termin)** | trener1 | počinje za 5 dana; otkazan; prijave clan6 i clan3 otkazane | rezervacija otkazanog termina se odbija (409) |

Termin „Pilates" počinje 90 minuta posle upisa podataka. Kad taj termin počne, više ne pokazuje prošao rok nego
termin u toku, pa za ponovnu demonstraciju treba pokrenuti reset baze.

## Pokretanje testova

Testovi ne zahtevaju pokrenutu aplikaciju: svaka test celina sama podiže aplikaciju sa sopstvenom privremenom bazom.

```bash
# Komponentni testovi (NUnit)
dotnet test tests/Teretana.KomponentniTestovi

# Playwright E2E i API testovi (Chromium se instalira automatski pri prvom pokretanju)
dotnet test tests/Teretana.PlaywrightTestovi

# Samo API testovi
dotnet test tests/Teretana.PlaywrightTestovi --filter TestCategory=API

# Samo E2E testovi
dotnet test tests/Teretana.PlaywrightTestovi --filter TestCategory=E2E
```

Na Linuxu Chromium traži i sistemske biblioteke; posle build-a ih instalira komanda:

```bash
pwsh tests/Teretana.PlaywrightTestovi/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
```

Kad E2E test padne, trace, screenshot i video se čuvaju u
`tests/Teretana.PlaywrightTestovi/bin/<konfiguracija>/net10.0/playwright-artefakti/`.
Trace se otvara komandom `pwsh tests/Teretana.PlaywrightTestovi/bin/Debug/net10.0/playwright.ps1 show-trace <putanja>/trace.zip`.

## Izveštaj o pokrivenosti koda

Pokrivenost se meri za aplikaciju (`Teretana.Api`) iz obe test celine zajedno; migracije i generisani kod
se ne računaju (`tests/pokrivenost.runsettings`).

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
dotnet test tests/Teretana.KomponentniTestovi --collect "XPlat Code Coverage" --settings tests/pokrivenost.runsettings --results-directory TestResults
dotnet test tests/Teretana.PlaywrightTestovi --collect "XPlat Code Coverage" --settings tests/pokrivenost.runsettings --results-directory TestResults
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:TestResults/pokrivenost -reporttypes:"HtmlInline;TextSummary"
```

HTML izveštaj je u `TestResults/pokrivenost/index.html`, a kratak pregled u `TestResults/pokrivenost/Summary.txt`.
U CI-u je isti izveštaj dostupan kao artifact `izvestaj-pokrivenosti`, a sažetak se prikazuje na stranici pokretanja.
