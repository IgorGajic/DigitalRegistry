"""Second half: orders, voids, reservations, shifts, inventory, reports, QR."""
import datetime
import api
from api import call, utc, today, TILL


def at(days, hour):
    return (datetime.datetime.now(datetime.UTC) + datetime.timedelta(days=days)) \
        .strftime(f"%Y-%m-%dT{hour:02d}:00:00Z")


def run(state):
    owner, manager, waiter, guest = state["owner"], state["manager"], state["waiter"], state["guest"]
    table = state["table"]
    espresso = next(m for m in state["menu"] if m["name"] == "Espresso")
    gt = next(m for m in state["menu"] if m["name"] == "Gin and Tonic")

    print("\n--- PORUDZBINE ---")
    s, order = call("POST", f"{TILL}/api/orders", waiter,
                    body={"tableId": table["id"],
                          "items": [{"menuItemId": espresso["id"], "quantity": 2},
                                    {"menuItemId": gt["id"], "quantity": 1}]},
                    expect=201, label="otvaranje racuna")
    oid = order["id"] if s == 201 else None
    line = order["items"][0]["id"] if s == 201 else None

    call("POST", f"{TILL}/api/orders", manager,
         body={"tableId": table["id"], "items": [{"menuItemId": espresso["id"], "quantity": 1}]},
         expect=403, label="menadzer otvara racun")

    call("GET", f"{TILL}/api/orders/{oid}", waiter, expect=200, label="citanje racuna")

    call("PATCH", f"{TILL}/api/orders/{oid}/items", waiter,
         body={"orderId": oid, "change": 1, "menuItemId": gt["id"], "quantity": 1},
         expect=200, label="dodavanje stavke")

    call("PATCH", f"{TILL}/api/orders/{oid}/items", waiter,
         body={"orderId": oid, "change": 2, "orderItemId": line, "quantity": 4},
         expect=200, label="povecanje kolicine")

    call("PATCH", f"{TILL}/api/orders/{oid}/items", waiter,
         body={"orderId": oid, "change": 2, "orderItemId": line, "quantity": 1},
         expect=400, label="smanjenje kolicine (mora storno)")

    call("PATCH", f"{TILL}/api/orders/{oid}/items", waiter,
         body={"orderId": oid, "change": 3, "orderItemId": line, "notes": "bez secera"},
         expect=200, label="izmena napomene")

    print("\n--- STORNO ---")
    call("POST", f"{TILL}/api/orders/{oid}/items/{line}/void", waiter,
         body={"reason": "x"}, expect=400, label="storno bez razloga")

    call("POST", f"{TILL}/api/orders/{oid}/items/{line}/void", waiter,
         body={"reason": "Gost se predomislio", "quantity": 1},
         expect=200, label="delimican storno stavke")

    call("POST", f"{TILL}/api/orders/{oid}/reverse", waiter,
         body={"reason": "Pokusaj konobara da stornira placen racun"},
         expect=403, label="konobar stornira placen")

    call("GET", f"{TILL}/api/orders/{oid}/receipt", waiter, expect=200, label="racun pre naplate")

    call("POST", f"{TILL}/api/orders/{oid}/payment", waiter,
         body={"orderId": oid, "paymentMethod": 1}, expect=200, label="naplata gotovinom")

    call("POST", f"{TILL}/api/orders/{oid}/payment", waiter,
         body={"orderId": oid, "paymentMethod": 1}, expect=409, label="dvostruka naplata")

    call("POST", f"{TILL}/api/orders/{oid}/reverse", manager,
         body={"reason": "kratko"}, expect=400, label="storno placenog, kratak razlog")

    call("POST", f"{TILL}/api/orders/{oid}/reverse", manager,
         body={"reason": "Gost reklamirao pice, izdat povracaj novca"},
         expect=200, label="storno placenog racuna")

    call("POST", f"{TILL}/api/orders/{oid}/reverse", manager,
         body={"reason": "Ponovljeni pokusaj storniranja istog racuna"},
         expect=409, label="dvostruki storno")

    print("\n--- POSLEDNJI RACUNI ---")
    call("GET", f"{TILL}/api/orders", waiter, expect=200, label="lista racuna (dan)")
    call("GET", f"{TILL}/api/orders", manager, expect=200, label="lista racuna (menadzer)")
    call("GET", f"{TILL}/api/orders?status=4", owner, expect=200, label="lista placenih")
    call("GET", f"{TILL}/api/orders?tableId={table['id']}", waiter, expect=200,
         label="lista po stolu")
    call("GET", f"{TILL}/api/orders?from={utc()}&to={utc(-1)}", waiter, expect=400,
         label="lista obrnut period")
    call("GET", f"{TILL}/api/orders?take=0", waiter, expect=400, label="lista take=0")
    call("GET", f"{TILL}/api/orders/{oid}/receipt", manager, expect=200,
         label="menadzer cita otisak")

    s, order2 = call("POST", f"{TILL}/api/orders", waiter,
                     body={"tableId": table["id"], "items": [{"menuItemId": gt["id"], "quantity": 2}]},
                     expect=201, label="drugi racun")
    if s == 201:
        call("POST", f"{TILL}/api/orders/{order2['id']}/void", waiter,
             body={"reason": "Gosti otisli bez placanja"}, expect=200,
             label="storno otvorenog racuna")

    print("\n--- REZERVACIJE ---")
    s, res = call("POST", f"{TILL}/api/reservations", guest,
                  body={"tableId": table["id"], "startTime": at(1, 18), "endTime": at(1, 20),
                        "partySize": 2},
                  expect=201, label="rezervacija stola")
    rid = res["id"] if s == 201 else None

    if rid:
        call("GET", f"{TILL}/api/reservations/{rid}", guest, expect=200, label="citanje rezervacije")
        call("GET", f"{TILL}/api/reservations/mine", guest, expect=200, label="moje rezervacije")
        call("GET", f"{TILL}/api/reservations/schedule?date={today(1)}", waiter,
             expect=200, label="dnevni raspored rezervacija")
        call("POST", f"{TILL}/api/reservations/{rid}/check-in", waiter, expect=[200, 204],
             label="prijava dolaska")
        call("POST", f"{TILL}/api/reservations/{rid}/cancel", guest, expect=[200, 204, 409],
             label="otkazivanje rezervacije")

    s, res2 = call("POST", f"{TILL}/api/reservations", waiter,
                   body={"tableId": table["id"], "startTime": at(4, 18), "endTime": at(4, 20),
                         "partySize": 2, "contactName": f"Marko {api.RUN}",
                         "contactPhone": "060111222"},
                   expect=201, label="konobar prima rezervaciju za gosta")

    if s == 201:
        s, sheet = call("GET", f"{TILL}/api/reservations/schedule?date={today(4)}", waiter,
                        expect=200, label="raspored posle unosa")
        row = next((r for r in (sheet or []) if r["id"] == res2["id"]), None)
        if row and row["guestName"] != f"Marko {api.RUN}":
            print(f"    PAD  rezervacija se vodi na '{row['guestName']}', a ne na gosta")
            state.setdefault("leaks", []).append("reservation-name")
        call("POST", f"{TILL}/api/reservations/{res2['id']}/cancel", manager, expect=[200, 204],
             label="otkazivanje unete rezervacije")

    call("POST", f"{TILL}/api/reservations", waiter,
         body={"tableId": table["id"], "startTime": at(5, 18), "endTime": at(5, 20),
               "partySize": 2},
         expect=400, label="konobar bez imena gosta")

    call("POST", f"{TILL}/api/reservations", guest,
         body={"tableId": table["id"], "startTime": at(6, 18), "endTime": at(6, 20),
               "partySize": 2, "contactName": "Neko Drugi"},
         expect=403, label="gost rezervise na tudje ime")

    call("POST", f"{TILL}/api/reservations", guest,
         body={"tableId": table["id"], "startTime": at(2, 18), "endTime": at(2, 20),
               "partySize": 999},
         expect=400, label="rezervacija prevelika grupa")

    call("POST", f"{TILL}/api/reservations", guest,
         body={"tableId": table["id"], "startTime": at(3, 20), "endTime": at(3, 18),
               "partySize": 2},
         expect=400, label="rezervacija obrnut period")

    print("\n--- SMENE ---")
    s, tpl = call("GET", f"{TILL}/api/shifts/templates", manager, expect=200, label="sabloni smena")
    t2 = next((t for t in (tpl or []) if t["name"] == "II smena"), None)

    call("POST", f"{TILL}/api/shifts/templates", manager,
         body={"name": f"Nocna {api.RUN}", "startTime": "22:00:00", "endTime": "06:00:00"},
         expect=200, label="nov sablon (preko ponoci)")

    call("POST", f"{TILL}/api/shifts/templates", manager,
         body={"name": f"Losa {api.RUN}", "startTime": "10:00:00", "endTime": "10:00:00"},
         expect=400, label="sablon isto vreme")

    waiters = state.get("waiters", [])
    if t2 and waiters:
        s, asg = call("POST", f"{TILL}/api/shifts/assignments", manager,
                      body={"waiterId": waiters[0], "shiftTemplateId": t2["id"], "days": 62,
                            "validFrom": today(), "validTo": today(30)},
                      expect=200, label="dodela smene")
        if s == 200:
            state["assignment"] = asg["id"]

        call("POST", f"{TILL}/api/shifts/assignments", manager,
             body={"waiterId": waiters[0], "shiftTemplateId": t2["id"], "days": 62,
                   "validFrom": today(), "validTo": today(30)},
             expect=409, label="dupla dodela")

    call("GET", f"{TILL}/api/shifts/assignments", manager, expect=200, label="lista dodela")
    call("POST", f"{TILL}/api/shifts/generate", manager,
         body={"fromDate": today(), "toDate": today(13)}, expect=200, label="generisanje rasporeda")
    call("POST", f"{TILL}/api/shifts/generate", manager,
         body={"fromDate": today(), "toDate": today(13)}, expect=200, label="ponovno generisanje")
    call("GET", f"{TILL}/api/shifts/week?date={today()}", manager, expect=200, label="nedeljna mreza")
    call("GET", f"{TILL}/api/shifts?from={utc()}&to={utc(14)}", manager, expect=200, label="lista smena")

    if waiters:
        s, shift = call("POST", f"{TILL}/api/shifts", manager,
                        body={"waiterId": waiters[0], "startTime": at(60, 8), "endTime": at(60, 16)},
                        expect=201, label="ad-hoc smena")
        if s == 201:
            sid = shift["id"]
            call("PUT", f"{TILL}/api/shifts/{sid}", manager,
                 body={"id": sid, "startTime": at(60, 9), "endTime": at(60, 17)},
                 expect=204, label="izmena smene")
            call("DELETE", f"{TILL}/api/shifts/{sid}", manager, expect=204, label="brisanje smene")

    if state.get("assignment"):
        call("DELETE", f"{TILL}/api/shifts/assignments/{state['assignment']}", manager,
             expect=204, label="brisanje dodele")

    print("\n--- MAGACIN ---")
    gin = state["gin"]
    call("GET", f"{TILL}/api/inventory/low-stock", manager, expect=200, label="niske zalihe")
    call("POST", f"{TILL}/api/inventory/entries", manager,
         body={"ingredientId": gin, "quantity": 1000, "purchaseUnitPrice": 4.0,
               "supplier": "Vinarija", "referenceNumber": "OTP-1"},
         expect=200, label="ulaz robe")
    call("GET", f"{TILL}/api/inventory/entries?from={utc(-1)}&to={utc(1)}", manager,
         expect=200, label="lista nabavki")
    call("GET", f"{TILL}/api/inventory/movements?from={utc(-1)}&to={utc(1)}", manager,
         expect=200, label="knjiga prometa")
    call("POST", f"{TILL}/api/inventory/ingredients/{gin}/adjust", manager,
         body={"ingredientId": gin, "countedQuantity": 3500, "reason": "Popis - lom"},
         expect=200, label="inventura")
    call("POST", f"{TILL}/api/inventory/ingredients/{gin}/restock", manager,
         body={"ingredientId": gin, "quantity": 100}, expect=200, label="stari restock")
    call("POST", f"{TILL}/api/inventory/entries", waiter,
         body={"ingredientId": gin, "quantity": 10, "purchaseUnitPrice": 1},
         expect=403, label="konobar unosi robu")

    print("\n--- IZVESTAJI ---")
    call("GET", f"{TILL}/api/reports/turnover?from={today()}&to={today()}", owner,
         expect=200, label="dnevni pazar")
    call("GET", f"{TILL}/api/reports/top-items?from={utc(-1)}&to={utc(1)}&top=5", owner,
         expect=200, label="najprodavaniji")
    call("GET", f"{TILL}/api/reports/inventory?from={utc(-1)}&to={utc(1)}", owner,
         expect=200, label="vrednost zaliha")
    call("GET", f"{TILL}/api/reports/voids?from={utc(-1)}&to={utc(1)}", owner,
         expect=200, label="izvestaj storna")
    call("GET", f"{TILL}/api/reports/turnover?from={today()}&to={today()}", manager,
         expect=403, label="menadzer trazi pazar")
    call("GET", f"{TILL}/api/reports/turnover?from={today(5)}&to={today()}", owner,
         expect=400, label="pazar obrnut period")

    print("\n--- QR SESIJA GOSTA ---")
    s, t = call("GET", f"{TILL}/api/tables/{table['id']}", manager, expect=200,
                label="citanje QR tokena")
    if s == 200 and t.get("qrCodeToken"):
        s, sess = call("POST", f"{TILL}/api/tables/sessions",
                       body={"qrCodeToken": t["qrCodeToken"]}, expect=200, label="QR sesija")
        if s == 200:
            qr = sess["accessToken"]
            call("GET", f"{TILL}/api/menu", qr, expect=200, label="gost cita meni preko QR")
            call("POST", f"{TILL}/api/orders/qr", qr,
                 body={"items": [{"menuItemId": espresso["id"], "quantity": 1}]},
                 expect=201, label="gost narucuje preko QR")
            call("GET", f"{TILL}/api/orders/mine", qr, expect=200, label="gost vidi svoj sto")
            call("GET", f"{TILL}/api/orders", qr, expect=403, label="QR sesija trazi sve racune")
            call("GET", f"{TILL}/api/reports/turnover?from={today()}&to={today()}", qr,
                 expect=403, label="QR sesija trazi izvestaj")

    call("POST", f"{TILL}/api/tables/sessions",
         body={"qrCodeToken": "3f2504e0-4f89-11d3-9a0c-0305e82c3301"},
         expect=404, label="QR sesija nepoznat token")

