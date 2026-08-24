# Model podataka

Šema je izvedena iz domenskog modela EF Core migracijama; ovo je njen pregled. Dijagrami su Mermaid,
pa se renderuju na GitHubu, a za pisani deo se izvoze u sliku:

```powershell
mkdir docs/img
npx -y @mermaid-js/mermaid-cli -i docs/er-diagram.md   -o docs/img/er.png   -w 2400
npx -y @mermaid-js/mermaid-cli -i docs/architecture.md -o docs/img/arch.png -w 1600
```

Izlazni direktorijum mora da postoji, a alat pravi po jednu sliku za svaki dijagram u fajlu i
numeriše ih (`er-1.png`, `er-2.png` …).

## Kako se restorani razdvajaju

Sve u jednoj bazi. Svaka tabela koja pripada restoranu nosi kolonu `RestaurantId`, koju
`ApplicationDbContext` postavlja pri upisu i po kojoj EF Core global query filter odseca čitanje.
Presudno je da **filter nije stvar upita nego konteksta**: handler piše običan `context.Orders`, a
red tuđeg restorana ne postoji za njega.

Dve namerne posledice:

- `RestaurantId` na tim tabelama **nema strani ključ** ka `Restaurants`. Integritet drži filter i
  stampanje pri upisu, ne baza. Cena toga je da bi greška u kodu mogla da upiše red sa pogrešnim
  restoranom, a baza ne bi imala šta da kaže.
- `Table.QrCodeToken` je jedinstven **na nivou platforme**, a ne po restoranu: gost skenira kod pre
  nego što se zna o kom je restoranu reč, pa token mora sam da razreši restoran.

Prirodni ključevi koji se ponavljaju između restorana su jedinstveni tek u paru sa `RestaurantId`:
`Tables(RestaurantId, TableNumber)`, `MenuItems(RestaurantId, Name)`,
`Ingredients(RestaurantId, Name)`, `Rooms(RestaurantId, Name)`, `ShiftTemplates(RestaurantId, Name)`.

## Poslovni deo

```mermaid
erDiagram
    RESTAURANTS ||--o{ ROOMS : "ima"
    RESTAURANTS ||--o{ TABLES : "ima"
    RESTAURANTS ||--o{ MENU_ITEMS : "nudi"
    RESTAURANTS ||--o{ INGREDIENTS : "drzi"
    ROOMS ||--o{ TABLES : "raspoređuje"
    TABLES ||--o{ ORDERS : "nosi"
    TABLES ||--o{ RESERVATIONS : "rezerviše se"
    MENU_ITEMS ||--o{ RECIPE_ITEMS : "normativ"
    INGREDIENTS ||--o{ RECIPE_ITEMS : "ulazi u"
    ORDERS ||--|{ ORDER_ITEMS : "sadrži"
    MENU_ITEMS ||--o{ ORDER_ITEMS : "prodato kao"
    ORDERS ||--o{ TRANSACTIONS : "naplaćen"
    TRANSACTIONS ||--o| TRANSACTIONS : "protivstavka"
    ORDERS ||--o{ VOID_RECORDS : "storniran"
    ORDERS ||--o{ STOCK_MOVEMENTS : "razdužuje"
    INGREDIENTS ||--o{ STOCK_MOVEMENTS : "knjiži se"
    INGREDIENTS ||--o{ STOCK_ENTRIES : "nabavlja se"
    STOCK_ENTRIES ||--o| STOCK_MOVEMENTS : "ulaz"
    USERS ||--o{ ORDERS : "konobar"
    USERS ||--o{ RESERVATIONS : "gost"
    USERS ||--o{ SHIFTS : "radi"
    SHIFT_TEMPLATES ||--o{ SHIFT_ASSIGNMENTS : "šablon"
    USERS ||--o{ SHIFT_ASSIGNMENTS : "dodeljen"
    SHIFT_ASSIGNMENTS ||--o{ SHIFTS : "generiše"

    RESTAURANTS {
        guid Id PK
        string Slug UK "šifra za prijavu"
        string Name
        string CurrencyCode "RSD"
        string TimeZoneId "za smene"
        bool IsActive
    }
    ROOMS {
        guid Id PK
        guid RestaurantId
        string Name
        int CanvasWidth
        int CanvasHeight
    }
    TABLES {
        guid Id PK
        guid RestaurantId
        guid RoomId FK "null = neraspoređen"
        int TableNumber
        int Capacity
        guid QrCodeToken UK "jedinstven globalno"
        int PositionX
        int PositionY
        int Shape
        bool IsActive
    }
    MENU_ITEMS {
        guid Id PK
        guid RestaurantId
        string Name
        string Category
        decimal UnitPrice
        bool IsAvailable "gasi ga nedostatak sastojka"
    }
    INGREDIENTS {
        guid Id PK
        guid RestaurantId
        string Name
        decimal StockQuantity
        decimal AveragePurchasePrice "klizeći prosek"
        decimal LowStockThreshold
        int Unit
    }
    RECIPE_ITEMS {
        guid Id PK
        guid MenuItemId FK
        guid IngredientId FK
        decimal QuantityRequired "po porciji"
    }
    ORDERS {
        guid Id PK
        guid RestaurantId
        guid TableId FK
        guid WaiterId FK "null za QR porudžbinu"
        int Status "Open..Voided"
        datetime CreatedAt
    }
    ORDER_ITEMS {
        guid Id PK
        guid OrderId FK
        guid MenuItemId FK
        int Quantity
        decimal UnitPrice "cena u trenutku prodaje"
        string Notes
    }
    TRANSACTIONS {
        guid Id PK
        guid OrderId FK
        guid ProcessedByWaiterId FK
        decimal Amount "≥0 uplata, ≤0 protivstavka"
        int PaymentMethod
        guid ReversesTransactionId FK
    }
    VOID_RECORDS {
        guid Id PK
        guid OrderId FK
        guid MenuItemId FK
        int Type "Item/OpenOrder/PaidOrder"
        decimal Quantity
        decimal Amount
        string Reason "obavezan"
        guid PerformedByUserId FK
        guid ApprovedByUserId FK
    }
    STOCK_MOVEMENTS {
        guid Id PK
        guid IngredientId FK
        int Type "Purchase/Sale/Return/Adjustment"
        decimal Quantity "označena: + ulaz, − izlaz"
        decimal BalanceAfter
        guid OrderId FK
        guid StockEntryId FK
        string Note
    }
    STOCK_ENTRIES {
        guid Id PK
        guid IngredientId FK
        decimal Quantity
        decimal PurchaseUnitPrice
        decimal TotalCost
        string Supplier
        datetime EntryDateUtc
    }
    RESERVATIONS {
        guid Id PK
        guid RestaurantId
        guid TableId FK
        guid GuestId FK
        datetime StartTime
        datetime EndTime
        int PartySize
        int Status
    }
    SHIFT_TEMPLATES {
        guid Id PK
        guid RestaurantId
        string Name
        time StartTime "lokalno vreme"
        time EndTime
        bool IsActive
    }
    SHIFT_ASSIGNMENTS {
        guid Id PK
        guid WaiterId FK
        guid ShiftTemplateId FK
        int Days "bit polje, Pon–Ned"
        date ValidFrom
        date ValidTo
    }
    SHIFTS {
        guid Id PK
        guid WaiterId FK
        guid ShiftAssignmentId FK "null = ad-hoc"
        datetime StartTime "UTC"
        datetime EndTime
    }
    USERS {
        guid Id PK
        guid RestaurantId "null = admin platforme"
        string UserName UK "slug|email"
        string Email
        int Role
    }
```

