import json
import unittest
from unittest.mock import patch

from quota_probe import read_subscription, sanitize, window


class QuotaProbeTest(unittest.TestCase):
    def test_allowlist_and_missing_are_preserved(self):
        result = sanitize({"schemaVersion": 1, "accounts": [{
            "number": 1, "email": "test", "accessToken": "must-not-return",
            "credentials": {"token": "must-not-return"}, "usageStatus": "ok",
            "usage": {"fiveHour": {"pct": 0, "secret": "must-not-return"},
                      "sevenDay": None}}]})
        self.assertNotIn("must-not-return", json.dumps(result))
        self.assertEqual(result["accounts"][0]["usage"]["fiveHour"]["pct"], 0)
        self.assertIsNone(result["accounts"][0]["usage"]["sevenDay"])

    def test_failed_query_keeps_last_known_without_claiming_live(self):
        row = sanitize({"schemaVersion": 1, "accounts": [{"usageStatus": "token_expired",
            "usage": None, "lastGoodUsage": {"fiveHour": {"pct": 100}},
            "lastGoodFetchedAt": "2026-10-10T00:00:00Z"}]})["accounts"][0]
        self.assertIsNone(row["usage"])
        self.assertEqual(row["usageStatus"], "token_expired")
        self.assertEqual(row["lastGoodUsage"]["fiveHour"]["pct"], 100)

    def test_subscription_is_whitelisted_and_joined_by_account_number(self):
        result = sanitize({"schemaVersion": 1, "accounts": [{"number": 3}]}, {
            "3": {"type": "Max", "status": "ok", "secret": "must-not-return"}
        })
        row = result["accounts"][0]
        self.assertEqual(row["subscriptionType"], "Max")
        self.assertEqual(row["subscriptionStatus"], "ok")
        self.assertNotIn("must-not-return", json.dumps(result))

    @patch("quota_probe.subprocess.run")
    def test_auth_status_requires_matching_logged_in_identity(self, run):
        run.return_value.returncode = 0
        run.return_value.stdout = json.dumps({
            "loggedIn": True, "email": "other@example.com", "subscriptionType": "max",
            "accessToken": "must-not-return",
        })
        _, value = read_subscription("/safe/cswap", {"number": 2, "email": "expected@example.com"})
        self.assertIsNone(value["type"])
        self.assertEqual(value["status"], "identity_mismatch")

    @patch("quota_probe.subprocess.run")
    def test_unknown_subscription_is_not_echoed(self, run):
        run.return_value.returncode = 0
        run.return_value.stdout = json.dumps({
            "loggedIn": True, "email": "test@example.com", "subscriptionType": "untrusted-value",
        })
        _, value = read_subscription("/safe/cswap", {"number": 1, "email": "test@example.com"})
        self.assertIsNone(value["type"])
        self.assertEqual(value["status"], "unknown_type")

    def test_invalid_percentage_is_unknown(self):
        for value in (None, {}, {"pct": -1}, {"pct": 101}, {"pct": True}, {"pct": float("nan")}):
            self.assertIsNone(window(value))

    def test_future_schema_is_rejected(self):
        with self.assertRaises(ValueError):
            sanitize({"schemaVersion": 2, "accounts": []})


if __name__ == "__main__":
    unittest.main()
