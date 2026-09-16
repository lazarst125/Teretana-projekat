import { pozovi } from '../api.js';
import {
  el, greskaForme, izbor, link, naslovEkrana, ocistiGreskeForme, pocetakDana, polje, porukaGreske, postaviNaslov,
  potvrdnoPolje, prikaziGreskeForme, stanjeGreske, stanjePrazno, stanjeUcitavanja, stranicenje, vremeTermina, znackaTermina,
} from '../ui.js';

const SORTIRANJA = [
  ['pocetak', 'Početak, najraniji prvo'],
  ['-pocetak', 'Početak, najkasniji prvo'],
  ['naziv', 'Naziv, A–Ž'],
  ['-naziv', 'Naziv, Ž–A'],
  ['-slobodnaMesta', 'Najviše slobodnih mesta'],
  ['slobodnaMesta', 'Najmanje slobodnih mesta'],
];
const STATUSI = [['', 'Svi'], ['Aktivan', 'Aktivni'], ['Otkazan', 'Otkazani']];
const VELICINE_STRANICE = [['10', '10'], ['20', '20'], ['50', '50'], ['100', '100']];

/** Raspored svih termina, ili termina prijavljenog trenera kada je samoMoji. Filteri se čuvaju u adresi. */
export async function prikaziRaspored(kontejner, kontekst, { samoMoji }) {
  const naslov = samoMoji ? 'Moji termini' : 'Raspored termina';
  const putanja = samoMoji ? '/moji-termini' : '/termini';
  const upit = kontekst.upit;
  const filteri = {
    od: upit.get('od') ?? '',
    do: upit.get('do') ?? '',
    trenerId: upit.get('trenerId') ?? '',
    status: upit.get('status') ?? '',
    sortiranje: upit.get('sortiranje') ?? 'pocetak',
    velicinaStranice: upit.get('velicinaStranice') ?? '20',
    samoSlobodni: upit.get('samoSlobodni') === 'true',
    stranica: Math.max(1, Number.parseInt(upit.get('stranica') ?? '1', 10) || 1),
  };
  postaviNaslov(naslov);

  const izborTrenera = samoMoji
    ? null
    : izbor({ naziv: 'trenerId', oznaka: 'Trener', testid: 'filter-trener', vrednost: filteri.trenerId, opcije: [['', 'Svi treneri']] });
  const greskaFiltera = greskaForme('filteri-greska');
  const forma = el('form', { class: 'filteri', role: 'search', 'aria-label': 'Filteri termina', novalidate: true, onsubmit: primeni },
    polje({ naziv: 'od', oznaka: 'Od datuma', tip: 'date', testid: 'filter-od', vrednost: filteri.od }),
    polje({ naziv: 'do', oznaka: 'Do datuma', tip: 'date', testid: 'filter-do', vrednost: filteri.do }),
    izborTrenera,
    izbor({ naziv: 'status', oznaka: 'Status', testid: 'filter-status', vrednost: filteri.status, opcije: STATUSI }),
    izbor({ naziv: 'sortiranje', oznaka: 'Sortiranje', testid: 'filter-sortiranje', vrednost: filteri.sortiranje, opcije: SORTIRANJA }),
    izbor({ naziv: 'velicinaStranice', oznaka: 'Termina po strani', testid: 'filter-velicina-stranice', vrednost: filteri.velicinaStranice, opcije: VELICINE_STRANICE }),
    potvrdnoPolje({ naziv: 'samoSlobodni', oznaka: 'Samo termini sa slobodnim mestima', testid: 'filter-samo-slobodni', cekirano: filteri.samoSlobodni }),
    greskaFiltera,
    el('div', { class: 'akcije' },
      el('button', { type: 'submit', 'data-testid': 'filter-primeni' }, 'Primeni'),
      el('button', { type: 'button', class: 'dugme-sekundarno', 'data-testid': 'filter-ponisti', onclick: () => kontekst.idiNa(putanja) }, 'Poništi filtere')));
  const rezultat = el('section', { 'aria-label': 'Termini', 'data-testid': 'termini-rezultat' });

  kontejner.append(
    el('div', { class: 'naslov-ekrana' },
      naslovEkrana(naslov),
      kontekst.korisnik.uloga === 'Trener' ? link('Novi termin', '/termini/novi', 'novi-termin', 'dugme-link') : null),
    forma,
    rezultat);

  if (izborTrenera !== null) {
    void popuniTrenere(izborTrenera, filteri.trenerId);
  }

  await ucitaj();

  function primeni(dogadjaj) {
    dogadjaj.preventDefault();
    const podaci = new FormData(forma);
    kontekst.idiNa(putanja, {
      od: podaci.get('od'),
      do: podaci.get('do'),
      trenerId: podaci.get('trenerId'),
      status: podaci.get('status'),
      sortiranje: podaci.get('sortiranje'),
      velicinaStranice: podaci.get('velicinaStranice'),
      samoSlobodni: podaci.has('samoSlobodni'),
    });
  }

  async function ucitaj() {
    ocistiGreskeForme(forma, greskaFiltera);
    rezultat.replaceChildren(stanjeUcitavanja('termini'));
    try {
      const stranica = await pozovi('/api/termini', {
        parametri: {
          stranica: filteri.stranica,
          velicinaStranice: filteri.velicinaStranice,
          od: pocetakDana(filteri.od),
          do: pocetakDana(filteri.do, 1),
          trenerId: samoMoji ? kontekst.korisnik.id : filteri.trenerId,
          status: filteri.status,
          sortiranje: filteri.sortiranje,
          samoSlobodni: filteri.samoSlobodni || null,
        },
      });
      rezultat.replaceChildren(...prikazStranice(stranica));
    } catch (greska) {
      if (greska.status === 400) {
        prikaziGreskeForme(forma, greska, greskaFiltera);
      }

      rezultat.replaceChildren(stanjeGreske('termini', greska, ucitaj));
    }
  }

  function prikazStranice(stranica) {
    if (stranica.stavke.length === 0) {
      const tekst = stranica.ukupnoStavki === 0
        ? (samoMoji ? 'Još nemate termina koji odgovaraju filterima.' : 'Nema termina koji odgovaraju filterima.')
        : 'Na ovoj strani nema termina.';
      return [stanjePrazno('termini', tekst)];
    }

    return [
      el('ul', { class: 'kartice', 'data-testid': 'termini-lista' }, stranica.stavke.map(karticaTermina)),
      stranicenje({
        ...stranica,
        testid: 'termini-stranicenje',
        naStranicu: (broj) => kontekst.idiNa(putanja, { ...Object.fromEntries(upit), stranica: broj }),
      }),
    ];
  }
}

