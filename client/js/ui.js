import { ApiGreska } from './api.js';

const formatVremena = new Intl.DateTimeFormat('sr-Latn-RS', { dateStyle: 'medium', timeStyle: 'short' });
const formatSata = new Intl.DateTimeFormat('sr-Latn-RS', { timeStyle: 'short' });
const NAZIVI_STATUSA_REZERVACIJE = { Potvrdjena: 'Potvrđena', NaCekanju: 'Na čekanju', Otkazana: 'Otkazana' };

let brojacPolja = 0;

/**
 * Pravi element. Tekst se uvek dodaje kao tekstualni čvor, nikada kao HTML, pa podaci iz API-ja ne mogu da
 * ubace skriptu. Svojstva on* postaju slušaoci događaja, value se postavlja posle dece (zbog select-a),
 * a null, undefined i false se izostavljaju.
 */
export function el(tag, svojstva = {}, ...deca) {
  const element = document.createElement(tag);
  let vrednost;

  for (const [kljuc, podatak] of Object.entries(svojstva)) {
    if (podatak === undefined || podatak === null || podatak === false) {
      continue;
    }

    if (kljuc === 'value') {
      vrednost = podatak;
    } else if (kljuc.startsWith('on')) {
      element.addEventListener(kljuc.slice(2), podatak);
    } else {
      element.setAttribute(kljuc, podatak === true ? '' : String(podatak));
    }
  }

  element.append(...deca.flat(Infinity)
    .filter((dete) => dete !== null && dete !== undefined && dete !== false)
    .map((dete) => (dete instanceof Node ? dete : String(dete))));

  if (vrednost !== undefined) {
    element.value = vrednost;
  }

  return element;
}

export function postaviNaslov(naslov) {
  document.title = `${naslov} — Teretana`;
}

export function naslovEkrana(tekst, testid) {
  return el('h1', { tabindex: -1, 'data-testid': testid }, tekst);
}

export function link(tekst, putanja, testid, klasa) {
  return el('a', { href: `#${putanja}`, 'data-testid': testid, class: klasa }, tekst);
}

export function napomena(tekst, testid) {
  return el('p', { class: 'napomena', 'data-testid': testid }, tekst);
}

export function porukaGreske(greska) {
  if (typeof greska === 'string') {
    return greska;
  }

  if (greska instanceof ApiGreska) {
    return greska.message;
  }

  console.error(greska);
  return 'Došlo je do neočekivane greške.';
}

// Stanja ekrana

export function stanjeUcitavanja(testid, tekst = 'Učitavanje…') {
  return el('p', { class: 'stanje', role: 'status', 'data-testid': `${testid}-ucitavanje` }, tekst);
}

export function stanjeGreske(testid, greska, ponovo) {
  return el('div', { class: 'stanje stanje-greska', role: 'alert', 'data-testid': `${testid}-greska` },
    el('p', {}, porukaGreske(greska)),
    ponovo ? el('button', { type: 'button', class: 'dugme-sekundarno', 'data-testid': `${testid}-ponovo`, onclick: () => ponovo() }, 'Pokušaj ponovo') : null);
}

export function stanjePrazno(testid, tekst) {
  return el('p', { class: 'stanje', 'data-testid': `${testid}-prazno` }, tekst);
}

// Forme

export function polje({ naziv, oznaka, tip = 'text', testid, vrednost, pomoc, atributi = {} }) {
  const id = noviId(naziv);
  const opisi = [`${id}-greska`, pomoc ? `${id}-pomoc` : null].filter(Boolean).join(' ');
  const unos = tip === 'textarea'
    ? el('textarea', { id, name: naziv, 'data-testid': testid, 'aria-describedby': opisi, value: vrednost, ...atributi })
    : el('input', { type: tip, id, name: naziv, 'data-testid': testid, 'aria-describedby': opisi, value: vrednost, ...atributi });

  return el('div', { class: 'polje' },
    el('label', { for: id }, oznaka),
    pomoc ? el('p', { id: `${id}-pomoc`, class: 'pomoc' }, pomoc) : null,
    unos,
    greskaPolja(id, naziv, testid));
}

