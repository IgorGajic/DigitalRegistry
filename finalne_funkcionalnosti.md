# Finalne funkcionalnosti

Dve stvari koje se dodaju posle zatvorenog UI/UX prolaza (`Frontend/BUGS.md`):

1. **Elementi prostorije** — vlasnik crta šank, toalet, ulaz i slično na rasporedu, da konobar lakše
   čita salu.
2. **Izbor teme** — vlasnik bira paletu aplikacije za svoj restoran.

Zadaci se rade **jedan po jedan, odozgo nadole**. Redosled nije proizvoljan: unutar faze svaki
zadatak zavisi od prethodnog, a Faza A ide pre Faze B jer teme diraju svaki ekran — bolje je da
raspored već bude gotov kad krenemo da proveravamo boje na njemu, nego da isti posao radimo dvaput.

---

## Odluke donete pre početka

Zapisane ovde da se ne preispituju usred posla.

### Zajedničko

| Tema | Odluka | Zašto |
| :--- | :--- | :--- |
| Element **nije** sto | Zaseban entitet `RoomFixture`, ne `Table` sa `IsActive = false` | Sto koji nije sto i dalje bi mogao u rezervacije, dobio bi QR token i brojao se u kapacitet. Zamka koja se plaća kasnije |
| Boja elementa | **Ton iz imenovanog skupa**, ne heks iz birača | Na `/sala` boja **znači status**. Slobodan birač pre ili kasnije da šank u nijansi zauzetog stola. Isti razlog zbog kog je petrolej biran da stoji daleko od sva četiri statusna tona |
| Ton se čuva kao **enum**, ne kao heks | `FixtureTone.Wood`, ne `#6B4A2F` | Kad stigne Faza B, ton se pretoči po temi sam od sebe. Sačuvan heks bi ostao svetli šank na tamnoj podlozi |
| Element nikad iznad stola, nikad klikabilan | `pointer-events: none`, van tab redosleda, van `describe()` | Konobar ne sme da „otvori toalet", a šank preko stola bi sakrio dugovanje |

### Naziv elementa — tip **i** slobodan naziv

Pitanje je bilo: gotova lista tipova ili slobodno kucanje. **Oba**, i to nije kompromis nego jedina
tačna varijanta: tip nosi ikonu, podrazumevanu veličinu, ton i podrazumevani naziv, a naziv se može
prepisati — jer lokal sa dva toaleta treba „WC M" i „WC Ž", a lista tipova to ne može da zna.

Tipovi: **Šank, Toalet, Ulaz, Kuhinja, Stepenice, Zid/pregrada, Ostalo.**

### Teme — koje četiri i zašto baš te

| Tema | Podloga | Napomena |
| :--- | :--- | :--- |
| **Petrolej** | svetla (sadašnja) | Podrazumevana, ostaje netaknuta |
| **Ugalj** | tamno siva | Pravi „dark mode" |
| **Šumska** | tamno zelena | Traženo |
| **Pesak** | **svetla topla** | Braon, ali svetlo |

**Pesak je namerno svetao.** Crvena „zauzet" i narandžasta „rezervisan" su hue-om najbliže braon od
svega u aplikaciji; tamno braon podloga je jedini slučaj u kom bi statusne boje počele da se stapaju
sa hromom. Svetla peščana daje istu toplinu bez toga.

### Gde tema živi

Po **restoranu** (`Restaurant.Theme`), ne po korisniku — vlasnik bira za lokal, konobar je zatiče.

**Prijava ne zna koji je restoran**, pa ostaje u podrazumevanoj temi; tema se primeni posle prijave.
Da to ne izgleda kao bljesak boje, poslednja tema se pamti u `localStorage` i primenjuje odmah, a
odgovor sa servera je ispravlja ako se ne slaže.

---

## Faza A — Elementi prostorije

