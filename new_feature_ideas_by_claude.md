# Predlozi funkcionalnosti

Nastalo dok sam prolazio kroz celu aplikaciju noću 2/3. septembra. Ništa od ovoga nije urađeno —
spisak je za tebe da pogledaš i odlučiš.

Poređano po odnosu **koliko vredi / koliko košta**, ne po tome koliko je zanimljivo za pisanje.
Uz svaku stoji i zašto mislim da je vredna, i gde bi se zakačila u postojeći kod.

---

## Vredi mnogo, košta malo

### 1. Vreme čekanja na porudžbinu, mereno

`Order.ServedAtUtc` je dodat večeras zbog vraćanja greškom iznetih kartica. Ali sada u bazi **stoji
podatak koji niko ne gleda**: koliko je prošlo od `CreatedAt` do `ServedAtUtc`.

To je jedina brojka u celom sistemu koja meri **uslugu**, a ne novac. Prosek po smeni, po konobaru,
po satu — vlasnik prvi put može da vidi da se petkom uveče čeka 14 minuta, a utorkom 4.

*Gde:* novi red u „Izveštaji", uz postojeće. Podatak već postoji, treba samo upit i grafikon —
`BarChart` iz `shared/ui` je već tu.

### 2. Zvuk kad stigne nova porudžbina sa telefona

Kartica se pojavljuje sama preko SignalR-a, ali samo ako neko gleda u ekran. U bučnom lokalu niko ne
gleda. Kratak, tih zvuk kad red poraste — i prekidač da se ugasi.

*Gde:* `floor.page.ts` već zna kad se red promeni. Petnaestak linija, uz `podesavanja` prekidač.

### 3. „Sve izneto" na kartici sa više stavki

Sada je jedno dugme po **rundi**. Ako gost naruči četiri stvari u jednoj rundi, konobar ih nosi
odjednom i to radi. Ali ako naruči tri puta po jednom, dobiješ tri kartice za isti sto.

Grupisanje kartica **po stolu**, sa dugmetom „Sve izneto", bilo bi bliže tome kako se stvarno nosi.

*Gde:* `GetServiceQueueQueryHandler` grupiše, `floor.page.ts` iscrtava.

### 4. Filter „samo moji stolovi"

Konobar u velikom lokalu vidi ceo red, uključujući stolove koje pokriva neko drugi. Prekidač koji bi
red sveo na stolove iz njegovog dela sale.

*Košta više nego što izgleda:* nema pojma o „delu sale" — trebalo bi vezati konobara za prostoriju
ili za skup stolova. Ali je to i samo po sebi korisno (raspored smena po prostorijama).

---

## Vredi, ali traži odluku

### 5. Storno traži razlog, ali niko ga ne čita

`VoidRecord` beleži razlog svakog storna, a izveštaj „Storno" prikazuje iznose. Razlozi se upisuju i
tu ostaju.

Ako se razlozi ponavljaju („pogrešno ukucano", „gost odustao", „prosuto"), to je podatak o tome gde
se gubi novac. Ponuditi **spisak čestih razloga** uz slobodan unos, pa ih grupisati u izveštaju.

*Odluka koju traži:* spisak razloga je stvar politike lokala, ne softvera. Možda treba da bude
podesiv, a to je već ceo mali ekran.

### 6. Sto se ne oslobađa sam

Sto ostaje crven dok se račun ne naplati. Ako gosti odu bez plaćanja — ili konobar zaboravi da
zatvori — sto stoji zauzet zauvek. U bazi sada ima računa otvorenih **osam dana**.

Predlog: kad je sto otvoren duže od nekog praga, na sali dobija tihu oznaku „proveri", a vlasnik u
izveštajima vidi listu. Ne automatsko zatvaranje — novac se ne dira automatski — nego pitanje.

*Zašto vredi:* to je jedina greška u sistemu koja **tiho troši kapacitet**: sto koji izgleda zauzet
a nije, gost koji stoji na vratima.

### 7. Gostov ekran u temi lokala

`GET /api/settings` već odgovara i sesiji stola (proveravano večeras, namerno ostavljeno). Gostov
ekran bi mogao da nosi paletu lokala umesto podrazumevane.

*Odluka:* tamna tema na telefonu u mračnom lokalu je dobra; ista tema na terasi po suncu nije.
Možda gost treba da prati sistemsku postavku telefona, a ne lokala.

---

## Veće, za posle diplomskog

### 8. Kuhinjski ekran

`KitchenHub` postoji, `OrderCreated` i `OrderItemUpdated` se emituju, `MarkInPreparation()` postoji
na entitetu — i **ništa od toga nema ekran**. Isto stanje u kom je do večeras bio `MarkServed()`.

Ekran za kuhinju: šta je poručeno, šta je u pripremi, šta je gotovo. Skoro sva mašinerija je već tu.

### 9. Više valuta / porezi

`Restaurant.CurrencyCode` postoji i nigde se ne koristi — svuda je RSD zakucan u šablonima. Ako
aplikacija ikad izađe iz Srbije, to je prvo što puca. Danas je to mrtvo polje koje obećava nešto što
ne radi; ili ga iskoristiti ili ga skloniti.

### 10. Izvoz izveštaja

Vlasnik vidi izveštaje samo na ekranu. CSV ili PDF za knjigovođu je očigledna sledeća stvar, i
verovatno prva koju bi tražio stvarni korisnik.

---

## Ne bih radio

- **Automatsko zatvaranje računa po vremenu.** Novac se ne dira bez čoveka. Vidi #6 — pitanje, ne radnja.
- **Slobodan birač boje** za teme i elemente. Obrazloženo u `finalne_funkcionalnosti.md`: na sali
  boja *znači* status, i skup mora ostati zatvoren.
- **Tema po korisniku.** Vlasnik bira za lokal; ako konobar hoće svoju, to je druga funkcionalnost
  sa drugim mestom čuvanja i drugom pričom.
