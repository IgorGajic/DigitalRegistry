"""Calls every endpoint that writes, then reads the database to see what it actually wrote.

`main.py` asks "did the API answer correctly?"; this asks "is the row right?". Those come apart more
often than they look: a handler can return 200 having written nothing, write the right row under the
wrong restaurant, or move stock without leaving a ledger entry — and none of that shows in the
response body.

Run against the same live instances as the walkthrough, and point `db.DATABASE` at the same database
the API is using.
"""
import datetime
import sys

import api
import db
from api import MASTER, TILL, call

DEMO_PASSWORD = "Demo#Pass123"
ADMIN_EMAIL = "admin@digitalregistry.local"
ADMIN_PASSWORD = "Admin#Pass123"


def utc(offset_minutes=0):
    moment = datetime.datetime.now(datetime.UTC) + datetime.timedelta(minutes=offset_minutes)
    return moment.replace(microsecond=0).isoformat().replace("+00:00", "Z")


def stamp():
    """A suffix unique to this run, so re-running does not collide on names and emails."""
    return datetime.datetime.now().strftime("%H%M%S")


def sign_in(role):
    _, payload = call("POST", f"{TILL}/api/auth/login",
                      body={"restaurantSlug": "demo", "email": f"{role}@digitalregistry.local",
                            "password": DEMO_PASSWORD},
                      expect=200, label=f"prijava {role}")
    return payload["accessToken"]


def demo_restaurant_id():
    return db.scalar("SELECT CONVERT(char(36), Id) FROM Restaurants WHERE Slug = 'demo'")


def ingredient(name):
    return db.query(
        "SELECT CONVERT(char(36), Id), StockQuantity, AveragePurchasePrice "
        f"FROM Ingredients WHERE Name = '{name}'")[0]


def menu_item(name):
    return db.scalar(f"SELECT CONVERT(char(36), Id) FROM MenuItems WHERE Name = '{name}'")


# ------------------------------------------------------------------------------------------ auth

def section_auth(tenant):
    print("\n--- AUTH: nalog gosta ---")
    email = f"gost{stamp()}@example.com"

    call("POST", f"{TILL}/api/auth/register",
         body={"restaurantSlug": "demo", "email": email, "password": "Gost#Pass123",
               "firstName": "Gost", "lastName": "Testni"},
         expect=200, label="registracija gosta")

    row = db.query(
        "SELECT UserName, Role, CONVERT(char(36), RestaurantId) "
        f"FROM AspNetUsers WHERE Email = '{email}'")

    db.check_true("gost upisan u AspNetUsers", bool(row))
    if row:
        db.check("korisnicko ime nosi slug restorana", row[0][0], f"demo|{email}")
        db.check("uloga je Guest (1)", row[0][1], "1")
        db.check("gost pripada demo restoranu", row[0][2].lower(), tenant.lower())


# ---------------------------------------------------------------------------------------- tables

def next_table_number():
    """One above the highest number in use — a deactivated table still holds its number."""
    return int(db.scalar("SELECT ISNULL(MAX(TableNumber), 0) + 1 FROM [Tables]", "1"))


def section_tables(owner, manager, tenant):
    print("\n--- STOLOVI ---")
    number = next_table_number()

    status, table = call("POST", f"{TILL}/api/tables", manager,
                         body={"tableNumber": number, "capacity": 4},
                         expect=201, label="nov sto")
    if status != 201:
        return None

    tid = table["id"]
    row = db.query("SELECT TableNumber, Capacity, IsActive, CONVERT(char(36), RestaurantId), "
                   f"CONVERT(char(36), QrCodeToken) FROM [Tables] WHERE Id = '{tid}'")[0]

    db.check("sto ima trazeni broj", row[0], str(number))
    db.check("kapacitet upisan", row[1], "4")
    db.check("sto je aktivan", row[2], "1")
    db.check("sto pripada demo restoranu", row[3].lower(), tenant.lower())
    db.check_true("QR token dodeljen pri kreiranju", row[4] not in (None, "NULL"))

    before_token = row[4]

    call("PUT", f"{TILL}/api/tables/{tid}", manager,
         body={"id": tid, "tableNumber": number, "capacity": 6, "isActive": True},
         expect=204, label="izmena stola")
    db.check("kapacitet promenjen na 6",
             db.scalar(f"SELECT Capacity FROM [Tables] WHERE Id = '{tid}'"), "6")

    call("POST", f"{TILL}/api/tables/{tid}/qr-code", manager, expect=200, label="rotacija QR koda")
    after_token = db.scalar(f"SELECT CONVERT(char(36), QrCodeToken) FROM [Tables] WHERE Id = '{tid}'")
    db.check_true("QR token je promenjen", after_token != before_token,
                  f"ostao {after_token}")

    call("POST", f"{TILL}/api/tables/sessions", body={"qrCodeToken": after_token},
         expect=200, label="QR sesija gosta")

    return tid


def delete_table(manager, tid):
    """Deleting is only for a table that never traded; anything else is deactivated instead."""
    has_history = (db.count("Orders", f"TableId = '{tid}'") > 0
                   or db.count("Reservations", f"TableId = '{tid}'") > 0)

    if has_history:
        call("DELETE", f"{TILL}/api/tables/{tid}", manager, expect=409,
             label="sto sa prometom se ne brise")
        db.check("sto sa prometom je i dalje u bazi", db.count("Tables", f"Id = '{tid}'"), 1)

        call("PUT", f"{TILL}/api/tables/{tid}", manager,
             body={"id": tid, "tableNumber": int(db.scalar(f"SELECT TableNumber FROM [Tables] WHERE Id = '{tid}'")),
                   "capacity": 6, "isActive": False},
             expect=204, label="deaktivacija umesto brisanja")
        db.check("sto je deaktiviran",
                 db.scalar(f"SELECT IsActive FROM [Tables] WHERE Id = '{tid}'"), "0")
        return

    call("DELETE", f"{TILL}/api/tables/{tid}", manager, expect=204, label="brisanje stola")
    db.check("sto obrisan iz baze", db.count("Tables", f"Id = '{tid}'"), 0)


def delete_unused_table(manager):
    """A table created and removed without ever being used — the path that is allowed to delete."""
    number = next_table_number()

    status, table = call("POST", f"{TILL}/api/tables", manager,
                         body={"tableNumber": number, "capacity": 2},
                         expect=201, label="sto za brisanje")
    if status != 201:
        return

    tid = table["id"]
    call("DELETE", f"{TILL}/api/tables/{tid}", manager, expect=204, label="brisanje neiskoriscenog stola")
    db.check("neiskoriscen sto obrisan iz baze", db.count("Tables", f"Id = '{tid}'"), 0)


# ------------------------------------------------------------------------------------ floor plan

