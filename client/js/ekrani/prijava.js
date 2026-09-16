import { pozovi } from '../api.js';
import { zapocni } from '../sesija.js';
import { el, greskaForme, link, naslovEkrana, ocistiGreskeForme, polje, postaviNaslov, prikaziGreskeForme, saCekanjem } from '../ui.js';

export async function prikaziPrijavu(kontejner, kontekst) {
  postaviNaslov('Prijava');
  const globalnaGreska = greskaForme('prijava-greska');
  const dugme = el('button', { type: 'submit', 'data-testid': 'prijava-potvrdi' }, 'Prijavi se');
  const forma = el('form', { class: 'forma', novalidate: true, onsubmit: posalji },
    polje({ naziv: 'email', oznaka: 'Email', tip: 'email', testid: 'prijava-email', atributi: { autocomplete: 'username' } }),
    polje({ naziv: 'lozinka', oznaka: 'Lozinka', tip: 'password', testid: 'prijava-lozinka', atributi: { autocomplete: 'current-password' } }),
    globalnaGreska,
    el('div', { class: 'akcije' }, dugme));

  kontejner.append(
    naslovEkrana('Prijava'),
    forma,
    el('p', {}, 'Nemate nalog? ', link('Registrujte se kao član.', '/registracija', 'prijava-link-registracija')));

  async function posalji(dogadjaj) {
    dogadjaj.preventDefault();
    ocistiGreskeForme(forma, globalnaGreska);
    const podaci = new FormData(forma);

    await saCekanjem(dugme, 'Prijavljivanje…', async () => {
      try {
        zapocni(await pozovi('/api/auth/prijava', { metod: 'POST', telo: { email: podaci.get('email'), lozinka: podaci.get('lozinka') } }));
        kontekst.idiNa(povratnaPutanja(kontekst.upit.get('povratak')));
      } catch (greska) {
        prikaziGreskeForme(forma, greska, globalnaGreska);
      }
    });
  }
}

function povratnaPutanja(povratak) {
  return povratak?.startsWith('/') && !povratak.startsWith('//') && povratak !== '/prijava' ? povratak : '/termini';
}
