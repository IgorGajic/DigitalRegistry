"""Reads the database directly, so a call can be judged by what it left behind.

The walkthrough checks status codes; this checks the rows. The two answer different questions: an
endpoint can return 200 and write nothing, or write the right row against the wrong restaurant, and
neither shows up in the response body. Every assertion here is a `SELECT` run through `sqlcmd`,
because the walkthrough is deliberately dependency-free and this keeps it that way.
"""
import json
import os
import re
import subprocess


def _from_api_settings():
    """Server and database from the API's own development settings.

    Read rather than restated, because the row checks are meaningless against a different database
    from the one the API is writing to — and a stale literal here fails every check for the wrong
    reason, which looks exactly like a real fault.
    """
    settings = os.path.join(os.path.dirname(__file__), "..", "..",
                            "src", "DigitalRegistry.Api", "appsettings.Development.json")

    try:
        with open(settings, encoding="utf-8-sig") as file:
            connection = json.load(file)["ConnectionStrings"]["DefaultConnection"]
    except (OSError, KeyError, ValueError):
        return None

    server = re.search(r"Server=([^;]+)", connection, re.IGNORECASE)
    database = re.search(r"(?:Initial Catalog|Database)=([^;]+)", connection, re.IGNORECASE)

    if not server or not database:
        return None

    return server.group(1).strip(), database.group(1).strip()


SERVER, DATABASE = _from_api_settings() or (r"localhost\SQLEXPRESS", "DigitalRegistry")

results = []


def query(sql):
    """Runs one query and returns the rows as lists of strings.

    Columns are separated with `~` rather than the more obvious `|`, because a user name here is
    `slug|email` — splitting on `|` tore those rows into the wrong columns.
    """
    completed = subprocess.run(
        ["sqlcmd", "-S", SERVER, "-E", "-d", DATABASE, "-h", "-1", "-W", "-s", "~",
         "-Q", "SET NOCOUNT ON; " + sql],
        capture_output=True, text=True)

    if completed.returncode != 0:
        raise RuntimeError(f"sqlcmd failed: {completed.stdout}{completed.stderr}")

    rows = []
    for line in completed.stdout.splitlines():
        line = line.strip()
        if not line or line.startswith("---"):
            continue
        rows.append([cell.strip() for cell in line.split("~")])

    return rows


def scalar(sql, default=None):
    """The first cell of the first row, or `default` when nothing came back."""
    rows = query(sql)

    if not rows or rows[0][0] == "NULL":
        return default

    return rows[0][0]


def count(table, where="1=1"):
    return int(scalar(f"SELECT COUNT(*) FROM [{table}] WHERE {where}", "0"))


def check(label, actual, expected):
    """Records one comparison. Values are compared as strings, which is what sqlcmd returns."""
    ok = str(actual) == str(expected)
    results.append((ok, label, actual, expected))
    mark = "  ok  " if ok else " PALO "
    detail = "" if ok else f"   (dobijeno {actual!r}, ocekivano {expected!r})"
    print(f"[{mark}] {label}{detail}")

    return ok


def check_num(label, actual, expected, tolerance=0.001):
    """Same as `check`, for money and quantities — sqlcmd renders these as `180.00` and `.250`."""
    try:
        ok = abs(float(actual) - float(expected)) <= tolerance
    except (TypeError, ValueError):
        ok = False

    results.append((ok, label, actual, expected))
    mark = "  ok  " if ok else " PALO "
    detail = "" if ok else f"   (dobijeno {actual!r}, ocekivano {expected!r})"
    print(f"[{mark}] {label}{detail}")

    return ok


def check_true(label, condition, detail=""):
    results.append((bool(condition), label, condition, True))
    mark = "  ok  " if condition else " PALO "
    print(f"[{mark}] {label}{'' if condition else '   ' + detail}")

    return bool(condition)


def report(title):
    total = len(results)
    failed = [r for r in results if not r[0]]
    print("\n" + "=" * 78)
    print(f"  {title}: {total} provera, palo {len(failed)}")
    for _, label, actual, expected in failed:
        print(f"    - {label}: dobijeno {actual!r}, ocekivano {expected!r}")
    print("=" * 78)

    return 1 if failed else 0
