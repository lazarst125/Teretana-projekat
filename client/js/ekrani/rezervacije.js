import { pozovi } from '../api.js';
import {
  el, izbor, link, naslovEkrana, napomena, postaviNaslov, stanjeGreske, stanjePrazno, stanjeUcitavanja, stranicenje, vremeTermina,
  znackaRezervacije,
} from '../ui.js';
import { oblastOtkazivanja } from './otkazivanje-rezervacije.js';

const STATUSI = [['', 'Sve'], ['Potvrdjena', 'Potvrđene'], ['NaCekanju', 'Na čekanju'], ['Otkazana', 'Otkazane']];
const SORTIRANJA = [['pocetak', 'Termin, najraniji prvo'], ['-pocetak', 'Termin, najkasniji prvo']];
const VELICINE_STRANICE = [['10', '10'], ['20', '20'], ['50', '50'], ['100', '100']];
const PUTANJA = '/rezervacije';

export async function prikaziMojeRezervacije(kontejner, kontekst) {
  postaviNaslov('Moje rezervacije');
  const upit = kontekst.upit;
  const filteri = {
    status: upit.get('status') ?? '',
    sortiranje: upit.get('sortiranje') ?? 'pocetak',
    velicinaStranice: upit.get('velicinaStranice') ?? '20',
    stranica: Math.max(1, Number.parseInt(upit.get('stranica') ?? '1', 10) || 1),
  };

  const forma = el('form', { class: 'filteri', role: 'search', 'aria-label': 'Filteri rezervacija', novalidate: true, onsubmit: primeni },
    izbor({ naziv: 'status', oznaka: 'Status', testid: 'rezervacije-filter-status', vrednost: filteri.status, opcije: STATUSI }),
    izbor({ naziv: 'sortiranje', oznaka: 'Sortiranje', testid: 'rezervacije-filter-sortiranje', vrednost: filteri.sortiranje, opcije: SORTIRANJA }),
    izbor({ naziv: 'velicinaStranice', oznaka: 'Rezervacija po strani', testid: 'rezervacije-filter-velicina-stranice', vrednost: filteri.velicinaStranice, opcije: VELICINE_STRANICE }),
    el('div', { class: 'akcije' }, el('button', { type: 'submit', 'data-testid': 'rezervacije-filter-primeni' }, 'Primeni')));
  const rezultat = el('section', { 'aria-label': 'Rezervacije', 'data-testid': 'rezervacije-rezultat' });

  kontejner.append(
    el('div', { class: 'naslov-ekrana' }, naslovEkrana('Moje rezervacije'), link('Rezerviši novi termin', '/termini', 'rezervacije-na-raspored', 'dugme-link')),
    forma,
    rezultat);

  await ucitaj();

  function primeni(dogadjaj) {
    dogadjaj.preventDefault();
    const podaci = new FormData(forma);
    kontekst.idiNa(PUTANJA, { status: podaci.get('status'), sortiranje: podaci.get('sortiranje'), velicinaStranice: podaci.get('velicinaStranice') });
  }

  async function ucitaj() {
    rezultat.replaceChildren(stanjeUcitavanja('rezervacije'));
    try {
      const stranica = await pozovi('/api/rezervacije/moje', { parametri: filteri });
      rezultat.replaceChildren(...prikazStranice(stranica));
    } catch (greska) {
      rezultat.replaceChildren(stanjeGreske('rezervacije', greska, ucitaj));
    }
  }

  function prikazStranice(stranica) {
    if (stranica.stavke.length === 0) {
      return [stanjePrazno('rezervacije', stranica.ukupnoStavki === 0 ? 'Nemate rezervacija koje odgovaraju filterima.' : 'Na ovoj strani nema rezervacija.')];
    }

    return [
      el('ul', { class: 'kartice', 'data-testid': 'rezervacije-lista' }, stranica.stavke.map(karticaRezervacije)),
      stranicenje({
        ...stranica,
        testid: 'rezervacije-stranicenje',
        naStranicu: (broj) => kontekst.idiNa(PUTANJA, { ...Object.fromEntries(upit), stranica: broj }),
      }),
    ];
  }

  function karticaRezervacije(rezervacija) {
    return el('li', { class: 'kartica', 'data-testid': 'rezervacija-kartica', 'data-rezervacija-id': rezervacija.id },
      el('h2', { class: 'kartica-naslov' }, link(rezervacija.termin.naziv, `/rezervacije/${rezervacija.id}`, 'rezervacija-detalji')),
      el('p', {}, vremeTermina(rezervacija.termin.pocetak, rezervacija.termin.kraj)),
      el('p', {}, `Trener: ${rezervacija.termin.trener.imePrezime}`),
      el('p', {}, znackaRezervacije(rezervacija)),
      rezervacija.termin.status === 'Otkazan' ? napomena('Trener je otkazao ovaj termin.', 'rezervacija-termin-otkazan') : null,
      oblastOtkazivanja(rezervacija, {
        naUspeh: async (poruka) => {
          await ucitaj();
          kontekst.obavesti({ tekst: poruka });
        },
      }));
  }
}
