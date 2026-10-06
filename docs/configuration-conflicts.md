# Configuration conflicts in Ursitoare

Review date: 2026-10-03. Code version: `f2168cd` (main).

This report lists configuration switches that work against each other. A conflict here means that some combination of settings looks valid but leaves an entity with **no correction path**, silently turns another switch off, or does something the switch's name doesn't promise.

Everything below comes from reading the code; nothing was run. Line numbers refer to `Runtime/src` at `f2168cd`.

Configured values for **AntShipWars** (from `MasterConfig.json` via `NetworkPredictionManagerAdapter.ApplyConfig`) are listed where they matter, because they decide whether a conflict is live today.

## Summary

| # | Switches | What goes wrong | Severity | Live in AntShipWars? |
|---|---|---|---|---|
| 1 | `IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS` × `predictAsFollower` | Controllable followers are neither resimulated nor snapped, so they drift | **High** | No (flag set to false). Yes with library defaults |
| 2 | `IGNORE_NON_AUTH_RESIM_DECISIONS` × `predictAsFollower` | Same as #1, for every follower | **High** | No (false) |
| 3 | `predictAsFollower = false` × controllable-only snap | Followers without input components (balls, props) are never corrected for spectators, or when prediction is off | **High** | Yes (spectators) |
| 4 | `BROADCAST_INPUTS` × `APPLY_SERVER_INPUT_TO_FOLLOWERS` | The server re-samples input on its own machine instead of sending the client's input, so followers replay the wrong input | **High** | Yes (both on) |
| 5 | Follower distance thresholds × distance calculation | Distance is always 0, so the thresholds can't tell near from far, and the precise threshold turns off `FOLLOWER_INSTANCE_RESIM_CHECKER` | Medium | Yes (precise threshold 3 m) |
| 6 | `INCREMENT_TICK_WHEN_NO_INPUT` × `IGNORE_OLD_INPUT` | The server moves past a missing input, then drops it when it arrives. Every input gap becomes a guaranteed misprediction | Medium | No (increment off) |
| 7 | `CATCHUP_SECTIONS` × `BUFFER_FULL_THRESHOLD` | Catch-up is sized from the entity history length, not the target buffer. It can eat the buffer it's meant to protect | Medium | Borderline (catch-up at 6 vs buffer 5) |
| 8 | `oversimProtectWithTickInterval` × `minTicksBetweenResims` / `maxTickResimulationCount` | One of the two limits is always silently ignored. The defaults make protection a no-op while it reports itself as on | Medium | No (protection off) |
| 9 | `DO_RESIM = false` / blocked resim × snap path | When a resim is skipped, nothing else corrects the entity. Snaps requested in the same tick are also dropped | Medium | No |
| 10 | `protectFromOversimulation` × `TRUST_ALREADY_RESIMULATED_TICKS` | Documented as "not both" but not enforced. They use two separate counters | Low | No (both off) |
| 11 | `correctWholeWorldWhenResimulating` × the ignore flags | The ignore flags apply when deciding *whether* to resimulate, but not when deciding *what to correct* | Low | No (whole-world on) |
| 12 | `IGNORE_OLD_INPUT` × `APPLY_OLD_INPUTS_IN_CURRENT_TICK` | With the default `IGNORE_OLD_INPUT = true`, the second flag can almost never do anything | Low | No |
| 13 | Dead or misleading switches | `APPLY_FORCES_TO_EACH_CATCHUP_INPUT`, `BUFFER_ONCE` do nothing. `CATCHUP_SECTIONS` is only read in the constructor | Low | AntShipWars sets the dead flag |

---

## 1. `IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS` × `predictAsFollower`
**Severity: High.** Library default: `true` (`PredictionManager.cs:24`). AntShipWars: `false`. The Ursitoare demo: `false` since 2026-10-03; before that it used the default and showed the bug.

A follower has two ways to be corrected:
- **Resimulation:** its own resim request counts in `ComputePredictionDecision`.
- **Snap:** `ClientFollowerSimulationTick` moves it to the newest server state.

The two switches control one each:
- `ShouldIgnoreResimulationDecision` (`ClientPredictionManager.cs:260-266`) drops the request of any *controllable* follower when the flag is on.
- `ClientFollowerSimulationTick` (`ClientPredictedEntity.cs:218`) snaps only when `!predictAsFollower`. The comment there says "otherwise let the resimulation do the snapping".
- `ConfigureFollowerResimulation` (`ClientPredictionManager.cs:289-310`) sets `predictAsFollower = true` for every follower whenever a local entity exists. With a threshold of 0 that's unconditional; with a threshold above 0 the distance is always 0 (#5).

