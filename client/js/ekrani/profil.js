import { pozovi } from '../api.js';
import { azurirajKorisnika, zavrsi } from '../sesija.js';
import {
  el, greskaForme, listaPodataka, naslovEkrana, napomena, ocistiGreskeForme, polje, porukaGreske, postaviNaslov,
  potvrdi, prikaziGreskeForme, prikaziPoruku, saCekanjem, sakrij,
} from '../ui.js';

/** Profil prijavljenog korisnika: izmena imena i lozinke i brisanje sopstvenog naloga. */
export async function prikaziProfil(kontejner, kontekst) {
  postaviNaslov('Moj profil');
  const korisnik = kontekst.korisnik;
  const greska = greskaForme('profil-forma-greska');
  const greskaBrisanja = greskaForme('profil-brisanje-greska');
  const sacuvaj = el('button', { type: 'submit', 'data-testid': 'profil-forma-sacuvaj' }, 'Sačuvaj izmene');
  const obrisi = el('button', { type: 'button', class: 'dugme-opasno', 'data-testid': 'profil-obrisi', onclick: obrisiNalog }, 'Obriši nalog');
  const forma = el('form', { class: 'forma', novalidate: true, onsubmit: posalji },
    polje({
      naziv: 'imePrezime', oznaka: 'Ime i prezime', testid: 'profil-forma-ime-prezime',
      vrednost: korisnik.imePrezime, atributi: { maxlength: 100 },
    }),
    polje({
      naziv: 'lozinka', oznaka: 'Nova lozinka', tip: 'password', testid: 'profil-forma-lozinka', vrednost: '',
      pomoc: 'Ostavite prazno da zadržite postojeću lozinku.', atributi: { maxlength: 100 },
    }),
    greska,
    el('div', { class: 'akcije' }, sacuvaj));

  kontejner.append(
    naslovEkrana('Moj profil'),
    listaPodataka([
      ['Email', korisnik.email, 'profil-email'],
      ['Uloga', korisnik.uloga === 'Trener' ? 'Trener' : 'Član', 'profil-prikaz-uloge'],
    ]),
    napomena('Email i uloga se ne menjaju.'),
    forma,
    el('section', {},
      el('h2', {}, 'Brisanje naloga'),
      napomena('Nalog koji ima termine ili prijave, i otkazane, ne može da se obriše; istorija se čuva.'),
      greskaBrisanja,
      el('div', { class: 'akcije' }, obrisi)));

  async function posalji(dogadjaj) {
    dogadjaj.preventDefault();
    ocistiGreskeForme(forma, greska);
    const podaci = new FormData(forma);
    const novaLozinka = podaci.get('lozinka');
    const zahtev = {
      imePrezime: podaci.get('imePrezime'),
      // Prazno polje znači „zadrži postojeću lozinku“; server tada ne dira heš.
      lozinka: novaLozinka === '' ? null : novaLozinka,
    };

    await saCekanjem(sacuvaj, 'Čuvanje…', async () => {
      try {
        const izmenjen = await pozovi('/api/auth/ja', { metod: 'PUT', telo: zahtev });
        azurirajKorisnika(izmenjen);
        forma.elements.namedItem('lozinka').value = '';
        kontekst.obavesti({ tekst: 'Profil je sačuvan.' });
      } catch (greskaZahteva) {
        prikaziGreskeForme(forma, greskaZahteva, greska);
      }
    });
  }

  async function obrisiNalog() {
    const potvrdjeno = await potvrdi({
      naslov: 'Obrisati nalog?',
      poruka: 'Brisanje je trajno i odjavljuje vas. Nalog koji ima termine ili prijave ne može da se obriše.',
      potvrdiTekst: 'Obriši nalog',
    });
    if (!potvrdjeno) {
      return;
    }

    sakrij(greskaBrisanja);
    await saCekanjem(obrisi, 'Brisanje…', async () => {
      try {
        await pozovi('/api/auth/ja', { metod: 'DELETE' });
        zavrsi('nalog-obrisan');
      } catch (greskaZahteva) {
        prikaziPoruku(greskaBrisanja, porukaGreske(greskaZahteva));
      }
    });
  }
}
