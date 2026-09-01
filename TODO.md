# DigitalRegistry — plan rada (Faze 9–16)

Full-stack digitalna kasa za restorane sa licencnom master aplikacijom.

- **Backend:** .NET 10, Clean Architecture, MediatR CQRS, EF Core + SQL Server, JWT + RBAC, SignalR
- **Frontend:** Angular workspace (Angular Material + CDK) — `pos` i `master` aplikacija
- **Valuta:** RSD · **Jezik interfejsa:** srpski · **Račun:** simulacija (bez ESIR/fiskalizacije)

> Faze 1–8 (osnovni domen, Identity/JWT, stolovi, QR sesije, rezervacije, porudžbine,
> naplata, magacin, smene, SignalR) su implementirane — vidi `Backend/todo.md` i git istoriju.
> Ovaj dokument pokriva preostali posao.
> **Ispravka:** `Backend/todo.md` navodi .NET 8; kod je zapravo na `net10.0` (`Directory.Build.props`).

---

## Potvrđene projektne odluke

| Tema | Odluka |
| :--- | :--- |
| Multi-tenancy | Jedna baza + `RestaurantId` + EF Core Global Query Filters |
| Prijava | Šifra restorana (slug) + email + lozinka; JWT nosi `restaurant_id` |
| Master aplikacija | Zaseban API host + zasebna Angular aplikacija |
| Master obim | Restorani, licence, naplata licenci, globalna statistika |
| Gost-QR i rezervacije | Zadržavamo (kod već postoji) |
| Raspored stolova | Drag & drop platno, prostorije, X/Y koordinate, oblici |
| Smene | Šabloni smena + dodela po danima u nedelji → generisanje konkretnih smena |
| Magacin | Ulaz robe sa nabavnom cenom + automatsko razduživanje po normativu |
| Storno | Stavka, ceo otvoren račun, plaćen račun (uz odobrenje), + izveštaj |
| Izveštaji | Dnevni pazar, najprodavaniji artikli, utrošak i vrednost zaliha, storno |

---

## Ciljna struktura

```text
Backend/
  src/
    DigitalRegistry.Domain          + Restaurant, License, LicensePayment, Room,
                                      ShiftTemplate, ShiftAssignment, StockEntry,
                                      StockMovement, VoidRecord
    DigitalRegistry.Application     + Features/FloorPlan, Reports, Licensing, Platform
                                      ITenantContext, IRestaurantScoped
    DigitalRegistry.Infrastructure  jedan ApplicationDbContext — tenant entiteti filtrirani,
                                      platformski entiteti nefiltrirani
    DigitalRegistry.Api             kasa + LicenseGuardMiddleware
    DigitalRegistry.Master.Api      NOVO — platformski admin API, bez tenant filtera
Frontend/                           NOVO — jedan Angular workspace
  projects/pos                      konobar / menadžer / vlasnik
  projects/master                   platformski admin
  projects/shared                   auth, http interceptori, modeli, SignalR
```

## Redosled i zavisnosti

```text
Faza 9  (multi-tenancy)  ──┬──> Faza 10 (licence + Master API)
                           ├──> Faza 11 (floor plan)
                           ├──> Faza 12 (storno)
                           ├──> Faza 13 (smene)
                           └──> Faza 14 (magacin, jelovnik, izveštaji)
                                        │
Faza 10 + 11..14  ────────────────────> Faza 15 (Angular) ──> Faza 16 (testovi, dokumentacija)
```

Faza 9 mora prva jer menja svaki entitet i svaki upit. Faze 11–14 su međusobno nezavisne.
Angular može da počne čim su Faze 9–11 gotove.

---

## Faza 9 — Multi-tenancy (temelj, blokira sve ostalo)

### Domain
- [x] `Domain/Common/IRestaurantScoped.cs` — `Guid RestaurantId { get; set; }`
- [x] Implementirati `IRestaurantScoped` na: `Table`, `Reservation`, `Shift`, `MenuItem`,
      `Ingredient`, `RecipeItem`, `Order`, `OrderItem`, `Transaction`
      (ne dirati `BaseEntity` — platformski entiteti nisu tenant-scoped)
- [x] `Entities/Restaurant.cs` — `Name`, `Slug` (jedinstven, koristi se pri prijavi), `Address`,
      `ContactEmail`, `PhoneNumber`, `CurrencyCode` (default `"RSD"`), `TimeZoneId`, `IsActive`
- [x] `ApplicationUser` dobija `Guid? RestaurantId` (null = platformski admin)
- [x] `Enums/UserRole.cs` — dodati `PlatformAdmin = 5`
- [x] `ValueObjects/Money.cs` — `DefaultCurrencyCode` sa `"EUR"` na `"RSD"`

### Application
- [x] `Common/Interfaces/ITenantContext.cs` — `Guid RestaurantId`, `bool HasTenant`
- [x] `Common/Security/DigitalRegistryClaimTypes.cs` — dodati `RestaurantId`, `RestaurantSlug`
- [x] `LoginCommand` prima `RestaurantSlug`; validator ga zahteva
- [x] `IdentityService` mapira prijavu na Identity `UserName = "{slug}|{email}"`
      (`Email` ostaje za prikaz; platformski admini se prijavljuju bez prefiksa na Master.Api)
- [x] `AuthenticationResult` dobija `RestaurantId` i `RestaurantSlug`

### Infrastructure
- [x] `ApplicationDbContext.OnModelCreating` — refleksijom dodati
      `HasQueryFilter(e => e.RestaurantId == _tenant.RestaurantId)` za svaki `IRestaurantScoped` tip
      (isti obrazac kao postojeći `Ignore(DomainEvents)`, linije 100–108)
- [x] `ApplicationDbContext.SaveChangesAsync` — dodati `ApplyTenantStamp()` uz postojeći
      `ApplyAuditTimestamps()`; na `EntityState.Added` postavlja `RestaurantId` iz `ITenantContext`
