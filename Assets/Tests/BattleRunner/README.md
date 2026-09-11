# Battle termination

Run `Diceforge.BattleRunner.Tests` in Unity Test Runner's **EditMode** tab.
The assembly is independent of the legacy EditMode test configuration. A small
reflection bridge accesses the production classes in `Assembly-CSharp`.

The turn limit counts completed player turns, including passes and empty rolls.
The final allowed turn may use every playable die. Bearing off the last stone
during that turn remains a normal win. Timeout is recorded once in `MatchLog`
without emitting a fake `OnMoveApplied` event; no next turn is started.

Timeout comparison is symmetric:

1. More borne-off stones wins.
2. With equal borne-off counts, the smaller total remaining distance wins.
   Distance uses each side's start cell and direction; a bar stone costs
   `boardSize + 1` pips, and every stone in a stack is counted.
3. Exact equality is a draw: `Winner == null`, `IsDraw == true`, reason `Timeout`.

A draw displays **Draw**, grants no battle rewards and leaves the campaign node
available to retry. The existing win/loss paths remain in use for decisive results.

Coverage includes blocked human/bot turns, empty rolls, final-turn dice, a normal
win on the final die, invalid requests, once-only completion, reset, both winners,
wrapped paths, stacks, the bar, draw rewards and the result overlay's retry button.