export function izbor({ naziv, oznaka, opcije, vrednost, testid }) {
  const id = noviId(naziv);
  return el('div', { class: 'polje' },
    el('label', { for: id }, oznaka),
    el('select', { id, name: naziv, 'data-testid': testid, 'aria-describedby': `${id}-greska`, value: vrednost },
      opcije.map(([vrednostOpcije, tekst]) => el('option', { value: vrednostOpcije }, tekst))),
    greskaPolja(id, naziv, testid));
}

export function potvrdnoPolje({ naziv, oznaka, cekirano, testid }) {
  const id = noviId(naziv);
  return el('div', { class: 'polje polje-potvrda' },
    el('input', { type: 'checkbox', id, name: naziv, 'data-testid': testid, checked: cekirano }),
    el('label', { for: id }, oznaka));
}

export function greskaForme(testid) {
  return el('p', { class: 'greska-forme', role: 'alert', 'data-testid': testid, hidden: true });
}

export function prikaziPoruku(element, tekst) {
  element.textContent = tekst;
  element.hidden = false;
}

export function sakrij(element) {
  element.textContent = '';
  element.hidden = true;
}

export function ocistiGreskeForme(forma, globalnaGreska) {
  for (const unos of forma.querySelectorAll('[aria-invalid]')) {
    unos.removeAttribute('aria-invalid');
  }

  for (const poruka of forma.querySelectorAll('.greska-polja')) {
    sakrij(poruka);
  }

  sakrij(globalnaGreska);
}

/** Greške po polju iz ProblemDetails.errors idu ispod odgovarajućeg polja, ostale u globalnu poruku forme. */
export function prikaziGreskeForme(forma, greska, globalnaGreska) {
  ocistiGreskeForme(forma, globalnaGreska);
  const neraspodeljene = [];

  for (const [naziv, poruke] of Object.entries(greska instanceof ApiGreska ? greska.greskePolja : {})) {
    const unos = forma.elements.namedItem(naziv);
    const poruka = forma.querySelector(`[data-greska-za="${CSS.escape(naziv)}"]`);
    if (unos === null || poruka === null) {
      neraspodeljene.push(...poruke);
      continue;
    }

    unos.setAttribute('aria-invalid', 'true');
    // Server vraća sva prekršena pravila (npr. za prazan naziv i „obavezan“ i „najmanje 2 znaka“); prvo je najkorisnije.
    prikaziPoruku(poruka, poruke[0]);
  }

  // Fokus ide na prvo neispravno polje po redosledu u formi, a ne po redosledu grešaka u odgovoru servera.
  const prvoNeispravno = forma.querySelector('[aria-invalid="true"]');
  prikaziPoruku(globalnaGreska, neraspodeljene.length > 0
    ? neraspodeljene.join(' ')
    : prvoNeispravno !== null ? 'Proverite označena polja.' : porukaGreske(greska));
  prvoNeispravno?.focus();
}

/** Onemogućava dugme dok akcija traje, da isti zahtev ne bi bio poslat dva puta. */
export async function saCekanjem(dugme, tekstCekanja, akcija) {
  const tekst = dugme.textContent;
  dugme.disabled = true;
  dugme.setAttribute('aria-busy', 'true');
  dugme.textContent = tekstCekanja;
  try {
    await akcija();
  } finally {
    dugme.disabled = false;
    dugme.removeAttribute('aria-busy');
    dugme.textContent = tekst;
  }
}

export function potvrdi({ naslov, poruka, potvrdiTekst }) {
  return new Promise((razresi) => {
    const prethodniFokus = document.activeElement;
    const dijalog = el('dialog', { class: 'dijalog', 'aria-labelledby': 'potvrda-naslov', 'data-testid': 'potvrda-dijalog' },
      el('form', { method: 'dialog' },
        el('h2', { id: 'potvrda-naslov' }, naslov),
        el('p', {}, poruka),
        el('div', { class: 'akcije' },
          el('button', { value: 'ne', class: 'dugme-sekundarno', 'data-testid': 'potvrda-ne' }, 'Nazad'),
          el('button', { value: 'da', class: 'dugme-opasno', 'data-testid': 'potvrda-da' }, potvrdiTekst))));

    dijalog.addEventListener('close', () => {
      dijalog.remove();
      prethodniFokus?.focus?.();
      razresi(dijalog.returnValue === 'da');
    });
    document.body.append(dijalog);
    dijalog.showModal();
  });
}