- [x] Kompozitni unique indeksi sa `RestaurantId` u `Persistence/Configurations/`:
      `Table.TableNumber`, `MenuItem.Name`, `Ingredient.Name` — to su prirodni ključevi koji se
      ponavljaju između restorana. `Table.QrCodeToken` ostaje jedinstven na nivou platforme jer se
      razrešava **pre** nego što se zna tenant; `RecipeItem(MenuItemId, IngredientId)` i
      `Transaction.OrderId` ostaju kakvi jesu jer strani ključ već implicira restoran.
      Dodati i vodeći `RestaurantId` na indekse za čitanje (Orders, Reservations, Shifts, Transactions)
- [x] `RestaurantConfiguration.cs` — unique `Slug`
- [x] `NullTenantContext` za dizajn-vreme i testove (analogno `NullDomainEventDispatcher`)
- [x] `Api/Services/TenantContext.cs` — čita `restaurant_id` claim iz `HttpContext`
- [x] `JwtTokenGenerator.GenerateForUser` — dodati `restaurant_id` claim
- [x] `JwtTokenGenerator.GenerateForTableSession` — primiti i ugraditi `restaurantId`
      (QR sesija bi inače zaobišla query filter)
- [x] `ApplicationDbContextSeeder` — demo restoran; svi demo korisnici i podaci ga dobijaju
- [x] Obrisati postojeću migraciju `20260810190631_InitialCreate` i generisati jednu novu
      (nema produkcijskih podataka — čistije nego lančanje), pa presnimiti bazu seedom

---

## Faza 10 — Licenciranje i master aplikacija (backend)

### Domain (platformski entiteti — **bez** `IRestaurantScoped`)
- [x] `Enums/LicensePlan.cs` — `Monthly = 1`, `Quarterly = 3`, `SemiAnnual = 6`, `Annual = 12`
      (vrednost = broj meseci → `ExpiresAtUtc.AddMonths((int)plan)`)
- [x] `Enums/LicenseStatus.cs` — `Active`, `Expired`, `Suspended`, `Cancelled`
- [x] `Entities/License.cs` — `RestaurantId`, `Plan`, `StartsAtUtc`, `ExpiresAtUtc`, `Status`,
      `Price`, `IssuedByAdminId`, `Notes`; `IsCurrentlyValid(utcNow)`, `Renew(plan, utcNow)`, `Suspend(reason)`
- [x] `Entities/LicensePayment.cs` — `LicenseId`, `Amount`, `PaidAtUtc`, `PaymentMethod`,
      `ReferenceNumber`, `RecordedByAdminId`

### Provera licence u kasi
- [x] `Application/Common/Interfaces/ILicenseService.cs` — `GetStateAsync(restaurantId, ct)`
- [x] `Infrastructure/Licensing/LicenseService.cs` — status se **izvodi** iz `ExpiresAtUtc` vs. sada
      (nema noćnog posla koji bi zakazao). **Bez keša**: kasa i master su odvojeni procesi, pa
      `Invalidate()` u jednom ne vidi keš drugog — restoran koji je upravo platio ostajao je
      zaključan. Upit je jedan indeksirani red, pa se radi po zahtevu.
- [x] `Api/Middleware/LicenseGuardMiddleware.cs` — posle autentifikacije; nevažeća licenca →
      **HTTP 402** sa `ProblemDetails`, `extensions["code"] = "LICENSE_EXPIRED"` i `expiresAtUtc`.
      Izuzeci: `/api/auth/*`, `/api/license/status`, `/swagger`, `/health`
- [x] `GET /api/license/status` — za traku „licenca ističe za N dana“ i blokirajući ekran

### Master API — novi projekat `src/DigitalRegistry.Master.Api`
- [x] Kreirati projekat, dodati u `DigitalRegistry.slnx`, referencirati Application + Infrastructure
- [x] Izdvojiti `DigitalRegistry.Api.Shared` (ApiControllerBase, ExceptionHandlingMiddleware,
      CurrentUserService, TenantContext, UTC JSON konverteri) — dele ih oba hosta
- [x] Registrovati `NullTenantContext`; podatke restorana čitati sa `IgnoreQueryFilters()`
      (samo u `Features/Platform/` handlerima, da se ne raširi po kodu)
- [x] Zasebna `Jwt:Audience` + politika `PlatformAdminOnly` — token kase ne sme raditi na master API-ju
- [x] `POST /api/platform/auth/login`
- [x] `GET|POST|PUT /api/platform/restaurants`, `POST /{id}/suspend`, `POST /{id}/activate`
- [x] `POST /api/platform/restaurants/{id}/owner` — kreira vlasnički nalog
- [x] `GET|POST /api/platform/licenses`, `POST /{id}/renew`, `POST /{id}/suspend`
- [x] `POST /api/platform/licenses/{id}/payments` — ručna evidencija uplate
- [x] `GET /api/platform/dashboard` — aktivne/istekle licence, isteci za 30 dana,
      prihod od licenci po mesecima, broj restorana
- [x] `POST /api/platform/licenses/{id}/reactivate` i `/cancel` (uz obavezan razlog)
- [x] `SeedPlatformAdminAsync` — prvi admin iz konfiguracije; demo restoran dobija godišnju licencu
- [x] `Application/Features/Licensing/` i `Features/Platform/` po postojećem obrascu
      (Command/Handler/Validator + Dtos, sve vraća `Result<T>`)

---

## Faza 11 — Raspored stolova (floor plan)

- [x] `Entities/Room.cs` — `RestaurantId`, `Name` („Sala“, „Bašta“, „Sprat“), `DisplayOrder`,
      `CanvasWidth`, `CanvasHeight`
