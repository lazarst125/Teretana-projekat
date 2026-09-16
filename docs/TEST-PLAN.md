# TEST-PLAN

Strategija testiranja aplikacije za rezervaciju termina: koji nivoi postoje i zašto, kako su testovi izolovani,
koji test pokriva koje poslovno pravilo i API operaciju, šta svesno nije pokriveno i kako se čitaju izveštaji.
Komande za pokretanje su u `README.md`, a arhitektura u `ARHITEKTURA.md`.

## 1. Nivoi testiranja

Testovi su podeljeni u dve celine koje se pokreću nezavisno, kako traži specifikacija predmeta.

| Nivo | Celina i kategorija | Broj | Šta dokazuje | Kako radi |
|---|---|---|---|---|
| Unit | `Teretana.KomponentniTestovi`, `Unit` | 19 | granice roka za otkazivanje, validacija zahteva, grane servisa kada uslovni upis u bazi ne uspe posle provere, pravila arhitekture | bez baze i HTTP-a; zavisnosti su NSubstitute lažnjaci, vreme je `FakeTimeProvider` |
| Komponentni | `Teretana.KomponentniTestovi`, `Komponentni` | 142 | svaka API operacija sa uspešnim i neuspešnim ishodima i tačnim status kodom, matrica pravila pristupa (401 i 403) za svaku zaštićenu operaciju, ograničenja baze, konkurentnost, demonstracioni podaci, OpenAPI ugovor | aplikacija u procesu (`WebApplicationFactory`), sopstvena SQLite baza i sopstveni lažni sat po testu; test sme da čita i piše bazu da bi pripremio stanje ili dokazao ograničenje |
| API | `Teretana.PlaywrightTestovi`, `API` | 13 | tokovi preko pravog HTTP-a i Kestrel servera: Bearer zaglavlje i odbijanje pokvarenog tokena, vreme sa zonom i straničenje u query string-u, izmena i brisanje termina, lista čekanja između više korisnika, prisustvo i „moje rezervacije“, istovremeni zahtevi preko mreže, oblik svake vrste greške (400/401/403/404/409/422), serviranje frontenda i dokumentacije iz istog procesa | Playwright `APIRequestContext` nad aplikacijom na slobodnom portu, baza po test klasi |
| E2E | `Teretana.PlaywrightTestovi`, `E2E` | 34 | kompletni tokovi kroz UI za člana i trenera, unos parametara (filteri po datumu i treneru, sortiranje, straničenje), poruke grešaka i oporavak posle prekida veze, istekla sesija, nepoznata adresa, dijalog potvrde i fokus, rad samo tastaturom, prikaz na telefonu, XSS | Chromium, Page Object Model, lokatori samo po `data-testid`, web-first `Expect` asercije |

Ukupno **208** testova: 161 u celini komponentnih testova i 47 u Playwright celini.

**Zašto ovakva raspodela.** Specifikacija traži najmanje tri komponentna testa po API operaciji, pa je najviše
testova na komponentnom nivou; tamo je i najjeftinije proveriti tačan status kod, telo odgovora i stanje baze.
API testovi ne ponavljaju slučajeve koji već postoje identično u komponentnim testovima: pokrivaju samo ono što
se vidi tek preko pravog HTTP-a. E2E testovi pokrivaju tokove i prikaz, a ne svaki status kod ponovo.

## 2. Izolacija i nezavisnost