def section_floorplan(owner, manager, tid):
    print("\n--- RASPORED (PROSTORIJE) ---")
    name = f"Test sala {stamp()}"

    status, room = call("POST", f"{TILL}/api/floor-plan/rooms", manager,
                        body={"name": name, "displayOrder": 9, "canvasWidth": 1000,
                              "canvasHeight": 800},
                        expect=200, label="nova prostorija")
    if status != 200:
        return

    rid = room["id"]
    row = db.query("SELECT Name, DisplayOrder, CanvasWidth, CanvasHeight "
                   f"FROM Rooms WHERE Id = '{rid}'")[0]
    db.check("naziv prostorije upisan", row[0], name)
    db.check("redosled prikaza upisan", row[1], "9")
    db.check("sirina platna upisana", row[2], "1000")

    call("PUT", f"{TILL}/api/floor-plan/rooms/{rid}", manager,
         body={"id": rid, "name": name + " B", "displayOrder": 8, "canvasWidth": 1200,
               "canvasHeight": 900},
         expect=200, label="izmena prostorije")
    row = db.query(f"SELECT Name, CanvasWidth FROM Rooms WHERE Id = '{rid}'")[0]
    db.check("naziv prostorije promenjen", row[0], name + " B")
    db.check("platno prosireno", row[1], "1200")

    call("PUT", f"{TILL}/api/floor-plan/rooms/{rid}/layout", manager,
         body={"roomId": rid, "tables": [{"tableId": tid, "positionX": 210, "positionY": 320,
                                          "width": 90, "height": 90, "shape": 2, "rotation": 45}]},
         expect=200, label="snimanje rasporeda")
    row = db.query("SELECT PositionX, PositionY, Width, Shape, Rotation, "
                   f"CONVERT(char(36), RoomId) FROM [Tables] WHERE Id = '{tid}'")[0]
    db.check("X koordinata upisana", row[0], "210")
    db.check("Y koordinata upisana", row[1], "320")
    db.check("sirina stola upisana", row[2], "90")
    db.check("oblik upisan (Rectangle)", row[3], "2")
    db.check("rotacija upisana", row[4], "45")
    db.check("sto je u prostoriji", row[5].lower(), rid.lower())

    # A table left out of the layout is dragged out of the room — that is how the editor removes it.
    call("PUT", f"{TILL}/api/floor-plan/rooms/{rid}/layout", manager,
         body={"roomId": rid, "tables": []}, expect=200, label="prazan raspored izbacuje sto")
    db.check("izostavljen sto vise nije u prostoriji",
             db.scalar(f"SELECT CONVERT(char(36), RoomId) FROM [Tables] WHERE Id = '{tid}'", "NULL"),
             "NULL")

    call("DELETE", f"{TILL}/api/floor-plan/rooms/{rid}", manager, expect=204,
         label="brisanje prostorije")
    db.check("prostorija obrisana", db.count("Rooms", f"Id = '{rid}'"), 0)
    db.check("sto prezivio brisanje prostorije", db.count("Tables", f"Id = '{tid}'"), 1)


# ------------------------------------------------------------------------------------------ menu

def section_menu(manager, tenant):
    print("\n--- JELOVNIK I NORMATIV ---")
    name = f"Testni artikal {stamp()}"

    status, item = call("POST", f"{TILL}/api/menu/items", manager,
                        body={"name": name, "category": "Drink", "unitPrice": 320,
                              "isAvailable": True},
                        expect=200, label="nov artikal")
    if status != 200:
        return

    mid = item["id"]
    row = db.query("SELECT Name, Category, UnitPrice, IsAvailable, CONVERT(char(36), RestaurantId) "
                   f"FROM MenuItems WHERE Id = '{mid}'")[0]
    db.check("naziv artikla upisan", row[0], name)
    db.check("kategorija upisana", row[1], "Drink")
    db.check_num("cena upisana", row[2], 320)
    db.check("artikal je u ponudi", row[3], "1")
    db.check("artikal pripada demo restoranu", row[4].lower(), tenant.lower())

    call("POST", f"{TILL}/api/menu/items", manager,
         body={"id": mid, "name": name, "category": "Drink", "unitPrice": 350,
               "isAvailable": False},
         expect=200, label="izmena artikla")
    row = db.query(f"SELECT UnitPrice, IsAvailable FROM MenuItems WHERE Id = '{mid}'")[0]
    db.check_num("cena promenjena", row[0], 350)
    db.check("artikal skinut iz ponude", row[1], "0")

    tonic = ingredient("Tonic water")[0]
    lime = ingredient("Lime")[0]

    call("PUT", f"{TILL}/api/menu/items/{mid}/recipe", manager,
         body={"menuItemId": mid, "lines": [{"ingredientId": tonic, "quantityRequired": 200},
                                            {"ingredientId": lime, "quantityRequired": 0.5}]},
         expect=200, label="normativ (2 sastojka)")
    db.check("normativ ima 2 stavke", db.count("RecipeItems", f"MenuItemId = '{mid}'"), 2)
    db.check_num("kolicina tonika u normativu",
                 db.scalar(f"SELECT QuantityRequired FROM RecipeItems "
                           f"WHERE MenuItemId = '{mid}' AND IngredientId = '{tonic}'"), 200)

    # Replacing a recipe must not leave the old lines behind — this is the bug the walkthrough found.
    call("PUT", f"{TILL}/api/menu/items/{mid}/recipe", manager,
         body={"menuItemId": mid, "lines": [{"ingredientId": tonic, "quantityRequired": 250}]},
         expect=200, label="zamena normativa (1 sastojak)")
    db.check("stari red normativa obrisan", db.count("RecipeItems", f"MenuItemId = '{mid}'"), 1)
    db.check_num("nova kolicina tonika",
                 db.scalar(f"SELECT QuantityRequired FROM RecipeItems WHERE MenuItemId = '{mid}'"),
                 250)

    call("DELETE", f"{TILL}/api/menu/items/{mid}", manager, expect=204, label="brisanje artikla")
    db.check("artikal obrisan", db.count("MenuItems", f"Id = '{mid}'"), 0)
    db.check("normativ obrisanog artikla ociscen",
             db.count("RecipeItems", f"MenuItemId = '{mid}'"), 0)


# ------------------------------------------------------------------------------------- inventory

