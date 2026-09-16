import { pozovi } from '../api.js';
import { zapocni } from '../sesija.js';
import { el, greskaForme, link, naslovEkrana, ocistiGreskeForme, polje, postaviNaslov, prikaziGreskeForme, saCekanjem } from '../ui.js';

export async function prikaziRegistraciju(kontejner, kontekst) {
  postaviNaslov('Registracija');
  const globalnaGreska = greskaForme('registracija-greska');
  const dugme = el('button', { type: 'submit', 'data-testid': 'registracija-potvrdi' }, 'Napravi nalog');
  const forma = el('form', { class: 'forma', novalidate: true, onsubmit: posalji },
    polje({ naziv: 'imePrezime', oznaka: 'Ime i prezime', testid: 'registracija-ime-prezime', atributi: { autocomplete: 'name', maxlength: 100 } }),
    polje({ naziv: 'email', oznaka: 'Email', tip: 'email', testid: 'registracija-email', atributi: { autocomplete: 'email', maxlength: 254 } }),
    polje({ naziv: 'lozinka', oznaka: 'Lozinka', tip: 'password', testid: 'registracija-lozinka', pomoc: 'Najmanje 8 znakova.', atributi: { autocomplete: 'new-password', maxlength: 100 } }),
    globalnaGreska,
    el('div', { class: 'akcije' }, dugme));

  kontejner.append(
    naslovEkrana('Registracija člana'),
    el('p', { class: 'napomena' }, 'Nalog se pravi sa ulogom član. Trenerske naloge dodeljuje teretana.'),
    forma,
    el('p', {}, 'Već imate nalog? ', link('Prijavite se.', '/prijava', 'registracija-link-prijava')));

  async function posalji(dogadjaj) {
    dogadjaj.preventDefault();
    ocistiGreskeForme(forma, globalnaGreska);
    const podaci = new FormData(forma);
    const email = podaci.get('email');
    const lozinka = podaci.get('lozinka');

    await saCekanjem(dugme, 'Pravljenje naloga…', async () => {
      try {
        await pozovi('/api/auth/registracija', { metod: 'POST', telo: { imePrezime: podaci.get('imePrezime'), email, lozinka } });
        zapocni(await pozovi('/api/auth/prijava', { metod: 'POST', telo: { email, lozinka } }));
        kontekst.idiNa('/termini', null, { tekst: 'Nalog je napravljen i prijavljeni ste.' });
      } catch (greska) {
        prikaziGreskeForme(forma, greska, globalnaGreska);
      }
    });
  }
}