| Mera | Gde |
|---|---|
| Svaki komponentni test dobija novu instancu aplikacije i sopstveni SQLite fajl koji se briše posle testa | `Infrastruktura/KomponentniTest.cs`, `tests/Zajednicko/TestnaBazaIKonfiguracija.cs` |
| Svaki komponentni test ima sopstveni sat postavljen na fiksni trenutak; datumi u testovima ne zastarevaju | `KomponentniTest.Vreme` (`FakeTimeProvider`), `TestniEntiteti.Sada` |
| Paralelno izvršavanje klasa i metoda | `[assembly: Parallelizable(ParallelScope.Fixtures)]`, `[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` i `[Parallelizable(ParallelScope.All)]` na baznoj klasi |
| Playwright klase idu paralelno, svaka sa sopstvenom aplikacijom i bazom; testovi unutar klase dele browser, a svaki dobija novi browser context | `PokretanjeTestova.cs`, `Infrastruktura/HostovanaAplikacija.cs`, `Infrastruktura/BazneKlase.cs` |
| API i E2E testovi prave sopstvene podatke sa jedinstvenim email-ovima kroz API; direktno u bazu idu samo trenerski nalog i termin koji je već počeo, jer ih API ne dozvoljava | `Infrastruktura/TestniPodaci.cs` |
| Nijedan test ne čita demonstracione podatke osim testova samih demonstracionih podataka (test okruženje je `Testing`) | `BazaPodataka/PocetniPodaciTestovi.cs` |
| Nema `Thread.Sleep`, `Task.Delay`, `[Retry]`, `[Random]` ni fiksnih timeout-a | provereno pretragom kroz `tests/` |

Nezavisnost se dokazuje paralelnim izvršavanjem uz izolovano stanje: vremena iz TRX izveštaja pokazuju da testovi iste
klase počinju istovremeno. Ceo set je pokrenut tri puta zaredom, bez ijednog pada. Testovi se ne pokreću
nasumičnim redosledom, jer NUnit to ne podržava bez oslanjanja na interne klase.

## 3. Pokrivenost poslovnih pravila

