# DigitalRegistry

Digitalna kasa za restorane, sa zasebnom master aplikacijom preko koje se restorani registruju i
licenciraju. Diplomski rad.

Jedna instalacija opslužuje više restorana: prijava ide na šifru restorana, a JWT nosi
`restaurant_id` po kome EF Core global query filteri odsecaju tuđe podatke. Kasa bez važeće licence
odgovara **HTTP 402** na svaki poziv.

- **Backend:** .NET 10, Clean Architecture, MediatR (CQRS), EF Core + SQL Server, JWT + RBAC, SignalR
- **Frontend:** Angular 21 workspace (Angular Material + CDK), aplikacije `pos` i `master`
- **Valuta:** RSD · **Jezik interfejsa:** srpski
- **Račun je simulacija** — nema ESIR-a ni fiskalizacije; otisak nosi tu napomenu

---

## Preduslovi

| Alat | Verzija | Napomena |
| :--- | :--- | :--- |
| .NET SDK | 10.0 | `dotnet --version` |
| SQL Server | Express 2019+ | podrazumevana instanca `localhost\SQLEXPRESS` |
| Node.js | 22+ | uz npm 11 |
| Python | 3.13 (opciono) | samo za skripte u `Backend/tools/` |

Baza se ne pravi ručno: oba API hosta pri pokretanju primenjuju migracije, a kasa u razvojnom
režimu ubacuje i demo restoran sa podacima (`SeedDemoData: true`).

---

## Pokretanje

Četiri procesa, svaki u svom terminalu. Backend prvo — frontend bez njega prikazuje samo prijavu.

```powershell
# 1. Kasa (API)          http://localhost:5275   Swagger na /swagger
dotnet run --project Backend/src/DigitalRegistry.Api

# 2. Master (API)        http://localhost:5285   Swagger na /swagger
dotnet run --project Backend/src/DigitalRegistry.Master.Api

# 3. Kasa (Angular)      http://localhost:4200
cd Frontend
npm install            # samo prvi put
npx ng build shared    # samo prvi put i posle izmena u projects/shared
npx ng serve pos

# 4. Master (Angular)    http://localhost:4300
npx ng serve master
```

`projects/shared` je Angular biblioteka: `pos` i `master` je koriste kroz `dist/`, pa mora da bude
sagrađena pre prvog `ng serve`. Ako je gradiš dok `ng serve` već radi, restartuj `ng serve` — watcher
ostane na starim tipovima i prijavljuje greške kojih u kodu nema.

### Drugi konekcioni string

Podrazumevani stoji u `appsettings.Development.json` svakog hosta. Za drugu bazu — na primer da se
testira bez diranja razvojne — dovoljna je promenljiva okruženja:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost\SQLEXPRESS;Initial Catalog=DigitalRegistryTest;Integrated Security=SSPI;MultipleActiveResultSets=true;TrustServerCertificate=True;"
```

---

## Demo nalozi

Kasa traži **šifru restorana** uz email, jer isti email može postojati u više restorana
(Identity čuva `UserName` kao `{slug}|{email}`).

| Aplikacija | Šifra restorana | Email | Lozinka | Uloga |
| :--- | :--- | :--- | :--- | :--- |
| Kasa | `demo` | `owner@digitalregistry.local` | `Demo#Pass123` | Vlasnik |
| Kasa | `demo` | `manager@digitalregistry.local` | `Demo#Pass123` | Menadžer |
| Kasa | `demo` | `waiter@digitalregistry.local` | `Demo#Pass123` | Konobar |
| Master | — | `admin@digitalregistry.local` | `Admin#Pass123` | Administrator platforme |

Lozinke su poznate i namerno slabe; seeder ih upisuje samo u razvojnom režimu i to uz upozorenje u
logu.

---

## Šta koja uloga vidi

| Ekran | Ruta | Uloga |
| :--- | :--- | :--- |
| Sala (glavni ekran) | `/sala` | Konobar+ |
| Račun stola | `/sala/:tableId` | Konobar+ |
| Poslednji računi | `/racuni` | Konobar+ (storno plaćenog: Menadžer+) |
| Rezervacije | `/rezervacije` | Konobar+ (otkazivanje: Menadžer+) |
| Smene | `/smene` | Menadžer+ |
| Magacin | `/magacin` | Menadžer+ |
| Jelovnik | `/jelovnik` | Menadžer+ |
| Raspored stolova | `/raspored` | Vlasnik |
| Izveštaji | `/izvestaji` | Vlasnik |
| Zaposleni | `/zaposleni` | Vlasnik |

