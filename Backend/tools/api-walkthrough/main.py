"""Runs the whole backend walkthrough and reports what broke.

Repeatable: everything it creates carries `api.RUN`, a per-run suffix, and what it cannot make
unique — a table number — follows the highest already in use. So a second run against the same
database is a second pass, not a wall of 409s.
"""
import sys
import api
from api import call, report, TILL


def main():
    state = {}

    print("--- AUTH ---")
    for role in ["owner", "manager", "waiter", "guest"]:
        s, p = call("POST", f"{TILL}/api/auth/login",
                    body={"restaurantSlug": "demo", "email": f"{role}@digitalregistry.local",
                          "password": "Demo#Pass123"},
                    expect=200, label=f"login {role}")
        if s == 200:
            state[role] = p["accessToken"]

    if "owner" not in state:
        print("Prijava ne radi; prekidam.")
        return 1

    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "demo", "email": "waiter@digitalregistry.local",
               "password": "wrong"},
         expect=401, label="login pogresna lozinka")

    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "nepostojeci", "email": "waiter@digitalregistry.local",
               "password": "Demo#Pass123"},
         expect=401, label="login nepostojeci restoran")

    novi_gost = f"novigost{api.RUN}@example.com"

    call("POST", f"{TILL}/api/auth/register",
         body={"restaurantSlug": "demo", "email": novi_gost,
               "password": "Gost#Pass123", "firstName": "Nikola", "lastName": "Nikolic"},
         expect=200, label="register gost")

    call("POST", f"{TILL}/api/auth/register",
         body={"restaurantSlug": "demo", "email": novi_gost,
               "password": "Gost#Pass123", "firstName": "Nikola", "lastName": "Nikolic"},
         expect=409, label="register duplikat")

    call("POST", f"{TILL}/api/auth/register",
         body={"restaurantSlug": "nepostoji", "email": "x@example.com",
               "password": "Gost#Pass123", "firstName": "X", "lastName": "Y"},
         expect=404, label="register nepostojeci restoran")

    owner, manager, waiter, guest = state["owner"], state["manager"], state["waiter"], state["guest"]

    print("\n--- LICENCA ---")
    call("GET", f"{TILL}/api/license/status", owner, expect=200, label="status licence")
    call("GET", f"{TILL}/api/license/status", expect=401, label="status bez tokena")

    print("\n--- JELOVNIK ---")
    s, menu = call("GET", f"{TILL}/api/menu", waiter, expect=200, label="meni (konobar)")
    state["menu"] = menu or []
    espresso = next(m for m in state["menu"] if m["name"] == "Espresso")

    call("GET", f"{TILL}/api/menu?category=Coffee", waiter, expect=200, label="meni po kategoriji")
    call("GET", f"{TILL}/api/menu/items/{espresso['id']}", manager, expect=200,
         label="artikal + normativ")
    call("GET", f"{TILL}/api/menu/items/{espresso['id']}", waiter, expect=403,
         label="konobar trazi normativ")

    rakija = f"Domaca rakija {api.RUN}"

    s, novi = call("POST", f"{TILL}/api/menu/items", manager,
                   body={"name": rakija, "category": "Zestoka pica", "unitPrice": 320},
                   expect=200, label="nov artikal")
    if s == 200:
        state["novi"] = novi["id"]

    call("POST", f"{TILL}/api/menu/items", manager,
         body={"name": "Espresso", "category": "Coffee", "unitPrice": 200},
         expect=409, label="artikal duplo ime")

    call("POST", f"{TILL}/api/menu/items", manager,
         body={"id": state.get("novi"), "name": rakija, "category": "Zestoka pica",
               "unitPrice": 350},
         expect=200, label="izmena artikla")

    s, val = call("GET", f"{TILL}/api/reports/inventory?from={api.utc(-1)}&to={api.utc(1)}", owner,
                  expect=200, label="vrednost zaliha (pocetna)")
    state["gin"] = next(l["ingredientId"] for l in val["lines"] if l["name"] == "Gin")

    if state.get("novi"):
        call("PUT", f"{TILL}/api/menu/items/{state['novi']}/recipe", manager,
             body={"menuItemId": state["novi"],
                   "lines": [{"ingredientId": state["gin"], "quantityRequired": 30}]},
             expect=200, label="postavljanje normativa")

        call("PUT", f"{TILL}/api/menu/items/{state['novi']}/recipe", manager,
             body={"menuItemId": state["novi"],
                   "lines": [{"ingredientId": state["gin"], "quantityRequired": 30},
                             {"ingredientId": state["gin"], "quantityRequired": 10}]},
             expect=400, label="normativ sa duplim sastojkom")

        call("DELETE", f"{TILL}/api/menu/items/{state['novi']}", manager, expect=204,
             label="brisanje artikla bez istorije")

    print("\n--- STOLOVI I RASPORED ---")
    s, fp = call("GET", f"{TILL}/api/floor-plan", waiter, expect=200, label="raspored")
    state["fp"] = fp
    state["table"] = fp["rooms"][0]["tables"][0]
    table = state["table"]

    call("GET", f"{TILL}/api/floor-plan?includeInactive=true", owner, expect=200,
         label="raspored + neaktivni")
    call("GET", f"{TILL}/api/tables/availability?partySize=2&from={api.utc()}&to={api.at_hours(6)}",
         guest, expect=200, label="slobodni stolovi (gost)")
    call("GET", f"{TILL}/api/tables/availability?partySize=2&from={api.utc()}&to={api.utc(1)}",
         guest, expect=400, label="slobodni stolovi preko 12h")
    call("GET", f"{TILL}/api/tables/{table['id']}", manager, expect=200, label="sto po id")
    call("GET", f"{TILL}/api/tables/{table['id']}", waiter, expect=403, label="konobar trazi sto")

    # One past the highest in use, rather than a fixed 99 a crashed run may have left behind.
    broj = 1 + max((t["tableNumber"]
                    for t in [*[x for r in fp["rooms"] for x in r["tables"]], *fp["unplacedTables"]]),
                   default=0)

    s, novisto = call("POST", f"{TILL}/api/tables", manager,
                      body={"tableNumber": broj, "capacity": 4}, expect=201, label="nov sto")
    if s == 201:
        state["novisto"] = novisto["id"]

    call("POST", f"{TILL}/api/tables", manager, body={"tableNumber": broj, "capacity": 4},
         expect=409, label="sto duplo broj")

    if state.get("novisto"):
        call("PUT", f"{TILL}/api/tables/{state['novisto']}", manager,
             body={"id": state["novisto"], "tableNumber": broj, "capacity": 6, "isActive": True},
             expect=204, label="izmena stola")
        call("POST", f"{TILL}/api/tables/{state['novisto']}/qr-code", manager, expect=200,
             label="rotacija QR koda")

    call("GET", f"{TILL}/api/tables/qr-codes", manager, expect=200, label="list QR kodova")
    call("GET", f"{TILL}/api/tables/qr-codes?includeInactive=true", owner, expect=200,
         label="QR kodovi + neaktivni")
    call("GET", f"{TILL}/api/tables/qr-codes", waiter, expect=403,
         label="konobar trazi QR kodove")

    terasa = f"Terasa {api.RUN}"

    s, room = call("POST", f"{TILL}/api/floor-plan/rooms", owner,
                   body={"name": terasa, "canvasWidth": 800, "canvasHeight": 600},
                   expect=200, label="nova prostorija")
    if s == 200:
        state["room"] = room["id"]
        if state.get("novisto"):
            call("PUT", f"{TILL}/api/floor-plan/rooms/{room['id']}/layout", owner,
                 body={"roomId": room["id"], "tables": [
                     {"tableId": state["novisto"], "positionX": 100, "positionY": 100,
                      "width": 80, "height": 80, "shape": 1, "rotation": 0}]},
                 expect=200, label="snimanje rasporeda")
            call("PUT", f"{TILL}/api/floor-plan/rooms/{room['id']}/layout", owner,
                 body={"roomId": room["id"], "tables": [
                     {"tableId": state["novisto"], "positionX": 790, "positionY": 100,
                      "width": 80, "height": 80, "shape": 1, "rotation": 0}]},
                 expect=400, label="sto van platna")
        call("PUT", f"{TILL}/api/floor-plan/rooms/{room['id']}", owner,
             body={"id": room["id"], "name": terasa, "displayOrder": 5,
                   "canvasWidth": 900, "canvasHeight": 700},
             expect=200, label="izmena prostorije")
        call("PUT", f"{TILL}/api/floor-plan/rooms/{room['id']}/layout", waiter,
             body={"roomId": room["id"], "tables": []}, expect=403,
             label="konobar menja raspored")
        call("DELETE", f"{TILL}/api/floor-plan/rooms/{room['id']}", owner, expect=204,
             label="brisanje prostorije")

    if state.get("novisto"):
        call("DELETE", f"{TILL}/api/tables/{state['novisto']}", manager, expect=204,
             label="brisanje stola bez istorije")

    # Waiter ids, needed by the shift walkthrough.
    s, week = call("GET", f"{TILL}/api/shifts/week?date={api.today()}", manager, expect=200,
                   label="nedeljna mreza (prazna)")
    state["waiters"] = [w["waiterId"] for w in (week or {}).get("waiters", [])]

    import walk2
    walk2.run(state)

    # Now that the table carries orders, deleting it must be refused.
    call("DELETE", f"{TILL}/api/tables/{table['id']}", manager, expect=409,
         label="brisanje stola sa istorijom")

    import walk4
    walk4.run(state)

    import walk3
    walk3.run(state)

    return report()


if __name__ == "__main__":
    sys.exit(main())
