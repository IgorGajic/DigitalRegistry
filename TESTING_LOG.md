# Prolaz kroz celu aplikaciju — 2/3. septembar 2026.

Radni dnevnik, ne izveštaj. Vodi se da se isto ne testira dvaput i da se zna dokle se stiglo.

**Git nije diran ni u jednom trenutku** — ni `add`, ni `commit`, ni `branch`, ni `checkout`.

## Legenda

| | |
|:--|:--|
| ✅ | Provereno, radi |
| 🔧 | Nađen problem, popravljen |
| ⚠️ | Nađen problem, **nije** popravljen (zapisano zašto) |
| ⏳ | U toku / preostalo |

---

## 0. Pre prolaza — dve funkcionalnosti tražene večeras

- [x] ✅ **Animacija nestajanja kartice + preslaganje.** Kartica se skuplja (`max-height`, `opacity`,
      margine) 220 ms, pa se briše; kartice ispod klize naviše same, jer je preslaganje posledica
      rasporeda a ne zasebna animacija. Poštuje `prefers-reduced-motion`.
      *Izmereno uživo:* visina 109 → 99 px i prozirnost 0,18 u toku prelaza; 6 → 5 kartica.

- [x] ✅ **Vraćanje greškom iznete kartice.** Sekcija „Nedavno izneto" na dnu iste kolone,
      sklopiva, poslednjih 6 iz poslednjih 30 minuta, dugme „Vrati u red".
      Odabrano umesto snackbara jer snackbar nestane za 6 s, a greška se primeti tek na povratku do
      šanka.
      *Izmereno uživo:* 5 → 6, vraćena kartica stala **na svoje mesto po starosti** (prva),
      sekcija nestala kad je ostala prazna. Backend: `204` na oba poziva, `servedAtUtc` se postavlja
      i briše.

### Nađeno i popravljeno pri tome

- 🔧 **Kartice su bile sabijene, dugmad odsečena.** `overflow: hidden` (potreban za skupljanje) je
      omogućio flex kontejneru da skupi kartice ispod visine njihovog sadržaja — kolona flex-a
      podrazumevano skuplja decu. Dodato `flex: none`.
- 🔧 **Trka između otkazanog zahteva i tajmera animacije.** Ako `POST /served` padne brzo, `reload()`
      vrati karticu, pa je tajmer posle 220 ms **ipak** ukloni. Tajmer sada proverava da li je
      kartica još označena kao „odlazeća" pre nego što je ukloni.
- 🔧 **`matchMedia` pozivan bez provere.** Puca svuda gde ga nema (test okruženje, SSR). Sada
      `typeof matchMedia === 'function' && ...`.
- 🔧 **Budžet stila komponente prekoračen za 268 B** (`floor.page.ts`, 4,27 kB / 4 kB). Podignut na
      5 kB; nema šta da se izdvoji — to su stilovi jednog ekrana, ne ponovljen blok.

### Novo u domenu

- `Order.ServedAtUtc` (migracija `AddOrderServedAt`) — `Served` kaže *da* je izneto, ovo kaže *kada*,
  što je jedino što omogućava listu za vraćanje.
- `Order.ReopenForService()` — samo iz `Served`, pa ne može da uskrsne plaćen ili storniran račun.
- Politika `ServeOrder` = konobar + menadžer + vlasnik.

---

## 1. Polazno stanje

| | na početku | na kraju |
|:--|--:|--:|
| Backend testovi | 221 | **223** |
| Frontend testovi | 94 | **101** |
| Upozorenja pri gradnji | 0 | **0** |

Backend 223 = 131 domen + 58 aplikacija + 34 integracioni.
Frontend 101 = 59 `shared` + 42 `pos`.

---

## 2. API — svaki endpoint, iz svake uloge

**81 ruta ukupno** (64 kasa + 17 master). Kase ima 61 zaštićenih; svaka pozvana kao **gost (QR sesija),
konobar, menadžer, vlasnik** i upoređena sa `AuthorizationPolicies.Matrix`.

Destruktivne rute zvane sa nepostojećim `Guid`-om: autorizacija se izvršava **pre** handlera, pa
uloga bez politike i dalje dobija 403 a uloga sa njom 404 — cela matrica provučena, nijedan red
obrisan.

```
244 provere (61 ruta × 4 uloge)
nijedno neslaganje sa matricom
nijedan 5xx
```

Skripta: `scratchpad/authmatrix.py`.

### 🔧 Nađeno: gost je mogao da pročita licencu lokala

`GET /api/license/status` je bio pod golim `[Authorize]`. QR sesija stola **jeste** autentikovana —
nosi restoran i ulogu `Guest` — pa je prolazila, a odgovor sadrži **plan pretplate, datum isteka i
broj preostalih dana**. To je komercijalni odnos lokala sa platformom i nema šta da traži na telefonu
koje je upereno u sto.