def section_inventory(owner, manager):
    print("\n--- MAGACIN ---")
    iid, before_qty, before_avg = ingredient("Gin")
    before_qty, before_avg = float(before_qty), float(before_avg)

    call("POST", f"{TILL}/api/inventory/ingredients/{iid}/restock", manager,
         body={"ingredientId": iid, "quantity": 500}, expect=200, label="dopuna zaliha")
    after = float(db.scalar(f"SELECT StockQuantity FROM Ingredients WHERE Id = '{iid}'"))
    db.check_num("zaliha uvecana za 500", after, before_qty + 500)
    db.check_num("nabavna cena nepromenjena posle dopune",
                 db.scalar(f"SELECT AveragePurchasePrice FROM Ingredients WHERE Id = '{iid}'"),
                 before_avg)

    movement = db.query("SELECT TOP 1 Type, Quantity, BalanceAfter FROM StockMovements "
                        f"WHERE IngredientId = '{iid}' ORDER BY Created DESC")[0]
    db.check("promet tipa Purchase (1)", movement[0], "1")
    db.check_num("promet nosi +500", movement[1], 500)
    db.check_num("stanje posle prometa odgovara", movement[2], after)

    # A delivery at a different price moves the average; that is what makes margins meaningful.
    entries_before = db.count("StockEntries", f"IngredientId = '{iid}'")
    call("POST", f"{TILL}/api/inventory/entries", manager,
         body={"ingredientId": iid, "quantity": 1000, "purchaseUnitPrice": 4.0,
               "supplier": "Test dobavljac", "note": "dbwalk"},
         expect=200, label="ulaz robe sa nabavnom cenom")
    db.check("upisan ulaz robe", db.count("StockEntries", f"IngredientId = '{iid}'"),
             entries_before + 1)

    entry = db.query("SELECT TOP 1 Quantity, PurchaseUnitPrice, TotalCost, Supplier "
                     f"FROM StockEntries WHERE IngredientId = '{iid}' ORDER BY Created DESC")[0]
    db.check_num("kolicina ulaza", entry[0], 1000)
    db.check_num("nabavna cena ulaza", entry[1], 4.0)
    db.check_num("ukupna vrednost ulaza = kolicina x cena", entry[2], 4000)
    db.check("dobavljac upisan", entry[3], "Test dobavljac")

    expected_avg = (after * before_avg + 1000 * 4.0) / (after + 1000)
    db.check_num("klizeci prosek preracunat",
                 db.scalar(f"SELECT AveragePurchasePrice FROM Ingredients WHERE Id = '{iid}'"),
                 expected_avg, tolerance=0.01)

    # Stocktaking states the counted quantity, not the difference.
    current = float(db.scalar(f"SELECT StockQuantity FROM Ingredients WHERE Id = '{iid}'"))
    counted = current - 25
    call("POST", f"{TILL}/api/inventory/ingredients/{iid}/adjust", manager,
         body={"ingredientId": iid, "countedQuantity": counted, "reason": "Popis, dbwalk"},
         expect=200, label="korekcija po popisu")
    db.check_num("zaliha postavljena na prebrojano",
                 db.scalar(f"SELECT StockQuantity FROM Ingredients WHERE Id = '{iid}'"), counted)

    adjustment = db.query("SELECT TOP 1 Type, Quantity FROM StockMovements "
                          f"WHERE IngredientId = '{iid}' ORDER BY Created DESC")[0]
    db.check("promet tipa Adjustment (4)", adjustment[0], "4")
    db.check_num("korekcija nosi razliku, ne stanje", adjustment[1], -25)

    call("GET", f"{TILL}/api/inventory/movements", manager, expect=200, label="knjiga prometa")
    call("GET", f"{TILL}/api/inventory/entries", manager, expect=200, label="pregled nabavki")
    call("GET", f"{TILL}/api/inventory/low-stock", manager, expect=200, label="ispod minimuma")


# ---------------------------------------------------------------------------------------- orders

def section_orders(owner, waiter, manager, tenant):
    print("\n--- RACUN: OTVARANJE, IZMENE, STORNO, NAPLATA ---")
    espresso = menu_item("Espresso")
    gin = menu_item("Gin and Tonic")
    beans_id, beans_before, _ = ingredient("Espresso beans")
    beans_before = float(beans_before)

    table = db.query("SELECT TOP 1 CONVERT(char(36), t.Id), t.TableNumber FROM [Tables] t "
                     "WHERE t.IsActive = 1 AND NOT EXISTS (SELECT 1 FROM Orders o "
                     "WHERE o.TableId = t.Id AND o.Status IN (1,2,3)) ORDER BY t.TableNumber")[0]
    tid = table[0]

    status, order = call("POST", f"{TILL}/api/orders", waiter,
                         body={"tableId": tid, "items": [{"menuItemId": espresso, "quantity": 2}]},
                         expect=201, label="otvaranje racuna (2 espresa)")
    if status != 201:
        return

    oid = order["id"]
    row = db.query("SELECT Status, CONVERT(char(36), TableId), CONVERT(char(36), RestaurantId), "
                   f"CONVERT(char(36), WaiterId) FROM Orders WHERE Id = '{oid}'")[0]
    db.check("racun je otvoren (Status 1)", row[0], "1")
    db.check("racun vezan za sto", row[1].lower(), tid.lower())
    db.check("racun pripada demo restoranu", row[2].lower(), tenant.lower())
    db.check_true("konobar upisan", row[3] not in (None, "NULL"))

    db.check("jedna stavka na racunu", db.count("OrderItems", f"OrderId = '{oid}'"), 1)
    db.check_num("kolicina 2", db.scalar(f"SELECT Quantity FROM OrderItems WHERE OrderId = '{oid}'"), 2)
    db.check_num("cena zabelezena sa stavkom",
                 db.scalar(f"SELECT UnitPrice FROM OrderItems WHERE OrderId = '{oid}'"), 180)

    # 2 espressos at 18 g of beans each.
    db.check_num("zalihe umanjene po normativu (2 x 18 g)",
                 db.scalar(f"SELECT StockQuantity FROM Ingredients WHERE Id = '{beans_id}'"),
                 beans_before - 36)
    sale = db.query("SELECT TOP 1 Type, Quantity, CONVERT(char(36), OrderId) FROM StockMovements "
                    f"WHERE IngredientId = '{beans_id}' ORDER BY Created DESC")[0]
    db.check("promet tipa Sale (2)", sale[0], "2")
    db.check_num("izlaz je negativan", sale[1], -36)
    db.check("izlaz vezan za racun", sale[2].lower(), oid.lower())

    # A second round on the same tab — the table stays open across rounds.
    call("PATCH", f"{TILL}/api/orders/{oid}/items", waiter,
         body={"orderId": oid, "change": 1, "menuItemId": gin, "quantity": 1},
         expect=200, label="dodavanje pica na otvoren racun")
    db.check("racun ima 2 stavke", db.count("OrderItems", f"OrderId = '{oid}'"), 2)

    item_id = db.scalar("SELECT CONVERT(char(36), Id) FROM OrderItems "
                        f"WHERE OrderId = '{oid}' AND MenuItemId = '{espresso}'")

    call("PATCH", f"{TILL}/api/orders/{oid}/items", waiter,
         body={"orderId": oid, "change": 2, "orderItemId": item_id, "quantity": 3},
         expect=200, label="povecanje kolicine na 3")
    db.check_num("kolicina espresa je 3",
                 db.scalar(f"SELECT Quantity FROM OrderItems WHERE Id = '{item_id}'"), 3)
    db.check_num("jos 18 g zrna razduzeno",
                 db.scalar(f"SELECT StockQuantity FROM Ingredients WHERE Id = '{beans_id}'"),
                 beans_before - 54)

    call("PATCH", f"{TILL}/api/orders/{oid}/items", waiter,
         body={"orderId": oid, "change": 3, "orderItemId": item_id, "notes": "bez secera"},
         expect=200, label="napomena uz stavku")
    db.check("napomena upisana",
             db.scalar(f"SELECT Notes FROM OrderItems WHERE Id = '{item_id}'"), "bez secera")

    # Partial void: one of three, at the price the line recorded.
    voids_before = db.count("VoidRecords", f"OrderId = '{oid}'")
    call("POST", f"{TILL}/api/orders/{oid}/items/{item_id}/void", waiter,
         body={"orderId": oid, "orderItemId": item_id, "quantity": 1,
               "reason": "Gost se predomislio"},
         expect=200, label="delimican storno stavke")
    db.check("upisan zapis storna", db.count("VoidRecords", f"OrderId = '{oid}'"), voids_before + 1)
    void_row = db.query("SELECT TOP 1 Type, Quantity, Amount, Reason, ItemName FROM VoidRecords "
                        f"WHERE OrderId = '{oid}' ORDER BY Created DESC")[0]
    db.check("storno tipa Item (1)", void_row[0], "1")
    db.check_num("stornirana 1 jedinica", void_row[1], 1)
    db.check_num("vrednost storna po zabelezenoj ceni", void_row[2], 180)
    db.check("razlog sacuvan", void_row[3], "Gost se predomislio")
    db.check("naziv artikla zabelezen na stornu", void_row[4], "Espresso")
    db.check_num("kolicina na stavci smanjena na 2",
                 db.scalar(f"SELECT Quantity FROM OrderItems WHERE Id = '{item_id}'"), 2)
    db.check_num("zrna vracena u magacin",
                 db.scalar(f"SELECT StockQuantity FROM Ingredients WHERE Id = '{beans_id}'"),
                 beans_before - 36)
    ret = db.query("SELECT TOP 1 Type, Quantity FROM StockMovements "
                   f"WHERE IngredientId = '{beans_id}' ORDER BY Created DESC")[0]
    db.check("promet tipa Return (3)", ret[0], "3")
    db.check_num("povracaj je pozitivan", ret[1], 18)

    expected_total = 2 * 180 + 650
    status, tx = call("POST", f"{TILL}/api/orders/{oid}/payment", waiter,
                      body={"orderId": oid, "paymentMethod": 1}, expect=200, label="naplata gotovinom")
    db.check("racun je placen (Status 4)",
             db.scalar(f"SELECT Status FROM Orders WHERE Id = '{oid}'"), "4")
    tx_row = db.query("SELECT Amount, PaymentMethod, CONVERT(char(36), ReversesTransactionId) "
                      f"FROM Transactions WHERE OrderId = '{oid}'")[0]
    db.check_num("iznos transakcije = zbir stavki", tx_row[0], expected_total)
    db.check("nacin placanja Cash (1)", tx_row[1], "1")
    db.check("uplata nije protivstavka", tx_row[2], "NULL")

    call("GET", f"{TILL}/api/orders/{oid}/receipt", waiter, expect=200, label="racun za stampu")

    # Reversing a settled bill: a manager's signature, a counter-entry, and stock back.
    call("POST", f"{TILL}/api/orders/{oid}/reverse", manager,
         body={"orderId": oid, "reason": "Pogresno naplaceno, greska konobara"},
         expect=200, label="storno placenog racuna")
    db.check("racun je storniran (Status 6)",
             db.scalar(f"SELECT Status FROM Orders WHERE Id = '{oid}'"), "6")
    db.check("dve transakcije na racunu", db.count("Transactions", f"OrderId = '{oid}'"), 2)
    reversal = db.query("SELECT Amount, CONVERT(char(36), ReversesTransactionId) FROM Transactions "
                        f"WHERE OrderId = '{oid}' AND ReversesTransactionId IS NOT NULL")[0]
    db.check_num("protivstavka je negativna", reversal[0], -expected_total)
    db.check_true("protivstavka pokazuje na uplatu", reversal[1] not in (None, "NULL"))
    db.check("zapis storna tipa PaidOrder (3)",
             db.scalar("SELECT TOP 1 Type FROM VoidRecords "
                       f"WHERE OrderId = '{oid}' ORDER BY Created DESC"), "3")
    db.check_true("odobrio menadzer",
                  db.scalar("SELECT TOP 1 CONVERT(char(36), ApprovedByUserId) FROM VoidRecords "
                            f"WHERE OrderId = '{oid}' ORDER BY Created DESC", "NULL") != "NULL")

    return tid