## Platforma i licence

Ove tabele **nisu** tenant-scoped: master aplikacija ih vidi sve, a kasa ih dodiruje samo kroz
proveru licence.

```mermaid
erDiagram
    RESTAURANTS ||--o{ LICENSES : "licencira se"
    LICENSES ||--o{ LICENSE_PAYMENTS : "uplate"
    USERS ||--o{ LICENSES : "izdao admin"

    LICENSES {
        guid Id PK
        guid RestaurantId FK
        int Plan "1/3/6/12 meseci"
        datetime StartsAtUtc
        datetime ExpiresAtUtc
        int Status "Active/Suspended/Cancelled"
        decimal Price
        guid IssuedByAdminId
        string Notes "razlog suspenzije/otkaza"
    }
    LICENSE_PAYMENTS {
        guid Id PK
        guid LicenseId FK
        decimal Amount
        datetime PaidAtUtc
        int PaymentMethod
        string ReferenceNumber
        guid RecordedByAdminId
    }
```

Vrednost `LicensePlan` **je** broj meseci (`Monthly = 1`, `Quarterly = 3`, `SemiAnnual = 6`,
`Annual = 12`), pa je produženje `ExpiresAtUtc.AddMonths((int)plan)` i nema tabele preslikavanja.
Status važenja se **izvodi** iz `ExpiresAtUtc` u odnosu na sada — ne postoji noćni posao koji bi
licencu prebacio u „istekla", pa ne postoji ni prozor u kome bi zapis lagao.

## Pravila koja drži baza

Ograničenja nisu ukras — ona hvataju ono što bi promaklo grešci u kodu:

| Ograničenje | Šta sprečava |
| :--- | :--- |
| `CK_Transaction_Amount_Sign` | uplata mora biti ≥ 0, a protivstavka ≤ 0 |
| `IX_Transactions_OrderId` (filtriran, `ReversesTransactionId IS NULL`) | dve uplate na istom računu; protivstavka je namerno drugi red |
| `IX_Transactions_ReversesTransactionId` (filtriran) | dve protivstavke nad istom uplatom |
| `CK_StockMovement_Quantity_NonZero` | promet koji ništa ne pomera |
| `CK_Ingredient_Stock_NonNegative` | negativna zaliha |
| `CK_License_Period` | licenca koja ističe pre nego što počne |
| `CK_Reservation_Period`, `CK_Shift_Period` | period koji se završava pre početka |
| `CK_ShiftTemplate_Period` | šablon smene bez trajanja (`StartTime = EndTime`) |
| `CK_Table_Rotation_Range` | rotacija stola van 0–359° |

Brisanja: `OrderItems` i `RecipeItems` idu kaskadno sa roditeljem; `Tables.RoomId` i
`Shifts.ShiftAssignmentId` su `SET NULL` — sto preživi brisanje prostorije, a već generisana smena
preživi brisanje stalne dodele i samo prestane da tvrdi da pripada rasporedu. Sve ostalo je
`NO ACTION`, pa se istorija ne može tiho izgubiti.
