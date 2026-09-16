import { ApiGreska, pozovi } from './api.js';
import { dodajRutu, idiNa, pronadjiRutu, trenutnaAdresa } from './ruter.js';
import { azurirajKorisnika, naPromenuSesije, trenutniKorisnik, zavrsi } from './sesija.js';
import { poveziStatusSistema } from './status-sistema.js';
import { el, stanjeGreske } from './ui.js';
import { prikaziNematePristup, prikaziNepostojecuStranicu } from './ekrani/informacije.js';
import { prikaziPolaznike } from './ekrani/polaznici.js';
import { prikaziProfil } from './ekrani/profil.js';
import { prikaziPrijavu } from './ekrani/prijava.js';
import { prikaziRaspored } from './ekrani/raspored.js';
import { prikaziRegistraciju } from './ekrani/registracija.js';
import { prikaziDetaljRezervacije } from './ekrani/rezervacija-detalj.js';
import { prikaziMojeRezervacije } from './ekrani/rezervacije.js';
import { prikaziDetaljTermina } from './ekrani/termin-detalj.js';
import { prikaziFormuTermina } from './ekrani/termin-forma.js';

const TRENER = 'Trener';
const CLAN = 'Clan';

const sadrzaj = document.getElementById('sadrzaj');
const navigacija = document.getElementById('navigacija');
const profil = document.getElementById('profil');
const obavestenje = document.getElementById('obavestenje');

let brojPrikaza = 0;
let brojEkrana = 0;
let porukaZaSledeciPrikaz = null;

dodajRutu('/prijava', prikaziPrijavu, { samoAnonimno: true });
dodajRutu('/registracija', prikaziRegistraciju, { samoAnonimno: true });
dodajRutu('/termini', (kontejner, kontekst) => prikaziRaspored(kontejner, kontekst, { samoMoji: false }), { prijava: true });
dodajRutu('/termini/novi', prikaziFormuTermina, { uloga: TRENER });
dodajRutu('/termini/:id', prikaziDetaljTermina, { prijava: true });
dodajRutu('/termini/:id/izmena', prikaziFormuTermina, { uloga: TRENER });
dodajRutu('/termini/:id/polaznici', prikaziPolaznike, { uloga: TRENER });
dodajRutu('/moji-termini', (kontejner, kontekst) => prikaziRaspored(kontejner, kontekst, { samoMoji: true }), { uloga: TRENER });
dodajRutu('/rezervacije', prikaziMojeRezervacije, { uloga: CLAN });
dodajRutu('/rezervacije/:id', prikaziDetaljRezervacije, { uloga: CLAN });
dodajRutu('/profil', prikaziProfil, { prijava: true });

naPromenuSesije((razlog) => {
  if (razlog === 'odjava') {
    preusmeri('/prijava', null, { tekst: 'Odjavljeni ste.' });
  } else if (razlog === 'nalog-obrisan') {
    preusmeri('/prijava', null, { tekst: 'Nalog je obrisan.' });
  } else if (razlog === 'sesija-istekla') {
    preusmeri('/prijava', null, { tekst: 'Sesija je istekla. Prijavite se ponovo.', vrsta: 'upozorenje' });
  } else {
    osveziZaglavlje(trenutnaAdresa().putanja);
  }
});

poveziStatusSistema(document.getElementById('status-sistema-proveri'), document.getElementById('status-sistema-rezultat'));
document.getElementById('preskoci-na-sadrzaj').addEventListener('click', () => sadrzaj.focus());
window.addEventListener('hashchange', prikaziTrenutnuAdresu);
void prikaziTrenutnuAdresu();
osveziProfil();

