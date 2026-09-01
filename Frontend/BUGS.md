# Frontend — nađeno skeniranjem, i šta je urađeno

Popis nastao prolaskom kroz ceo `Frontend/` (3 projekta, ~12.300 linija) 1. septembra 2026.
Prati ga redom po rundama; runde su poređane tako da ništa u kasnijoj ne zavisi od ranije.

Polazno stanje: `ng build shared` i `ng build pos` prolaze čisti, uz jedno upozorenje
(`initial bundle 717 kB` prema budžetu od 500 kB). To upozorenje nije bug i ne dira se ovde.

---

## Runda 1 — netačnost

Stvari koje su ili merljivo pokvarene, ili mrtav kod. Ništa od ovoga nije stvar ukusa.

- [x] **1. Tooltipovi na ekranu računa ne rade.**
      `order.page.ts` koristi `matTooltip` na dva mesta (linije 99 i 111), ali `MatTooltipModule`
      nije u `imports`. Angular statički atribut na poznatom elementu ne prijavljuje kao grešku —
      samo ga ignoriše, pa se gradi čisto a tooltipa nema. Pogođena dugmad su „još jedno" i
      „storno stavke", koja se razlikuju samo po ikoni, na najprometnijem ekranu kase.
      Svaka druga stranica modul uvozi ispravno; ova je preskočena.

- [x] **2. Četiri razorne radnje bez potvrde**, dok `ConfirmDialog` postoji i koristi se drugde:
      - brisanje artikla iz jelovnika — `menu.page.ts:364`
      - ukidanje stalne dodele smene — `schedule.page.ts:564`
      - gašenje naloga zaposlenog — `staff.page.ts:254`
      - gašenje celog restorana — `restaurant-detail.page.ts:569`; jedan klik zaključava
        svakog zaposlenog u tom restoranu napolju

- [x] **3. Srpska množina pokvarena na 10 mesta**, iako `plural()` / `daysLabel()` / `itemsLabel()`
      postoje u `shared/format/labels`. Koriste ih samo gostov ekran i shell. Uživo danas piše
      „3 sto(lova)", „2 nalog(a)", „1 dana", „1 mesta", „1 dodela".
      Ovo je tačno ona greška koju TODO.md beleži kao rešenu — rešena je na jednom mestu i ostala
      na svim ostalim.

- [x] **4. `badgeColour()` je mrtav switch.** `restaurants.page.ts:296` vraća `'#fff'` iz sve
      četiri grane, uključujući `daysRemaining <= 30 ? '#fff' : '#fff'`.

- [x] **5. Pretraga restorana šalje zahtev po otkucanom znaku.**
      `restaurants.page.ts:46` vezuje `(ngModelChange)="load()"`. Bez odlaganja i bez `switchMap`,
      pa odgovori mogu i da stignu u pogrešnom redosledu. (Pretraga po broju računa na `/racuni`
      je urađena ispravno, na klijentu.)

**Runda 1 zatvorena.** `ng build` prolazi za sva tri projekta, `ng test shared` i `ng test pos`
daju 47 testova (bilo 43 — dodata su četiri za nove oblike množine). Provereno grepom da nije
ostao nijedan `matTooltip` bez modula, nijedna ručno pisana množina i nijedno brisanje bez potvrde.

## Runda 2 — osećaj

- [x] **6. Deset od sedamnaest stranica nema nikakvu naznaku učitavanja.**
      *(U prvom prolazu sam napisao devet i propustio `rezervacije` — ispravljeno pri radu.)*
      Traku imaju samo dve prijave, `sala`, `sala/:id`, `racuni` i gostov ekran. Ostale
      (`izvestaji`, `magacin`, `smene`, `zaposleni`, `jelovnik`, `raspored`, `rezervacije`,
      `pregled`, `restorani`, `restorani/:id`) iscrtaju praznu ljušturu pa podaci uskoče.
      Izveštaji pri tom šalju tri paralelna zahteva bez ijedne povratne informacije.

      Rešeno preko `LoadingState` (`shared/http/loading.ts`) — brojač, ne zastavica, jer bi na
      stranicama sa više paralelnih poziva prvi pristigli odgovor ugasio traku dok ostali još
      stižu. Pokriveno sa 5 testova. `licenca` je namerno izostavljena: ona već gasi svoje dugme
      dok proverava, što je uža i bolja povratna informacija od trake preko cele strane.

- [x] **7. Forme magacina se otvaraju ispod prevoja.**
      „Ulaz robe" i „Popis" iscrtavali su se *posle* tab grupe, van ekrana čim ima više od
      nekoliko sastojaka, i ništa ih nije dovlačilo u vidno polje. Svaka druga forma u aplikaciji
      je dijalog.

      Rešeno: `stock-entry.dialog.ts` i `stock-count.dialog.ts`. Uz premeštanje su dobile i ono
      što panel nije imao — ulaz robe računa ukupno i novo stanje pre slanja, popis prikazuje
      manjak/višak u boji i proverava razlog pre poziva. Mrtav CSS i četiri uvoza modula koje
      stranica više ne koristi obrisani.