| # | Pravilo | Glavni nivo i zašto | Testovi |
|---|---|---|---|
| 1 | Isti član ne može dva puta da rezerviše isti termin | **Komponentni**, jer garanciju daje jedinstveni indeks u bazi, a ne samo servis | `Rezervacija_ClanVecImaPotvrdjenuRezervaciju_Vraca409VecPrijavljenIBrojacSeNeMenja`, `DrugaPotvrdjenaRezervacijaIstogClanaMimoServisa_BazaOdbijaJedinstvenimIndeksom`, `Rezervacija_ClanPotvrdjenIIstovremenoNaCekanjuZaIstiTermin_BazaOdbija`, `RezervacijaURepozitorijumu_ClanVecImaAktivnuPrijavu_VracaVecPrijavljenIPonistavaZauzetoMesto`, `PrijavaNaListuCekanja_ClanVecImaPotvrdjenuRezervaciju_Vraca409VecPrijavljen`; unit `Rezervacija_IstiClanUpisanPosleProvere_OdbijaSeSaVecPrijavljen`; E2E `TerminNaKomClanVecImaRezervaciju_NeNudiNovuRezervacijuVecPostojecuPrijavu` |
| 2 | Dva istovremena zahteva za poslednje mesto: tačno jedan prolazi, drugi dobija jasan odgovor | **Komponentni**, deterministički: ishod ne sme da zavisi od tajminga | `DvaIstovremenaZahtevaZaPoslednjeMesto_TacnoJedanDobijaMestoADrugiDobija409TerminPopunjen` (dva paralelna HTTP zahteva, 201 + 409 `termin-popunjen`, brojač jednak broju potvrđenih), `DvaKontekstaKojaObaVideSlobodnoMesto_UslovnoZauzimanjePropustaSamoPrvog`, `Termin_BrojPotvrdjenihVeciOdKapaciteta_BazaOdbija`, `Rezervacija_PunTermin_Vraca409TerminPopunjen`; API `DvaIstovremenaZahtevaZaPoslednjeMesto_PrekoKestrela_TacnoJedanDobijaMesto` |
| 3 | Otkazivanje tačno na granici roka, neposredno pre i posle | **Unit i komponentni**, jer su granice čista funkcija vremena, a lažni sat ih pogađa u sekundu | `Otkazivanje_SekundPreIstekaRoka_Vraca204`, `Otkazivanje_TacnoUTrenutkuIstekaRoka_Vraca204`, `Otkazivanje_SekundPosleIstekaRoka_Vraca422RokZaOtkazivanjeIstekao`; unit `RokJeIstekao_SekundPreIstekaRoka_JosNije`, `RokJeIstekao_TacnoUTrenutkuIstekaRoka_JosNije`, `RokJeIstekao_SekundPosleIstekaRoka_Jeste`, `Otkazivanje_PotvrdjenaRezervacijaPosleIstekaRoka_OdbijaSeIRepozitorijumNeOtkazuje`; E2E `PotvrdjenaRezervacija_PosleIstekaRoka_NemaOtkazivanjaIObjasnjavaZasto`, `NoviClan_RegistrujeSeRezervisePaOtkazujePreRoka_MestoSeOslobadja` |
| 4 | Kada se mesto oslobodi, prvi sa liste čekanja automatski prelazi u rezervaciju | **Komponentni**, jer je to transakcija nad više redova čiji se ishod proverava u bazi | `Otkazivanje_PotvrdjenaRezervacijaSaListomCekanja_PrviSaListePostajePotvrdjenADrugiIDaljeCeka`, `Otkazivanje_PotvrdjenaRezervacijaBezListeCekanja_SmanjujeBrojPotvrdjenih`, `PrijavaNaListuCekanja_PunTermin_Vraca201NaCekanjuIzaRanijePrijavljenih`, `Polaznici_TerminSaPotvrdjenimICekanjem_VracaPotvrdjeneIListuCekanjaPoVremenuPrijave`; API `PoslednjeMestoListaCekanjaIOtkazivanje_ClanSaListeAutomatskiDobijaMesto_PrekoHttp`; E2E `PopunjenTermin_KadaPotvrdjeniClanOtkaze_PrviSaListeCekanjaAutomatskiDobijaMesto` (dva browser context-a) |
| 5 | Član ne sme da pristupi trenerskim operacijama (kreiranje, izmena, brisanje termina, uvid u polaznike) | **Komponentni**, jer je autorizacija HTTP ugovor; arhitekturni test sprečava da nova akcija ostane bez pravila | `OperacijaNamenjenaDrugojUlozi_Vraca403IPodaciOstajuIsti` (11 slučajeva: član na svakoj trenerskoj operaciji i trener na svakoj operaciji člana, nad postojećim terminom i rezervacijom, uz proveru da se ništa nije promenilo), `ZasticenaOperacija_BezTokena_Vraca401IPodaciOstajuIsti` (14 slučajeva), `Polaznici_TudjiTermin_Vraca403TudjiTermin`, `Izmena_TudjiTermin_Vraca403TudjiTermin`, `Otkazivanje_TudjiTermin_Vraca403TudjiTermin`, `Prisustvo_RezervacijaNaTudjemTerminu_Vraca403TudjiTermin`; unit `SvakaAkcijaKontrolera_ImaEksplicitnoPraviloPristupa`; E2E `ClanNaTrenerskojAdresi_VidiNematePristupINemaTrenerskuNavigaciju` |
| 6 | Rezervacija termina koji je već prošao ili ga je trener otkazao | **Komponentni** | `Rezervacija_TerminKojiJePoceo_Vraca422TerminJePoceo`, `Rezervacija_OtkazanTermin_Vraca409TerminOtkazan`, `PrijavaNaListuCekanja_OtkazanTermin_Vraca409TerminOtkazan`, `Otkazivanje_AktivanTerminSaPrijavama_OtkazujeTerminISvePrijaveIBrojacJeNula`; E2E `OtkazivanjeTermina_ClanSaRezervacijomVidiDaJeTerminOtkazan` |
| 7 | Brisanje ili izmena termina na kom već postoje rezervacije | **Komponentni i E2E**: baza čuva pravilo i kad se servis zaobiđe, a korisnik mora da vidi razlog | `Izmena_TerminSaAktivnomPrijavom_Vraca409TerminImaPrijaveIPodaciOstajuIsti`, `Brisanje_TerminSaOtkazanomPrijavom_Vraca409ITerminOstaje`, `BrisanjeMimoServisa_TerminSaPrijavom_BazaOdbijaStranimKljucem`, `BrisanjeURepozitorijumu_PrijavaNastalaPosleProvereUServisu_VracaFalseITerminOstaje`; unit `Izmena_PrijavaNastalaPosleProvere_OdbijaSeSaTerminImaPrijave`; E2E `BrisanjeTerminaSaRezervacijom_PrikazujeKonfliktITerminOstaje`, `IzmenaTerminaSaRezervacijom_PrikazujeKonfliktUFormi` |