RBAC matrica je na jednom mestu — `Application/Common/Security/AuthorizationPolicies.cs`. Rute u
frontendu je preslikavaju da se korisniku ne bi nudio ekran koji bi API odbio sa 403.

---

## Struktura

```text
Backend/
  src/
    DigitalRegistry.Domain           entiteti, enumi, domenska pravila
    DigitalRegistry.Application      CQRS handleri, validatori, DTO-ovi, politike
    DigitalRegistry.Infrastructure   EF Core, Identity, JWT, SignalR, licenciranje
    DigitalRegistry.Api              kasa + LicenseGuardMiddleware + hubovi
    DigitalRegistry.Api.Shared       zajedničko za oba hosta (kontroler baza, greške, tenant)
    DigitalRegistry.Master.Api       platformski admin API, bez tenant filtera
  tests/                             unit testovi (Domain, Application) i integracioni projekat
  tools/api-walkthrough/             provera oba API-ja i baze na živoj instanci
Frontend/
  projects/pos                       kasa
  projects/master                    administracija platforme
  projects/shared                    auth, interceptori, modeli, formatiranje
  projects/shared/ui                 dijalozi i grafikon (vuku Angular Material)
  projects/shared/realtime           SignalR hub konekcija
```

Biblioteka ima tri ulazne tačke, i to iz jednog razloga: obe aplikacije uvoze iz `shared` pre nego
što znaju ko gleda, pa je sve što stoji tu u njihovom **početnom** bundle-u. Bundler deli chunkove
po izvornom fajlu a biblioteka je jedan fajl, tako da je ispred ekrana za prijavu stajao i ceo
SignalR i ceo Material dijalog sloj — ni jedno ni drugo prijavi ne treba. Razdvajanje je skinulo
224 kB sa kase i 161 kB sa mastera.

Realtime ide preko dva huba: `/hubs/kitchen` (kuhinja) i `/hubs/order` (sala).

Detaljnije: [`docs/architecture.md`](docs/architecture.md) (slojevi, put jednog zahteva, odakle
dolazi restoran) i [`docs/er-diagram.md`](docs/er-diagram.md) (model podataka i pravila koja drži
baza).

---

## Provera

```powershell
dotnet build Backend/DigitalRegistry.slnx
dotnet test  Backend/DigitalRegistry.slnx

cd Frontend
npx ng test shared --watch=false
npx ng test pos --watch=false
```

`dotnet test` ne traži ništa instalirano: unit testovi rade nad domenom i handlerima, a integracioni
dižu ceo host u procesu nad in-memory bazom sa demo podacima.

Frontend testovi (vitest, 82 — 53 `shared` + 29 `pos`) pokrivaju ono što se ne vidi na ekranu:
interceptore (402 nasuprot 401 nasuprot sesije stola), prevod poruka sa backenda, srpsku množinu i
proteklo vreme, geometriju grafikona, sesiju stola iz QR koda, pravila boja na sali, i validaciju
unosa rezervacije. Backend ih ima 208 (131 domen, 51 aplikacija, 26 integracionih).

Uz to, dve skripte rade protiv **pokrenutih** API-ja i žive baze:

```powershell
python Backend/tools/api-walkthrough/main.py      # svaka ruta oba API-ja, sa očekivanim statusom
python Backend/tools/api-walkthrough/dbwalk.py    # svaki endpoint koji piše + provera reda u bazi
```

Obe su ponovljive — sve što prave nosi sufiks jedinstven za pokretanje — pa se mogu vrteti nad
istom bazom više puta. Detalji su u `Backend/tools/api-walkthrough/README.md`.

Nova migracija:

```powershell
dotnet ef migrations add <Naziv> --project Backend/src/DigitalRegistry.Infrastructure --startup-project Backend/src/DigitalRegistry.Infrastructure
dotnet ef database update           --project Backend/src/DigitalRegistry.Infrastructure --startup-project Backend/src/DigitalRegistry.Infrastructure
```

Infrastructure je i startni projekat: `DesignTimeDbContextFactory` pravi kontekst bez dizanja
veb-hosta, a API projekti ne referenciraju `Microsoft.EntityFrameworkCore.Design`.

---

## Ograničenja

- Račun je simulacija: nijedan fiskalni uređaj ga nije video i tako je i označen na otisku.
- Poruke grešaka sa backenda su na engleskom, dok je interfejs na srpskom.
- Sve je podešeno za lokalni razvoj — ključ za potpisivanje tokena, CORS i demo podaci nisu za
  produkciju.