- [x] `Enums/TableShape.cs` — `Round`, `Rectangle`, `Square`
- [x] `Table` dobija `Guid? RoomId`, `PositionX`, `PositionY`, `Width`, `Height`, `Shape`, `Rotation`
- [x] `Features/FloorPlan/Queries/GetFloorPlan` — prostorije + stolovi + **izveden** status
      (`Available` / `Occupied` sa iznosom i vremenom otvorenog računa / `Reserved`).
      Glavni ekran kase; postojeći `TableStatus` enum se ovde konačno koristi
- [x] `Features/FloorPlan/Commands/SaveRoomLayout` — bulk snimanje pozicija po prostoriji u jednom
      upisu (editor šalje ceo raspored, ne pojedinačne pomeraje). Sto izostavljen iz liste se
      **izbacuje iz prostorije** — tako editor briše sto iz sale prevlačenjem, bez zasebnog endpointa
- [x] `Features/FloorPlan/Commands/CreateRoom|UpdateRoom|DeleteRoom`
- [x] `FloorPlanController` — `ManageTables` za izmene, `ViewTableAvailability` za čitanje
- [x] Provereno: `DeleteTableCommandHandler` već odbija sto sa istorijom i upućuje na deaktivaciju
- [x] Izdvojen `Features/Tables/TableStatusRules.cs` — pravilo statusa stola dele floor plan i
      postojeći `GetAvailableTables` (`OutOfService` > `Occupied` > `Reserved` > `Available`)
- [x] Brisanje prostorije: stolovi preživljavaju kao neraspoređeni (FK `SetNull`); odbija se dok
      ima otvorenih računa. Smanjenje platna se odbija ako bi sto ostao van vidljivog dela
- [x] Seeder: demo restoran dobija „Sala" i „Bašta" sa razmeštenim stolovima

---

## Faza 12 — Storno

- [x] `Enums/VoidType.cs` — `Item`, `OpenOrder`, `PaidOrder`
- [x] `OrderStatus` dobija `Voided = 6`
- [x] `Entities/VoidRecord.cs` — `RestaurantId`, `OrderId`, `Guid? OrderItemId`, `Type`, `Quantity`,
      `Amount`, `Reason` (obavezno), `PerformedByUserId`, `Guid? ApprovedByUserId`, `VoidedAtUtc`
- [x] `Transaction` dobija `Guid? ReversesTransactionId` i `bool IsReversal`
- [x] `Order.Reverse(approvedByUserId, reason)` — protivstavka sa negativnim iznosom,
      status → `Voided`, diže `OrderVoidedDomainEvent`
- [x] `Features/Orders/Commands/VoidOrderItem` — politika `ModifyOrder`; vraća zalihe preko
      postojećeg `IInventoryAllocator.ReturnAsync`, upisuje `VoidRecord`
- [x] `Features/Orders/Commands/VoidOpenOrder` — politika `ModifyOrder`; vraća sve zalihe, oslobađa sto
- [x] `Features/Orders/Commands/VoidPaidOrder` — **nova politika `ApproveVoid`** (`Manager`, `Owner`)
- [x] `Features/Reports/Queries/GetVoidReport` — po periodu, konobaru i tipu; politika `FinancialReports`
- [x] **Zatvorena rupa u `UpdateOrderItemCommand`**: `Remove` je uklonjen, a `ChangeQuantity` je
      postao `IncreaseQuantity` (samo naviše). Ranije se stavka mogla obrisati ili količina smanjiti
      **bez razloga**, čime bi izveštaj storna bio bezvredan kao kontrola. Sada je *svako* smanjenje
      računa storno
- [x] `Order.VoidItem` podržava **delimičan** storno (npr. 1 od 3 espresa), po ceni koju je stavka
      zabeležila — kasnija promena cenovnika ne menja vrednost storna
- [x] Posledice po šemu koje protivstavka nosi: `IX_Transactions_OrderId` je postao **filtriran**
      (`WHERE ReversesTransactionId IS NULL`) jer je protivstavka namerno drugi red za isti račun;
      `CK_Transaction_Amount_NonNegative` zamenjen sa `CK_Transaction_Amount_Sign` koji traži
      uplatu ≥ 0 i protivstavku ≤ 0
- [x] Duži minimalni razlog za storno plaćenog računa (10 znakova) nego za stavku (3)

---

## Faza 13 — Smene: šabloni i nedeljni raspored

- [x] `Enums/WeekDays.cs` — `[Flags]`, vrednosti prate `DayOfWeek` pomerene u bitove, pa je pretvaranje
      pomeraj a ne tabela; imenovani obrasci `Weekdays` i `Weekend`
- [x] `Entities/ShiftTemplate.cs` — `RestaurantId`, `Name`, `TimeOnly StartTime`, `TimeOnly EndTime`,
      `IsActive`. **`CrossesMidnight` se izvodi** (`EndTime <= StartTime`), ne čuva se — sačuvana
      zastavica bi mogla da protivreči vremenima nakon izmene šablona
- [x] `Entities/ShiftAssignment.cs` — `RestaurantId`, `WaiterId`, `ShiftTemplateId`, `Days`,
      `DateOnly ValidFrom`, `DateOnly? ValidTo`, `AssignedByManagerId`
- [x] `Features/Shifts/Commands/GenerateSchedule` — materijalizuje konkretne `Shift` redove za opseg
      datuma; preskače postojeće i prijavljuje preklapanja koristeći **postojeći**
      `Features/Shifts/ShiftOverlapRules.cs` (`HasOverlappingShiftAsync`)
- [x] `Features/Shifts/Queries/GetWeeklySchedule` — mreža konobari × dani, sa ukupnim satima
- [x] **`ShiftClock`** — pretvaranje lokalnog vremena šabolna u UTC preko `Restaurant.TimeZoneId`.
      Bez toga bi „II smena 15–23" za Beograd bila upisana dva sata pomereno. Rešeni i sat koji
      ne postoji (pomeranje unapred) i sat koji se dešava dvaput (uzima se raniji)
