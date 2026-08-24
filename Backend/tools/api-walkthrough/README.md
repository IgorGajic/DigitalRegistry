# API walkthrough

Exercises every endpoint of both APIs against a running instance and reports what broke.

Written because unit tests do not catch the failures that only appear once EF Core is talking to
SQL Server — the two `DbUpdateConcurrencyException` bugs this script found were both invisible to the
in-memory provider.

## Running it

Start from an empty database, so the demo seed is present and nothing is left over from a previous
run:

```powershell
dotnet ef database drop --force --project src/DigitalRegistry.Infrastructure --startup-project src/DigitalRegistry.Infrastructure
dotnet run --project src/DigitalRegistry.Api          # http://localhost:5275
dotnet run --project src/DigitalRegistry.Master.Api   # http://localhost:5285
python tools/api-walkthrough/main.py
```

It creates data as it goes — a restaurant, a licence, orders, shifts — so run it against a
development database only, and re-drop before running it again.

## Layout

| File | What it covers |
| :--- | :--- |
| `api.py` | HTTP helper; every call states the status it expects |
| `main.py` | Sign-in, licence, menu, tables and the floor plan |
| `walk2.py` | Orders, voids, reservations, shifts, inventory, reports, guest QR |
| `walk3.py` | The master API, licence enforcement, and isolation between two live venues |
| `walk4.py` | Edge cases and permissions the earlier files leave uncovered |
| `db.py` | Reads the database through `sqlcmd`, and records pass/fail for row-level checks |
| `dbwalk.py` | Calls every endpoint that writes, then checks what it left in the database |

## Two different questions

`main.py` asks whether the API answered correctly. `dbwalk.py` asks whether the row is right — they
come apart more often than they look. A handler can answer 200 having written nothing, file a row
under the wrong restaurant, or move stock without leaving a ledger entry, and none of that shows in
the response body. `dbwalk.py` found exactly that: `POST /api/inventory/ingredients/{id}/restock`
raised `StockQuantity` without writing a `StockMovement`, so `SUM(Quantity)` silently stopped
reconciling with the balance it is supposed to explain.

It also checks the arithmetic reports claim: turnover against `SUM(Transactions.Amount)`, the
best-seller list against the paid order lines, stock valuation against quantity times moving average,
and the platform dashboard against the tables it counts.

```powershell
python tools/api-walkthrough/dbwalk.py
```

Unlike `main.py`, this one is repeatable: names carry a per-run suffix, table numbers follow the
highest in use, and it removes the shifts it generates. Point `db.DATABASE` at whatever database the
API is running against — the two must be the same, or every row check fails for the wrong reason.