def section_void_open_order(waiter, tid):
    print("\n--- STORNO OTVORENOG RACUNA ---")
    espresso = menu_item("Espresso")
    beans_id, beans_before, _ = ingredient("Espresso beans")
    beans_before = float(beans_before)

    status, order = call("POST", f"{TILL}/api/orders", waiter,
                         body={"tableId": tid, "items": [{"menuItemId": espresso, "quantity": 2}]},
                         expect=201, label="otvaranje racuna za storno")
    if status != 201:
        return

    oid = order["id"]
    call("POST", f"{TILL}/api/orders/{oid}/void", waiter,
         body={"orderId": oid, "reason": "Gosti otisli bez porudzbine"},
         expect=200, label="storno celog otvorenog racuna")

    # Cancelled (5), not Voided (6): nothing was ever taken, so there is no money to take back.
    # A settled bill reversed later is the one that becomes Voided.
    db.check("otvoren racun je otkazan (Status 5)",
             db.scalar(f"SELECT Status FROM Orders WHERE Id = '{oid}'"), "5")
    db.check("zapis storna tipa OpenOrder (2)",
             db.scalar(f"SELECT TOP 1 Type FROM VoidRecords WHERE OrderId = '{oid}'"), "2")
    db.check_num("sve zalihe vracene",
                 db.scalar(f"SELECT StockQuantity FROM Ingredients WHERE Id = '{beans_id}'"),
                 beans_before)
    db.check("nema transakcije za storniran otvoren racun",
             db.count("Transactions", f"OrderId = '{oid}'"), 0)


def section_qr_order(manager, tenant):
    print("\n--- QR PORUDZBINA GOSTA ---")
    row = db.query("SELECT TOP 1 CONVERT(char(36), t.Id), CONVERT(char(36), t.QrCodeToken) "
                   "FROM [Tables] t WHERE t.IsActive = 1 AND NOT EXISTS (SELECT 1 FROM Orders o "
                   "WHERE o.TableId = t.Id AND o.Status IN (1,2,3)) ORDER BY t.TableNumber")[0]
    tid, token = row

    status, session = call("POST", f"{TILL}/api/tables/sessions", body={"qrCodeToken": token},
                           expect=200, label="otvaranje QR sesije")
    if status != 200:
        return

    espresso = menu_item("Espresso")
    status, order = call("POST", f"{TILL}/api/orders/qr", session["accessToken"],
                         body={"items": [{"menuItemId": espresso, "quantity": 1}]},
                         expect=201, label="porudzbina preko QR-a")
    if status != 201:
        return

    oid = order["id"]
    row = db.query("SELECT CONVERT(char(36), TableId), CONVERT(char(36), RestaurantId), "
                   f"CONVERT(char(36), WaiterId) FROM Orders WHERE Id = '{oid}'")[0]
    db.check("QR racun je na stolu iz tokena", row[0].lower(), tid.lower())
    db.check("QR racun nosi restoran (nije zaobisao filter)", row[1].lower(), tenant.lower())
    db.check("QR racun nema konobara", row[2], "NULL")


# ---------------------------------------------------------------------------------- reservations

