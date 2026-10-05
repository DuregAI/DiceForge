"""HTTP guard and CLI protocol tests; does not alter any player database."""
import importlib.util
import json
import sys
import threading
import unittest
from http.client import HTTPConnection
from pathlib import Path
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("admin_server", Path(__file__).with_name("admin-server.py"))
admin = importlib.util.module_from_spec(spec)
spec.loader.exec_module(admin)


class RecordingBackend:
    def __init__(self):
        self.resets = []

    def players(self):
        return []

    def reset(self, identity, expected_epoch):
        self.resets.append((identity, expected_epoch))


class AdminSecurityTests(unittest.TestCase):
    def setUp(self):
        self.backend = RecordingBackend()
        self.server = admin.AdminServer(0, self.backend)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.port = self.server.server_address[1]
        self.host = f"127.0.0.1:{self.port}"
        self.identity = "ab" * 32
        self.body = json.dumps({"identity": self.identity, "expected_reset_epoch": 2, "confirm": self.identity})

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()

    def request(self, method="POST", path="/api/reset", body=None, overrides=None):
        headers = {"Host": self.host, "Origin": "http://" + self.host,
                   "Content-Type": "application/json", "X-CSRF-Token": self.server.csrf_token}
        headers.update(overrides or {})
        connection = HTTPConnection("127.0.0.1", self.port, timeout=3)
        connection.request(method, path, body=self.body if body is None else body, headers=headers)
        response = connection.getresponse()
        result = response.status, response.read()
        connection.close()
        return result

    def test_dns_rebinding_host_rejected(self):
        self.assertEqual(403, self.request(overrides={"Host": f"evil.test:{self.port}"})[0])
        self.assertEqual([], self.backend.resets)

    def test_cross_origin_and_missing_token_rejected(self):
        self.assertEqual(403, self.request(overrides={"Origin": "https://evil.test"})[0])
        self.assertEqual(403, self.request(overrides={"X-CSRF-Token": ""})[0])
        self.assertEqual([], self.backend.resets)

    def test_get_never_resets(self):
        self.assertEqual(404, self.request(method="GET")[0])
        self.assertEqual([], self.backend.resets)

    def test_identity_confirmation_and_epoch_required(self):
        payload = json.loads(self.body)
        payload["confirm"] = "different-player"
        self.assertEqual(400, self.request(body=json.dumps(payload))[0])
        payload["confirm"] = self.identity
        payload["expected_reset_epoch"] = True
        self.assertEqual(400, self.request(body=json.dumps(payload))[0])
        self.assertEqual([], self.backend.resets)

    def test_valid_reset_forwards_expected_epoch(self):
        self.assertEqual(200, self.request()[0])
        self.assertEqual([(self.identity, 2)], self.backend.resets)

    def test_owner_auth_errors_are_not_exposed(self):
        backend = admin.Backend("spacetime", "http://127.0.0.1:3000", "diceforgelocaldev")
        class Result:
            returncode = 1
            stdout = ""
            stderr = "secret-token-example"
        with patch.object(admin.subprocess, "run", return_value=Result()):
            with self.assertRaises(admin.BackendError) as caught:
                backend.players()
        self.assertNotIn("secret-token-example", str(caught.exception))

    def test_cli_schema_row_mapping(self):
        raw = '[{"schema":{"elements":[{"name":{"some":"current_level"}},{"name":{"some":"player_name"}}]},"rows":[[3,"Игрок"]]}]'
        self.assertEqual([{"current_level": 3, "player_name": "Игрок"}], admin.sql_rows(raw))

    def test_identity_cli_encoding_preserves_bits(self):
        self.assertEqual("0x" + self.identity, admin.identity_hex(self.identity))
        self.assertEqual("0x" + self.identity, admin.identity_hex("0x" + self.identity.upper()))
        with self.assertRaises(ValueError):
            admin.identity_hex("0xnot-an-identity")

    def test_sats_u256_identity_does_not_lose_precision(self):
        expected = "0x" + self.identity
        integer = int(self.identity, 16)
        self.assertEqual(expected, admin.identity_hex({"__identity__": integer}))
        self.assertEqual(expected, admin.identity_hex([expected]))
        with self.assertRaises(ValueError):
            admin.identity_hex(True)


if __name__ == "__main__":
    unittest.main()