**Result:** while the client controls an entity, every other player is predicted (no snap) *and* has its requests ignored (no resim). It's only corrected when some other entity triggers a resimulation, because `correctWholeWorldWhenResimulating` then snaps everything (#11). Players see remote entities drift, jump back, and drift again.

**Direction:** remove the flag and let `predictAsFollower` be the only answer:
- predicted ⇒ its request counts
- not predicted ⇒ ignored *and* snapped

A global `PREDICT_FOLLOWERS` can feed into `predictAsFollower`. #2, #3 and #5 need to be fixed together with this, or the gap just moves.

**Tests:** `Tests/Runtime/PredictionManagerFollowerTest.cs`
- `TestIgnoredControllableFollowerIsSnappedInstead` (fails today)
- `TestIgnoredControllableFollowerConvergesToServerWithoutLocalResimulation` (fails today)
- `TestControllableFollowerResimDecisionCountsWhenNotIgnored` (passes)

## 2. `IGNORE_NON_AUTH_RESIM_DECISIONS` × `predictAsFollower`
**Severity: High.** Default: `false`. AntShipWars: `false`, but `ApplyConfig` turns it on whenever `resim_ignore_follower_decisions` is true.

The first clause of `ShouldIgnoreResimulationDecision` drops the request of **every** follower, controllable or not. Like #1, nothing switches those followers to snapping. Balls aren't snapped at all (#3), so with this flag on, followers are only ever corrected as a side effect of resimulations triggered by local entities. The code itself asks what the clause is for (`//TODO: clarify first case. what is it for?`).

**Direction:** remove it. In a single-answer model it means "predict no followers", which is `PREDICT_FOLLOWERS = false`.

## 3. Non-predicted followers without input components are never snapped
**Severity: High.** Live for spectators and whenever `PREDICTION_ENABLED = false`.

`ClientFollowerSimulationTick` only snaps inside `if (IsControllable())` (`ClientPredictedEntity.cs:210`). For the rest it says `//NOTE: non-controllable followers need nothing here...` (`:250`). That only holds while their resim request counts. It stops holding in two situations:
- **No local entity** (spectator, or between owning entities). `ConfigureFollowerResimulation` sets `predictAsFollower = false` (`:291-296`), and the third clause of `ShouldIgnoreResimulationDecision` (`IsFollower && !predictAsFollower`) then drops every follower's request. Controllable followers get snapped; balls get nothing.
- **`PREDICTION_ENABLED = false`.** `ClientResimulationCheckPass` isn't called at all (`:93`), so `ConfigureFollowerResimulation` never runs and nothing resimulates. Controllable followers still snap. Balls drift.

**Direction:** move the snap out of the `IsControllable()` branch so that every follower with `!predictAsFollower` is snapped. Loading the server's input stays in the controllable branch.

## 4. `BROADCAST_INPUTS` × `APPLY_SERVER_INPUT_TO_FOLLOWERS`
**Severity: High.** Both default to `true`, and both are on in AntShipWars.

- **Server:** `ServerPredictedEntity.SamplePhysicsState` (`:273-281`) fills the input sent out with each state by calling `SampleInput(serverStateRecord.input)`. That reads the components' **live input sources on the server machine**, not the input the server applied for that tick. For an entity owned by a remote client, that's the server's own keyboard or AI, usually empty or wrong. The input actually applied is available as `lastAppliedInput` / `GetLastInput()` (`:390`) and isn't used.
- **Client:** with `APPLY_SERVER_INPUT_TO_FOLLOWERS`, followers load that input every follower tick (`ClientPredictedEntity.cs:226`) and during resimulation (`:516`).

**Result:**
- Remote players are extrapolated with the wrong input. This is the cause behind AntShipWars' "gunner client never simulates the pilot boost" analysis.
- On a host, the host's own key presses leak into every remote player's sent-out input.

Broadcasting is only correct for entities the server controls itself.

**Direction:** send `lastAppliedInput` for client-owned entities, and only re-sample for entities the server owns.

## 5. Follower distance thresholds × `GetMinSqrDistToAllLocalEnts`
**Severity: Medium.** AntShipWars: followers threshold `0`, precise threshold `3 m`.

`GetMinSqrDistToAllLocalEnts` (`ClientPredictionManager.cs:268-287`) computes `(ent.position - ent.position)`, which is always 0. It doesn't take the follower as a parameter. So:
- **`RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD > 0`:** every follower is "near", so the far ⇒ snap fallback that #1 was presumably meant to rely on can never happen.
- **`RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD > 0`:** every follower uses the precise checker, so `FOLLOWER_INSTANCE_RESIM_CHECKER` is never consulted (`RunPredictionDecisionHook`, `ClientPredictedEntity.cs:375`). In AntShipWars, `AdjustFollowerResimPrecision` and the `follower_resim_precision` / `maxFollowerResim*` settings therefore have no effect.
- **The two thresholds read differently:** `0` means "always predict" for the followers threshold, while for the precise threshold `0` means "never use the precise checker".

