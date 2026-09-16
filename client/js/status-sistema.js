const PORUKE = {
  ucitavanje: 'Provera u toku…',
  ispravan: 'Sistem radi ispravno.',
  neispravan: 'Sistem trenutno ne radi ispravno.',
  greska: 'Server nije dostupan. Pokušajte ponovo.',
};

export function poveziStatusSistema(dugme, rezultat) {
  dugme.addEventListener('click', async () => {
    postaviStanje(rezultat, 'ucitavanje');
    dugme.disabled = true;
    try {
      const odgovor = await fetch('/health', { headers: { Accept: 'application/json' } });
      postaviStanje(rezultat, odgovor.ok ? 'ispravan' : 'neispravan');
    } catch {
      postaviStanje(rezultat, 'greska');
    } finally {
      dugme.disabled = false;
    }
  });
}

function postaviStanje(element, stanje) {
  element.dataset.stanje = stanje;
  element.textContent = PORUKE[stanje];
}
