/**
 * Serbian for the API's error messages.
 *
 * The API answers in English — `"Only a settled bill can be reversed"` — while every other word on
 * these screens is Serbian. Translating here rather than at the source was a deliberate choice: the
 * message is presentation, the API is a contract shared with the master host and with anything built
 * against it later, and pinning that contract to one language is hard to undo.
 *
 * The cost of that choice is this file. There is no stable error code to match on — only
 * `LICENSE_EXPIRED`, set by the licence middleware — so the match is against the English text, which
 * means a reworded message on the backend silently stops being translated. That is why the fallback
 * is the original string and never a placeholder: an untranslated English sentence still tells the
 * person what went wrong, whereas "Nepoznata greška" would have thrown away the only useful part.
 *
 * Patterns keep their interpolated values through capture groups, so "Table 12 was not found"
 * arrives as "Sto 12 nije pronađen" rather than losing the number.
 */

interface Translation {
  readonly match: RegExp;
  readonly serbian: (...groups: string[]) => string;
}

/**
 * Ordered: the first match wins, so a specific pattern must come before a general one that would
 * also match it.
 */
const TRANSLATIONS: Translation[] = [
  // --- Bills, voids and payment -------------------------------------------------------------
  {
    match: /^This order is (\w+); its lines can no longer be voided\.\s*A settled bill is reversed instead\.$/,
    serbian: () =>
      'Ovaj račun je zatvoren, pa mu se stavke više ne storniraju. '
      + 'Plaćen račun se stornira protivstavkom, sa otiska računa.',
  },
  { match: /^A reversal cannot itself be reversed\.$/, serbian: () => 'Protivstavka se ne stornira.' },
  {
    match: /^A payment has already been recorded for this order\.$/,
    serbian: () => 'Ovaj račun je već naplaćen.',
  },
  { match: /^An empty order cannot be paid\.$/, serbian: () => 'Prazan račun se ne naplaćuje.' },
  {
    match: /^This order is already (\w+)\.$/,
    serbian: (status) => `Račun je već ${orderStatusWord(status)}.`,
  },
  {
    match: /^The line does not belong to this order\.$/,
    serbian: () => 'Ta stavka nije na ovom računu.',
  },
  {
    match: /^Line (\S+) is not on this order\.$/,
    serbian: () => 'Ta stavka nije na ovom računu.',
  },
  {
    match: /^That payment does not belong to this order\.$/,
    serbian: () => 'Ta uplata ne pripada ovom računu.',
  },
  {
    match: /^This endpoint only increases a line\.\s*Cancel servings through a void, which records who did it and why\.$/,
    serbian: () =>
      'Količina se ovim putem samo povećava. Smanjenje ide kroz storno, koji beleži ko ga je '
      + 'uradio i zašto.',
  },
  { match: /^Cancel at least one serving\.$/, serbian: () => 'Stornirajte bar jedan komad.' },
  { match: /^Only signed-in staff can take payment\.$/, serbian: () => 'Naplatu obavlja osoblje.' },
  {
    match: /^Only signed-in staff can open a tab directly\.$/,
    serbian: () => 'Račun direktno otvara osoblje.',
  },
  {
    match: /^The member of staff (voiding|receiving) this could not be identified\.$/,
    serbian: () => 'Nije moguće utvrditi ko obavlja ovu radnju. Prijavite se ponovo.',
  },
  { match: /^Order (\S+) was not found\.$/, serbian: () => 'Račun nije pronađen.' },
  { match: /^No such order\.$/, serbian: () => 'Račun nije pronađen.' },

  // --- Tables and rooms ---------------------------------------------------------------------
  {
    match: /^Table (\d+) has order or reservation history and cannot be deleted\.\s*Deactivate it instead\.$/,
    serbian: (number) =>
      `Sto ${number} ima istoriju računa ili rezervacija i ne može da se obriše. `
      + 'Umesto toga ga isključite iz upotrebe.',
  },
  {
    match: /^Table (\d+) would fall outside a (\d+)×(\d+) room\.\s*Move it first, or choose a larger area\.$/,
    serbian: (number, width, height) =>
      `Sto ${number} bi ostao izvan prostorije ${width}×${height}. `
      + 'Prvo ga pomerite, ili izaberite veće platno.',
  },
  {
    match: /^Table number (\d+) is already in use\.$/,
    serbian: (number) => `Sto broj ${number} već postoji.`,
  },
  {
    match: /^Table (\d+) is not in service\.$/,
    serbian: (number) => `Sto ${number} je van upotrebe.`,
  },
  { match: /^Table (\S+) was not found\.$/, serbian: () => 'Sto nije pronađen.' },
  {
    match: /^The same table appears more than once in the layout\.$/,
    serbian: () => 'Isti sto se u rasporedu pojavljuje više puta.',
  },
  { match: /^A table cannot sit outside the room\.$/, serbian: () => 'Sto mora biti unutar prostorije.' },
  { match: /^A table must be chosen\.$/, serbian: () => 'Izaberite sto.' },
  { match: /^No such room\.$/, serbian: () => 'Prostorija nije pronađena.' },
  {
    match: /^A room called '(.+)' already exists\.$/,
    serbian: (name) => `Prostorija „${name}“ već postoji.`,
  },
  {
    match: /^The table (?:for reservation \S+ )?(?:this session belongs to )?no longer exists\.$/,
    serbian: () => 'Taj sto više ne postoji.',
  },
  {
    match: /^Capacity must be greater than zero\.$/,
    serbian: () => 'Broj mesta mora biti veći od nule.',
  },
  {
    match: /^Capacity must be 50 or fewer seats\.$/,
    serbian: () => 'Sto ne može imati više od 50 mesta.',
  },
  {
    match: /^Table number must be greater than zero\.$/,
    serbian: () => 'Broj stola mora biti veći od nule.',
  },
  {
    match: /^Table (width|height) must be between 20 and 600\.$/,
    serbian: (side) => `${side === 'width' ? 'Širina' : 'Visina'} stola mora biti između 20 i 600.`,
  },

  // --- Menu and recipes ---------------------------------------------------------------------
  {
    match: /^'(.+)' appears on past orders and cannot be deleted\.\s*Take it off the menu instead by clearing its availability\.$/,
    serbian: (name) =>
      `„${name}“ se pojavljuje na ranijim računima i ne može da se obriše. `
      + 'Umesto toga ga isključite iz ponude.',
  },
  {
    match: /^'(.+)' is already on the menu\.$/,
    serbian: (name) => `„${name}“ već postoji u jelovniku.`,
  },
  {
    match: /^'(.+)' is currently unavailable\.$/,
    serbian: (name) => `„${name}“ trenutno nije u ponudi.`,
  },
  {
    match: /^The same ingredient appears more than once; combine it into one line\.$/,
    serbian: () => 'Isti sastojak je unet više puta — spojite ga u jednu stavku.',
  },
  {
    match: /^A recipe line must consume more than zero\.$/,
    serbian: () => 'Stavka normativa mora trošiti više od nule.',
  },
  { match: /^A menu item needs a name\.$/, serbian: () => 'Artikal mora imati naziv.' },
  { match: /^A menu item needs a category.*$/, serbian: () => 'Artikal mora imati kategoriju.' },
  { match: /^Menu item (\S+) was not found\.$/, serbian: () => 'Artikal nije pronađen.' },
  { match: /^No such menu item\.$/, serbian: () => 'Artikal nije pronađen.' },
  {
    match: /^(?:An order|Each line) must (?:be named|name a menu item)\.$/,
    serbian: () => 'Svaka stavka mora imati artikal.',
  },
  {
    match: /^An order must contain at least one item\.$/,
    serbian: () => 'Porudžbina mora imati bar jednu stavku.',
  },
  { match: /^Adding a line requires a menu item\.$/, serbian: () => 'Za novu stavku izaberite artikal.' },

  // --- Store --------------------------------------------------------------------------------
  { match: /^No such ingredient\.$/, serbian: () => 'Sastojak nije pronađen.' },
  { match: /^Ingredient (\S+) was not found\.$/, serbian: () => 'Sastojak nije pronađen.' },
  { match: /^An ingredient must be named\.$/, serbian: () => 'Sastojak mora imati naziv.' },
  {
    match: /^A stocktake cannot find less than nothing\.$/,
    serbian: () => 'Popisana količina ne može biti negativna.',
  },
  {
    match: /^State why the stock is being corrected\.$/,
    serbian: () => 'Navedite razlog korekcije zaliha.',
  },
  {
    match: /^(?:A delivery must be for more than zero|The restocked quantity must be greater than zero|Restocked quantity must be greater than zero)\.$/,
    serbian: () => 'Količina isporuke mora biti veća od nule.',
  },
  {
    match: /^The restocked quantity is implausibly large\.$/,
    serbian: () => 'Unesena količina je nerealno velika. Proverite broj.',
  },
  {
    match: /^A purchase price cannot be negative\.$/,
    serbian: () => 'Nabavna cena ne može biti negativna.',
  },
  { match: /^A total cost cannot be negative\.$/, serbian: () => 'Ukupan iznos ne može biti negativan.' },
  {
    match: /^(?:Quantity|Deducted quantity) must be greater than zero\.$/,
    serbian: () => 'Količina mora biti veća od nule.',
  },
  { match: /^A quantity is required\.$/, serbian: () => 'Unesite količinu.' },

  // --- Reservations -------------------------------------------------------------------------
  {
    match: /^(?:A completed reservation cannot be cancelled|This reservation has already been completed and cannot be cancelled)\.$/,
    serbian: () => 'Rezervacija na kojoj je gost već primljen ne može da se otkaže.',
  },
  {
    match: /^A (\w+) reservation cannot be checked in\.$/,
    serbian: (status) => `Rezervacija sa statusom „${reservationStatusWord(status)}“ ne prima dolazak.`,
  },
  {
    match: /^You can only cancel your own reservations\.$/,
    serbian: () => 'Možete otkazati samo svoje rezervacije.',
  },
  {
    match: /^A reservation must start in the future\.$/,
    serbian: () => 'Rezervacija mora biti u budućnosti.',
  },
  {
    match: /^The reservation must end after it starts\.$/,
    serbian: () => 'Kraj rezervacije mora biti posle početka.',
  },
  {
    match: /^Party size must be at least one guest\.$/,
    serbian: () => 'Rezervacija mora biti za bar jednog gosta.',
  },
  {
    match: /^Party size must be 50 guests or fewer\.$/,
    serbian: () => 'Rezervacija ne može biti za više od 50 gostiju.',
  },
  {
    match: /^The guest's name is too short to identify anybody\.$/,
    serbian: () => 'Ime gosta je prekratko.',
  },
  {
    match: /^The guest's name must be 200 characters or fewer\.$/,
    serbian: () => 'Ime gosta može imati najviše 200 znakova.',
  },
  {
    match: /^The contact number must be 50 characters or fewer\.$/,
    serbian: () => 'Broj telefona može imati najviše 50 znakova.',
  },
  { match: /^Reservation (\S+) was not found\.$/, serbian: () => 'Rezervacija nije pronađena.' },

  // --- Shifts -------------------------------------------------------------------------------
  {
    match: /^This waiter already has (?:another )?shift that overlaps the requested period\.$/,
    serbian: () => 'Ovaj konobar već ima smenu koja se preklapa sa traženim periodom.',
  },
  {
    match: /^'(.+)' overlaps '(.+)', which this waiter already works on one of those days\.$/,
    serbian: (first, second) =>
      `„${first}“ se preklapa sa „${second}“, koju ovaj konobar već radi nekog od tih dana.`,
  },
  {
    match: /^A shift called '(.+)' already exists\.$/,
    serbian: (name) => `Smena „${name}“ već postoji.`,
  },
  { match: /^A shift must be named\.$/, serbian: () => 'Smena mora imati naziv.' },
  { match: /^Give the shift a name.*$/, serbian: () => 'Smena mora imati naziv.' },
  {
    match: /^A shift must start before it ends\.$/,
    serbian: () => 'Smena mora početi pre nego što se završi.',
  },
  {
    match: /^A shift cannot start and end at the same time\.$/,
    serbian: () => 'Smena ne može početi i završiti se u isti čas.',
  },
  { match: /^A waiter must be named\.$/, serbian: () => 'Izaberite konobara.' },
  {
    match: /^That account is not a waiter at this restaurant\.$/,
    serbian: () => 'Taj nalog nije konobar u ovom restoranu.',
  },
  {
    match: /^The chosen user does not exist or is not a waiter\.$/,
    serbian: () => 'Izabrani nalog ne postoji ili nije konobar.',
  },
  { match: /^Choose at least one day of the week\.$/, serbian: () => 'Izaberite bar jedan dan.' },
  {
    match: /^The arrangement cannot end before it starts\.$/,
    serbian: () => 'Dodela ne može da se završi pre nego što počne.',
  },
  {
    match: /^Only a signed-in manager or owner can (assign shifts|set the rota)\.$/,
    serbian: () => 'Smene raspoređuje menadžer ili vlasnik.',
  },
  { match: /^No such shift template\.$/, serbian: () => 'Šablon smene nije pronađen.' },
  { match: /^No such assignment\.$/, serbian: () => 'Dodela nije pronađena.' },
  { match: /^Shift (\S+) was not found\.$/, serbian: () => 'Smena nije pronađena.' },

  // --- Accounts and sign-in -----------------------------------------------------------------
  {
    match: /^No restaurant is registered under that code\.$/,
    serbian: () => 'Ne postoji restoran sa tom šifrom.',
  },
  {
    match: /^An account with this email address already exists at this restaurant\.$/,
    serbian: () => 'Nalog sa ovom email adresom već postoji u ovom restoranu.',
  },
  {
    match: /^The owner's account cannot be switched off\.$/,
    serbian: () => 'Vlasnički nalog ne može da se ugasi.',
  },
  {
    match: /^You cannot switch off your own account\.$/,
    serbian: () => 'Ne možete ugasiti sopstveni nalog.',
  },
  { match: /^The role of the owner cannot be changed\.$/, serbian: () => 'Uloga vlasnika se ne menja.' },
  {
    match: /^Staff accounts can only be waiters or managers\.$/,
    serbian: () => 'Zaposleni može biti konobar ili menadžer.',
  },
  {
    match: /^Staff can only be waiters, managers or the owner\.$/,
    serbian: () => 'Uloga može biti konobar, menadžer ili vlasnik.',
  },
  {
    match: /^No such (?:account|member of staff at this restaurant)\.$/,
    serbian: () => 'Nalog nije pronađen.',
  },
  { match: /^(?:Email is required|Email must be a valid address)\.$/, serbian: () => 'Unesite ispravnu email adresu.' },
  { match: /^Contact email must be a valid address\.$/, serbian: () => 'Unesite ispravnu email adresu.' },
  { match: /^(?:A password is required|Password is required)\.$/, serbian: () => 'Unesite lozinku.' },
  {
    match: /^Password must be at least 8 characters long\.$/,
    serbian: () => 'Lozinka mora imati bar 8 znakova.',
  },
  {
    match: /^Password must contain (a digit|a lowercase letter|an uppercase letter)\.$/,
    serbian: (what) =>
      `Lozinka mora sadržati ${
        what === 'a digit' ? 'cifru' : what === 'a lowercase letter' ? 'malo slovo' : 'veliko slovo'
      }.`,
  },
  { match: /^First name is required\.$/, serbian: () => 'Unesite ime.' },
  { match: /^Last name is required\.$/, serbian: () => 'Unesite prezime.' },

  // --- Licences and the platform ------------------------------------------------------------
  {
    match: /^A cancelled licence cannot be renewed; issue a new one\.$/,
    serbian: () => 'Otkazana licenca se ne produžava — izdajte novu.',
  },
  {
    match: /^A cancelled licence cannot be suspended\.$/,
    serbian: () => 'Otkazana licenca ne može da se suspenduje.',
  },
  {
    match: /^A (suspension|cancellation) must state a reason\.$/,
    serbian: (kind) =>
      kind === 'suspension'
        ? 'Suspenzija mora imati razlog.'
        : 'Otkazivanje mora imati razlog.',
  },
  {
    match: /^State why the licence is being suspended or cancelled\.$/,
    serbian: () => 'Navedite razlog suspenzije ili otkazivanja.',
  },
  {
    match: /^Choose a licence term of 1, 3, 6 or 12 months\.$/,
    serbian: () => 'Izaberite trajanje od 1, 3, 6 ili 12 meseci.',
  },
  {
    match: /^(?:A licence price cannot be negative|A price cannot be negative|Price cannot be negative)\.$/,
    serbian: () => 'Cena ne može biti negativna.',
  },
  {
    match: /^A payment must be for more than zero\.$/,
    serbian: () => 'Uplata mora biti veća od nule.',
  },
  {
    match: /^The restaurant code '(.+)' is already taken\.$/,
    serbian: (slug) => `Šifra restorana „${slug}“ je već zauzeta.`,
  },
  {
    match: /^Currency code must be (?:a three-letter ISO code|three letters)\.$/,
    serbian: () => 'Valuta se piše sa tri slova, npr. RSD.',
  },
  { match: /^No such licence\.$/, serbian: () => 'Licenca nije pronađena.' },
  { match: /^No such restaurant\.$/, serbian: () => 'Restoran nije pronađen.' },
  {
    match: /^The restaurant on this token no longer exists\.$/,
    serbian: () => 'Restoran sa ovog koda više ne postoji.',
  },
  {
    match: /^The (issuing|recording|renewing) administrator could not be identified\.$/,
    serbian: () => 'Nije moguće utvrditi administratora. Prijavite se ponovo.',
  },
  {
    match: /^The authorising manager could not be identified\.$/,
    serbian: () => 'Nije moguće utvrditi menadžera koji odobrava. Prijavite se ponovo.',
  },
  {
    match: /^This endpoint is only meaningful for restaurant staff\.$/,
    serbian: () => 'Ova radnja je namenjena osoblju restorana.',
  },

  // --- Reasons, periods and other shared validators -----------------------------------------
  {
    match: /^Give a reason of at least (\d+) characters?\.$/,
    serbian: (count) => `Razlog mora imati bar ${count} ${charactersWord(Number(count))}.`,
  },
  {
    match: /^State why this settled bill is being reversed\.$/,
    serbian: () => 'Navedite zašto se plaćen račun stornira.',
  },
  {
    match: /^State why this (?:tab|is being cancelled)/,
    serbian: () => 'Navedite razlog storniranja.',
  },
  {
    match: /^The period (?:cannot end before it starts|must end after it starts|must start before it ends)\.$/,
    serbian: () => 'Kraj perioda mora biti posle početka.',
  },
  {
    match: /^The end of the period must be after its start\.$/,
    serbian: () => 'Kraj perioda mora biti posle početka.',
  },
  {
    match: /^The period cannot be longer than 12 hours\.$/,
    serbian: () => 'Period ne može biti duži od 12 sati.',
  },
  {
    match: /^Between 1 and 500 bills can be listed at a time\.$/,
    serbian: () => 'Odjednom se prikazuje između 1 i 500 računa.',
  },
  {
    match: /^Notes must be 500 characters or fewer\.$/,
    serbian: () => 'Napomena može imati najviše 500 znakova.',
  },
  { match: /^A QR code token is required\.$/, serbian: () => 'Nedostaje kod stola.' },
  { match: /^Table id is required\.$/, serbian: () => 'Izaberite sto.' },
  { match: /^Changing a line requires the line's id\.$/, serbian: () => 'Nedostaje oznaka stavke.' },
  { match: /^Unknown change type\.$/, serbian: () => 'Nepoznata vrsta izmene.' },
  { match: /^Unknown payment method\.$/, serbian: () => 'Nepoznat način plaćanja.' },

  // --- Titles the API sets on problem responses ----------------------------------------------
  {
    match: /^One or more validation errors occurred\.$/,
    serbian: () => 'Uneti podaci nisu ispravni.',
  },
  {
    match: /^The request conflicts with the current state\.$/,
    serbian: () => 'Radnja se kosi sa trenutnim stanjem podataka.',
  },
  { match: /^Resource not found\.$/, serbian: () => 'Traženo nije pronađeno.' },
  {
    match: /^You are not allowed to perform this action\.$/,
    serbian: () => 'Nemate ovlašćenje za ovu radnju.',
  },
  { match: /^Authentication failed\.$/, serbian: () => 'Prijava nije uspela.' },
  {
    match: /^The request could not be processed\.$/,
    serbian: () => 'Zahtev nije mogao da se obradi.',
  },
];

/** Order statuses as they appear inside an English message, in the case a sentence needs. */
function orderStatusWord(status: string): string {
  switch (status.toLowerCase()) {
    case 'paid':
      return 'plaćen';
    case 'cancelled':
      return 'otkazan';
    case 'voided':
      return 'storniran';
    case 'served':
      return 'serviran';
    case 'inpreparation':
      return 'u pripremi';
    default:
      return 'zatvoren';
  }
}

function reservationStatusWord(status: string): string {
  switch (status.toLowerCase()) {
    case 'cancelled':
      return 'Otkazana';
    case 'completed':
      return 'Gost stigao';
    case 'confirmed':
      return 'Potvrđena';
    default:
      return 'Na čekanju';
  }
}

/** "znak" / "znaka" / "znakova" — the reason-length message is the only place this is needed. */
function charactersWord(count: number): string {
  const last = count % 10;
  const lastTwo = count % 100;

  if (last === 1 && lastTwo !== 11) {
    return 'znak';
  }

  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) {
    return 'znaka';
  }

  return 'znakova';
}

/**
 * Translates one message from the API, or hands it back untouched.
 *
 * Untouched is the deliberate fallback. A message this file has not seen is still a true statement
 * about what went wrong, and an English sentence beats a generic Serbian one that says nothing.
 */
export function toSerbian(message: string): string {
  const trimmed = message.trim();

  for (const { match, serbian } of TRANSLATIONS) {
    const found = trimmed.match(match);

    if (found) {
      return serbian(...found.slice(1));
    }
  }

  return message;
}