- [x] **8. Nedostaju prazna stanja**: tab „Zalihe" u magacinu, pločice master pregleda pre
      učitavanja, i cela stranica `restorani/:id` pre učitavanja (potpuno prazna).
      Sva prazna stanja su uslovljena i sa `!loading.active()`, da poruka „nema ničega" ne
      bljesne preko ekrana dok podaci još stižu.

- [x] **9. Sala nema `<h1>`.** Svaka druga stranica ga ima. Dodat, vizuelno tih — sala je ono
      što konobar čita, ne naslov iznad nje — ali stranica se sada predstavlja čitaču ekrana kao
      i sve ostale.

**Runda 2 zatvorena.** Sva tri projekta se grade, 52 testa prolaze (28 `shared` + 24 `pos`;
bilo 47 — dodato pet za `LoadingState`).

## Runda 3 — traži odluku

- [x] **10. Poruke grešaka sa backenda stižu na engleskom** („Only a settled bill can be
      reversed") u interfejsu koji je inače na srpskom. Ovo je jedina otvorena stavka Faze 17 u
      `TODO.md`, sa dve ponuđene mogućnosti i nedonetom odlukom.

      **Odluka: mapiranje u interceptoru.** API ostaje netaknut za master host i za sve buduće
      klijente — vezivanje ugovora za jedan jezik je teško vratiti. Cena te odluke je
      `shared/http/messages.ts`: pošto stabilnog `code` polja nema osim `LICENSE_EXPIRED`, mapa
      hvata sam engleski tekst, pa preformulisana poruka na backendu tiho prestane da se prevodi.
      Zato je fallback originalni string a nikad zamena: neprevedena engleska rečenica i dalje
      kaže šta je pošlo naopako, dok bi „Nepoznata greška" bacila jedini koristan deo.
      Oko 120 poruka pokriveno, interpolirane vrednosti se čuvaju kroz capture grupe
      („Table 12 was not found" → „Sto 12 nije pronađen"). 8 testova, prvi od njih na fallbacku.

- [x] **11. Cela aplikacija ima četiri prelomne tačke.**
      `order` na 900px, shell na 1100px, `menu`/`raspored`/`pregled`/`restorani-detalji` na 1000px.
      Ništa drugo se ne prilagođava: `racuni`, `rezervacije`, `zaposleni`, `izvestaji`, `magacin`
      i `smene` su široke `mat-table` koje na tabletu — navedenom ciljnom uređaju — prosto ispadaju
      iz ekrana.

      **Odluka: kartice ispod 900px.** Ispod prelomne tačke red postaje kartica, a ćelija označena
      linija. Oznake dolaze iz CSS-a a ne iz `data-label` atributa na četrdesetak ćelija: Material
      već stavlja klasu `cdk-column-{ime}` na svaku ćeliju, pa se oznaka kači na kolonu umesto na
      ćeliju — nijedan šablon nije menjan. Mixin je `shared/styles/_responsive-table.scss`.

      `smene` su izuzetak i namerno skroluju vodoravno: nedeljni raspored je konobari × dani, a
      smena izvan svoje kolone ne znači ništa. Skrol stoji na mreži a ne na stranici, pa se dve ose
      ne tuku.

- [x] **12. Devet mogućnosti koje backend nudi a interfejs ne dohvata:**
      `updateRoom` (preimenovanje prostorije, promena platna), `updateTable` (broj, kapacitet,
      deaktivacija), `deleteTable`, `rotateQrCode` — *tekst u samom QR listu upućuje korisnika da
      „obnovi token stola", a dugmeta za to nema* — `changeNotes` (napomene se prikazuju na računu
      i na otisku, ali se nigde ne mogu uneti), `stockEntries`, `lowStock`, `deleteShift`, te
      izmena i brisanje šablona smene.

      Urađeno: obnova QR tokena (uz potvrdu koja kaže da odštampani kodovi prestaju da rade, i uz
      otvaranje novog koda za štampu odmah — obnovljen token bez odštampanog koda je sto sa kog
      niko ne može da poruči); izmena stola (broj, mesta, upotreba) i brisanje stola;
      izmena prostorije (naziv, platno, redosled) sa proverom koji bi sto ostao izvan platna
      *pre* slanja; napomene uz stavku računa; izmena šablona smene i povlačenje iz upotrebe.

      **Odstupanje:** „brisanje" šablona je zapravo povlačenje (`isActive: false`) — backend
      namerno nema DELETE, jer šablon iz kog su smene već generisane ne sme da nestane.
      Dugme i potvrda to i kažu, umesto da obećaju brisanje.

      Nije rađeno: `stockEntries` i `lowStock` (podaci koje magacin već pokriva kroz knjigu
      prometa i kolonu „ispod minimuma") i `deleteShift` (ad-hoc brisanje pojedinačne smene).

      **Uz put nađena i zatečena greška:** `addRoom`, `createTable` i `removeRoom` su zvali
      `load()`, koji kroz `reset()` gasi `dirty` i briše nesačuvano prevlačenje — vlasnik rasporedi
      salu, doda sto, i raspored se tiho vrati na staro. Uvedeno je `guardUnsaved()`; kod radnji
      koje ionako traže potvrdu upozorenje je uklopljeno u postojeću poruku, jer bi drugi dijalog
      povrh prvog samo naučio vlasnika da oba preskače nepročitana.

- [x] **13. Izveštaji su vlasnikov ekran i nemaju nijedan grafikon** — četiri tabele — dok ga
      master pregled, manje važan od njih, ima.

      **Odstupanje od zadatka:** traženo je „promet, gotovina/kartica" na jednom grafikonu. To bi
      bio složeni stubac po danu, i tu se ne sme raditi: storno se knjiži kao **negativan** iznos u
      korpu načina plaćanja koji poništava, pa dnevna gotovina može da ode ispod nule — a segment
      složenog stupca ne može pošteno da bude negativan. Zato su dva pitanja dobila dve forme:
      *kako je period išao* su stubci po danima na nultoj liniji (jedna serija), a *čime je
      naplaćeno* je jedan mereni pojas za ceo period. Tačne brojke su i dalje u tabeli ispod.

      Paleta pojasa (`#C07C2E`, `#00949C`, `#7B52A8`) nije birana okom nego provučena kroz
      validator iz `dataviz` skilla: prolazi opseg svetline, prag hrome, razdvojenost za daltonizam,
      prag za normalan vid i kontrast prema površini. Brend-petrolej je **pao** kao boja podatka
      (pretaman, presiv) — što je uredu, primarna boja je birana za hrom a ne za grafikon.

      Grafikon je i pogledan, ne samo napisan: kroz Chrome, na 30 dana sa nedeljnim ritmom, jednim
      nultim i jednim negativnim danom. Nađeno i popravljeno pri gledanju — oznake `0` i `-4k` su
      se sudarale kad je negativni raspon mali, a tooltip je bio zakucan za vrh pa je kod negativnog
      dana stajao daleko od svog stupca. 10 testova geometrije.

- [x] **14. Nema tamne teme.** Oba `styles.scss` drže `color-scheme: light`.
      **Ostaje svesna odluka, sada zapisana:** kasa radi po ceo dan na jednom ekranu u osvetljenom
      lokalu, a otisak računa je crn na belom. Tamna tema bi tražila drugu paletu za stanja stolova
      i drugu proveru grafikona, i nije besplatna.

- [x] **15. Izgled je prepoznatljivo podrazumevani Angular Material.**

      Rađeno na nivou tokena, bez razbijanja ijedne Material komponente.

      **Boja.** Kasa dobija petrolej-mastilo `#0E4F52`, master šljivu `#4A2C4D`; obe su prave M3
      tonalne palete generisane kroz `ng generate @angular/material:theme-color`, ne najbliža
      gotova paleta. Petrolej je biran i zato što stoji daleko od sva četiri statusna tona, pa hrom
      nikad ne takmiči sa stolom koji traži pažnju. Statusne boje su ostale netaknute.

      **Tipografija.** Archivo za naslove i hrom, Inter za tekst i tabele, IBM Plex Mono za brojeve.
      Sva tri nose srpske dijakritike, što ne važi za svako display pismo.

      **Potpis** je ono što projekat već ima a nije koristio: otisak računa. To je stvarni artefakt
      ovog posla — 80 mm fiksne širine, poravnate kolone — pa svaki broj u aplikaciji dobija to
      pismo. `font-variant-numeric: tabular-nums` je već bio ručno pisan na 12 mesta; sada je pravilo
      umesto izuzetka, a cifre se poravnavaju zato što su iste širine, ne zato što se neko setio
      font feature-a.

      **Uz put nađena greška:** otisak računa je tražio `'Roboto Mono'`, pismo koje nikad nije bilo
      učitano — svaki odštampan račun izlazio je u onome što je platforma slučajno imala.

      **Gustina** je razdvojena po nameni: kasa ostaje na 0 (radi se stojeći, prstom), master ide na
      -1 (radi se sedeći, mišem, nad tabelama licenci).

      Dodato i ono što nije postojalo: vidljiv fokus tastature i poštovanje `prefers-reduced-motion`.

**Runda 3 zatvorena.** Sva tri projekta se grade, **71 test** prolazi (37 `shared` + 34 `pos`;
bilo 43 na početku dana).

---

## Ostalo posle 1. septembra

Petnaest stavki iznad je zatvoreno, ali „zatvoreno" nije isto što i „nema više šta da se radi".
Ovo je ono što je ostalo, poređano po tome koliko košta ako se zaboravi.

### Provera

- [ ] **Ništa nije prokliktano uživo.** Grafikon i responsive kartice su gledani u Chrome-u, ali
      kroz harness: kompajliran pravi mixin nad DOM-om koji Material stvarno emituje, i verna
      transkripcija geometrije grafikona. To je pouzdano za pitanje koje je postavljeno — CSS i
      geometrija — ali **nije isto što i živa aplikacija** sa backendom i pravim podacima.
      Kad se digne ceo stek (četiri procesa, vidi `README.md`), proći kroz `/izvestaji`, `/racuni`,
      `/magacin` i `/smene`, i to na širini tableta a ne samo na desktopu.
      Ovo je najveća preostala stavka: sve ostalo ispod je poznato i ograničeno, a ovo može da
      otvori nepoznato.

- [ ] **Rad nije komitovan.** Stoji u radnom stablu.

### Nedoslednosti koje je ovaj rad ostavio za sobom

- [ ] **Tri `mat-table` nisu dobile responsive tretman.**
      - `pos/features/menu/menu.page.ts` — **propušteno.** Nije bilo na spisku od šest jer stranica
        već ima prelomnu tačku na 1000px za dvokolonski raspored, ali sama tabela (4 kolone) i dalje
        ne stakuje. Manje hitno od `racuni` sa 8 kolona, ali je nedoslednost unutar iste aplikacije.
      - `master/restaurants.page.ts` (6 kolona) i tabela uplata u `master/restaurant-detail.page.ts`.
        Master nije bio u dogovorenom obimu, ali ispada iz ekrana isto kao i kasa.

- [ ] **Dve nove budžetske opomene, napravljene ovim radom.**
      `reports.page.ts` je sada 5,59 kB stila prema budžetu od 4 kB, `inventory.page.ts` 4,04 kB —
      oba zbog blokova sa oznakama kolona za `rt.stacked`. Opomene su, ne greške (prag za grešku je
      8 kB), ali je build bučniji nego što je bio.
      Rešenje: izmestiti ponovljene `@media` blokove u parametrizovan mixin, umesto da svaka
      stranica nosi svoj.

- [ ] **Dva grafikona u sistemu koji bi trebalo da bude jedan.**
      „Prihod po mesecima" u master pregledu su i dalje div-stubci sa sopstvenom logikom skaliranja,
      dok Izveštaji sada imaju SVG grafikon sa validiranom paletom, tooltipom i testovima geometrije.
      Master nije bio u obimu, ali razlika se vidi.

- [ ] **`TODO.md` linija 548 i dalje vodi jezik grešaka kao otvorenu stavku**, iako je odrađen
      (`shared/http/messages.ts`). Fazu 17 treba zatvoriti i pokazati odatle na ovaj fajl.

- [ ] **Početni bundle: 736 kB (kasa), 660 kB (master)** prema budžetu od 500 kB. Zatečeno stanje
      je bilo 718 kB / 640 kB — poraslo je oko 18 kB od tri porodice fontova i grafikona.
      Nije hitno za diplomski, ali budžet stoji prekoračen otkad postoji.

### Namerno nije rađeno

- **`stockEntries`, `lowStock`, `deleteShift`** — vidi obrazloženje uz stavku 12.
- **Tamna tema** — vidi stavku 14. Odluka, ne propust.
- **Četiri produkcijske stavke iz `TODO.md`** (ključ za potpisivanje tokena, `SeedDemoData`,
  CORS/HTTPS, strani ključ na `RestaurantId`). Tamo su eksplicitno označene kao „nije za diplomski".

---

## Provereno pa odbačeno — nisu greške

- **Legenda sale ne prikazuje „Van upotrebe", i tako treba.**
  U prvom prolazu sam ovo zapisao kao propust. Nije: `/sala` zove `floorPlan()` sa
  `includeInactive = false`, upit filtrira `table.IsActive`
  (`GetFloorPlanQueryHandler.cs:24`), a `OutOfService` je jedini status koji traži `!isActive`
  (`TableStatusRules.cs:40`). Sto van upotrebe se na tom ekranu ne može pojaviti, pa bi ključ za
  njega u legendi objašnjavao boju koju niko nikada ne vidi.
  Grane za `OutOfService` u `colour()` i `background()` jesu nedostižne sa ovog ekrana, ali su
  odbrambene i jeftine — ostaju.