async function prikaziTrenutnuAdresu() {
  const broj = ++brojPrikaza;
  const { putanja, upit } = trenutnaAdresa();
  const korisnik = trenutniKorisnik();
  const pogodak = pronadjiRutu(putanja);

  if (putanja === '/') {
    preusmeri(korisnik ? '/termini' : '/prijava', null, porukaZaSledeciPrikaz);
    return;
  }

  if (pogodak?.ruta.samoAnonimno && korisnik) {
    preusmeri('/termini', null, porukaZaSledeciPrikaz);
    return;
  }

  if ((pogodak?.ruta.prijava || pogodak?.ruta.uloga) && !korisnik) {
    preusmeri('/prijava', { povratak: putanja }, porukaZaSledeciPrikaz ?? { tekst: 'Prijavite se da biste nastavili.', vrsta: 'upozorenje' });
    return;
  }

  postaviObavestenje(porukaZaSledeciPrikaz);
  porukaZaSledeciPrikaz = null;
  osveziZaglavlje(putanja);

  const kontejner = el('div', { class: 'ekran' });
  sadrzaj.replaceChildren(kontejner);
  const kontekst = {
    parametri: pogodak?.parametri ?? {},
    upit,
    korisnik,
    idiNa: (cilj, ciljniUpit, poruka) => {
      if (broj === brojPrikaza) {
        preusmeri(cilj, ciljniUpit, poruka);
      }
    },
    obavesti: (poruka) => {
      if (broj === brojPrikaza) {
        postaviObavestenje(poruka);
      }
    },
  };
  const prikaz = pogodak === null ? prikaziNepostojecuStranicu
    : pogodak.ruta.uloga && pogodak.ruta.uloga !== korisnik.uloga ? prikaziNematePristup
      : pogodak.ruta.prikazi;

  const zavrsetak = prikaz(kontejner, kontekst);
  // Naslov ekrana nastaje pre prvog čekanja na API, pa fokus odmah prelazi na novi sadržaj (čitači ekrana ga najavljuju).
  // Prvi ekran je izuzetak: tada fokus ostaje na početku dokumenta, da prvi Tab dovede do prečice „Preskoči na sadržaj“.
  // Broje se samo iscrtani ekrani, jer preusmeravanja posle učitavanja strane troše brojPrikaza bez ijednog prikaza.
  if (++brojEkrana > 1) {
    kontejner.querySelector('h1')?.focus();
  }
  try {
    await zavrsetak;
  } catch (greska) {
    kontejner.append(stanjeGreske('ekran', greska));
  }
}

function preusmeri(putanja, upit, poruka) {
  porukaZaSledeciPrikaz = poruka ?? null;
  idiNa(putanja, upit);
}

function postaviObavestenje(poruka) {
  obavestenje.hidden = poruka === null;
  obavestenje.textContent = poruka?.tekst ?? '';
  obavestenje.dataset.vrsta = poruka?.vrsta ?? 'uspeh';
}

function osveziZaglavlje(putanja) {
  const korisnik = trenutniKorisnik();
  const stavke = korisnik === null
    ? [['Prijava', '/prijava', 'nav-prijava'], ['Registracija', '/registracija', 'nav-registracija']]
    : korisnik.uloga === TRENER
      ? [['Raspored', '/termini', 'nav-raspored'], ['Moji termini', '/moji-termini', 'nav-moji-termini'], ['Novi termin', '/termini/novi', 'nav-novi-termin'], ['Profil', '/profil', 'nav-profil']]
      : [['Raspored', '/termini', 'nav-raspored'], ['Moje rezervacije', '/rezervacije', 'nav-moje-rezervacije'], ['Profil', '/profil', 'nav-profil']];

  navigacija.replaceChildren(el('ul', { class: 'navigacija-lista' }, stavke.map(([tekst, cilj, testid]) =>
    el('li', {}, el('a', { href: `#${cilj}`, 'data-testid': testid, 'aria-current': putanja === cilj ? 'page' : null }, tekst)))));

  profil.replaceChildren(...(korisnik === null ? [] : [
    el('span', { 'data-testid': 'profil-ime' }, korisnik.imePrezime),
    el('span', { class: 'znacka', 'data-testid': 'profil-uloga', 'data-uloga': korisnik.uloga }, korisnik.uloga === TRENER ? 'Trener' : 'Član'),
    el('button', { type: 'button', class: 'dugme-sekundarno dugme-malo', 'data-testid': 'odjava', onclick: () => zavrsi('odjava') }, 'Odjava'),
  ]));
}

/** Proverava sačuvani token i osvežava profil (GET /api/auth/ja) pri otvaranju aplikacije. */
function osveziProfil() {
  if (trenutniKorisnik() === null) {
    return;
  }

  pozovi('/api/auth/ja').then(azurirajKorisnika, (greska) => {
    // Odgovor 401 je već završio sesiju i vratio korisnika na prijavu.
    if (!(greska instanceof ApiGreska) || greska.status !== 401) {
      postaviObavestenje({ tekst: `Profil nije osvežen: ${greska.message}`, vrsta: 'upozorenje' });
    }
  });
}