Uklapa se u postojeću mašineriju skoro bez otpora: `Room` je već koordinatni prostor
(`CanvasWidth`/`CanvasHeight`), `SaveRoomLayoutCommand` već snima celu prostoriju odjednom i već
proverava staje li nešto u platno, a editor već ima `cdkDrag` sa granicom i klizače za veličinu.

### Backend

- [x] **A1 — Domen.** `FixtureKind`, `FixtureShape` (`Rectangle`, `Ellipse`) i `FixtureTone`
      (`Wood`, `Slate`, `Stone`, `Glass`) u `Domain/Enums`; `RoomFixture : BaseEntity, IRestaurantScoped`
      u `Domain/Entities` sa `RoomId`, `Kind`, `Label`, `Shape`, `Tone`, `PositionX/Y`, `Width`,
      `Height`, `Rotation`, `DisplayOrder`. `Room.Fixtures` kolekcija.

      `FixtureShape` je nov enum a ne `TableShape`: taj kaže „kako se **sto** crta", i razlika
      kvadrat/pravougaonik postoji samo zbog stolova. Pravougaonik jednakih strana je kvadrat,
      elipsa jednakih strana je krug — dva oblika su dovoljna i poštena.

      `RoomId` je **obavezan**, za razliku od `Table.RoomId`. Sto van prostorije i dalje prima
      porudžbine; element van prostorije ne znači ništa.

- [x] **A2 — Baza.** `RoomFixtureConfiguration` (kaskadno brisanje uz prostoriju, `Label` max 30,
      indeks po `RoomId`), `DbSet<RoomFixture>` na `IDigitalRegistryDbContext` i `ApplicationDbContext`,
      migracija `AddRoomFixtures`. Proveriti da global query filter hvata novi entitet.

- [x] **A3 — DTO-ovi.** `RoomFixtureDto` i `FixtureLayoutRequest` u `FloorPlanDtos.cs`;
      `RoomDto` dobija `IReadOnlyList<RoomFixtureDto> Fixtures`.
      `FixtureLayoutRequest.Id` je **nullable** — `null` znači nov element.

- [x] **A4 — Čitanje.** `GetFloorPlanQueryHandler` učitava i projektuje elemente.
      Elementi se vraćaju uvek, i kad je `includeInactive = false`: oni nemaju stanje.