function karticaTermina(termin) {
  return el('li', { class: 'kartica', 'data-testid': 'termin-kartica', 'data-termin-id': termin.id },
    el('h2', { class: 'kartica-naslov' }, link(termin.naziv, `/termini/${termin.id}`, 'termin-detalji')),
    el('p', {}, vremeTermina(termin.pocetak, termin.kraj)),
    el('p', {}, `Trener: ${termin.trener.imePrezime}`),
    el('p', { 'data-testid': 'termin-zauzetost' },
      `Zauzeto ${termin.brojPotvrdjenih} od ${termin.kapacitet}`,
      termin.brojNaCekanju > 0 ? `, na čekanju ${termin.brojNaCekanju}` : ''),
    el('p', {}, znackaTermina(termin)));
}

/** Spisak trenera se sastavlja iz termina, jer API nema posebnu listu trenera. */
async function popuniTrenere(poljeTrenera, izabraniTrener) {
  const select = poljeTrenera.querySelector('select');
  try {
    const stranica = await pozovi('/api/termini', { parametri: { velicinaStranice: 100, sortiranje: '-pocetak' } });
    const treneri = new Map(stranica.stavke.map((termin) => [String(termin.trener.id), termin.trener.imePrezime]));
    if (izabraniTrener && !treneri.has(izabraniTrener)) {
      treneri.set(izabraniTrener, `Trener #${izabraniTrener}`);
    }

    select.append(...[...treneri]
      .sort(([, prvi], [, drugi]) => prvi.localeCompare(drugi, 'sr'))
      .map(([id, imePrezime]) => el('option', { value: id }, imePrezime)));
    select.value = izabraniTrener;
  } catch (greska) {
    poljeTrenera.append(el('p', { class: 'pomoc', 'data-testid': 'filter-trener-nedostupan' }, `Spisak trenera nije učitan: ${porukaGreske(greska)}`));
  }
}