- [x] `Shift.ShiftAssignmentId` — generator prepoznaje svoj izlaz, pa dvostruko pokretanje dopunjava
      umesto da duplira; ujedno se ad-hoc zamena razlikuje od redovnog rasporeda
- [x] CRUD za `ShiftTemplate` i `ShiftAssignment` (politika `ManageShifts`)
- [x] Postojeći `AssignShift`/`UpdateShift`/`DeleteShift` ostaju za ad-hoc izmene i zamene

---

## Faza 14 — Magacin, jelovnik, izveštaji

### Magacin
- [x] `Entities/StockEntry.cs` — `RestaurantId`, `IngredientId`, `Quantity`, `PurchaseUnitPrice`,
      `TotalCost`, `EntryDate`, `Note`, `RecordedByUserId`
- [x] `Entities/StockMovement.cs` — knjiga prometa sa **označenom** količinom (+ ulaz, − izlaz), pa
      `SUM(Quantity)` rekonstruiše stanje i može da se sravni sa `Ingredient.StockQuantity`.
      Tipovi: `Purchase`/`Sale`/`Return`/`Adjustment`
- [x] `Ingredient.AveragePurchasePrice` — klizeći prosek, ažurira se u `Restock()`
- [x] `Features/Inventory/Commands/RecordStockEntry` — proširuje postojeći `RestockIngredient`
      (nabavna cena + `StockMovement`)
- [x] `InventoryAllocator` (`Application/Common/Services/`) uz izmenu `StockQuantity` upisuje
      i `StockMovement` — jedna tačka izmene, svi tokovi (prodaja, storno) je koriste
- [x] `Features/Inventory/Commands/AdjustStock` — inventura uz obavezan razlog. Zadaje se
      **prebrojana količina**, ne razlika — to je ono što čovek sa popisnom listom ima u ruci
- [x] `GET /api/inventory/movements` i `/entries` — knjiga prometa i pregled nabavki
- [x] `Ingredient.Receive()` menja prosek, `Restock()` (povraćaj sa storna) ga **ne dira** — roba koja
      se vraća je već kupljena i već obračunata

### Jelovnik (CRUD trenutno ne postoji — samo `GetMenu`)
- [x] `Features/Menu/Commands/CreateMenuItem`, `UpdateMenuItem`, `DeleteMenuItem` — politika `ManageMenu`
- [x] `Features/Menu/Commands/SetRecipe` — normativ (koliko kog sastojka ide u jedan artikal)
- [x] Flaširana pića: normativ 1:1 (`QuantityRequired = 1`, `UnitOfMeasure.Units`) —
      isti mehanizam pokriva i šank i kuhinju

### Izveštaji (`Features/Reports/`, politika `FinancialReports`)
- [x] `GetDailyTurnover` — promet po danu, gotovina/kartica, broj računa, prosečan račun
- [x] `GetTopSellingItems` — rang po količini i po prometu, filter period + kategorija
- [x] `GetInventoryValuation` — utrošak u periodu (prodaja minus povraćaji), vrednost zaliha po
      nabavnim cenama, nabavljeno, korekcije, artikli ispod minimuma
- [x] `ReportsController` + `GET /api/reports/*`

### Račun (simulacija)
- [x] `Features/Orders/Queries/GetReceipt` — stavke, količine, cene, ukupno, način plaćanja,
      konobar, vreme, podaci restorana (frontend štampa preko `window.print()` + print CSS).
      Stornirani račun se **označava** na otisku, da ne bi mogao da prođe kao važeći
- [x] Marža po artiklu se računa iz normativa i klizećeg proseka; `null` kad nabavna cena nije
      poznata — marža nad nulom bi čitala kao čist profit, što je laskavo i netačno

---

## Prolaz kroz backend (pre frontenda)

`Backend/tools/api-walkthrough/` prolazi kroz svih 77 ruta oba API-ja na živoj bazi — 134 provere,
uključujući RBAC (pogrešna rola → 403), granične slučajeve i izolaciju između dva restorana.

Napisan zato što unit testovi ne hvataju greške koje se pojave tek kad EF Core priča sa SQL Serverom:
obe nađene greške bile su nevidljive za in-memory provajder.

### Nađeno i popravljeno

- [x] **`SetRecipe` → 500.** `RemoveRange` nad kolekcijom praćenog roditelja plus `Clear()` navigacije
      tera EF da iste redove obradi dvaput; drugi prolaz briše ono što je prvi već obrisao
- [x] **`PATCH /orders/{id}/items` (dodavanje) → 500.** EF je slao `UPDATE [OrderItems]` umesto
      `INSERT`, jer `BaseEntity` dodeljuje `Guid` pri konstrukciji, pa dete nađeno na već praćenom
      roditelju izgleda kao postojeći red
- [x] Zajednički koren: **rad preko kolekcije roditelja umesto preko `DbSet`-a**. Oba mesta rešena
      predajom deteta svom `DbSet`-u; regresioni testovi u `Persistence/TrackedChildInsertTests.cs`

### Potvrđeno kao ispravno (ne greške)

- `GET /tables/availability` odbija period duži od 12 sati
- QR sesija sa praznim GUID-om vraća 400 (validacija), a sa nepoznatim 404

---

## Prolaz kroz bazu i frontend (posle Faze 15)

Status kod ne dokazuje da je red upisan kako treba, pa je dodat `tools/api-walkthrough/dbwalk.py`:
poziva svaki endpoint koji piše, pa čita bazu preko `sqlcmd` i proverava red — 91 HTTP provera i
172 provere u bazi, ponovljivo (imena nose sufiks pokretanja, brojevi stolova idu od najvećeg u
upotrebi, generisane smene se brišu na kraju). Izveštaji se porede sa agregatima: promet sa
`SUM(Transactions.Amount)`, najprodavaniji sa plaćenim stavkama, vrednost zaliha sa
`zaliha × prosečna nabavna`, dashboard sa tabelama koje broji.