def section_reservations(owner, waiter, manager, tenant):
    print("\n--- REZERVACIJE ---")
    tid = db.scalar("SELECT TOP 1 CONVERT(char(36), Id) FROM [Tables] "
                    "WHERE IsActive = 1 AND Capacity >= 4 ORDER BY TableNumber")

    # A guest books for themselves: no name given, so it lands on their own account. Staff are
    # deliberately not allowed to do this — a booking with no name would go down under whoever took
    # it, which is the fault this endpoint was changed to make impossible.
    guest = sign_in("guest")

    status, first = call("POST", f"{TILL}/api/reservations", guest,
                         body={"tableId": tid, "startTime": utc(60), "endTime": utc(180),
                               "partySize": 4},
                         expect=201, label="nova rezervacija")
    if status != 201:
        return

    rid = first["id"]
    row = db.query("SELECT Status, PartySize, CONVERT(char(36), GuestId), "
                   f"CONVERT(char(36), RestaurantId), ContactName, "
                   f"CONVERT(char(36), TakenByUserId) FROM Reservations WHERE Id = '{rid}'")[0]
    db.check("rezervacija na cekanju (Status 1)", row[0], "1")
    db.check("broj gostiju upisan", row[1], "4")
    db.check_true("gost je onaj ko je rezervisao", row[2] not in (None, "NULL"))
    db.check("rezervacija pripada demo restoranu", row[3].lower(), tenant.lower())
    db.check("sopstvena rezervacija nema upisano ime", row[4], "NULL")
    db.check("sopstvenu rezervaciju niko nije primio", row[5], "NULL")

    # And the other kind: taken at the desk, for somebody with no account at all. The point of the
    # row check is that it is *not* filed under the waiter who took it.
    ime = f"Marko {stamp()}"
    status, desk = call("POST", f"{TILL}/api/reservations", waiter,
                        body={"tableId": tid, "startTime": utc(1500), "endTime": utc(1620),
                              "partySize": 2, "contactName": ime, "contactPhone": "060111222"},
                        expect=201, label="rezervacija primljena telefonom")

    if status == 201:
        did = desk["id"]
        row = db.query("SELECT CONVERT(char(36), GuestId), ContactName, ContactPhone, "
                       f"CONVERT(char(36), TakenByUserId) FROM Reservations WHERE Id = '{did}'")[0]
        waiter_id = db.scalar("SELECT CONVERT(char(36), Id) FROM AspNetUsers "
                              "WHERE Email = 'waiter@digitalregistry.local'")
        db.check("rezervacija sa telefona nema nalog gosta", row[0], "NULL")
        db.check("ime gosta upisano", row[1], ime)
        db.check("kontakt telefon upisan", row[2], "060111222")
        db.check("zabelezeno ko je primio", row[3].lower(), waiter_id.lower())

        call("POST", f"{TILL}/api/reservations/{did}/cancel", manager, expect=204,
             label="otkazivanje telefonske rezervacije")

    call("POST", f"{TILL}/api/reservations", waiter,
         body={"tableId": tid, "startTime": utc(1700), "endTime": utc(1800), "partySize": 2},
         expect=400, label="osoblje bez imena gosta")

    call("GET", f"{TILL}/api/reservations/schedule", waiter, expect=200, label="dnevni pregled")
    call("GET", f"{TILL}/api/reservations/mine", guest, expect=200, label="moje rezervacije")
    call("GET", f"{TILL}/api/reservations/{rid}", guest, expect=200, label="rezervacija po id")

    call("POST", f"{TILL}/api/reservations/{rid}/check-in", waiter, expect=204,
         label="prijava dolaska")
    db.check("rezervacija zavrsena dolaskom (Status 4)",
             db.scalar(f"SELECT Status FROM Reservations WHERE Id = '{rid}'"), "4")

    status, second = call("POST", f"{TILL}/api/reservations", guest,
                          body={"tableId": tid, "startTime": utc(300), "endTime": utc(420),
                                "partySize": 2},
                          expect=201, label="druga rezervacija")
    if status == 201:
        call("POST", f"{TILL}/api/reservations/{second['id']}/cancel", manager, expect=204,
             label="otkazivanje (menadzer)")
        db.check("rezervacija otkazana (Status 3)",
                 db.scalar(f"SELECT Status FROM Reservations WHERE Id = '{second['id']}'"), "3")


# ---------------------------------------------------------------------------------------- shifts

def section_shifts(owner, manager, tenant):
    print("\n--- SMENE ---")
    waiter_id = db.scalar("SELECT TOP 1 CONVERT(char(36), Id) FROM AspNetUsers "
                          "WHERE Email = 'waiter@digitalregistry.local'")
    name = f"Test smena {stamp()}"

    # An hour of the night nothing else uses, at a minute unique to this run: the generator skips a
    # slot a shift already covers, so a fixed time would report nothing generated on the second run.
    minute = datetime.datetime.now().second
    start_time = f"03:{minute:02d}:00"
    end_time = f"07:{minute:02d}:00"

    status, template = call("POST", f"{TILL}/api/shifts/templates", manager,
                            body={"name": name, "startTime": start_time, "endTime": end_time,
                                  "isActive": True},
                            expect=200, label="sablon smene")
    if status != 200:
        return

    template_id = template["id"]
    row = db.query("SELECT Name, CONVERT(varchar(8), StartTime), CONVERT(varchar(8), EndTime), "
                   f"IsActive, CONVERT(char(36), RestaurantId) FROM ShiftTemplates "
                   f"WHERE Id = '{template_id}'")[0]
    db.check("naziv sablona upisan", row[0], name)
    db.check("pocetak smene upisan", row[1], start_time)
    db.check("kraj smene upisan", row[2], end_time)
    db.check("sablon je aktivan", row[3], "1")
    db.check("sablon pripada demo restoranu", row[4].lower(), tenant.lower())

    monday = datetime.date.today() + datetime.timedelta(days=(7 - datetime.date.today().weekday()))
    friday = monday + datetime.timedelta(days=4)

    status, assignment = call("POST", f"{TILL}/api/shifts/assignments", manager,
                              body={"waiterId": waiter_id, "shiftTemplateId": template_id,
                                    "days": 62, "validFrom": monday.isoformat(),
                                    "validTo": friday.isoformat()},
                              expect=200, label="dodela smene Pon-Pet")
    if status != 200:
        return

    assignment_id = assignment["id"]
    row = db.query("SELECT Days, CONVERT(varchar(10), ValidFrom, 120), "
                   f"CONVERT(char(36), WaiterId) FROM ShiftAssignments WHERE Id = '{assignment_id}'")[0]
    db.check("dani su Pon-Pet (bitovi 62)", row[0], "62")
    db.check("vazi od upisano", row[1], monday.isoformat())
    db.check("dodela vezana za konobara", row[2].lower(), waiter_id.lower())

    # Counted per assignment: the generator runs every standing assignment the venue has, so a
    # database that has been used already will report more than this one contributed.
    before = db.count("Shifts", f"ShiftAssignmentId = '{assignment_id}'")

    status, first_run = call("POST", f"{TILL}/api/shifts/generate", manager,
                             body={"fromDate": monday.isoformat(), "toDate": friday.isoformat()},
                             expect=200, label="generisanje rasporeda")
    if status == 200:
        db.check("nema prijavljenih preklapanja", len(first_run["conflicts"]), 0)

    generated = db.count("Shifts", f"ShiftAssignmentId = '{assignment_id}'")
    db.check("generisano 5 smena iz ove dodele (Pon-Pet)", generated - before, 5)
    # The template says 03:30 as a wall clock in the venue's own zone; the row must be that instant
    # in UTC, which for Europe/Belgrade in summer is 01:30. Storing 03:30 here would put every
    # generated shift two hours out.
    db.check("smene su upisane u UTC (03h u Beogradu = 01h UTC)",
             db.count("Shifts", f"ShiftAssignmentId = '{assignment_id}' "
                                "AND DATEPART(hour, StartTime) = 1 "
                                f"AND DATEPART(minute, StartTime) = {minute}"),
             generated)

    # Running it twice must fill gaps, not duplicate what is already there.
    status, second_run = call("POST", f"{TILL}/api/shifts/generate", manager,
                              body={"fromDate": monday.isoformat(), "toDate": friday.isoformat()},
                              expect=200, label="ponovno generisanje")
    if status == 200:
        db.check("drugo pokretanje nista ne pravi", second_run["created"], 0)
    db.check("broj smena nepromenjen posle ponavljanja",
             db.count("Shifts", f"ShiftAssignmentId = '{assignment_id}'"), generated)

    call("GET", f"{TILL}/api/shifts/week?date={monday.isoformat()}", manager, expect=200,
         label="nedeljni raspored")
    call("GET", f"{TILL}/api/shifts", manager, expect=200, label="lista smena")

    # An ad-hoc shift is the same table, but with no assignment behind it.
    start = datetime.datetime.combine(monday + datetime.timedelta(days=6),
                                      datetime.time(9, 0), datetime.UTC)
    status, adhoc = call("POST", f"{TILL}/api/shifts", manager,
                         body={"waiterId": waiter_id,
                               "startTime": start.isoformat().replace("+00:00", "Z"),
                               "endTime": (start + datetime.timedelta(hours=6)).isoformat().replace("+00:00", "Z")},
                         expect=201, label="ad-hoc smena")
    if status == 201:
        sid = adhoc["id"]
        db.check("ad-hoc smena nema dodelu",
                 db.scalar(f"SELECT CONVERT(char(36), ShiftAssignmentId) FROM Shifts WHERE Id = '{sid}'",
                           "NULL"), "NULL")

        call("PUT", f"{TILL}/api/shifts/{sid}", manager,
             body={"id": sid, "waiterId": waiter_id,
                   "startTime": (start + datetime.timedelta(hours=1)).isoformat().replace("+00:00", "Z"),
                   "endTime": (start + datetime.timedelta(hours=7)).isoformat().replace("+00:00", "Z")},
             expect=204, label="izmena smene")
        db.check("pocetak smene pomeren za sat",
                 db.scalar(f"SELECT CONVERT(varchar(5), StartTime, 108) FROM Shifts WHERE Id = '{sid}'"),
                 "10:00")

        call("DELETE", f"{TILL}/api/shifts/{sid}", manager, expect=204, label="brisanje smene")
        db.check("smena obrisana", db.count("Shifts", f"Id = '{sid}'"), 0)

    # Generated shifts are cleared through the API, both to exercise the delete and to leave the
    # night window free: the generator skips a slot an existing shift already overlaps, so leftovers
    # from an earlier run would make the next one report conflicts instead of work.
    for row in db.query("SELECT CONVERT(char(36), Id) FROM Shifts "
                        f"WHERE ShiftAssignmentId = '{assignment_id}'"):
        call("DELETE", f"{TILL}/api/shifts/{row[0]}", manager, expect=204,
             label="brisanje generisane smene")

    db.check("generisane smene uklonjene",
             db.count("Shifts", f"ShiftAssignmentId = '{assignment_id}'"), 0)

    call("DELETE", f"{TILL}/api/shifts/assignments/{assignment_id}", manager, expect=204,
         label="brisanje dodele")
    db.check("dodela obrisana", db.count("ShiftAssignments", f"Id = '{assignment_id}'"), 0)


