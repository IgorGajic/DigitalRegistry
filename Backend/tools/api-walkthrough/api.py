"""Minimal HTTP helper for the backend walkthrough."""
import json, urllib.request, urllib.error, datetime

TILL = "http://localhost:5275"
MASTER = "http://localhost:5285"

# One suffix for the whole run, so every name, slug and email this script creates is new.
# Without it a second run collides on the first thing it tries to create and reports a 409 as a
# failure — which is what made the walkthrough need a freshly dropped database each time.
RUN = datetime.datetime.now().strftime("%H%M%S")

results = []


def call(method, url, token=None, body=None, expect=None, label=None):
    req = urllib.request.Request(url, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    data = json.dumps(body).encode() if body is not None else None
    if data:
        req.data = data
    try:
        with urllib.request.urlopen(req) as r:
            status, raw = r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        status, raw = e.code, e.read().decode()
    except Exception as e:
        status, raw = 0, str(e)

    payload = None
    if raw:
        try:
            payload = json.loads(raw)
        except Exception:
            payload = raw

    name = label or f"{method} {url.split('://')[1].split('/', 1)[1]}"
    if expect is not None:
        ok = status in (expect if isinstance(expect, (list, tuple)) else [expect])
        detail = ""
        if not ok:
            detail = (payload.get("detail") or payload.get("title") or str(payload)[:160]) \
                if isinstance(payload, dict) else str(payload)[:160]
        results.append((ok, name, status, expect, detail))
    return status, payload


def report():
    bad = [r for r in results if not r[0]]
    print()
    print("=" * 78)
    print(f"  ukupno provera: {len(results)}   proslo: {len(results) - len(bad)}   PALO: {len(bad)}")
    print("=" * 78)
    for ok, name, status, expect, detail in bad:
        print(f"  PAD  {name}")
        print(f"       ocekivano {expect}, dobijeno {status}   {detail}")
    return len(bad)


def utc(offset_days=0):
    return (datetime.datetime.now(datetime.UTC) + datetime.timedelta(days=offset_days)) \
        .strftime("%Y-%m-%dT%H:%M:%SZ")


def today(offset_days=0):
    return (datetime.datetime.now(datetime.UTC) + datetime.timedelta(days=offset_days)).strftime("%Y-%m-%d")


def at_hours(hours):
    return (datetime.datetime.now(datetime.UTC) + datetime.timedelta(hours=hours))         .strftime("%Y-%m-%dT%H:%M:%SZ")
