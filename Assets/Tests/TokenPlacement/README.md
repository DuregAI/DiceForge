# Token placement tests

In Unity 6000.6.0f1, open **Window > General > Test Runner**, select **EditMode**,
and run the `Diceforge.TokenPlacement.Tests` assembly.

- `TokenPlacementResolverTests` exercises the pure calculation without Unity objects.
- `StonesTokensViewTests` enters Play Mode to compare actual objects with animation
  enabled and disabled, verify picking, recovery, pool resizing and cancellation.
- The three level tests load the authored levels 1, 5 and 9 through
  `BattleLauncher.Start`, apply legal moves for both players, call the real picking
  method at the moved object's screen position, and restart while moving.

The integration tests use reflection only to bridge the existing `Assembly-CSharp`
runtime, which Unity does not allow an assembly definition to reference.
Their teardown disposes the local SpacetimeDB runtime before Unity destroys
diagnostics, avoiding the existing unrelated diagnostics shutdown ordering bug.
They do not suppress unexpected error logs.

Verified in Unity 6000.6.0f1: **26 passed, 0 failed, 0 skipped**. The movement test
includes 14 scenarios, each exercised with animation both enabled and disabled.
No changes to the legacy EditMode assembly are required.