# ----------------------------------------------------------------------------------------- staff

def section_staff(owner, tenant):
    print("\n--- ZAPOSLENI ---")
    email = f"konobar{stamp()}@demo.rs"

    status, member = call("POST", f"{TILL}/api/staff", owner,
                          body={"email": email, "password": "Konobar#123", "firstName": "Mika",
                                "lastName": "Mikic", "role": 2},
                          expect=200, label="nov konobar")
    if status != 200:
        return

    uid = member["id"]
    row = db.query("SELECT UserName, Role, FirstName, CONVERT(char(36), RestaurantId), "
                   f"LockoutEnd FROM AspNetUsers WHERE Id = '{uid}'")[0]
    db.check("korisnicko ime nosi slug", row[0], f"demo|{email}")
    db.check("uloga Waiter (2)", row[1], "2")
    db.check("ime upisano", row[2], "Mika")
    db.check("nalog pripada demo restoranu", row[3].lower(), tenant.lower())

    call("PUT", f"{TILL}/api/staff/{uid}", owner,
         body={"id": uid, "firstName": "Mikica", "lastName": "Mikic", "role": 3},
         expect=200, label="izmena zaposlenog")
    row = db.query(f"SELECT FirstName, Role FROM AspNetUsers WHERE Id = '{uid}'")[0]
    db.check("ime promenjeno", row[0], "Mikica")
    db.check("uloga promenjena u Manager (3)", row[1], "3")

    before_hash = db.scalar(f"SELECT PasswordHash FROM AspNetUsers WHERE Id = '{uid}'")
    call("POST", f"{TILL}/api/staff/{uid}/password", owner, body={"id": uid, "newPassword": "Nova#Lozinka123"},
         expect=204, label="nova lozinka")
    db.check_true("hes lozinke promenjen",
                  db.scalar(f"SELECT PasswordHash FROM AspNetUsers WHERE Id = '{uid}'") != before_hash)

    call("POST", f"{TILL}/api/staff/{uid}/disable", owner, expect=204, label="gasenje naloga")
    lockout = db.scalar(f"SELECT CONVERT(varchar(30), LockoutEnd, 126) FROM AspNetUsers WHERE Id = '{uid}'",
                        "NULL")
    db.check_true("nalog zakljucan u buducnost", lockout != "NULL", f"LockoutEnd={lockout}")

    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "demo", "email": email, "password": "Nova#Lozinka123"},
         expect=403, label="ugasen nalog ne moze na kasu")

    call("POST", f"{TILL}/api/staff/{uid}/enable", owner, expect=204, label="vracanje naloga")
    db.check("zakljucavanje uklonjeno",
             db.scalar(f"SELECT CONVERT(varchar(30), LockoutEnd, 126) FROM AspNetUsers WHERE Id = '{uid}'",
                       "NULL"), "NULL")

    call("POST", f"{TILL}/api/auth/login",
         body={"restaurantSlug": "demo", "email": email, "password": "Nova#Lozinka123"},
         expect=200, label="vracen nalog radi sa novom lozinkom")


# --------------------------------------------------------------------------------------- reports

def paid_order_for_reports(waiter):
    """A bill that is paid and stays paid, so the reports have something to count.

    Everything the earlier sections settle gets reversed again, which is correct netting but leaves
    turnover at zero — and a report that agrees with an empty database proves very little.
    """
    cappuccino = menu_item("Cappuccino")

    # A free table if there is one, otherwise any active table: a table may carry several tabs, and
    # this only needs one bill that stays paid.
    tid = (db.scalar("SELECT TOP 1 CONVERT(char(36), t.Id) FROM [Tables] t WHERE t.IsActive = 1 "
                     "AND NOT EXISTS (SELECT 1 FROM Orders o WHERE o.TableId = t.Id "
                     "AND o.Status IN (1,2,3)) ORDER BY t.TableNumber")
           or db.scalar("SELECT TOP 1 CONVERT(char(36), Id) FROM [Tables] WHERE IsActive = 1 "
                        "ORDER BY TableNumber"))

    status, order = call("POST", f"{TILL}/api/orders", waiter,
                         body={"tableId": tid, "items": [{"menuItemId": cappuccino, "quantity": 2}]},
                         expect=201, label="racun za izvestaje")
    if status != 201:
        return

    call("POST", f"{TILL}/api/orders/{order['id']}/payment", waiter,
         body={"orderId": order["id"], "paymentMethod": 2}, expect=200,
         label="naplata karticom (racun ostaje placen)")