**Direction:** pass the follower in and measure from it to every locally controlled entity. Pick one meaning for `0`, preferably "disabled".

**Tests:**
- `TestFarFollowerIsNotPredictedWhenOutsideDistanceThreshold` (fails today)
- `TestFarFollowerDoesNotUsePreciseCheckerWhenOutsidePreciseThreshold` (fails today)
- `TestNearFollowerIsPredictedWhenInsideDistanceThreshold` (passes)

## 6. `INCREMENT_TICK_WHEN_NO_INPUT` × `IGNORE_OLD_INPUT`
**Severity: Medium.** Defaults: `false` and `true`. AntShipWars: `server_increment_ticks = false`.

- With `INCREMENT_TICK_WHEN_NO_INPUT`, a tick with no input still advances the entity's client tick (`ServerPredictedEntity.cs:238`, `:297-298`).
- When the late input arrives, `BufferClientTick` sees `tid < clientTickId` and, with `IGNORE_OLD_INPUT`, drops it (`:342-343`).

The client simulated that input; the server never will. Every gap in the input stream becomes a misprediction and a resimulation.

**Direction:** treat these as one policy for late input, either "wait for it" or "skip it and replace it", rather than two independent switches. If ticks keep advancing, apply late inputs (see #12) or reconcile through `lastAppliedInput`.

## 7. `CATCHUP_SECTIONS` × `BUFFER_FULL_THRESHOLD`
**Severity: Medium.**

- **Buffer target:** buffering keeps `BUFFER_FULL_THRESHOLD` inputs queued before consuming (`:351`).
- **Catch-up step:** `ticksPerCatchupSection = floor(bufferSize / CATCHUP_SECTIONS) + 1` (`:72`), where `bufferSize` is the entity's **history length**, not the buffer target.
- **Catch-up amount:** each tick runs `floor(queueFill / ticksPerCatchupSection)` extra inputs (`:261-266`).

The two numbers are set independently, so nothing keeps catch-up from starting at or below the buffer target. For example, with `bufferSize 50`, sections ≥ 17 gives a step of ≤ 3, and with the default target of 3 the server catches up every tick and drains the cushion the buffer exists to provide. It goes the other way too: the Ursitoare demo's defaults (50 / 3 → 17) only catch up once 17 inputs are queued, more than five times the target. AntShipWars sits right at the edge: 50 / 10 → 6 against a target of 5.

Buffering also never starts again once the queue drains; only `Reset` / `ResetClientState` turn it back on.

**Direction:** express catch-up relative to `BUFFER_FULL_THRESHOLD` (for example "catch up while fill > target + N"), and check at setup that the step is greater than the target.

## 8. `oversimProtectWithTickInterval` × `minTicksBetweenResims` / `maxTickResimulationCount`
**Severity: Medium.** Defaults: protection `true`, interval mode `true`, `minTicksBetweenResims = 0`, `maxTickResimulationCount = 1`. AntShipWars: protection off.

`CanResiumlate` (`ClientPredictionManager.cs:558-563`) uses `minTicksBetweenResims` in interval mode and `maxTickResimulationCount` otherwise. Whichever one isn't selected is silently ignored, yet AntShipWars and the demo set both. With the defaults, interval mode checks `ticksSinceResim >= 0`, which is always true. So `protectFromOversimulation = true` does nothing except keep its counter (`MarkResimulatedTick`, `:512-518`).

**Direction:** replace the two booleans with one mode enum (`Off | Interval | PerTickBudget`), and use defaults where the selected mode actually limits something.

## 9. Blocked or disabled resimulation × snap path
**Severity: Medium.** `DO_RESIM` defaults to `true` and `DO_SNAP` to `true`. AntShipWars: `DO_SNAP = false`.

- `ComputePredictionDecision` keeps only the strongest request (`:339-342`): RESIMULATE beats SNAP.
- If the resim is blocked (`CanResiumlate` false), the decision becomes **NOOP**, not SNAP (`ClientResimulationCheckPass`, `:384-391`). Entities that asked to snap in that tick are not snapped.
- If `DO_RESIM = false`, `Resimulate` returns early (`:412`) and nothing at all corrects the local entity.

The library's own deciders never return SNAP; only a commented-out line in `TestSimpleConfigurableResimulationDecider.cs:58` does. So `DO_SNAP` and `Snap()` currently only matter for custom deciders. In practice `DO_RESIM = false` turns off all correction, except the `SIMULATION_FREEZE` reset.

**Direction:** when a resim is blocked or disabled, fall back to snapping the entities that asked for one (or all of them). Document `DO_SNAP` as applying to custom deciders only, or remove it.

## 10. `protectFromOversimulation` × `TRUST_ALREADY_RESIMULATED_TICKS`
**Severity: Low.** Both are off in AntShipWars.

`PredictionManager.cs:61` says "either use protectFromOversimulation or TRUST_ALREADY_RESIMULATED_TICKS, not both", but nothing enforces it. They also count different things:
- the manager's `tickResimCounter` is a ring buffer, written only while protection is on (`ClientPredictionManager.cs:30, 516`)
- each entity's `tickResimCounter` is a `Dictionary` that's never trimmed, and is written on every resim step (`ClientPredictedEntity.cs:84, 535`)

With both on, a tick can be blocked by the manager budget and skipped by the entity check at once, so a real desync goes uncorrected for longer than either setting alone would allow.

**Direction:** reject the combination at setup, or merge both into the single mode from #8.

## 11. `correctWholeWorldWhenResimulating` × the ignore flags
**Severity: Low.** Default: `true`.

`Resimulate` (`:455`) snaps an entity if the whole world is being corrected **or** that entity's own decision is RESIMULATE. It never calls `ShouldIgnoreResimulationDecision`. So an "ignored" follower still gets snapped, but only when *another* entity triggered the resimulation. This is what makes #1 look like periodic snapping rather than unbounded drift.

**Direction:** once #1 and #2 are fixed this mostly goes away. Use the same predicted/not-predicted answer in both places.

## 12. `IGNORE_OLD_INPUT` × `APPLY_OLD_INPUTS_IN_CURRENT_TICK`
**Severity: Low.** Defaults: `true` and `false`.

`APPLY_OLD_INPUTS_IN_CURRENT_TICK` (`ServerPredictedEntity.cs:135`) only affects inputs already queued that have fallen behind `clientTickId`. With the default `IGNORE_OLD_INPUT = true`, late inputs never get into the queue (`:342-343`). The second flag can only act on inputs that went stale while queued, which practically needs `INCREMENT_TICK_WHEN_NO_INPUT` (#6).

**Direction:** merge into the single late-input policy from #6.

## 13. Dead or misleading switches
**Severity: Low.**
- **`ServerPredictedEntity.APPLY_FORCES_TO_EACH_CATCHUP_INPUT`** (`:14`) is never read. Catch-up always applies forces once for several inputs (`ServerSimulationTick`, `:216-257`), so the forces of skipped inputs are lost. AntShipWars sets this flag to `false`, and setting it to `true` would change nothing.
- **`ServerPredictedEntity.BUFFER_ONCE`** (`:16`) is never read. Buffering always happens once (see #7).
- **`CATCHUP_SECTIONS`** is only read in the `ServerPredictedEntity` constructor (`:72`). Changing it later affects new entities only. AntShipWars and the demo apply config after some entities may already exist.
- **Values copied at registration:** `SNAPSHOT_INSTANCE_RESIM_CHECKER` and `FOLLOWER_INSTANCE_RESIM_CHECKER` are also copied when an entity registers (`ClientPredictionManager.AddPredictedEntity`). AntShipWars works around this by re-assigning every entity in `AdjustFollowerResimPrecision`.

**Direction:**
- delete the dead flags, or implement them (applying forces per catch-up input is a real option)
- make settings that are read at registration either live, or documented and applied explicitly

---

## Other issues found while tracing (not configuration)
- **Gap detection never fires.** `GetPredictionDecision` sets `lastCheckedServerTickId = serverState.tickId` (`ClientPredictedEntity.cs:348`) just *before* the check `serverState.tickId > lastCheckedServerTickId` (`:357`). The comparison is always false, so `GAP_IN_SERVER_STREAM` is never reported.
- **Removing an entity leaks its ownership.** `RemovePredictedEntity` calls `SetEntityOwner(entity, invalidConnectionId)`, which returns early for the invalid id (`ServerPredictionManager.cs:150`). The connection's entity set keeps the removed entity, and the client is never told it lost ownership. This was already reported in the earlier library review.

## Suggested order
1. **#1, #2, #3, #5 together:** a single "is this follower predicted" answer, snapping for every non-predicted follower, and a working distance calculation. The new follower tests cover this.
2. **#4:** broadcast the input that was actually applied. Highest gameplay impact in AntShipWars today.
3. **#6 and #12:** one late-input policy.
4. **#8, #9, #10:** one oversimulation mode with a fallback to snapping.
5. **#7, #13:** catch-up relative to the buffer target, and removal of the dead flags.