## 4. Pokrivenost API operacija

Specifikacija traži najmanje tri komponentna testa po operaciji. Broj u tabeli je broj komponentnih testova koji
pozivaju operaciju preko HTTP-a (slučajevi `[TestCase]` se broje pojedinačno); unit testovi servisa su navedeni posebno.

| # | Operacija | Komponentni | Pokriveni status kodovi | Dodatno |
|---|---|---|---|---|
| 1 | `POST /api/auth/registracija` | 5 | 201, 400, 409 | uloga iz tela se ignoriše; dupli email kroz jedinstveni indeks; API i E2E tok |
| 2 | `POST /api/auth/prijava` | 4 | 200, 400, 401 | isti odgovor za nepostojeći email i pogrešnu lozinku; E2E |
| 3 | `GET /api/auth/ja` | 6 | 200, 401 (bez tokena, drugi ključ, istekao, obrisan korisnik) | API tok |
| 4 | `PUT /api/auth/ja` | 4 | 200, 400, 401 | lozinka se menja samo kada je zadata; heš ostaje netaknut pri izmeni imena; E2E forma |
| 5 | `DELETE /api/auth/ja` | 6 | 204, 401, 409 | nalog sa terminima ili prijavama, i otkazanim, ostaje; FK u bazi; E2E dijalog potvrde i konflikt |
| 6 | `GET /api/termini` | 12 | 200, 400, 401 | straničenje, prazna lista, opseg datuma, samo slobodni, status, svih šest sortiranja; API straničenje kroz query string i 400 sa imenom poslatog parametra; E2E filteri (uključujući opseg datuma), sortiranje po slobodnim mestima i straničenje |
| 7 | `GET /api/termini/{id}` | 3 | 200, 401, 404 | pozicija na čekanju; API tok; E2E stanje greške |
| 8 | `POST /api/termini` | 6 | 201, 400, 401, 403, 422 | serverska polja iz tela se ignorišu; API tok; E2E forma |
| 9 | `PUT /api/termini/{id}` | 8 | 200, 401, 403, 404, 409, 422 | + 2 unit (trka); API tok; E2E 409 |
| 10 | `DELETE /api/termini/{id}` | 6 | 204, 401, 403, 409 | FK u bazi i trka u repozitorijumu; API tok; E2E 409 |
| 11 | `POST /api/termini/{id}/otkazivanje` | 6 | 200, 401, 403, 409, 422 | + 2 unit (trka); API tok; E2E |
| 12 | `GET /api/termini/{id}/polaznici` | 4 | 200, 401, 403 | redosled liste čekanja; API tok; E2E |
| 13 | `POST /api/termini/{id}/rezervacije` | 11 | 201, 401, 403, 409, 422 | konkurentnost; + 1 unit; API i E2E |
| 14 | `POST /api/termini/{id}/lista-cekanja` | 6 | 201, 401, 403, 409 | API i E2E |
| 15 | `GET /api/rezervacije/moje` | 5 | 200, 401, 403 | straničenje, filter, sortiranje; API tok; E2E prazno stanje i filter statusa |
| 16 | `GET /api/rezervacije/{id}` | 5 | 200, 401, 403, 404 (i za tuđu) | rok i `mozeDaSeOtkaze` u odgovoru; API i E2E |
| 17 | `PUT /api/rezervacije/{id}/prisustvo` | 6 | 200, 401, 403, 409, 422 | + 1 unit (trka); API tok; E2E |
| 18 | `DELETE /api/rezervacije/{id}` | 9 | 204, 401, 403, 404, 409, 422 | granice roka; + 2 unit servisa i 4 unit politike roka; API i E2E |
| 19 | `GET /health` | 3 | 200, 503 | API i E2E |

