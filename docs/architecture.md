# Arhitektura

Dva API hosta nad istim domenom i istom bazom, i dve Angular aplikacije koje ih koriste. Podela nije
kozmetička: kasa radi unutar jednog restorana i sve što pročita prolazi kroz tenant filter, dok
master aplikacija radi **iznad** restorana i taj filter namerno zaobilazi.

## Celina

```mermaid
flowchart TB
    subgraph klijenti["Klijenti"]
        pos["Angular pos<br/>konobar · menadžer · vlasnik"]
        master["Angular master<br/>administrator platforme"]
        gost["Gost sa telefonom<br/>QR kod stola"]
    end

    subgraph hostovi["API hostovi"]
        api["DigitalRegistry.Api<br/>kasa · SignalR hubovi<br/>LicenseGuardMiddleware"]
        madmin["DigitalRegistry.Master.Api<br/>restorani · licence · uplate"]
    end

    subgraph jezgro["Zajedničko jezgro"]
        app["Application<br/>CQRS handleri · validatori · politike"]
        dom["Domain<br/>entiteti · pravila · domenski događaji"]
        infra["Infrastructure<br/>EF Core · Identity · JWT · licenciranje"]
    end

    db[("SQL Server<br/>jedna baza, RestaurantId kolona")]

    pos -->|"REST + JWT"| api
    pos <-->|"WebSocket"| api
    gost -->|"token stola"| api
    master -->|"REST + JWT<br/>audience: Master"| madmin

    api --> app
    madmin --> app
    app --> dom
    app -.->|"interfejsi"| infra
    infra --> db

    api -. "provera licence pri svakom pozivu" .-> db
```

Strelica od `Application` ka `Infrastructure` je isprekidana jer ide obrnuto od zavisnosti: aplikacija
deklariše interfejse (`IDigitalRegistryDbContext`, `IIdentityService`, `ILicenseService`,
`ITenantContext`, `IInventoryAllocator`), a infrastruktura ih implementira i registruje pri
pokretanju. Zato domen i aplikacija ne znaju ni za EF Core ni za ASP.NET.

## Put jednog zahteva

Šta se dešava kad konobar doda pivo na račun:

```mermaid
sequenceDiagram
    autonumber
    participant K as Angular pos
    participant M as Middleware
    participant C as OrdersController
    participant H as UpdateOrderItemHandler
    participant A as InventoryAllocator
    participant DB as SQL Server
    participant S as SignalR

    K->>M: PATCH /api/orders/{id}/items + Bearer
    M->>M: autentifikacija → claim restaurant_id
    M->>DB: važeća licenca?
    alt nije važeća
        M-->>K: 402 LICENSE_EXPIRED
    else važeća
        M->>C: politika ModifyOrder (Konobar/Vlasnik)
        C->>H: MediatR komanda
        Note over H,DB: query filter već ograničava na restoran iz claima
        H->>DB: učitaj račun
        H->>A: razduži zalihe po normativu
        A->>DB: StockQuantity − i red u StockMovements
        H->>DB: SaveChanges (jedna transakcija)
        H-->>C: Result<OrderDto>
        C-->>K: 200 + račun
        H->>S: domenski događaj → hub
        S-->>K: osveži salu kod svih
    end
```

Tri stvari koje ovaj put drži zajedno:

- **Licenca se proverava pre kontrolera**, ne u handleru, pa nijedan endpoint ne može da je zaboravi.
- **Zalihe i račun se snimaju u istom `SaveChanges`.** `InventoryAllocator` ništa ne snima sam —
  jedinicu posla drži pozivalac, pa promet i izmena računa ili prođu zajedno ili nikako.
- **Realtime je posledica, ne zamena za odgovor.** Klijent koji je poslao izmenu dobija je kroz HTTP;
  hub samo kaže ostalima da osveže.

## Slojevi i šta u kom sme

```mermaid
flowchart LR
    D["Domain"] --> A["Application"] --> I["Infrastructure"] --> H["Api / Master.Api"]
    A --> H

    style D fill:#e8f5e9,stroke:#2e7d32
    style A fill:#e3f2fd,stroke:#1565c0
    style I fill:#fff3e0,stroke:#ef6c00
    style H fill:#f3e5f5,stroke:#6a1b9a
```

| Sloj | Sadrži | Ne sme da zna |
| :--- | :--- | :--- |
| Domain | entiteti, enumi, `Money`, domenska pravila i događaji | ništa spoljašnje — nema referenci |
| Application | komande, upiti, handleri, validatori, DTO-ovi, RBAC matrica | EF Core, ASP.NET, SQL |
| Infrastructure | `ApplicationDbContext`, Identity, JWT, SignalR, `LicenseService` | detalje pojedinačnih ekrana |
| Api / Master.Api | kontroleri, middleware, DI, konfiguracija | poslovna pravila |

`Api.Shared` drži ono što oba hosta koriste na isti način: baznu klasu kontrolera koja `Result<T>`
prevodi u statusni kod, obradu grešaka u RFC 7807 obliku, `CurrentUserService` i `TenantContext`.

## Odakle dolazi restoran

```mermaid
flowchart LR
    login["POST /api/auth/login<br/>slug + email + lozinka"] --> ident["IdentityService<br/>UserName = slug|email"]
    ident --> jwt["JWT sa claimom<br/>restaurant_id"]
    jwt --> ctx["TenantContext<br/>čita claim"]
    ctx --> filter["Global query filter<br/>e.RestaurantId == tenant"]
    ctx --> stamp["SaveChanges<br/>upisuje RestaurantId"]
```

Vrednost uvek dolazi iz **potpisanog** tokena, nikad iz rute, zaglavlja ili tela zahteva — inače bi
menjanje jednog polja u zahtevu bilo dovoljno da se vidi tuđi restoran. Isti mehanizam pokriva i
gosta: token QR sesije nosi `restaurant_id` stola, pa anonimna porudžbina ne izlazi iz filtera.

Master API koristi **drugu `Jwt:Audience`**, pa token kase na njemu ne radi i obrnuto, a njegovi
handleri čitaju kroz `IgnoreQueryFilters()` — držano isključivo u `Features/Platform/`, da se
zaobilaženje filtera ne raširi po kodu.

## Provera u više slojeva

Jedna stvar se proverava na više mesta, i to namerno:

| Nivo | Šta hvata | Gde |
| :--- | :--- | :--- |
| Unit testovi | domenska pravila i handleri | `tests/*.UnitTests` — 182 testa |
| Integracioni testovi | rutiranje, RBAC, licencni zid, filteri, in-memory baza | `tests/DigitalRegistry.IntegrationTests` — 26 testova |
| Frontend testovi | interceptori, sesija stola, boje sale, validacija rezervacije | `Frontend/projects/**/*.spec.ts` — 43 testa (vitest) |
| Prolaz kroz API | svaka ruta oba hosta protiv SQL Servera | `tools/api-walkthrough/main.py` |
| Prolaz kroz bazu | šta je svaki endpoint zaista upisao | `tools/api-walkthrough/dbwalk.py` |

Poslednja dva postoje zato što in-memory provajder ne prevodi SQL i ne primenjuje ograničenja: dve
greške koje su ranije nađene (`SetRecipe` i dodavanje stavke na praćen račun) bile su nevidljive za
sve što ne priča sa pravim SQL Serverom, a jedna kasnija (dopuna zaliha bez reda u knjizi prometa)
bila je nevidljiva za sve što gleda samo statusni kod.