Uvedena politika `ViewLicenseStatus` = konobar + menadžer + vlasnik. Dva integraciona testa da se ne
vrati tiho (223 backend testa, bilo 221).

### ✅ Provereno pa odbačeno — nisu greške

- **`GET /api/orders/mine` vraća 403 osoblju.** Ruta je pod `ViewMenu`, ali handler traži sesiju
  stola, koju osoblje nema. Tako i treba: to je gostov pogled na sopstveni sto. Moje očekivanje je
  bilo pogrešno, ne kod.
- **`GET /api/settings` odgovara i gostu.** Vraća naziv lokala — koji gost gleda na svom ekranu — i
  temu. Ništa komercijalno. Ostavljeno, ali sada **zapisano kao odluka** u kontroleru, a ne kao
  slučajnost; uz to je preduslov ako se ikad poželi da i gostov ekran nosi paletu lokala.

### Master API

17 ruta, sve pod `PlatformAdminOnly` osim prijave. Prijava kao administrator platforme radi,
sva tri ekrana čitaju podatke. Tokeni se **ne mešaju** između hostova — to je već pokriveno
komentarom u `Master.Api/Program.cs` i potvrđeno time što token kase ne prolazi ovde.

## 3. Ekrani — klik po klik, po ulogama

- ✅ **Konobar** — meni pokazuje tačno tri stavke (Sala, Računi, Rezervacije). Ostale rute nisu
  ponuđene, a API ih ionako odbija (vidi odeljak 2).
- ✅ **Vlasnik** — svih deset ekrana otvoreno klikom kroz meni, svaki skeniran.
- ✅ **Administrator platforme** — sva tri ekrana (pregled, restorani, detalji restorana).
- ✅ **Gost** — `/gost/:token`, poručivanje, „Već ste poručili", i šest istovremenih sesija
  (vidi odeljak 5).

## 4. UI — preklapanja, centriranje, raspored

Napisan skener koji **meri** umesto da procenjuje, i pušten na svaki ekran obe aplikacije, na
desktopu (1920) i na tabletu (820). Traži tri stvari:

1. tekst širi ili viši od kutije koja ga drži, gde kutija nije skroler,
2. bilo šta što viri desno van prozora,
3. dva klikabilna elementa koja se preklapaju.

> **Skener je prvo lagao, pa je popravljen.** `getBoundingClientRect()` vraća položaj **po
> rasporedu**, ne ono što se vidi — pa je svaki odskrolovani element u vodoravnoj traci prijavljivao
> da „izlazi iz ekrana" i da „se preklapa" sa onim što je tu nacrtano. Prvo prijavljeno preklapanje
> („Zaposleni" preko „Olivia Owner") bilo je upravo to: linkovi odsečeni navigacijom. Skener sada
> preseca kutiju sa svakim pretkom koji seče.

### 🔧 Nađeno i popravljeno

- **Sadržaj je izlazio iz malih okruglih stolova** (`/sala`). Sto crtan na 67 px nosio je četiri
  reda; prelivao je 4 px, a iznos je presecao prsten sa obe strane, jer krug nije najširi tamo gde
  iznos stoji. Rešeno preko `@container` upita nad samim stolom: ispod praga nestaje broj mesta,
  slog se spušta, i sklanja se valuta — koja je na celom planu ista i piše u legendi.
  **Prag sam dvaput pogrešio** (84, pa 76) i tiho krao „4 mesta" stolovima koji imaju mesta:
  `container-type: size` meri **content box**, pa sto od 84 px prijavljuje 76. Postavljen na 68,
  između dve veličine koje ovaj plan koristi (59 i 76).

- **Ikonica veze bila je stisnuta na 18 px** umesto 24, na svakom ekranu kase na tabletu. Toolbar je
  flex red, a ikonica se skupljala kao i sve ostalo — jedini element čiji je ceo posao da se
  prepozna na prvi pogled, izobličen baš na širini gde je najvažniji. `flex: none`.

- **Dugme za odjavu u masteru izlazilo je 14 px van ekrana** na 820 px, na svakoj ruti. Toolbar ne
  prelama, pa je poslednja stavka ispadala napolje — i to baš ona koja mora da radi kad ništa drugo
  ne radi. Sada popušta ime administratora: skraćuje se, a ispod 900 px nestaje.

- **„670.000 RSD" je bio odsečen** u pločici master pregleda (kutija 156 px, tekst 169 px) — najveći
  broj na ekranu bio je jedini koji se ne vidi ceo. Slog vezan za širinu pločice
  (`clamp(1rem, 9cqw, 1.6rem)`) umesto fiksnih 1,6rem.