Ostali komponentni testovi: ograničenja baze (6), demonstracioni podaci i reset (13), obrada grešaka (2),
OpenAPI dokument i njegova nedostupnost u Production-u (4), pokretanje bez JWT ključa (1).

Kodovi 401 i 403 za operacije 3–18 dolaze iz matrice `PravilaPristupaTestovi`: svaka zaštićena operacija bez tokena
vraća 401, a svaka operacija namenjena drugoj ulozi vraća 403 bez koda domenske greške (odbija je politika, a ne
servis). Zahtevi gađaju termin i rezervaciju koji postoje, i test posle odbijanja proverava da se u bazi ništa nije
promenilo. Test `Dokument_ZaSvakuOperaciju_DokumentujeTacnoStatusKodoveIzUgovora` dodatno proverava da OpenAPI
dokument za svaku operaciju navodi tačno dokumentovani skup kodova. Playwright test
`SvakaVrstaGreske_PrekoHttp_StizeKaoProblemDetailsSaStatusomIKodom` jednom, preko pravog servera, proverava da svaka
vrsta greške stiže kao `application/problem+json` sa poljima `status`, `type`, `traceId` i, gde postoji, `code`.

## 5. E2E tokovi

| Klasa | Tok |
|---|---|
| `AutentikacijaE2ETestovi` | pogrešna lozinka; registracija sa prekratkom lozinkom (poruka ispod polja) pa sa zauzetim email-om (poruka forme); povratak na traženu stranu posle prijave; član na trenerskoj adresi vidi „Nemate pristup"; adresa koju ruter ne prepoznaje; token koji server više ne prihvata vraća na prijavu sa porukom o istekloj sesiji; odjava |
| `RasporedE2ETestovi` | filter po treneru i slobodnim mestima uz sortiranje po nazivu (tačan skup i redosled); veličina strane i prelazak na sledeću stranu |
| `FilteriRasporedaE2ETestovi` | opseg datuma u poljima „Od“ i „Do“ pa poništavanje filtera; sortiranje po broju slobodnih mesta; prekinut poziv ka API-ju daje stanje greške, a „Pokušaj ponovo“ vraća listu |
| `RezervacijeE2ETestovi` | registracija → rezervacija → otkazivanje pre roka; nema otkazivanja posle roka; automatsko unapređenje sa liste čekanja (dva korisnika); nema druge rezervacije istog termina; prazno stanje „Moje rezervacije"; filter statusa u „Mojim rezervacijama"; stanje greške za nepostojeći termin; Escape zatvara dijalog potvrde, fokus se vraća na dugme, a rezervacija ostaje |
| `TreneriE2ETestovi` | prazna forma (poruke ispod polja i fokus na prvo polje) pa kreiranje; 409 pri brisanju i izmeni termina sa rezervacijom; otkazivanje termina kako ga vidi član; evidencija prisustva; naziv sa HTML-om prikazan kao tekst |
| `ResponsivniPrikazE2ETestovi` | raspored i polaznici na širini telefona bez horizontalnog skrola stranice |
| `ProfilE2ETestovi` | izmena imena i lozinke pa prijava novom lozinkom; prazno ime daje poruku ispod polja; brisanje naloga sa rezervacijom prikazuje razlog i nalog ostaje; brisanje naloga bez prijava vraća na prijavu i stari nalog više ne radi |
| `PristupacnostE2ETestovi` | prvi Tab otvara prečicu koja pomera fokus na glavni sadržaj; prijava popunjena i poslata samo tastaturom |
| `StatusSistemaE2ETestovi` | provera statusa sistema iz podnožja |

## 6. Pokrivenost koda

Pokrivenost se meri za `Teretana.Api` iz obe celine zajedno (`tests/pokrivenost.runsettings`; migracije i generisani
kod se ne računaju). Stanje pri poslednjem merenju:

| Metrika | Pokrivenost |
|---|---|
| Linije | 97,3 % |
| Grane | 89,6 % |
| Metode | 98,5 % |

Frontend (JavaScript) coverlet ne meri; njega pokrivaju E2E testovi.

## 7. Šta svesno nije pokriveno i zašto

| Deo | Zašto nije pokriven automatskim testom |
|---|---|
| `--reset-db` iz komandne linije (`PripremaBaze.ResetujIzKomandneLinijeAsync`, grana u `Program.cs`) | zahteva pokretanje procesa; logika reseta je pokrivena testom `ResetBaze_PosleIzmenaPodataka_VracaSamoPocetnePodatke`, a odbijanje van Development-a je ručno provereno (izlazni kod 1) |
| Pretvaranje relativne putanje SQLite fajla u putanju aplikacije (`BazaPodatakaRegistracija`) | testovi uvek koriste apsolutnu putanju privremene baze; relativna putanja se koristi pri svakom `dotnet run` |
| Podrazumevane grane `_ => throw` u `switch` izrazima za sortiranje i vrstu greške | nedostižne kroz API, jer validacija dozvoljava samo poznate vrednosti; postoje da bi nova vrednost bez obrade odmah pala |
| Nedostajući `sub` claim u tokenu i atribut `[Posle]` sa pogrešnim imenom svojstva | odbrana od greške u programiranju; važeći token uvek ima `sub`, a atribut se koristi samo sa postojećim svojstvima |
| Vizuelni izgled (boje, kontrast, raspored na svim širinama) | automatski se proverava samo odsustvo horizontalnog skrola na telefonu; izgled je proveren pregledom snimaka ekrana |
| Pristupačnost čitačem ekrana | automatski su pokriveni fokus na neispravno polje, `aria-invalid`, prečica „Preskoči na sadržaj“ i popunjavanje forme prijave tastaturom; najava promena čitačem ekrana nije automatski testirana |
| Opterećenje i performanse | van obima; ispravnost pri trci je dokazana testovima konkurentnosti, a propusnost SQLite-a je navedena kao ograničenje u `ARHITEKTURA.md` |
| Više browsera | E2E se izvršava u Chromium-u; Firefox i WebKit nisu deo seta da bi CI ostao brz |

## 8. Kako se čitaju izveštaji

| Izveštaj | Gde | Kako se čita |
|---|---|---|
| Rezultat `dotnet test` | konzola | red `Passed!` ili `Failed!` sa brojem testova; za pali test ispisuju se poruka asercije (očekivano / dobijeno) i stack trace |
| TRX rezultati | CI artifact `rezultati-testova` (`komponentni.trx`, `playwright.trx`) ili `--logger trx` lokalno | otvara se u Visual Studio-u ili bilo kom TRX pregledaču; sadrži trajanje i ishod svakog testa |
| Artefakti palog E2E testa | `tests/Teretana.PlaywrightTestovi/bin/<konfiguracija>/net10.0/playwright-artefakti/<ime testa>/`; u CI-u u artifact-u `rezultati-testova` | `screenshot.png` je stanje ekrana u trenutku pada, `video.webm` ceo test, a `trace.zip` se otvara komandom `pwsh tests/Teretana.PlaywrightTestovi/bin/Debug/net10.0/playwright.ps1 show-trace trace.zip` i prikazuje svaki korak sa DOM-om, mrežnim zahtevima i konzolom |
| Pokrivenost koda | CI: sažetak na stranici pokretanja i artifact `izvestaj-pokrivenosti`; lokalno `TestResults/pokrivenost/` | `index.html` prikazuje procenat po klasi, a klik na klasu otvara izvorni kod: zelene linije su izvršene, crvene nisu, žute imaju nepokrivenu granu; `Summary.txt` je kratak tekstualni pregled |

Artefakti E2E testa nastaju samo kada test padne; prolazni testovi ne ostavljaju ni snimke ni video.