// Prikaz podataka

export function listaPodataka(redovi) {
  return el('dl', { class: 'podaci' }, redovi.filter(Boolean).map(([oznaka, vrednost, testid]) => [
    el('dt', {}, oznaka),
    el('dd', { 'data-testid': testid }, vrednost),
  ]));
}

export function tabela(zaglavlja, redovi, testid) {
  return el('div', { class: 'tabela-omotac' },
    el('table', { 'data-testid': testid },
      el('thead', {}, el('tr', {}, zaglavlja.map((zaglavlje) => el('th', { scope: 'col' }, zaglavlje)))),
      el('tbody', {}, redovi)));
}

export function stranicenje({ stranica, ukupnoStranica, ukupnoStavki, testid, naStranicu }) {
  return el('nav', { class: 'stranicenje', 'aria-label': 'Stranice' },
    el('button', { type: 'button', class: 'dugme-sekundarno', 'data-testid': `${testid}-prethodna`, disabled: stranica <= 1, onclick: () => naStranicu(stranica - 1) }, 'Prethodna'),
    el('span', { 'data-testid': `${testid}-info` }, `Strana ${stranica} od ${Math.max(ukupnoStranica, 1)} (ukupno ${ukupnoStavki})`),
    el('button', { type: 'button', class: 'dugme-sekundarno', 'data-testid': `${testid}-sledeca`, disabled: stranica >= ukupnoStranica, onclick: () => naStranicu(stranica + 1) }, 'Sledeća'));
}

export function formatirajVreme(iso) {
  return formatVremena.format(new Date(iso));
}

export function vremeTermina(pocetak, kraj) {
  return el('time', { datetime: pocetak }, `${formatirajVreme(pocetak)} – ${formatSata.format(new Date(kraj))}`);
}

/** Pretvara UTC vreme iz API-ja u vrednost za input type="datetime-local" u lokalnoj zoni. */
export function uLokalniUnos(iso) {
  const vreme = new Date(iso);
  const dvocifreno = (broj) => String(broj).padStart(2, '0');
  return `${vreme.getFullYear()}-${dvocifreno(vreme.getMonth() + 1)}-${dvocifreno(vreme.getDate())}`
    + `T${dvocifreno(vreme.getHours())}:${dvocifreno(vreme.getMinutes())}`;
}

export function izLokalnogUnosa(vrednost) {
  return vrednost ? new Date(vrednost).toISOString() : null;
}

/** Ponoć zadatog lokalnog datuma (uz pomak u danima) kao ISO vreme za filtere. */
export function pocetakDana(datum, pomakDana = 0) {
  if (!datum) {
    return null;
  }

  const vreme = new Date(`${datum}T00:00`);
  vreme.setDate(vreme.getDate() + pomakDana);
  return vreme.toISOString();
}

export function znackaTermina(termin) {
  const sada = Date.now();
  const [stanje, tekst] = termin.status === 'Otkazan' ? ['otkazan', 'Otkazan']
    : new Date(termin.kraj).getTime() <= sada ? ['zavrsen', 'Završen']
      : new Date(termin.pocetak).getTime() <= sada ? ['u-toku', 'U toku']
        : termin.slobodnaMesta === 0 ? ['popunjen', 'Popunjen']
          : ['slobodan', `Slobodnih mesta: ${termin.slobodnaMesta}`];

  return el('span', { class: `znacka znacka-${stanje}`, 'data-testid': 'termin-stanje', 'data-stanje': stanje }, tekst);
}

export function znackaRezervacije(rezervacija) {
  const tekst = rezervacija.status === 'NaCekanju'
    ? `Na čekanju, pozicija ${rezervacija.pozicijaNaCekanju}`
    : NAZIVI_STATUSA_REZERVACIJE[rezervacija.status];

  return el('span', { class: `znacka znacka-rezervacija-${rezervacija.status}`, 'data-testid': 'rezervacija-status', 'data-status': rezervacija.status }, tekst);
}

function noviId(naziv) {
  brojacPolja += 1;
  return `polje-${naziv}-${brojacPolja}`;
}

function greskaPolja(id, naziv, testid) {
  return el('p', { id: `${id}-greska`, class: 'greska-polja', 'data-greska-za': naziv, 'data-testid': `${testid}-greska`, hidden: true });
}