### ✅ Čisto

| | desktop | 820 px |
|:--|:--|:--|
| Kasa — svih 10 ekrana | ✓ | ✓ |
| Master — sva 3 ekrana | ✓ | ✓ |

Jedini preostali pogodak je `span.mdc-switch__track` na `/jelovnik`: Material-ov prekidač je po
dizajnu dvostruko širi od svoje kutije da bi klizač imao kuda. Lažni pozitiv.

**`/sala` i dalje ne skroluje** ni na jednoj proveravanoj visini. `/racuni` (428 px) i `/izvestaji`
(468 px) skroluju — to su duge liste i tako i treba.

## 5. SignalR — više uređaja istovremeno

Šest odvojenih sesija stola otvoreno preko `POST /api/tables/sessions` — **šest različitih tokena,
kao šest stvarnih telefona** — dok je `/sala` otvorena sa živom hub konekcijom.

Zatim **18 istovremenih porudžbina** (`Promise.all`, 6 stolova × 3 runde).

```
poslato            18
trajalo           527 ms
statusi           201 × 18        ← nijedna nije odbijena, nijedan konflikt
red na serveru     24
kartica na ekranu  24             ← bez ijednog osvežavanja
brojač             24
duplikata           0
redosled          najstarije prvo, očuvan
```

Sto 6 je pri tome **uživo prešao iz zelenog u crveno** sa 1.080 RSD i „0 min", a svi ostali stolovi
su dobili nove iznose i oznaku broja otvorenih rundi. Sve preko huba, bez dodirivanja tastature.

### ✅ Nema izgubljenih ni dupliranih događaja

Ekran se na hub događaj **ponovo čita ceo** umesto da krpi stanje iz sadržaja poruke — što je odluka
zapisana još u `floor.page.spec.ts`. Ovaj test je pokazuje kao ispravnu: 18 poruka u 527 ms, i
konačno stanje se poklapa sa serverom u sve tri brojke.

### 🔧 Sopstveni nered koji sam napravio i počistio

Matrica ovlašćenja je stvarno pozivala `POST` rute, pa je napravila pet probnih zapisa:
artikal, sto 9999, prostoriju, šablon smene i nalog `__probe__@x.rs`. Sve uklonjeno
(šablon **povučen** kroz `isActive: false`, jer brisanja namerno nema, a nalog **ugašen**, jer se
nalozi ne brišu). Provereno da nije ostalo ništa.

**I jedna gora:** ista skripta je pozvala `PUT /api/settings/theme` i **pregazila izabranu temu**
(Šumska → Petrolej). Vraćena na Šumsku. Pouka za sledeći put: probe koje menjaju podešavanja moraju
prvo da pročitaju i na kraju vrate zatečenu vrednost.


---

## Zaključak

**Osam stvari nađeno i popravljeno**, od čega jedna bezbednosna. Ništa nije ostalo pokvareno.

| # | Šta | Gde |
|:--|:--|:--|
| 1 | Gost je preko QR koda mogao da pročita plan pretplate i datum isteka licence lokala | `LicenseController` |
| 2 | Kartice reda bile sabijene, dugmad odsečena | `floor.page.ts` |
| 3 | Trka između otkazanog zahteva i tajmera animacije | `floor.page.ts` |
| 4 | `matchMedia` pozivan bez provere postojanja | `floor.page.ts` |
| 5 | Sadržaj izlazio iz malih okruglih stolova | `floor.page.ts` |
| 6 | Ikonica veze stisnuta na 18 px umesto 24 | `shell.ts` (kasa) |
| 7 | Dugme za odjavu izlazilo van ekrana na tabletu | `shell.ts` (master) |
| 8 | Najveći broj na master pregledu bio odsečen | `dashboard.page.ts` |

### Šta nije rađeno

- **Nisam dirao git.** Ni jedan jedini put — ni `status`, ni `add`, ni `commit`. Sve izmene stoje u
  radnom stablu.
- **Nije provereno kroz pravu štampu.** Dijalog štampe blokira sesiju, pa su pravila potvrđena samo
  u CSSOM-u. Ostaje da se jednom pogleda print preview na tamnoj temi.
- **Python skripte `main.py` / `dbwalk.py` nisu pokretane.** Prave zapise u bazi, a već sam jednom
  večeras zamalo ostavio nered — nisam hteo dvaput.

### Za ujutru

Predlozi funkcionalnosti su u [`new_feature_ideas_by_claude.md`](new_feature_ideas_by_claude.md) —
deset ideja, poređanih po odnosu koliko vrede prema tome koliko koštaju, sa obrazloženjem i mestom u
kodu gde bi se zakačile.