- [x] **A5 — Snimanje.** `SaveRoomLayoutCommand` dobija listu elemenata; handler pravi nove, menja
      postojeće i **briše** one kojih nema u listi.

      Pažnja na razliku koju treba i u kodu objasniti: sto koji nedostaje u listi se *ispisuje iz
      prostorije* (`RoomId = null`), element koji nedostaje se *briše*. Sto ima život van prostorije,
      element nema.

      Provera „staje li u platno" se koristi ista kao za stolove, i poruka mora da imenuje element
      („Šank ne staje u prostoriju 1200×800"), ne samo da kaže da nešto ne staje.

- [x] **A6 — Validacija.** `SaveRoomLayoutCommandValidator`: dužina naziva, najmanja i najveća
      veličina, gornja granica broja elemenata po prostoriji (da jedan zahtev ne može da ubaci
      hiljadu), i da su `Kind`/`Shape`/`Tone` definisane vrednosti enuma.

- [x] **A7 — Testovi.** Dopuniti `FloorPlan/SaveRoomLayoutCommandHandlerTests.cs`: nov element se
      pravi; postojeći se pomera; izostavljen se briše; element van platna se odbija; element tuđeg
      restorana se ne dohvata. Uz to test da brisanje prostorije nosi i njene elemente.

> **Nađeno pri radu na A4:** `UpdateRoomCommandHandler` proverava koji bi **sto** ostao van platna
> kad se prostorija smanji, a element može da ostane napolju isto tako — i podjednako je nedohvatljiv.
> Provera je proširena i na elemente, uz poruku koja imenuje element njegovim nazivom.

> **Migracije se prave sa Infrastructure kao startup projektom**, ne sa Api: paket
> `Microsoft.EntityFrameworkCore.Design` je tamo sa `PrivateAssets: all`, pa ne dolazi do Api-ja.
> Postoji `DesignTimeDbContextFactory` baš zbog toga.
> `dotnet ef migrations add <Naziv> --project Backend/src/DigitalRegistry.Infrastructure --startup-project Backend/src/DigitalRegistry.Infrastructure`

### Frontend

- [x] **A8 — Model.** `FixtureKind`, `FixtureShape`, `FixtureTone` u `shared/models/enums.ts`;
      `RoomFixtureDto`, `FixtureLayoutRequest` i `RoomDto.fixtures` u `dtos.ts`; srpski nazivi i
      podrazumevani naziv po tipu u `shared/format/labels.ts`.

- [x] **A9 — Tonovi.** Četiri para CSS promenljivih (`--dr-tone-wood`, `--dr-tone-wood-line`, …) u
      `pos/src/styles.scss`, uz postojeće statusne boje. Faza B ih pretače po temi.

- [x] **A10 — `/sala`.** Elementi se crtaju **ispod** stolova, kao `<div>` a ne `<button>`, sa
      `pointer-events: none` i `aria-hidden`. Naziv se ispisuje na elementu ako staje.

- [x] **A11 — `/raspored`.** Sloj elemenata sa istim `cdkDrag`; panel „Dodaj element" sa tipovima;
      bočni panel dobija granu za izabrani element (naziv, tip, oblik, ton, veličina, rotacija,
      brisanje); `dirty`/`guardUnsaved` obuhvataju i elemente; snimanje šalje obe liste.

      Selekcija je zajednička za stolove i elemente — izabrano može biti samo jedno.

- [x] **A12 — Testovi.** `floor.page.spec.ts`: element se iscrtava, **nije** dugme, nije u tab
      redosledu, i stoji ispod stola u z-redosledu.

- [x] **A13 — Živa provera.** Podići stek, u `/raspored` napraviti šank i dva toaleta, snimiti,
      osvežiti, pa pogledati `/sala` — na desktopu i na 820 px. Zabeležiti u `Frontend/BUGS.md`.

**Provera na kraju faze — prošla.** Sva tri projekta se grade bez ijednog upozorenja; **215**
backend testova (bilo 208) i **86** frontend (53 `shared` + 33 `pos`; bilo 82).

Živa provera: nacrtan šank i dva toaleta preimenovana u „WC M" i „WC Ž", prevučeni, snimljeni,
preživeli osvežavanje; na `/sala` stoje ispod stolova, klik na element ne otvara ništa
(`pointer-events: none`), `aria-hidden` stoji, i svih osam fokusabilnih elemenata na platnu su
stolovi. Na 820 px se skaliraju sa platnom i nijedan ne izlazi iz njega.

> **Nađeno pri radu na A11:** `.ed__side mat-card-content > button { width: 100% }` postoji namerno
> — akcije izabranog stola su rečenice i ne smeju da se seku na tabletu. Ali hvatalo je i sedam
> jednorečnih dugmadi za dodavanje elementa i gurnulo panel ispod pregiba. Nadjačano ciljano.

> **Dvaput napravljena ista greška:** backtick u komentaru unutar `styles` template literala razbija
> fajl. Jednom u `shell.ts`, jednom ovde. U tim blokovima se piše bez backtick-ova.

---

## Dorade posle Faze A

Tri stvari tražene kad je Faza A već bila na ekranu — ono što se vidi tek kad se gleda.

- [x] **Sala nikad ne izlazi iz vidokruga.** Platno je držalo odnos stranica i punu širinu, pa je na
      širokom ekranu visina ispadala ispod pregiba. Sada je širina ograničena visinom koja stvarno
      preostaje: `width = raspoloživa visina x odnos`.

      Raspoloživa visina se **meri, ne pogađa**. Iznad platna su toolbar, naslov, tabovi i legenda —
      legenda se pri tom lomi na uskom ekranu — pa bi konstanta oduzeta od visine prozora bila
      pogrešna prvi put kad se bilo šta od toga pomeri, i to nečujno. Sve ispod platna se meri kao
      jedan raspon, od donje ivice platna do dna stranice, umesto da se sabira po delovima.

      **Sabiranje po delovima je i bio prvi pokušaj i promašio je za 16 px** — napomena o
      neraspoređenim stolovima je paragraf i nosi margine koje nisam uračunao.
      **Drugi promašaj je bio moj pod od 260 px:** na prozoru visokom 520 px računica daje 219, pod
      ga diže na 260, i stranica se prelije za tačno tih 29 px. Pod je spušten na 200 i sada je samo
      zaštita od besmislenih visina. Provereno na 520, 700 i 1100 px visine — nula prelivanja.

- [x] **Elementi bez naziva na sali, sa nazivom u editoru.** Osoblje zna svoju salu; na radnom
      ekranu bi natpisi ponavljali ono što oblik i mesto već kažu, i to na jedinom mestu gde je svaki
      drugi tekst broj stola ili dugovanje. Stolovi zadržavaju sve svoje podatke.

- [x] **Rotacija pravougaonika za 45°, levo i desno.** Dva dugmeta u panelu elementa, i to **samo za
      pravougaonike** — elipsa jednakih strana je krug, a elipsa nejednakih je ista svakih 180°.
      Ugao se prelama u 0–359 (levo od 0 daje 315, ne −45): API prima samo taj opseg, a „−45" i „315"
      su isti crtež i ne treba im dva zapisa.

      **Ostavljeno kako jeste:** provera „staje li u platno" gleda nezaokrenutu geometriju, pa
      zaokrenut element blizu ivice može biti odsečen — platno ima `overflow: hidden`. Vlasnik to
      vidi odmah u editoru i pomeri ga; računanje zaokrenutog okvira ne plaća se za ovo.

      **Nije rađeno:** stolovi imaju isto polje `Rotation` i `/sala` ga već iscrtava, ali kontrole
      nemaju. Traženo je za elemente; iste dve dugmadi na stolu su jedan potez ako zatreba.

---

## Faza B — Izbor teme

Kod je mali deo posla. Pravi posao su boje — i to je tačno ono što je `BUGS.md` stavka 14 svesno
odbila („tražila bi drugu paletu za stanja stolova i drugu proveru grafikona, i nije besplatna").
Sada se ta cena plaća namerno, pa neka bude vidljiva u zadacima.

- [ ] **B1 — Preduslov: boje grafikona iz koda u promenljive.** `turnover-chart.ts` ima tri zakucana
      heksa (`#C07C2E`, `#00949C`, `#7B52A8`) za pojas načina plaćanja. Dok su u TS-u, tema ih ne
      može dodirnuti. Izmestiti u CSS promenljive.

      `bar-chart.ts` je već na `--mat-sys-*` tokenima i ne dira se.

- [ ] **B2 — Domen.** `AppTheme` enum (`Petrol = 1`, `Charcoal = 2`, `Forest = 3`, `Sand = 4`) i
      `Restaurant.Theme` sa podrazumevanim `Petrol`.

- [ ] **B3 — Baza.** `RestaurantConfiguration` dopuna, migracija `AddRestaurantTheme`.

- [ ] **B4 — Application.** `RestaurantSettingsDto`, `GetRestaurantSettingsQuery`,
      `UpdateRestaurantThemeCommand` + validator. Nova politika `ManageRestaurantSettings` = Vlasnik
      u `AuthorizationPolicies`.

      Nova politika, a ne `ManageStaff`: ta je opisana kao „politika koja odlučuje ko uopšte može
      da priđe bilo čemu", i tema pod njom bi je razvodnila.

- [ ] **B5 — API.** `SettingsController`: `GET /api/settings` čita svako prijavljeno osoblje
      (konobaru treba tema kao i vlasniku), `PUT /api/settings/theme` samo vlasnik.

- [ ] **B6 — Testovi.** Integracioni: tema se snimi i pročita; konobar dobija **403** na upis a
      **200** na čitanje; nepoznata vrednost daje 400.

- [ ] **B7 — `shared/theme`.** `ThemeService`: postavlja `data-dr-theme` na `<html>`, pamti poslednju
      temu u `localStorage` i primenjuje je odmah pri podizanju, pa je ispravlja odgovorom sa
      `GET /api/settings`. Odjava briše zapamćenu temu.

- [ ] **B8 — Tokeni po temi.** Blokovi `:root[data-dr-theme="..."]` u `pos/src/styles.scss` za sve
      četiri teme. Svaki blok pokriva: M3 površine, `color-scheme`, **četiri para statusnih boja**,
      tonove elemenata iz A9, i boje pojasa iz B1.

      Podrazumevana tema ostaje na golom `:root`, kao što je sada.

- [ ] **B9 — Provera boja.** Najskuplji zadatak i jedini koji se ne može otaljati.
      Za svaku temu: četiri statusne boje moraju biti razdvojive međusobno i čitljive na svojoj
      podlozi, a paleta grafikona se ponovo provlači kroz validator iz `dataviz` skilla (opseg
      svetline, prag hrome, razdvojenost za daltonizam, kontrast prema površini).

      Pastelne `-bg` vrednosti (`#e8f5e9` i ostale) su računate za svetlu podlogu i na tamnoj se
      **ne vide** — tamnim temama treba sopstveni par, ne posvetljena ista vrednost.

- [ ] **B10 — Ekran za izbor.** Ruta `/podesavanja`, samo za vlasnika, sa stavkom u meniju.
      Izbor teme sa pregledom uživo (uključujući mustru sa četiri statusa stolova, jer se tema bira
      zbog njih). Ovo je i mesto gde buduća podešavanja mogu da sednu.

- [ ] **B11 — Štampa.** Otisak računa i list QR kodova ostaju crno na belom bez obzira na temu.
      Postojeći `@media print` blok već tera `#fff` na površinu dijaloga — proveriti da to važi i
      za tekst i za obe tamne teme.

- [ ] **B12 — Testovi.** `theme.service.spec.ts`: primena atributa, pamćenje i čitanje iz
      `localStorage`, ponašanje kad je zapamćena vrednost besmislena, i brisanje na odjavi.

- [ ] **B13 — Živa provera.** Svaka tema kroz `/sala` (četiri statusa istovremeno), `/izvestaji`
      (grafikon i pojas) i otisak računa. Zabeležiti u `Frontend/BUGS.md`.

**Provera na kraju faze:** kao za Fazu A, plus `dotnet test` zbog nove politike.

---

## Van obima

- **Slobodan birač boje**, i za elemente i za temu — vidi odluke gore.
- **Tema po korisniku.** Vlasnik bira za lokal; ako konobar želi svoju, to je druga funkcionalnost
  sa drugim mestom čuvanja.
- **Master aplikacija ne dobija izbor teme.** Ona je administracija platforme i njena šljiva postoji
  baš zato da se ne pomeša sa kasom.
- **Elementi ne ulaze u izveštaje ni u rezervacije.** Oni su crtež, ne inventar.

---

## Rizici

- **Migracije traže podignut SQL Server**, a `dotnet build` pada dok API-ji drže DLL-ove — gasiti ih
  pre gradnje (viđeno u ovoj sesiji).
- **`ng build shared` uz pokrenut `ng serve` ostavlja watcher na starim tipovima** i prijavljuje
  greške kojih u kodu nema. Posle svake izmene u `projects/shared` restartovati serve.
- **Statusne boje su najosetljiviji deo Faze B.** One su radni rečnik sale i čitaju se preko
  prostorije; ako neka tema tu popusti, tema je pogrešna — ne boje.
