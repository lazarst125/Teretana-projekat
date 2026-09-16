import { pozovi } from '../api.js';
import {
  el, formatirajVreme, link, listaPodataka, naslovEkrana, napomena, postaviNaslov, stanjeGreske, stanjeUcitavanja, vremeTermina,
  znackaRezervacije,
} from '../ui.js';
import { oblastOtkazivanja } from './otkazivanje-rezervacije.js';

export async function prikaziDetaljRezervacije(kontejner, kontekst) {
  const idRezervacije = kontekst.parametri.id;
  postaviNaslov('Rezervacija');
  const naslov = naslovEkrana('Rezervacija', 'rezervacija-naslov');
  const telo = el('div', { 'data-testid': 'rezervacija-detalj' });
  kontejner.append(el('p', { class: 'nazad' }, link('← Moje rezervacije', '/rezervacije', 'nazad-na-rezervacije')), naslov, telo);

  await ucitaj();

  async function ucitaj() {
    telo.replaceChildren(stanjeUcitavanja('rezervacija'));
    let rezervacija;
    try {
      rezervacija = await pozovi(`/api/rezervacije/${idRezervacije}`);
    } catch (greska) {
      telo.replaceChildren(stanjeGreske('rezervacija', greska, ucitaj));
      return;
    }

    naslov.textContent = `Rezervacija: ${rezervacija.termin.naziv}`;
    postaviNaslov(naslov.textContent);
    const delovi = [
      listaPodataka([
        ['Termin', link(rezervacija.termin.naziv, `/termini/${rezervacija.termin.id}`, 'rezervacija-termin-link')],
        ['Vreme', vremeTermina(rezervacija.termin.pocetak, rezervacija.termin.kraj)],
        ['Trener', rezervacija.termin.trener.imePrezime],
        ['Status', znackaRezervacije(rezervacija)],
        ['Prijavljena', formatirajVreme(rezervacija.kreiranaAt)],
        rezervacija.potvrdjenaAt ? ['Potvrđena', formatirajVreme(rezervacija.potvrdjenaAt)] : null,
        rezervacija.otkazanaAt ? ['Otkazana', formatirajVreme(rezervacija.otkazanaAt)] : null,
        rezervacija.status === 'Potvrdjena' ? ['Rok za otkazivanje', formatirajVreme(rezervacija.rokZaOtkazivanje), 'rezervacija-rok-za-otkazivanje'] : null,
        rezervacija.prisustvovao === null ? null : ['Prisustvo', rezervacija.prisustvovao ? 'Došli ste' : 'Niste došli', 'rezervacija-prisustvo'],
      ]),
      rezervacija.termin.status === 'Otkazan' ? napomena('Trener je otkazao ovaj termin.', 'rezervacija-termin-otkazan') : null,
      oblastOtkazivanja(rezervacija, {
        naUspeh: async (poruka) => {
          await ucitaj();
          kontekst.obavesti({ tekst: poruka });
        },
      }),
    ];
    // replaceChildren bi null prikazao kao tekst „null“, pa se izostavljeni delovi uklanjaju.
    telo.replaceChildren(...delovi.filter((deo) => deo !== null));
  }
}