Uz to je svaki ekran obe Angular aplikacije prokliktan uživo, uz proveru u bazi posle svake akcije.

### Nađeno i popravljeno

- [x] **Dopuna zaliha nije ostavljala trag.** `POST /api/inventory/ingredients/{id}/restock` je
      dizao `StockQuantity` bez `StockMovement`-a, pa `SUM(Quantity)` više nije mogao da rekonstruiše
      stanje — tačno ono što knjiga prometa treba da garantuje. Sada upisuje `Purchase` red bez cene
      i bez `StockEntry`-ja: to je brza korekcija, a ne evidentirana nabavka
- [x] **„Nabavljeno" u izveštaju zaliha nije bilo ono što je plaćeno.** Računalo se kao
      `količina × današnji prosek`, pa je prošla nabavka prevrednovana svakom novom isporukom
      (1.000 g po 1,5 RSD prikazivalo se kao 1.233 umesto 1.500). Sada dolazi iz `StockEntries.TotalCost`
- [x] **„Storno plaćenog" je stajao na otvorenom računu**, gde API s pravom odgovara 409, a za
      plaćen račun je bio nedostupan jer ekran učitava samo otvorene tabove. Premešten na otisak
      računa odmah posle naplate (Menadžer+), gde se pogrešna naplata i hvata

### Potvrđeno kao ispravno (ne greške)

- Storno **otvorenog** računa daje `Cancelled (5)`, a storno **plaćenog** `Voided (6)` — razlika je
  namerna: nenaplaćen račun nema šta da vrati
- Brisanje stalne dodele ostavlja već generisane smene (FK `SetNull`), a generator preskače termin
  koji postojeća smena već pokriva — otud „kreirano 0, već postojalo 5" pri ponovnom pokretanju
- Ugašen nalog na prijavi daje 403 (Identity lockout), ne 401
- `POST` za prostoriju, artikal, zaposlenog, licencu i uplatu vraća 200 sa resursom umesto 201 —
  nedosledno sa stolovima i računima, ali bezopasno

### Sitnice iz testiranja (poruke grešaka su prenete u „Preostalo")

- [x] SignalR se ne poveže ponovo sam kad licenca ponovo postane važeća — `start()` je izlazio čim
      niz veza nije prazan, a posle odbijenog handshake-a veze ostaju u nizu, samo mrtve. Sada se
      gleda stanje veze, ne broj; mrtve se odbace i hubovi otvore ponovo
- [x] Grafikon „Prihod po mesecima" sa jednim mesecom podataka izgleda kao pun blok boje — API je
      vraćao samo mesece u kojima ima uplata; sada popunjava ceo traženi prozor, jer je prazan mesec
      podatak a ne odsustvo podatka. Uz to je širina stupca ograničena na 44 px
