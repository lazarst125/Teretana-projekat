import { pozovi } from '../api.js';
import {
  el, greskaForme, link, listaPodataka, naslovEkrana, napomena, porukaGreske, postaviNaslov, potvrdi, prikaziPoruku,
  saCekanjem, sakrij, stanjeGreske, stanjeUcitavanja, vremeTermina, znackaTermina,
} from '../ui.js';

// Greške koje znače da je prikazani termin zastareo (neko je u međuvremenu zauzeo ili oslobodio mesto),
// pa se detalj ponovo učitava da bi ponuđena akcija odgovarala stvarnom stanju.
const KODOVI_ZASTARELOG_PRIKAZA = new Set(['termin-popunjen', 'termin-ima-slobodnih-mesta', 'termin-otkazan', 'termin-je-poceo', 'vec-prijavljen']);

export async function prikaziDetaljTermina(kontejner, kontekst) {
  const idTermina = kontekst.parametri.id;
  const naslov = naslovEkrana('Termin', 'termin-naziv');
  const telo = el('div', { 'data-testid': 'termin-detalj' });
  postaviNaslov('Termin');
  kontejner.append(
    el('p', { class: 'nazad' }, kontekst.korisnik.uloga === 'Trener'
      ? link('← Moji termini', '/moji-termini', 'nazad-na-moje-termine')
      : link('← Raspored termina', '/termini', 'nazad-na-raspored')),
    naslov,
    telo);

  await ucitaj();

  async function ucitaj(prethodnaGreska) {
    telo.replaceChildren(stanjeUcitavanja('termin'));
    let termin;
    try {
      termin = await pozovi(`/api/termini/${idTermina}`);
    } catch (greska) {
      telo.replaceChildren(stanjeGreske('termin', greska, ucitaj));
      return;
    }

    naslov.textContent = termin.naziv;
    postaviNaslov(termin.naziv);
    const greskaAkcije = greskaForme('termin-akcija-greska');
    if (prethodnaGreska !== undefined) {
      prikaziPoruku(greskaAkcije, porukaGreske(prethodnaGreska));
    }

    telo.replaceChildren(
      listaPodataka([
        ['Vreme', vremeTermina(termin.pocetak, termin.kraj)],
        ['Trener', termin.trener.imePrezime, 'termin-trener'],
        ['Opis', termin.opis ?? '—', 'termin-opis'],
        ['Kapacitet', termin.kapacitet, 'termin-kapacitet'],
        ['Potvrđenih rezervacija', termin.brojPotvrdjenih, 'termin-broj-potvrdjenih'],
        ['Slobodnih mesta', termin.slobodnaMesta, 'termin-slobodna-mesta'],
        ['Na listi čekanja', termin.brojNaCekanju, 'termin-broj-na-cekanju'],
        ['Stanje', znackaTermina(termin)],
      ]),
      greskaAkcije,
      kontekst.korisnik.uloga === 'Trener' ? akcijeTrenera(termin, greskaAkcije) : akcijeClana(termin, greskaAkcije));
  }

  function akcijeClana(termin, greskaAkcije) {
    const prijava = termin.mojaPrijava;
    if (prijava !== null) {
      const opis = prijava.status === 'NaCekanju'
        ? `Na listi čekanja ste, pozicija ${prijava.pozicijaNaCekanju}.`
        : 'Imate potvrđenu rezervaciju za ovaj termin.';
      return el('div', { class: 'akcije' },
        el('p', { 'data-testid': 'termin-moja-prijava', 'data-status': prijava.status }, opis),
        link('Pogledaj prijavu', `/rezervacije/${prijava.rezervacijaId}`, 'termin-moja-prijava-detalji', 'dugme-link'));
    }

    if (termin.status === 'Otkazan') {
      return napomena('Termin je otkazan i ne prima prijave.', 'termin-napomena');
    }

    if (new Date(termin.pocetak) <= new Date()) {
      return napomena('Termin je već počeo i ne prima prijave.', 'termin-napomena');
    }

    const imaMesta = termin.slobodnaMesta > 0;
    const dugme = el('button', { type: 'button', 'data-testid': imaMesta ? 'termin-rezervisi' : 'termin-lista-cekanja', onclick: prijaviSe },
      imaMesta ? 'Rezerviši mesto' : 'Prijavi se na listu čekanja');

    return el('div', { class: 'akcije' },
      imaMesta ? null : el('p', {}, 'Termin je popunjen. Kada se mesto oslobodi, prvi sa liste čekanja ga automatski dobija.'),
      dugme);

    async function prijaviSe() {
      sakrij(greskaAkcije);
      await saCekanjem(dugme, 'Slanje…', async () => {
        try {
          const nova = await pozovi(`/api/termini/${termin.id}/${imaMesta ? 'rezervacije' : 'lista-cekanja'}`, { metod: 'POST' });
          kontekst.idiNa(`/rezervacije/${nova.id}`, null, {
            tekst: imaMesta ? 'Mesto je rezervisano.' : `Prijavljeni ste na listu čekanja, pozicija ${nova.pozicijaNaCekanju}.`,
          });
        } catch (greska) {
          if (KODOVI_ZASTARELOG_PRIKAZA.has(greska.kod)) {
            await ucitaj(greska);
          } else {
            prikaziPoruku(greskaAkcije, porukaGreske(greska));
          }
        }
      });
    }
  }

  function akcijeTrenera(termin, greskaAkcije) {
    if (termin.trener.id !== kontekst.korisnik.id) {
      return napomena('Termin vodi drugi trener, pa ga možete samo pregledati.', 'termin-napomena');
    }

    const aktivan = termin.status === 'Aktivan';
    const otkazi = el('button', {
      type: 'button',
      class: 'dugme-opasno',
      'data-testid': 'termin-otkazi',
      onclick: () => izvrsi(otkazi, {
        naslov: 'Otkazati termin?',
        poruka: 'Sve potvrđene rezervacije i prijave na listi čekanja biće otkazane.',
        potvrdiTekst: 'Otkaži termin',
      }, async () => {
        await pozovi(`/api/termini/${termin.id}/otkazivanje`, { metod: 'POST' });
        await ucitaj();
        kontekst.obavesti({ tekst: 'Termin je otkazan.' });
      }),
    }, 'Otkaži termin');
    const obrisi = el('button', {
      type: 'button',
      class: 'dugme-opasno',
      'data-testid': 'termin-obrisi',
      onclick: () => izvrsi(obrisi, {
        naslov: 'Obrisati termin?',
        poruka: 'Brisanje je trajno. Termin koji ima ili je imao prijave ne može da se obriše; takav termin otkažite.',
        potvrdiTekst: 'Obriši termin',
      }, async () => {
        await pozovi(`/api/termini/${termin.id}`, { metod: 'DELETE' });
        kontekst.idiNa('/moji-termini', null, { tekst: `Termin „${termin.naziv}“ je obrisan.` });
      }),
    }, 'Obriši termin');

    return el('div', { class: 'akcije' },
      aktivan ? link('Izmeni', `/termini/${termin.id}/izmena`, 'termin-izmeni', 'dugme-link') : null,
      link('Polaznici i lista čekanja', `/termini/${termin.id}/polaznici`, 'termin-polaznici', 'dugme-link dugme-sekundarno'),
      aktivan ? otkazi : null,
      obrisi);

    async function izvrsi(dugme, potvrda, akcija) {
      if (!await potvrdi(potvrda)) {
        return;
      }

      sakrij(greskaAkcije);
      await saCekanjem(dugme, 'Slanje…', async () => {
        try {
          await akcija();
        } catch (greska) {
          prikaziPoruku(greskaAkcije, porukaGreske(greska));
        }
      });
    }
  }
}