def section_reports(owner, waiter, tenant):
    print("\n--- IZVESTAJI (protiv agregata iz baze) ---")
    paid_order_for_reports(waiter)
    today = datetime.date.today()
    from_utc = f"{today.isoformat()}T00:00:00Z"
    to_utc = f"{(today + datetime.timedelta(days=1)).isoformat()}T00:00:00Z"

    status, turnover = call("GET",
                            f"{TILL}/api/reports/turnover?from={today.isoformat()}&to={today.isoformat()}",
                            owner, expect=200, label="dnevni pazar")
    if status == 200:
        expected = db.scalar(
            "SELECT ISNULL(SUM(Amount), 0) FROM Transactions "
            f"WHERE RestaurantId = '{tenant}' AND TransactionDate >= '{from_utc}' "
            f"AND TransactionDate < '{to_utc}'", "0")
        db.check_num("promet odgovara zbiru transakcija (uz protivstavke)",
                     turnover["turnover"], expected, tolerance=0.02)

    status, tops = call("GET", f"{TILL}/api/reports/top-items?from={from_utc}&to={to_utc}",
                        owner, expect=200, label="najprodavaniji artikli")
    if status == 200 and tops:
        top = tops[0]
        expected_qty = db.scalar(
            "SELECT ISNULL(SUM(oi.Quantity), 0) FROM OrderItems oi JOIN Orders o ON o.Id = oi.OrderId "
            f"WHERE o.RestaurantId = '{tenant}' AND o.Status = 4 AND oi.MenuItemId = "
            f"(SELECT Id FROM MenuItems WHERE Name = '{top['name']}') "
            f"AND o.CreatedAt >= '{from_utc}' AND o.CreatedAt < '{to_utc}'", "0")
        db.check_num(f"kolicina za {top['name']} odgovara placenim stavkama",
                     top["quantitySold"], expected_qty)

        expected_revenue = db.scalar(
            "SELECT ISNULL(SUM(oi.Quantity * oi.UnitPrice), 0) FROM OrderItems oi "
            "JOIN Orders o ON o.Id = oi.OrderId "
            f"WHERE o.RestaurantId = '{tenant}' AND o.Status = 4 AND oi.MenuItemId = "
            f"(SELECT Id FROM MenuItems WHERE Name = '{top['name']}') "
            f"AND o.CreatedAt >= '{from_utc}' AND o.CreatedAt < '{to_utc}'", "0")
        db.check_num("promet artikla odgovara zbiru stavki", top["revenue"], expected_revenue,
                     tolerance=0.02)
    elif status == 200:
        db.check_true("najprodavaniji artikli nisu prazni", False, "lista je prazna")

    status, valuation = call("GET", f"{TILL}/api/reports/inventory?from={from_utc}&to={to_utc}",
                             owner, expect=200, label="vrednost zaliha")
    if status == 200:
        expected_value = db.scalar(
            "SELECT ISNULL(SUM(StockQuantity * AveragePurchasePrice), 0) FROM Ingredients "
            f"WHERE RestaurantId = '{tenant}'", "0")
        db.check_num("vrednost zaliha = zaliha x prosecna nabavna",
                     valuation["totalStockValue"], expected_value, tolerance=0.5)

    status, voids = call("GET", f"{TILL}/api/reports/voids?from={from_utc}&to={to_utc}",
                         owner, expect=200, label="izvestaj storna")
    if status == 200:
        expected_count = db.scalar(
            f"SELECT COUNT(*) FROM VoidRecords WHERE RestaurantId = '{tenant}' "
            f"AND VoidedAtUtc >= '{from_utc}' AND VoidedAtUtc < '{to_utc}'", "0")
        db.check_num("broj storna odgovara tabeli VoidRecords",
                     voids["totalVoids"], expected_count)


# ---------------------------------------------------------------------------------------- master

