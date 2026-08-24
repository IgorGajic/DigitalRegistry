"""Staff management: the accounts a venue creates for itself."""
from api import call, TILL


def run(state):
    owner, manager, waiter = state["owner"], state["manager"], state["waiter"]

    print("\n--- ZAPOSLENI ---")
    call("GET", f"{TILL}/api/staff", owner, expect=200, label="lista zaposlenih")
    call("GET", f"{TILL}/api/staff", manager, expect=403, label="menadzer trazi zaposlene")
    call("GET", f"{TILL}/api/staff", waiter, expect=403, label="konobar trazi zaposlene")

    s, created = call("POST", f"{TILL}/api/staff", owner,
                      body={"email": "novikonobar@demo.rs", "password": "Konobar#123",
                            "firstName": "Jovan", "lastName": "Jovanovic", "role": 2},
                      expect=200, label="nov konobar")

    call("POST", f"{TILL}/api/staff", owner,
         body={"email": "novikonobar@demo.rs", "password": "Konobar#123",
               "firstName": "Jovan", "lastName": "Jovanovic", "role": 2},
         expect=409, label="konobar duplo email")

    call("POST", f"{TILL}/api/staff", owner,
         body={"email": "drugivlasnik@demo.rs", "password": "Vlasnik#123",
               "firstName": "Drugi", "lastName": "Vlasnik", "role": 4},
         expect=400, label="pokusaj kreiranja drugog vlasnika")

    call("POST", f"{TILL}/api/staff", owner,
         body={"email": "slaba@demo.rs", "password": "slabo", "firstName": "A",
               "lastName": "B", "role": 2},
         expect=400, label="slaba lozinka")

    if s != 200:
        return

    new_id = created["id"]

    # The new waiter can sign in and take an order, which is the whole point of the feature.
    s, tok = call("POST", f"{TILL}/api/auth/login",
                  body={"restaurantSlug": "demo", "email": "novikonobar@demo.rs",
                        "password": "Konobar#123"},
                  expect=200, label="prijava novog konobara")
    if s == 200:
        call("GET", f"{TILL}/api/floor-plan", tok["accessToken"], expect=200,
             label="nov konobar vidi raspored")

    call("PUT", f"{TILL}/api/staff/{new_id}", owner,
         body={"id": new_id, "firstName": "Jovan", "lastName": "Jovanovic", "role": 3},
         expect=200, label="unapredjenje u menadzera")

    call("POST", f"{TILL}/api/staff/{new_id}/password", owner,
         body={"newPassword": "Nova#Lozinka1"}, expect=204, label="reset lozinke")

    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "demo", "email": "novikonobar@demo.rs",
               "password": "Konobar#123"},
         expect=401, label="stara lozinka vise ne radi")

    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "demo", "email": "novikonobar@demo.rs",
               "password": "Nova#Lozinka1"},
         expect=200, label="nova lozinka radi")

    call("POST", f"{TILL}/api/staff/{new_id}/disable", owner, expect=204, label="gasenje naloga")

    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "demo", "email": "novikonobar@demo.rs",
               "password": "Nova#Lozinka1"},
         expect=403, label="ugasen nalog ne moze da se prijavi")

    s, listed = call("GET", f"{TILL}/api/staff", owner, expect=200, label="lista bez ugasenih")
    if s == 200 and any(m["id"] == new_id for m in listed):
        print("    PAD  ugasen nalog se i dalje vidi u podrazumevanoj listi")
        state.setdefault("leaks", []).append("staff-disabled")

    s, listed = call("GET", f"{TILL}/api/staff?includeDisabled=true", owner, expect=200,
                     label="lista sa ugasenima")
    if s == 200 and not any(m["id"] == new_id and not m["isEnabled"] for m in listed):
        print("    PAD  ugasen nalog se ne vidi ni sa includeDisabled")
        state.setdefault("leaks", []).append("staff-include-disabled")

    call("POST", f"{TILL}/api/staff/{new_id}/enable", owner, expect=204, label="ponovno paljenje")
    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "demo", "email": "novikonobar@demo.rs",
               "password": "Nova#Lozinka1"},
         expect=200, label="prijava posle paljenja")

    # The owner must not be able to lock the venue out of its own management.
    s, staff = call("GET", f"{TILL}/api/staff", owner, expect=200, label="lista za vlasnika")
    owner_id = next((m["id"] for m in staff if m["role"] == 4), None)
    if owner_id:
        call("POST", f"{TILL}/api/staff/{owner_id}/disable", owner, expect=409,
             label="vlasnik gasi sam sebe")
        call("PUT", f"{TILL}/api/staff/{owner_id}", owner,
             body={"id": owner_id, "firstName": "Olivia", "lastName": "Owner", "role": 2},
             expect=409, label="vlasnik sebe spusta u konobara")