- [x] Razlog suspenzije/otkazivanja licence i naziv prostorije se traže preko `prompt()` — dodati
      `PromptDialog` i `ConfirmDialog` u `shared`, i zamenjeno svih šest mesta u obe aplikacije.
      Dijalozi sada i objašnjavaju posledicu („kasa staje odmah — sledeći poziv dobija 402")

---

## Faza 15 — Angular workspace i POS aplikacija

- [x] `ng new DigitalRegistry --create-application=false` u `Frontend/`
- [x] `ng g application pos`, `ng g application master`, `ng g library shared`
- [x] Angular Material + CDK; standalone komponente, signals,
      `provideHttpClient(withInterceptors(...))`. Rute su na srpskom (`/sala`, `/rezervacije`,
      `/jelovnik`…) — putanje ispod su iz plana, u kodu su prevedene

### `projects/shared`
- [x] `AuthService` — login, token u `localStorage`, dekodiran `restaurant_id` i rola
- [x] `authInterceptor` — Bearer token
- [x] `licenseInterceptor` — hvata **402** → ekran „Licenca istekla“. Nije zaseban interceptor:
      402 se hvata u `errorInterceptor`, gde su i ostali statusi, pa se lanac ne deli na dva mesta
      koja oba čitaju isti odgovor
- [x] `errorInterceptor` — RFC 7807 → snackbar
- [x] `roleGuard` — zaštita ruta po roli
- [x] `SignalRService` — `/hubs/kitchen` i `/hubs/order`
- [x] TypeScript modeli usklađeni sa DTO-ovima

### `projects/pos`
| Ruta | Ekran | Role | |
| :--- | :--- | :--- | :---: |
| `/prijava` | prijava (šifra restorana + email + lozinka) | svi | [x] |
| `/sala` | **glavni ekran** — tabovi prostorija, platno sa stolovima, boja po statusu, iznos otvorenog računa; SignalR uživo | Konobar+ | [x] |
| `/sala/:tableId` | panel računa — levo stavke + količine + „Storno“, desno mreža kategorija i artikala; „Pošalji“ / „Plaćanje“ / „Storno računa“ | Konobar+ | [x] |
| naplata (dijalog) | gotovina/kartica, uneto/kusur → pregled i štampa računa | Konobar+ | [x] |
| `/raspored` | drag & drop raspored (CDK `cdkDrag`), dodavanje/brisanje stolova, prostorije | Vlasnik | [x] |
| `/smene` | šabloni smena, nedeljna mreža dodela, „Generiši raspored“ | Menadžer+ | [x] |
| `/magacin` | zalihe, ulaz robe sa nabavnom cenom, korekcije, upozorenja | Menadžer+ | [x] |
| `/jelovnik` | artikli, cene, kategorije, normativi | Menadžer+ | [x] |
| `/rezervacije` | dnevni pregled rezervacija i prijava dolaska | Konobar+ | [x] |
| `/izvestaji` | dnevni pazar, najprodavaniji artikli, vrednost zaliha, storno | Vlasnik | [x] |
| `/zaposleni` | zaposleni (konobari, menadžeri) | Vlasnik | [x] |

> Ključni detalj kase: sto ostaje otvoren kroz više dodavanja — svako „Pošalji“ poziva
> `PATCH /api/orders/{id}/items`; račun se zaključuje tek na `POST /api/orders/{id}/payment`.

- [x] **Nastavljanje otvorenog računa.** `FloorPlanTableDto.OpenOrderCount` je zamenjen sa
      `OpenOrderIds` (id-jevi, od najstarijeg). Bez toga kasa nije imala nijedan poziv kojim bi
      saznala *koji* je račun otvoren na stolu: konobar koji se vrati na zauzet sto video bi prazan
      račun i otvorio drugi pored postojećeg. Sto sa više računa dobija prekidač između njih
- [x] **Štampa računa.** `GET /api/orders/{id}/receipt` → dijalog širine 80 mm (termalna traka) i
      `window.print()`. Print CSS je morao da razmota CDK overlay: dijalog je fiksiran i skrolabilan,
      pa bi se odštampao kao jedna odsečena strana preko snimka ekrana kase. Stornirani račun nosi
      oznaku „NEVAŽEĆI“, po pravilu iz Faze 14
- [x] **Rezervacije.** Dnevni pregled (datum, filter po stolu, zbir gostiju), prijava dolaska za
      Konobar+, otkazivanje samo za Menadžer+ — isto kao `ManageReservationDesk` i
      `CancelReservation` politike. Unos nove rezervacije se **ne nudi**: API knjiži rezervaciju na
      pozivaoca, pa bi rezervacija koju primi konobar glasila na konobarevo ime

### `projects/master`
- [x] Prijava platformskog admina
- [x] Lista restorana sa statusom licence i danima do isteka
- [x] Forma za novi restoran + vlasnički nalog
- [x] Izdavanje/produženje licence (1/3/6/12 meseci), suspenzija, reaktivacija, otkazivanje
- [x] Evidencija uplata
- [x] Dashboard: aktivne vs. istekle licence, prihod po mesecima

### Gost i QR (dodato posle testiranja)
- [x] `/gost/:token` — mobilni ekran do kog vodi QR kod sa stola: jelovnik restorana kome sto
      pripada, kategorije, korpa zakačena za dno ekrana, slanje porudžbine konobaru. Bez prijave —
      token iz linka je cela sesija i kaže samo koji je sto
- [x] `GuestSessionService` — sesija stola u `sessionStorage`, odvojena od `AuthService`: gost koji
      skenira kod na tuđem telefonu ne izbacuje kasu iz sesije, a istekla sesija stola ne odjavljuje
      osoblje. `TABLE_SESSION_REQUEST` govori interceptorima da ne diraju te pozive i da gosta ne
      šalju na ekran licence, koji je namenjen osoblju
- [x] `AuthenticationResult` dobio `TableNumber`, da gostov ekran može da kaže za koji sto poručuje
- Sam QR kod se generiše od Faze 17 — vidi kraj dokumenta

### Ostalo
- [x] CORS u `appsettings.Development.json` — Angular portovi `4200` (pos) i `4300` (master)
- [x] Izmena podataka restorana iz master aplikacije — forma na `/restorani/:id` (naziv, adresa,
      kontakt, valuta, vremenska zona). Slug se ne menja: osoblje ga kuca pri svakoj prijavi.
      `RestaurantSummaryDto` je dobio `TimeZoneId` — postavljao se pri kreiranju i posle nije bio
      vidljiv nigde, a `ShiftClock` po njemu pretvara smene u UTC

## Faza 16 — Testovi i dokumentacija

- [x] Commit-ovati postojeći netrekovani rad — Faze 9–15 (Master API, `Frontend/`, migracije,
      testovi, `tools/`) komitovane u `122dc58`
- [x] Unit testovi: izolacija tenanta (query filter odseca tuđe podatke) — `Persistence/TenantIsolationTests.cs`
- [x] Unit testovi: `License.IsCurrentlyValid` i produženje — `Entities/LicenseTests.cs`
- [x] Unit testovi: generisanje smena iz šablona, uključujući preklapanje — `Shifts/GenerateScheduleCommandHandlerTests.cs`,
      `Shifts/ShiftClockTests.cs`
- [x] Unit testovi: storno stavke/računa i povraćaj zaliha — `Entities/OrderVoidTests.cs`,
      `Inventory/InventoryAllocatorTests.cs`
- [x] Unit testovi: `StockMovement` saldo — `Inventory/StockLedgerTests.cs`
- [x] Integracioni testovi — `TillFlowTests`: prijava → otvaranje računa → dodavanje → delimičan
      storno → naplata, uz proveru zaliha, knjige prometa i statusa stola posle svakog koraka;
      plus protivstavka koju konobar ne sme (403) a menadžer sme, i konobarski RBAC na otvaranju računa
- [x] Integracioni test: istekla licenca → 402 sa `LICENSE_EXPIRED` na svakoj ruti kase, dok prijava
      i `/api/license/status` i dalje rade; produženje odmah vraća kasu u rad (`LicenseGuardTests`)
- [x] Host se u testovima diže sa in-memory provajderom (`DigitalRegistryApiFactory`), pa
      `dotnet test` ne traži SQL Server. `MigrateAsync` preskače nerelacione provajdere; vernost
      prema pravoj bazi ostaje na `tools/api-walkthrough`
- [x] Ažurirati `Backend/todo.md` — .NET 10, sve kvačice za Faze 1–8, pokazivač na ovaj dokument za
      ostalo, i napomena gde se implementacija namerno razišla sa prvobitnim tekstom
      (`UpdateOrderItemCommand` više ne briše stavke)
- [x] `README.md` sa uputstvom za pokretanje — preduslovi, četiri procesa, demo nalozi, RBAC po
      ekranima, struktura, provera i ograničenja
- [x] `docs/` — [`architecture.md`](docs/architecture.md) (celina, put jednog zahteva kroz slojeve,
      odakle dolazi restoran, provera u više slojeva) i [`er-diagram.md`](docs/er-diagram.md)
      (model podataka, platformski deo, ograničenja koja drži baza). Mermaid, renderuje se na
      GitHubu; svih 6 dijagrama provereno kroz `mermaid-cli`

---

## Provera (kraj svake faze)

```powershell
dotnet build Backend/DigitalRegistry.slnx
dotnet test  Backend/DigitalRegistry.slnx
dotnet ef migrations add <Naziv> --project Backend/src/DigitalRegistry.Infrastructure --startup-project Backend/src/DigitalRegistry.Api
dotnet run --project Backend/src/DigitalRegistry.Api          # https://localhost:7270/swagger
dotnet run --project Backend/src/DigitalRegistry.Master.Api
```

Ručna provera kroz Swagger sa demo nalozima iz `ApplicationDbContextSeeder`
(`owner@` / `manager@` / `waiter@digitalregistry.local`, lozinka `Demo#Pass123`):

1. **Izolacija tenanta** — kreirati dva restorana; prijaviti se kao vlasnik restorana A i potvrditi
   da `GET /api/tables` ne vraća nijedan sto restorana B.
2. **Licenca** — izdati licencu na 1 mesec, pomeriti `ExpiresAtUtc` u prošlost, potvrditi **402**
   sa `code: LICENSE_EXPIRED`; produžiti i potvrditi povratak na 200.
3. **Tok kase** — otvoriti račun → 2 espresa → +1 pivo → storno espresa uz razlog → naplata gotovinom;
   potvrditi da je `Ingredient.StockQuantity` umanjen tačno za preostale stavke i da `StockMovement`
   ima odgovarajuće `In`/`Out`/`Return` redove.
4. **Storno plaćenog** — stornirati naplaćen račun kao menadžer; potvrditi protivstavku sa negativnim
   iznosom i umanjen dnevni pazar.
5. **Raspored** — prevući stolove u `/layout-editor`, snimiti, osvežiti; otvoriti račun u drugom tabu
   i potvrditi da sto na `/floor` pocrveni preko SignalR-a bez osvežavanja.
6. **Smene** — „II smena 15–23“, dodela konobaru Pon–Pet za septembar, generisanje rasporeda;
   potvrditi 22 smene i da ponovno generisanje ne pravi duplikate.

---

## Faza 17 — rupe koje je testiranje otvorilo

Faze 9–16 su zatvorene; ovo je ono što je testiranje posle njih otvorilo. Funkcionalne rupe i
provera su odrađene, uz njih i backend testovi za sve novo (`BillHistoryTests`,
`DeskReservationTests`, `GuestTableTabTests`, `TableQrCodeSheetTests` — integracioni ih ima 26
umesto 9).

**Faza 17 je zatvorena.** Ostaje samo ono što se tiče produkcije, na dnu ovog odeljka.

> **Prolaz kroz UI/UX (1. septembar 2026.)** je zaseban posao i vodi se u
> [`Frontend/BUGS.md`](Frontend/BUGS.md): petnaest zatvorenih stavki — od tooltipa koji nije radio i
> pokvarene srpske množine, preko stanja učitavanja i responsive prikaza, do vizuelnog identiteta i
> grafikona — i sedam otvorenih, od kojih je najveća da ništa od toga nije prokliktano uživo.

### Funkcionalne rupe — zatvorene

- [x] **QR kod se generiše.** `GET /api/tables/qr-codes` (Menadžer+) vraća token po stolu, uz
      prostoriju, jer je token akreditiv i ne sme na ekran sale. U `/raspored` stoji dugme
      „QR kodovi" — list za štampu po prostoriji, i pojedinačni kod za odabrani sto, kad se
      odštampani ošteti ili se token obnovi. Slika ide kroz paket `qrcode`, iscrtana na 512 px i
      štampana na 45 mm; link se gradi iz `window.location.origin`, jer kod skenira telefon koji
      stoji u restoranu i mora da vodi na host sa koga se kasa zaista servira. Ispod svakog koda
      stoji i sama adresa, za telefon koji neće da skenira.
- [x] **Plaćen račun se ponovo nalazi.** `GET /api/orders?from&to&status&tableId&take` vraća
      sažetke bez stavki (lista služi da se račun nađe; stavke stižu uz otisak), sa istim kratkim
      brojem koji otisak štampa — onim koji gost izgovori preko telefona. Ekran `/racuni`
      („Poslednji računi") ima dan, status, sto i pretragu po broju; otisak se otvara odatle, a iz
      njega, uz razlog, protivstavka.

      **Odstupanje od plana gore:** ekran nije Menadžer+ nego Konobar+. Uvedena je politika
      `ViewOrderHistory` = Konobar/Menadžer/Vlasnik, i `GET /{id}/receipt` je prebačen s
      `ProcessPayment` na nju. Razlog: `ProcessPayment` je Konobar+Vlasnik, pa menadžer koji sme da
      stornira plaćen račun nije smeo ni da ga vidi; a konobar koji traži kopiju računa koji je sam
      zatvorio ne treba da zove nekog starijeg. Sam storno ostaje `ApproveVoid` (Menadžer+), i
      dugme se u ekranu krije po ulozi.
- [x] **Rezervacija se unosi u ime gosta.** `Reservation.GuestId` je sada opciono, uz
      `ContactName`, `ContactPhone` i `TakenByUserId`; migracija `AddDeskReservations`. Baza drži
      `CK_Reservation_Booker` — rezervacija mora imati ili nalog gosta ili zapisano ime. Komanda i
      dalje ne prima tuđi `guestId`: gost rezerviše samo sebi, a osoblje **mora** da unese ime,
      inače 400 — bez toga bi rezervacija opet pala na onoga ko je digao slušalicu. `/rezervacije`
      je dobio „Nova rezervacija", a dnevni raspored prikazuje ime i telefon, uz to ko je primio.
- [x] **Gost vidi šta je već poručio.** `GET /api/orders/mine` odgovara sesiji stola: sto se čita
      iz tokena, pa kod može da dohvati samo svoj sto. Vraća sve runde koje još nisu naplaćene,
      zbirno — jer svaka runda otvara svoj račun, pa nijedan odgovor koji je gost već video ne daje
      zbir za sto. Na `/gost/:token` stoji panel „Već ste poručili", a ekran posle slanja kaže i
      koliko sto duguje ukupno.

### Kvalitet i provera — zatvoreno

- [x] **Frontend ima testove** — 43 (vitest), `ng test shared` i `ng test pos`:
      `interceptors.spec.ts` (402 nasuprot 401 nasuprot sesije stola, 403, mrtav server, čitanje
      RFC 7807 odgovora, i to da `authInterceptor` ne dira token sesije stola),
      `guest-session.service.spec.ts` (sesija u `sessionStorage`, istekla i pokvarena sesija,
      sopstveno zaglavlje i `TABLE_SESSION_REQUEST`), `floor.page.spec.ts` (boja po statusu stola,
      čitanje za čitač ekrana, ponovno učitavanje na hub događaj), `reservation.dialog.spec.ts`
      (unos rezervacije) i `labels.spec.ts` (srpska množina).
- [x] **Vizuelna provera** obavljena kroz Chrome: prijava, `/racuni` sa otiskom i storniranim
      redovima, unos rezervacije s kraja na kraj, list QR kodova, skeniran kod → `/gost/:token`,
      poručivanje, SignalR (sto na `/sala` skočio sa 610 na 910 RSD bez osvežavanja), grafikon
      „Prihod po mesecima" u master aplikaciji. Konzola bez grešaka.

      Nađeno i ispravljeno u toku provere: srpska množina je bila dvočlana („3 stavki"), pa je
      dodat `plural()` u `shared/format/labels` — 1 stavka, 2–4 stavke, 5+ stavki, uz izuzetak za
      tinejdžerske brojeve; i precrtavanje reda na `/racuni` više ne precrtava dugme na njemu.
- [x] **`main.py` je ponovljiv.** Sve što skripta pravi nosi `api.RUN`, sufiks iz sata pokretanja
      (šifra restorana, email vlasnika, nalog konobara, artikal, prostorija, šablon smene), a broj
      stola prati najveći u upotrebi. Provereno: dva uzastopna pokretanja nad istom bazom, 173/173
      oba puta. `db.py` sada čita server i bazu iz `appsettings.Development.json` kase — ranije je
      pokazivao na `DigitalRegistryTest` dok je API pisao u `DigitalRegistry`, pa je `dbwalk.py`
      padao na svakoj proveri iz pogrešnog razloga.

### Jezik

- [x] **Poruke grešaka sa backenda stižu na engleskom** („Only a settled bill can be reversed"), dok
      je ostatak interfejsa na srpskom.

      **Odlučeno: mapiranje na frontendu.** API ostaje netaknut — ugovor dele master host i svaki
      budući klijent, a vezivanje za jedan jezik je odluka koja se teško vraća. Prevod je u
      `Frontend/projects/shared/src/lib/http/messages.ts`, provučen kroz `describe()`, koji je jedina
      tačka kroz koju poruka stiže do ekrana (snackbar i obe prijave).

      Cena te odluke: stabilnog `code` polja nema osim `LICENSE_EXPIRED`, pa mapa hvata sam engleski
      tekst — preformulisana poruka na backendu tiho prestane da se prevodi. Zato je fallback
      originalni string a nikad zamena: neprevedena engleska rečenica i dalje kaže šta ne valja, dok
      bi „Nepoznata greška" bacila jedini koristan deo. Pokriveno oko 120 poruka, interpolirane
      vrednosti se čuvaju („Table 12 was not found" → „Sto 12 nije pronađen"), 8 testova.

### Pre nego što bi ovo izašlo iz razvoja

Nije za diplomski, ali neka stoji zapisano da se zna da nije previđeno:

- [ ] Ključ za potpisivanje tokena dolazi iz `appsettings.Development.json`; u produkciji ide u
      secret store, i različit je po hostu.
- [ ] `SeedDemoData` isključen, demo nalozi sa poznatim lozinkama obrisani.
- [ ] CORS na stvarne domene umesto na `localhost`, i HTTPS svuda.
- [ ] `RestaurantId` na tenant tabelama nema strani ključ ka `Restaurants` (vidi
      [`docs/er-diagram.md`](docs/er-diagram.md)) — integritet drži query filter, ne baza. Razmotriti
      FK, uz cenu koju to nosi za brisanje restorana.

---

## Napomene i rizici

- **Identity i email** — ASP.NET Identity traži globalno jedinstven `UserName`. Rešenje:
  `UserName = "{slug}|{email}"`, `Email` za prikaz. Nema zadiranja u Identity store, a isti email
  može postojati u više restorana.
- **QR sesija** — token stola mora nositi `restaurant_id`, inače bi gost-sesija zaobišla query filter.
- **`IgnoreQueryFilters`** — Master API namerno zaobilazi filtere; držati te pozive isključivo
  u `Features/Platform/` handlerima.
- **Obim** — velik ali ravnomerno podeljen posao. Ako rokovi pritisnu, prvo se skraćuju izveštaji
  (Faza 14) i gost-QR ekrani u frontendu; jezgro (kasa + magacin + smene + licence) ostaje.
