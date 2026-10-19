"""Server-only adapter: return quota display fields, never credential contents."""
import datetime
import json
import math
import os
import subprocess
from concurrent.futures import ThreadPoolExecutor, as_completed


SUBSCRIPTION_LABELS = {
    "free": "Free",
    "pro": "Pro",
    "max": "Max",
    "team": "Team",
    "enterprise": "Enterprise",
}


def window(value):
    if not isinstance(value, dict):
        return None
    pct = value.get("pct")
    if not isinstance(pct, (int, float)) or isinstance(pct, bool) or not math.isfinite(pct) or not 0 <= pct <= 100:
        return None
    return {"pct": pct, "resetsAt": value.get("resetsAt")}


def sanitize(data, subscriptions=None):
    if data.get("schemaVersion") != 1 or not isinstance(data.get("accounts"), list):
        raise ValueError("unsupported_schema")
    subscriptions = subscriptions or {}
    rows = []
    for account in data["accounts"]:
        row = {key: account.get(key) for key in (
            "number", "email", "alias", "active", "disabled", "usageStatus",
            "usageFetchedAt", "usageAgeSeconds", "lastGoodFetchedAt", "lastGoodAgeSeconds", "usageRetryAt")}
        subscription = subscriptions.get(str(account.get("number")), {})
        row["subscriptionType"] = subscription.get("type")
        row["subscriptionStatus"] = subscription.get("status", "unavailable")
        for key in ("usage", "lastGoodUsage"):
            usage = account.get(key)
            row[key] = None if not isinstance(usage, dict) else {
                "fiveHour": window(usage.get("fiveHour")),
                "sevenDay": window(usage.get("sevenDay")),
                "scoped": [dict(window(item), name=item.get("name", ""))
                           for item in usage.get("scoped", []) if window(item) is not None],
            }
        rows.append(row)
    return {"ok": True, "schemaVersion": 1,
            "checkedAt": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "activeAccountNumber": data.get("activeAccountNumber"), "accounts": rows}


def read_subscription(cswap, account):
    number = account.get("number")
    if not isinstance(number, int) or isinstance(number, bool) or number < 1:
        return str(number), {"type": None, "status": "invalid_account"}
    env = os.environ.copy()
    local_bin = os.path.expanduser("~/.local/bin")
    env["PATH"] = local_bin + os.pathsep + env.get("PATH", "")
    try:
        result = subprocess.run(
            [cswap, "run", str(number), "--", "auth", "status"],
            capture_output=True, text=True, timeout=8, env=env,
        )
        if result.returncode:
            return str(number), {"type": None, "status": "auth_status_failed"}
        status = json.loads(result.stdout)
        if status.get("loggedIn") is not True:
            return str(number), {"type": None, "status": "not_logged_in"}
        if status.get("email", "").casefold() != str(account.get("email", "")).casefold():
            return str(number), {"type": None, "status": "identity_mismatch"}
        raw = str(status.get("subscriptionType", "")).strip().casefold()
        label = SUBSCRIPTION_LABELS.get(raw)
        return str(number), {"type": label, "status": "ok" if label else "unknown_type"}
    except subprocess.TimeoutExpired:
        return str(number), {"type": None, "status": "timeout"}
    except Exception:
        return str(number), {"type": None, "status": "unavailable"}


def read_subscriptions(cswap, accounts):
    values = {}
    with ThreadPoolExecutor(max_workers=min(4, max(1, len(accounts)))) as pool:
        futures = [pool.submit(read_subscription, cswap, account) for account in accounts]
        for future in as_completed(futures):
            number, value = future.result()
            values[number] = value
    return values


def main():
    try:
        cswap = os.path.expanduser("~/.local/bin/cswap")
        result = subprocess.run([cswap, "list", "--json"],
                                capture_output=True, text=True, timeout=11)
        if result.returncode:
            raise ValueError("cswap_failed")
        raw = json.loads(result.stdout)
        data = sanitize(raw, read_subscriptions(cswap, raw.get("accounts", [])))
    except subprocess.TimeoutExpired:
        data = {"ok": False, "error": "cswap_timeout"}
    except FileNotFoundError:
        data = {"ok": False, "error": "cswap_missing"}
    except Exception:
        data = {"ok": False, "error": "quota_unavailable"}
    print(json.dumps(data, ensure_ascii=False))


if __name__ == "__main__":
    main()
