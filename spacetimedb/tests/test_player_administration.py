"""Integration checks against the real local database; never reset an existing player.

Creates two clearly named QA identities. Tokens remain in memory and are never printed.
Requires the database owner's normal spacetime CLI login for the one QA-only reset.
"""
import argparse
import json
import subprocess
import urllib.error
import urllib.request
import uuid

SERVER = "http://127.0.0.1:3000"
DATABASE = "diceforgeadminqa"


def request(path, data=None, token=None):
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(SERVER + path, body, headers)
    try:
        with urllib.request.urlopen(req, timeout=20) as response:
            text = response.read().decode()
            return response.status, json.loads(text) if text else None
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def sql(query, token):
    req = urllib.request.Request(
        f"{SERVER}/v1/database/{DATABASE}/sql", query.encode(),
        {"Authorization": "Bearer " + token, "Content-Type": "text/plain"})
    with urllib.request.urlopen(req, timeout=20) as response:
        return json.load(response)


def call(name, args, token):
    return request(f"/v1/database/{DATABASE}/call/{name}", args, token)


def main():
    global DATABASE
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", default=DATABASE)
    args = parser.parse_args()
    if args.database == "diceforgelocaldev":
        raise SystemExit("Use an isolated QA database; the real player registry is not test data.")
    DATABASE = args.database
    players = []
    for suffix in ("A", "B"):
        status, auth = request("/v1/identity", {})
        assert status == 200, (status, "identity creation failed")
        identity = auth["identity"]
        if isinstance(identity, dict):
            identity = identity["__identity__"]
        token = auth["token"]
        guid = str(uuid.uuid4())
        args = [guid, "Admin QA " + suffix, "woodland", 3, 2, 6, 0]
        status, _ = call("sync_player_progress", args, token)
        assert status == 200, (status, "registration failed")
        players.append((identity, token, args))

    first, second = players
    own_rows = sql("SELECT * FROM player_progress", first[1])[0]["rows"]
    assert len(own_rows) == 1, "RLS exposed another player's progress"
    status, _ = call("reset_player_map", [second[0], 0], first[1])
    assert status >= 400, "Regular player reset another player"

    command = ["spacetime", "call", "--server", SERVER, "--no-config", DATABASE,
               "reset_player_map", json.dumps(first[0]), "0"]
    reset = subprocess.run(command, capture_output=True, text=True, check=False)
    assert reset.returncode == 0, "Owner reset failed (check owner CLI login separately)."
    row = sql("SELECT * FROM player_progress", first[1])[0]["rows"][0]
    assert row[2] == "Admin QA A", "Reset changed the player's name"
    assert (row[4], row[5], row[7]) == (1, 0, 1), "Reset did not clear the map and increment its epoch"
    duplicate_reset = subprocess.run(command, capture_output=True, text=True, check=False)
    assert duplicate_reset.returncode != 0, "An outdated admin page performed a second reset"

    status, _ = call("sync_player_progress", first[2], first[1])
    assert status >= 400, "Stale pre-reset save restored map progress"
    status, _ = call("sync_player_progress", first[2][:3] + [1, 0, 6, 1], first[1])
    assert status == 200, "Acknowledged reset epoch was not accepted"
    status, _ = call("sync_player_progress", first[2][:-1] + [-1], first[1])
    assert status >= 400, "Negative reset epoch was accepted"
    print("PASS: own-row privacy, reset authorization, reset epoch, stale-save rejection")


if __name__ == "__main__":
    main()
