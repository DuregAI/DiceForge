# Local player administration

`player_progress` mirrors the player's map summary, keyed by the authenticated
SpacetimeDB Identity. It is not a full cloud save. Each player can read only their
own row through RLS; the database owner can query all rows. Player GUID and name
are display data and do not authorize writes.

`sync_player_progress` requires the currently subscribed `reset_epoch`.
`reset_player_map` requires the administrator identity and the epoch displayed
by the admin page. It increments the epoch, clears the map summary, and keeps
the player name. A save from before the reset cannot overwrite this change.
The game must persist the new epoch and clear its local map before syncing again.

New databases capture the publisher identity in a private administrator table
during Init. The already published `diceforgelocaldev` predates that Init reducer;
its owner and database identity were verified via `GET /v1/database/diceforgelocaldev`.
The source contains a public-identity fallback restricted to that exact database.
No owner token is embedded in the game, module source, or admin browser page.

## Windows build

The existing module uses SpacetimeDB.Runtime 2.0.3 and .NET 8. Install the
`wasi-experimental` workload with the portable .NET SDK. `build-local.ps1` accepts
the .NET path, WASI SDK root, and tool-cache directory.
Build products go to `C:/Backforge/Tools/AdminBackendBuild` so the repository's
previously committed compiler outputs are not modified by local builds.

For this workspace, `C:/Backforge/Tools/Wasi24` reuses the LLVM compiler and headers
from Unity 6000.6.0f1's WebGL toolchain, copied into the workspace without changing
the Unity installation. It combines them with the official wasi-sdk-24
`wasi-sysroot-24.0.tar.gz` and `libclang_rt.builtins-wasm32-wasi-24.0.tar.gz`.
The root contains `bin/clang.exe`, `bin/wasm-ld.exe`, `lib/clang/22/include`,
`lib/clang/22/lib/wasi/libclang_rt.builtins-wasm32.a`, and `share/wasi-sysroot`.
`bin/clang.cfg` specifies `--target=wasm32-wasi` and `-Wl,--no-stack-first`.
The latter preserves Mono 8's expected data-before-stack layout; Unity LLVM 22's
linker otherwise places the stack first and Mono fails its stack-bounds assertion.
Alternatively pass a complete official WASI SDK root.

```powershell
./spacetimedb/build-local.ps1
C:/Backforge/Tools/Spacetime203/spacetimedb-cli.exe generate --lang csharp --bin-path C:/Backforge/Tools/AdminBackendBuild/bin/Release/net8.0/wasi-wasm/AppBundle/StdbModule.wasm --out-dir ./Assets/_Project/02_Integrations/SpacetimeDb/Generated --no-config --yes
spacetime publish --server http://127.0.0.1:3000 --no-config --bin-path C:/Backforge/Tools/AdminBackendBuild/bin/Release/net8.0/wasi-wasm/AppBundle/StdbModule.wasm diceforgelocaldev --yes
```

Never use `--clear-database` to install this additive update.
Generate client bindings with CLI **2.0.3**, matching the existing Unity SDK.
CLI 2.10 changes `RemoteTableName` to a public override, incompatible with this SDK's
protected abstract property. The isolated CLI archive came from the official
v2.0.3 release and its SHA-256 was verified against the release asset digest:
`add76c90c623ccd351aa82752739b01ac8e1d30f9b2e20ab9753a26417fd1039`.

## Integration checks

Publish the same module to an isolated local QA database, then run
`python ./spacetimedb/tests/test_player_administration.py --database diceforgeadminqa`.
The checks create two named QA identities only in that database. They exercise
row privacy, owner-only reset, preserving name, rejecting stale saves, and
rejecting a second reset from an outdated admin page. Never reset a real player
as part of verification.
