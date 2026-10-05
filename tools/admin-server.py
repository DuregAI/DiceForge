"""Local SpacetimeDB administration. Owner credentials stay in the spacetime CLI."""
import argparse
import hmac
import json
import secrets
import shutil
import subprocess
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parent


def sql_rows(raw):
    """Decode the CLI's positional JSON rows using its column schema."""
    results = json.loads(raw)
    rows = []
    for result in results:
        names = [column["name"]["some"] for column in result["schema"]["elements"]]
        rows.extend(dict(zip(names, row)) for row in result["rows"])
    return rows


class BackendError(Exception):
    pass


def identity_hex(value):
    # SATS ProductValues can be positional or named. Normalize before sending
    # a U256 to JavaScript, where integer values would lose precision.
    if isinstance(value, dict) and set(value) == {"__identity__"}:
        value = value["__identity__"]
    elif isinstance(value, list) and len(value) == 1:
        value = value[0]
    if type(value) is int and 0 <= value < 2 ** 256:
        return "0x" + format(value, "064x")
    if not isinstance(value, str):
        raise ValueError("Invalid identity")
    raw = value[2:] if value.startswith("0x") else value
    if len(raw) != 64 or any(c not in "0123456789abcdefABCDEF" for c in raw):
        raise ValueError("Invalid identity")
    return "0x" + raw.lower()


class Backend:
    def __init__(self, executable, server, database):
        self.executable, self.server, self.database = executable, server, database

    def run(self, command, *arguments):
        args = [self.executable, command, "--server", self.server, "--no-config", "--yes"]
        try:
            result = subprocess.run(args + list(arguments), capture_output=True, text=True,
                                    encoding="utf-8", timeout=20, check=False)
        except (OSError, subprocess.TimeoutExpired) as error:
            raise BackendError("SpacetimeDB недоступна. Проверьте локальный сервер и CLI.") from error
        if result.returncode:
            # CLI errors can contain authentication details; do not return or log them.
            raise BackendError("Запрос отклонён. Проверьте подключение, публикацию модуля и права владельца базы.")
        return result.stdout

    def players(self):
        query = ("SELECT identity, player_guid, player_name, chapter_id, current_level, completed_levels, "
                 "total_levels, updated_at_unix_ms_utc, reset_epoch FROM player_progress")
        try:
            players = sql_rows(self.run("sql", "--format", "json", self.database, query))
            for player in players:
                player["identity"] = identity_hex(player["identity"])
            return players
        except (ValueError, KeyError, TypeError) as error:
            raise BackendError("Не удалось прочитать список игроков из ответа CLI.") from error

    def feedback(self):
        query = ("SELECT feedback_id, player_guid, player_name, category, message, "
                 "created_at_unix_ms_utc, build_version, scene_name FROM feedback_entry")
        try:
            return sql_rows(self.run("sql", "--format", "json", self.database, query))
        except (ValueError, KeyError, TypeError) as error:
            raise BackendError("Не удалось прочитать отзывы из ответа CLI.") from error

    def reset(self, identity, expected_epoch):
        self.run("call", self.database, "reset_player_map", json.dumps(identity_hex(identity)), str(expected_epoch))


class AdminServer(ThreadingHTTPServer):
    def __init__(self, port, backend):
        super().__init__(("127.0.0.1", port), Handler)
        self.backend = backend
        self.csrf_token = secrets.token_urlsafe(32)


class Handler(BaseHTTPRequestHandler):
    server_version = "GlimbleHopAdmin"

    def setup(self):
        super().setup()
        self.connection.settimeout(10)

    def log_message(self, format, *args):
        # Avoid logging request paths, headers or submitted player information.
        pass

    def valid_host(self):
        port = self.server.server_address[1]
        return self.headers.get("Host") in (f"127.0.0.1:{port}", f"localhost:{port}")

    def send_bytes(self, status, body, content_type):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'")
        self.end_headers()
        self.wfile.write(body)

    def send_json(self, status, payload):
        self.send_bytes(status, json.dumps(payload, ensure_ascii=False).encode("utf-8"),
                        "application/json; charset=utf-8")

    def do_GET(self):
        if not self.valid_host():
            self.send_json(403, {"error": "Недопустимый адрес запроса."})
            return
        path = urlsplit(self.path).path
        if path == "/api/players":
            try:
                self.send_json(200, {"players": self.server.backend.players()})
            except BackendError as error:
                self.send_json(503, {"error": str(error)})
            return
        if path == "/api/feedback":
            try:
                self.send_json(200, {"feedback": self.server.backend.feedback()})
            except BackendError as error:
                self.send_json(503, {"error": str(error)})
            return
        assets = {"/": ("admin-index.html", "text/html; charset=utf-8"),
                  "/admin-ui.js": ("admin-ui.js", "application/javascript; charset=utf-8"),
                  "/admin-ui.css": ("admin-ui.css", "text/css; charset=utf-8")}
        if path not in assets:
            self.send_json(404, {"error": "Страница не найдена."})
            return
        filename, content_type = assets[path]
        body = (ROOT / filename).read_bytes()
        if path == "/":
            body = body.replace(b"__CSRF_TOKEN__", self.server.csrf_token.encode("ascii"))
        self.send_bytes(200, body, content_type)

    def do_POST(self):
        if not self.valid_host():
            self.send_json(403, {"error": "Недопустимый адрес запроса."})
            return
        if self.headers.get("Origin") != "http://" + self.headers.get("Host", ""):
            self.send_json(403, {"error": "Запрос разрешён только из локальной админки."})
            return
        token = self.headers.get("X-CSRF-Token", "")
        if not hmac.compare_digest(token, self.server.csrf_token):
            self.send_json(403, {"error": "Обновите страницу админки и повторите действие."})
            return
        if urlsplit(self.path).path != "/api/reset":
            self.send_json(404, {"error": "Действие не найдено."})
            return
        if self.headers.get("Content-Type", "").split(";")[0] != "application/json":
            self.send_json(415, {"error": "Ожидался JSON."})
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length < 1 or length > 2048:
                raise ValueError()
            payload = json.loads(self.rfile.read(length))
            identity = payload.get("identity")
            epoch = payload.get("expected_reset_epoch")
            identity_hex(identity)
            if (payload.get("confirm") != identity
                    or type(epoch) is not int or epoch < 0 or epoch > 9223372036854775807):
                raise ValueError()
        except (ValueError, UnicodeDecodeError, AttributeError):
            self.send_json(400, {"error": "Не выбран игрок или не подтверждён сброс."})
            return
        try:
            self.server.backend.reset(identity, epoch)
            self.send_json(200, {"ok": True})
        except BackendError as error:
            self.send_json(503, {"error": str(error) + " Обновите список игроков перед повторным сбросом."})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=18081)
    parser.add_argument("--server", default="http://127.0.0.1:3000")
    parser.add_argument("--database", default="diceforgelocaldev")
    parser.add_argument("--spacetime", default=shutil.which("spacetime"))
    args = parser.parse_args()
    if not args.spacetime:
        parser.error("CLI spacetime не найдена. Укажите путь через --spacetime.")
    server = AdminServer(args.port, Backend(args.spacetime, args.server, args.database))
    print(f"GlimbleHop admin: http://127.0.0.1:{server.server_address[1]}/", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