def section_master():
    print("\n--- MASTER API: RESTORAN I LICENCA ---")
    status, admin = call("POST", f"{MASTER}/api/platform/auth/login",
                         body={"email": ADMIN_EMAIL, "password": ADMIN_PASSWORD},
                         expect=200, label="prijava platformskog admina")
    if status != 200:
        return

    token = admin["accessToken"]
    slug = f"test-{stamp()}"

    status, venue = call("POST", f"{MASTER}/api/platform/restaurants", token,
                         body={"name": "Test Kafana", "slug": slug, "address": "Prva 1",
                               "contactEmail": "info@test.rs", "phoneNumber": "+381 11 111",
                               "currencyCode": "RSD", "timeZoneId": "Europe/Belgrade"},
                         expect=201, label="nov restoran")
    if status != 201:
        return

    rid = venue["id"]
    row = db.query("SELECT Name, Slug, Address, CurrencyCode, TimeZoneId, IsActive "
                   f"FROM Restaurants WHERE Id = '{rid}'")[0]
    db.check("naziv restorana upisan", row[0], "Test Kafana")
    db.check("slug upisan", row[1], slug)
    db.check("adresa upisana", row[2], "Prva 1")
    db.check("valuta upisana", row[3], "RSD")
    db.check("vremenska zona upisana", row[4], "Europe/Belgrade")
    db.check("restoran je aktivan", row[5], "1")

    # The edit screen added in Faza 15 — everything but the slug.
    call("PUT", f"{MASTER}/api/platform/restaurants/{rid}", token,
         body={"id": rid, "name": "Test Kafana 2", "address": "Druga 2",
               "contactEmail": "novi@test.rs", "phoneNumber": "+381 22 222",
               "currencyCode": "EUR", "timeZoneId": "Europe/Vienna"},
         expect=200, label="izmena podataka restorana")
    row = db.query("SELECT Name, Slug, Address, ContactEmail, PhoneNumber, CurrencyCode, TimeZoneId "
                   f"FROM Restaurants WHERE Id = '{rid}'")[0]
    db.check("naziv izmenjen", row[0], "Test Kafana 2")
    db.check("slug NIJE menjan", row[1], slug)
    db.check("adresa izmenjena", row[2], "Druga 2")
    db.check("kontakt email izmenjen", row[3], "novi@test.rs")
    db.check("telefon izmenjen", row[4], "+381 22 222")
    db.check("valuta izmenjena", row[5], "EUR")
    db.check("vremenska zona izmenjena", row[6], "Europe/Vienna")

    call("POST", f"{MASTER}/api/platform/restaurants/{rid}/suspend", token, expect=200,
         label="gasenje restorana")
    db.check("restoran ugasen", db.scalar(f"SELECT IsActive FROM Restaurants WHERE Id = '{rid}'"), "0")

    call("POST", f"{MASTER}/api/platform/restaurants/{rid}/activate", token, expect=200,
         label="ukljucivanje restorana")
    db.check("restoran ukljucen", db.scalar(f"SELECT IsActive FROM Restaurants WHERE Id = '{rid}'"), "1")

    owner_email = f"vlasnik{stamp()}@test.rs"
    call("POST", f"{MASTER}/api/platform/restaurants/{rid}/owner", token,
         body={"restaurantId": rid, "email": owner_email, "password": "Vlasnik#123",
               "firstName": "Ana", "lastName": "Anic"},
         expect=200, label="vlasnicki nalog")
    row = db.query("SELECT UserName, Role, CONVERT(char(36), RestaurantId) FROM AspNetUsers "
                   f"WHERE Email = '{owner_email}'")[0]
    db.check("vlasnik se prijavljuje sa slugom", row[0], f"{slug}|{owner_email}")
    db.check("uloga Owner (4)", row[1], "4")
    db.check("vlasnik vezan za svoj restoran", row[2].lower(), rid.lower())

    # No licence yet: the till must refuse the venue outright.
    status, session = call("POST", f"{TILL}/api/auth/login",
                           body={"restaurantSlug": slug, "email": owner_email,
                                 "password": "Vlasnik#123"},
                           expect=200, label="prijava vlasnika novog restorana")
    if status == 200:
        call("GET", f"{TILL}/api/floor-plan", session["accessToken"], expect=402,
             label="bez licence kasa vraca 402")

    print("\n--- MASTER API: LICENCA ---")
    status, license_row = call("POST", f"{MASTER}/api/platform/licenses", token,
                               body={"restaurantId": rid, "plan": 3, "price": 45000,
                                     "notes": "dbwalk"},
                               expect=200, label="izdavanje tromesecne licence")
    if status != 200:
        return

    lid = license_row["id"]
    row = db.query("SELECT [Plan], Status, Price, DATEDIFF(month, StartsAtUtc, ExpiresAtUtc), Notes "
                   f"FROM Licenses WHERE Id = '{lid}'")[0]
    db.check("plan je tromesecni (3)", row[0], "3")
    db.check("licenca aktivna (1)", row[1], "1")
    db.check_num("cena upisana", row[2], 45000)
    db.check("rok je 3 meseca od pocetka", row[3], "3")
    db.check("napomena upisana", row[4], "dbwalk")

    if status == 201:
        call("GET", f"{TILL}/api/floor-plan", session["accessToken"], expect=200,
             label="sa licencom kasa radi odmah")

    before_expiry = db.scalar(f"SELECT CONVERT(varchar(30), ExpiresAtUtc, 126) FROM Licenses WHERE Id = '{lid}'")
    call("POST", f"{MASTER}/api/platform/licenses/{lid}/renew", token,
         body={"licenseId": lid, "plan": 12, "price": 150000}, expect=200, label="produzenje na godinu")
    row = db.query("SELECT DATEDIFF(month, '" + before_expiry + "', ExpiresAtUtc), [Plan], Price "
                   f"FROM Licenses WHERE Id = '{lid}'")[0]
    db.check("produzeno za 12 meseci od dosadasnjeg roka", row[0], "12")
    db.check("plan promenjen na godisnji (12)", row[1], "12")
    db.check_num("nova cena upisana", row[2], 150000)

    call("POST", f"{MASTER}/api/platform/licenses/{lid}/payments", token,
         body={"licenseId": lid, "amount": 150000, "paymentMethod": 2,
               "referenceNumber": "97-123", "notes": "dbwalk uplata"},
         expect=200, label="evidencija uplate")
    row = db.query("SELECT Amount, PaymentMethod, ReferenceNumber FROM LicensePayments "
                   f"WHERE LicenseId = '{lid}'")[0]
    db.check_num("iznos uplate upisan", row[0], 150000)
    db.check("nacin placanja Card (2)", row[1], "2")
    db.check("poziv na broj upisan", row[2], "97-123")

    call("POST", f"{MASTER}/api/platform/licenses/{lid}/suspend", token,
         body={"licenseId": lid, "reason": "Neplacanje, dbwalk"}, expect=200, label="suspenzija licence")
    db.check("licenca suspendovana (3)",
             db.scalar(f"SELECT Status FROM Licenses WHERE Id = '{lid}'"), "3")
    db.check_true("razlog suspenzije zabelezen",
                  "Neplacanje" in (db.scalar(f"SELECT Notes FROM Licenses WHERE Id = '{lid}'", "") or ""))

    if status == 201:
        call("GET", f"{TILL}/api/floor-plan", session["accessToken"], expect=402,
             label="suspendovana licenca zaustavlja kasu")

    call("POST", f"{MASTER}/api/platform/licenses/{lid}/reactivate", token,
         body={"licenseId": lid}, expect=200, label="vracanje licence u rad")
    db.check("licenca opet aktivna (1)",
             db.scalar(f"SELECT Status FROM Licenses WHERE Id = '{lid}'"), "1")

    call("POST", f"{MASTER}/api/platform/licenses/{lid}/cancel", token,
         body={"licenseId": lid, "reason": "Restoran zatvoren, dbwalk"}, expect=200,
         label="otkazivanje licence")
    db.check("licenca otkazana (4)",
             db.scalar(f"SELECT Status FROM Licenses WHERE Id = '{lid}'"), "4")

    call("GET", f"{MASTER}/api/platform/licenses/{lid}/payments", token, expect=200,
         label="uplate po licenci")

    print("\n--- MASTER DASHBOARD (protiv agregata) ---")
    status, dashboard = call("GET", f"{MASTER}/api/platform/dashboard", token, expect=200,
                             label="dashboard")
    if status == 200:
        db.check_num("broj restorana odgovara tabeli",
                     dashboard["totalRestaurants"], db.count("Restaurants"))
        db.check_num("aktivni restorani odgovaraju",
                     dashboard["activeRestaurants"], db.count("Restaurants", "IsActive = 1"))
        db.check_num("otkazane licence odgovaraju",
                     dashboard["suspendedLicenses"], db.count("Licenses", "Status = 3"))
        db.check_num("prihod od licenci = zbir uplata",
                     dashboard["totalLicenseRevenue"],
                     db.scalar("SELECT ISNULL(SUM(Amount), 0) FROM LicensePayments", "0"),
                     tolerance=0.02)


def main():
    tenant = demo_restaurant_id()
    if not tenant:
        print("Nema demo restorana u bazi  pokreni API sa SeedDemoData.")
        return 1

    owner, manager, waiter = sign_in("owner"), sign_in("manager"), sign_in("waiter")

    section_auth(tenant)
    tid = section_tables(owner, manager, tenant)
    if tid:
        section_floorplan(owner, manager, tid)
    section_menu(manager, tenant)
    section_inventory(owner, manager)
    free_table = section_orders(owner, waiter, manager, tenant)
    if free_table:
        section_void_open_order(waiter, free_table)
    section_qr_order(manager, tenant)
    section_reservations(owner, waiter, manager, tenant)
    section_shifts(owner, manager, tenant)
    section_staff(owner, tenant)
    section_reports(owner, waiter, tenant)
    delete_unused_table(manager)
    if tid:
        delete_table(manager, tid)
    section_master()

    http_failed = api.report()
    db_failed = db.report("provere u bazi")

    return 1 if (http_failed or db_failed) else 0


if __name__ == "__main__":
    sys.exit(main())
