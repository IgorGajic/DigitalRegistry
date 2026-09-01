"""Third part: the master API, and cross-tenant isolation between two live venues."""
import api
from api import call, utc, today, TILL, MASTER


def run(state):
    # The second venue is created fresh each run: a slug is unique for the whole platform, so a
    # fixed one turns every later run into a 409 that aborts this file before it starts.
    slug = f"kafana-test-{api.RUN}"
    owner_email = f"vlasnik{api.RUN}@kafanatest.rs"

    print("\n--- MASTER API ---")
    s, admin = call("POST", f"{MASTER}/api/platform/auth/login",
                    body={"email": "admin@digitalregistry.local", "password": "Admin#Pass123"},
                    expect=200, label="prijava platform admina")
    if s != 200:
        return
    a = admin["accessToken"]

    call("POST", f"{MASTER}/api/platform/auth/login",
         body={"email": "owner@digitalregistry.local", "password": "Demo#Pass123"},
         expect=401, label="vlasnik pokusava master")

    # A till token must not work against the master API.
    call("GET", f"{MASTER}/api/platform/restaurants", state["owner"],
         expect=401, label="token kase na master API-ju")

    call("GET", f"{MASTER}/api/platform/dashboard", a, expect=200, label="dashboard")
    call("GET", f"{MASTER}/api/platform/restaurants", a, expect=200, label="lista restorana")
    call("GET", f"{MASTER}/api/platform/restaurants?search=demo", a, expect=200, label="pretraga restorana")
    call("GET", f"{MASTER}/api/platform/licenses", a, expect=200, label="lista licenci")

    s, r2 = call("POST", f"{MASTER}/api/platform/restaurants", a,
                 body={"name": f"Kafana Test {api.RUN}", "slug": slug, "address": "Neka 1",
                       "contactEmail": "info@kafanatest.rs", "currencyCode": "RSD"},
                 expect=201, label="nov restoran")
    if s != 201:
        return
    rid = r2["id"]

    call("POST", f"{MASTER}/api/platform/restaurants", a,
         body={"name": "Opet", "slug": slug}, expect=409, label="restoran duplo ime")

    call("POST", f"{MASTER}/api/platform/restaurants", a,
         body={"name": "Los slug", "slug": "Ima Razmak"}, expect=400, label="restoran los slug")

    call("GET", f"{MASTER}/api/platform/restaurants/{rid}", a, expect=200, label="restoran po id")

    call("PUT", f"{MASTER}/api/platform/restaurants/{rid}", a,
         body={"id": rid, "name": f"Kafana Test {api.RUN} 2", "address": "Neka 2",
               "currencyCode": "RSD"},
         expect=200, label="izmena restorana")

    s, own = call("POST", f"{MASTER}/api/platform/restaurants/{rid}/owner", a,
                  body={"restaurantId": rid, "email": owner_email,
                        "password": "Vlasnik#123", "firstName": "Pera", "lastName": "Peric"},
                  expect=200, label="vlasnicki nalog")

    call("POST", f"{MASTER}/api/platform/restaurants/{rid}/owner", a,
         body={"restaurantId": rid, "email": f"drugi{api.RUN}@kafanatest.rs",
               "password": "Vlasnik#123",
               "firstName": "Drugi", "lastName": "Vlasnik"},
         expect=409, label="drugi vlasnik istog restorana")

    # The new venue has no licence yet, so the till must refuse it with 402.
    s, tok = call("POST", f"{TILL}/api/auth/login",
                  body={"restaurantSlug": slug, "email": owner_email,
                        "password": "Vlasnik#123"},
                  expect=200, label="prijava vlasnika novog restorana")
    new_owner = tok["accessToken"] if s == 200 else None

    if new_owner:
        call("GET", f"{TILL}/api/menu", new_owner, expect=402, label="kasa bez licence -> 402")
        call("GET", f"{TILL}/api/license/status", new_owner, expect=200,
             label="status licence prolazi kroz gard")

    s, lic = call("POST", f"{MASTER}/api/platform/licenses", a,
                  body={"restaurantId": rid, "plan": 1, "price": 15000, "notes": "Prva"},
                  expect=200, label="izdavanje licence")
    lid = lic["id"] if s == 200 else None

    call("POST", f"{MASTER}/api/platform/licenses", a,
         body={"restaurantId": rid, "plan": 1, "price": 15000},
         expect=409, label="druga licenca istom restoranu")

    if new_owner:
        call("GET", f"{TILL}/api/menu", new_owner, expect=200, label="kasa posle licence -> 200")

    if lid:
        call("POST", f"{MASTER}/api/platform/licenses/{lid}/payments", a,
             body={"licenseId": lid, "amount": 15000, "paymentMethod": 2,
                   "referenceNumber": "IZV-1"},
             expect=200, label="evidencija uplate")
        call("GET", f"{MASTER}/api/platform/licenses/{lid}/payments", a, expect=200,
             label="lista uplata")

        call("POST", f"{MASTER}/api/platform/licenses/{lid}/suspend", a,
             body={"reason": "Neplacena faktura"}, expect=200, label="suspenzija licence")
        if new_owner:
            call("GET", f"{TILL}/api/menu", new_owner, expect=402,
                 label="kasa posle suspenzije -> 402")

        call("POST", f"{MASTER}/api/platform/licenses/{lid}/reactivate", a, expect=200,
             label="reaktivacija licence")
        if new_owner:
            call("GET", f"{TILL}/api/menu", new_owner, expect=200,
                 label="kasa posle reaktivacije -> 200")

        call("POST", f"{MASTER}/api/platform/licenses/{lid}/renew", a,
             body={"licenseId": lid, "plan": 3, "price": 40000}, expect=200,
             label="produzenje licence")

    print("\n--- IZOLACIJA IZMEDJU RESTORANA ---")
    if new_owner:
        # The new venue starts empty; the demo venue's data must be invisible to it.
        s, menu2 = call("GET", f"{TILL}/api/menu", new_owner, expect=200,
                        label="meni drugog restorana")
        if s == 200 and menu2:
            print(f"    PAD  meni drugog restorana nije prazan: {len(menu2)} stavki")
            state.setdefault("leaks", []).append("menu")

        s, fp2 = call("GET", f"{TILL}/api/floor-plan", new_owner, expect=200,
                      label="raspored drugog restorana")
        if s == 200:
            tables = sum(len(r["tables"]) for r in fp2["rooms"]) + len(fp2["unplacedTables"])
            if tables:
                print(f"    PAD  raspored drugog restorana nije prazan: {tables} stolova")
                state.setdefault("leaks", []).append("floor-plan")

        # Reaching another venue's order by id must not work either.
        if state.get("demo_order"):
            call("GET", f"{TILL}/api/orders/{state['demo_order']}", new_owner, expect=404,
                 label="tudji racun po id -> 404")

    call("POST", f"{MASTER}/api/platform/restaurants/{rid}/suspend", a, expect=200,
         label="gasenje restorana")
    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": slug, "email": owner_email, "password": "Vlasnik#123"},
         expect=401, label="prijava u ugasen restoran")
    call("POST", f"{MASTER}/api/platform/restaurants/{rid}/activate", a, expect=200,
         label="ponovno paljenje restorana")

    if lid:
        call("POST", f"{MASTER}/api/platform/licenses/{lid}/cancel", a,
             body={"reason": "Kraj ugovora"}, expect=200, label="otkazivanje licence")
        call("POST", f"{MASTER}/api/platform/licenses/{lid}/renew", a,
             body={"licenseId": lid, "plan": 1, "price": 100}, expect=409,
             label="produzenje otkazane licence")
