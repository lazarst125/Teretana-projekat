import { pozovi } from '../api.js';
import { el, formatirajVreme, greskaForme, napomena, porukaGreske, potvrdi, prikaziPoruku, saCekanjem, sakrij } from '../ui.js';

/**
 * Dugme za otkazivanje rezervacije ili odustajanje od čekanja, zajedničko za listu i detalj rezervacije.
 * Da li je otkazivanje moguće odlučuje server (polje mozeDaSeOtkaze); ovde se samo objašnjava zašto nije.
 */
export function oblastOtkazivanja(rezervacija, { naUspeh }) {
  if (rezervacija.status === 'Otkazana') {
    return null;
  }

  if (!rezervacija.mozeDaSeOtkaze) {
    const razlog = new Date(rezervacija.termin.pocetak) <= new Date()
      ? 'Termin je već počeo, pa otkazivanje više nije moguće.'
      : `Rok za otkazivanje je istekao ${formatirajVreme(rezervacija.rokZaOtkazivanje)}.`;
    return napomena(razlog, 'rezervacija-otkazivanje-nedostupno');
  }

  const naCekanju = rezervacija.status === 'NaCekanju';
  const greska = greskaForme('rezervacija-otkazivanje-greska');
  const dugme = el('button', { type: 'button', class: 'dugme-opasno', 'data-testid': 'rezervacija-otkazi', onclick: otkazi },
    naCekanju ? 'Odustani od čekanja' : 'Otkaži rezervaciju');

  return el('div', { class: 'akcije' },
    naCekanju ? null : el('p', { class: 'napomena', 'data-testid': 'rezervacija-rok' }, `Otkazivanje je moguće do ${formatirajVreme(rezervacija.rokZaOtkazivanje)}.`),
    dugme,
    greska);

  async function otkazi() {
    const potvrdjeno = await potvrdi(naCekanju
      ? { naslov: 'Odustati od čekanja?', poruka: `Izgubićete mesto na listi čekanja za termin „${rezervacija.termin.naziv}“.`, potvrdiTekst: 'Odustani od čekanja' }
      : { naslov: 'Otkazati rezervaciju?', poruka: `Vaše mesto na terminu „${rezervacija.termin.naziv}“ dobiće prvi član sa liste čekanja.`, potvrdiTekst: 'Otkaži rezervaciju' });
    if (!potvrdjeno) {
      return;
    }

    sakrij(greska);
    let otkazano = false;
    await saCekanjem(dugme, 'Otkazivanje…', async () => {
      try {
        await pozovi(`/api/rezervacije/${rezervacija.id}`, { metod: 'DELETE' });
        otkazano = true;
      } catch (greskaZahteva) {
        prikaziPoruku(greska, porukaGreske(greskaZahteva));
      }
    });

    if (otkazano) {
      await naUspeh(naCekanju ? 'Odustali ste od čekanja.' : 'Rezervacija je otkazana.');
    }
  }
}
