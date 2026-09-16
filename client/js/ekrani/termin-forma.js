import { pozovi } from '../api.js';
import {
  el, greskaForme, izLokalnogUnosa, link, naslovEkrana, napomena, ocistiGreskeForme, polje, postaviNaslov,
  prikaziGreskeForme, saCekanjem, stanjeGreske, stanjeUcitavanja, uLokalniUnos,
} from '../ui.js';

/** Forma za novi termin, ili za izmenu postojećeg kada adresa sadrži id. */
export async function prikaziFormuTermina(kontejner, kontekst) {
  const idTermina = kontekst.parametri.id;
  const izmena = idTermina !== undefined;
  const naslov = izmena ? 'Izmena termina' : 'Novi termin';
  postaviNaslov(naslov);
  const telo = el('div');
  kontejner.append(
    el('p', { class: 'nazad' }, izmena
      ? link('← Nazad na termin', `/termini/${idTermina}`, 'nazad-na-termin')
      : link('← Moji termini', '/moji-termini', 'nazad-na-moje-termine')),
    naslovEkrana(naslov),
    telo);

  if (!izmena) {
    telo.replaceChildren(napraviFormu(null));
    return;
  }

  await ucitaj();

  async function ucitaj() {
    telo.replaceChildren(stanjeUcitavanja('termin-forma'));
    try {
      const termin = await pozovi(`/api/termini/${idTermina}`);
      telo.replaceChildren(termin.trener.id === kontekst.korisnik.id
        ? napraviFormu(termin)
        : stanjeGreske('termin-forma', 'Možete da menjate samo svoje termine.'));
    } catch (greska) {
      telo.replaceChildren(stanjeGreske('termin-forma', greska, ucitaj));
    }
  }

  function napraviFormu(termin) {
    const greska = greskaForme('termin-forma-greska');
    const sacuvaj = el('button', { type: 'submit', 'data-testid': 'termin-forma-sacuvaj' }, izmena ? 'Sačuvaj izmene' : 'Kreiraj termin');
    const forma = el('form', { class: 'forma', novalidate: true, onsubmit: posalji },
      izmena ? napomena('Termin koji ima aktivne prijave ne može da se menja; takav termin možete otkazati.') : null,
      polje({ naziv: 'naziv', oznaka: 'Naziv', testid: 'termin-forma-naziv', vrednost: termin?.naziv, atributi: { maxlength: 100 } }),
      polje({ naziv: 'opis', oznaka: 'Opis (nije obavezan)', tip: 'textarea', testid: 'termin-forma-opis', vrednost: termin?.opis ?? '', atributi: { maxlength: 500, rows: 3 } }),
      polje({ naziv: 'pocetak', oznaka: 'Početak', tip: 'datetime-local', testid: 'termin-forma-pocetak', vrednost: termin ? uLokalniUnos(termin.pocetak) : '' }),
      polje({ naziv: 'kraj', oznaka: 'Kraj', tip: 'datetime-local', testid: 'termin-forma-kraj', vrednost: termin ? uLokalniUnos(termin.kraj) : '' }),
      polje({ naziv: 'kapacitet', oznaka: 'Kapacitet', tip: 'number', testid: 'termin-forma-kapacitet', vrednost: termin?.kapacitet ?? '', pomoc: 'Broj mesta, od 1 do 100.', atributi: { min: 1, max: 100, step: 1 } }),
      greska,
      el('div', { class: 'akcije' },
        sacuvaj,
        link('Odustani', izmena ? `/termini/${idTermina}` : '/moji-termini', 'termin-forma-odustani', 'dugme-link dugme-sekundarno')));
    return forma;

    async function posalji(dogadjaj) {
      dogadjaj.preventDefault();
      ocistiGreskeForme(forma, greska);
      const podaci = new FormData(forma);
      const zahtev = {
        naziv: podaci.get('naziv'),
        opis: podaci.get('opis'),
        pocetak: izLokalnogUnosa(podaci.get('pocetak')),
        kraj: izLokalnogUnosa(podaci.get('kraj')),
        // Prazan ili neispravan unos šalje 0, pa server vraća istu poruku kao za kapacitet van opsega.
        kapacitet: Number.parseInt(podaci.get('kapacitet'), 10) || 0,
      };

      await saCekanjem(sacuvaj, 'Čuvanje…', async () => {
        try {
          const sacuvan = await pozovi(izmena ? `/api/termini/${idTermina}` : '/api/termini', { metod: izmena ? 'PUT' : 'POST', telo: zahtev });
          kontekst.idiNa(`/termini/${sacuvan.id}`, null, { tekst: izmena ? 'Izmene su sačuvane.' : 'Termin je kreiran.' });
        } catch (greskaZahteva) {
          prikaziGreskeForme(forma, greskaZahteva, greska);
        }
      });
    }
  }
}
