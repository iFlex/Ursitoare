# Ursitoare library review: 2026-10-10

- **Code version:** Ursitoare `4ffb402` ("Ownership simplification"). Line numbers refer to that commit. Statuses last updated 2026-10-10 09:30.
- **Replaces:** `library-review-2026-10-09.md` and `tick-model-discussion-2026-10-09.md`, merged here. The tick model discussion is now [Q8](#q8-tick-model) and [Appendix A](#appendix-a-tick-model-discussion); its points on B1 and the client guard are in [T1](#t1-client-guard-against-future-stamped-states).
- **Method:** code reading only. No tests were run and no library code was changed. Appendix A's parts about other games come from web sources (listed at the end); the Overwatch and Rocket League details are second-hand.
- **Earlier reviews:** the 2026-10-09 pass re-checked `AntShipWars_URP/Documents/ursitoare-library-review.md` (2026-10-01), `docs/configuration-conflicts.md` (2026-10-03, still a separate file) and the Ursitoare notes in the AntShipWars project memory. See [Fixed since earlier reviews](#fixed-since-earlier-reviews) and [Prior claims that were wrong or changed](#prior-claims-that-were-wrong-or-changed).
- **Integration checked:**
  - `Ursitoare-Mirror` at `97d60c1` (`NetworkPredictionManagerAdapter`, `AbstractPredictedNetworkBehaviour`). It uses entity `bufferSize = 50`, `RewindablePhysicsController(120)` and `NetworkServer.connections.Keys` as the connection list. Its senders drop messages to connection 0 and to unknown ids (`GetNetConn`). It builds against `4ffb402` (confirmed by you).
  - `AntShipWars_URP` working tree: its own adapter (not Ursitoare-Mirror), ownership calls in `NetworkPlayerController`, defaults in `MasterConfig`. Its sender also drops messages to connection 0.

Each open item has an ID so it can be discussed on its own. IDs from the 2026-10-09 review are kept.

| Status | Meaning |
|---|---|
| **Regression** | Introduced since 2026-10-09 (`5c2c9a2`, `eff3bc5`, `4ffb402` or Ursitoare-Mirror changes). |
| **Confirmed** | The code contradicts its own stated intent (names, comments, tests) or your answers below. Ready to fix. |
| **Partly fixed** | Some of the direction is done; what's left is listed. |
| **Decision needed** | Whether it's a bug depends on how you want it to behave. |
| **Latent** | Wrong, but not reachable with the current lifecycle or configuration. |
| **Task** | Agreed follow-up work. |
| **Perf** | Performance or allocation only. |
| **Cleanup** | Docs, tests, dead code. |

---

## Summary

- **Fixed since 2026-10-09:**
  - B1 on the server, Q1 (followers without input components now snap), the follower distance bug, L4.
  - R1 and the ownership bugs that `eff3bc5` introduced, in `4ffb402`.
  - R2, in Ursitoare-Mirror `97d60c1`.
- **Partly fixed:** B5 (server side only), B6 (owner list cleaned up, client still not told, and the binding no longer works around it), B15 (no shared input record, but history now holds neither input nor component state).
- **Regressions still open:**
  - [R3](#r3-server-exceptions-are-swallowed-silently): server exceptions are swallowed silently; B13 now fails without any sign.
  - [R4](#r4-the-precise-follower-checker-needs-a-follower-distance-threshold): AntShipWars followers no longer use the precise checker.
- **Suggested order:** T2 and T3 (get the tests green and run them), R3, T1 (session restart), then B6 and B3.

### Risk to AntShipWars

From reading only; nothing was run.

- **Nothing known makes the game unplayable in its normal flow.** The last such bug, R1, is fixed in `4ffb402`.
- **Most likely to break play: T1 at a session restart.** `ServerReset()` on `PreppingSession` clears both sides while players stay connected. A plane that registers on a client within about one round trip can be left uncorrected for minutes. Timing-dependent; see [T1](#t1-client-guard-against-future-stamped-states).
- **Would hide a crash instead of causing one:** R3 on the server, B5 on the client. Neither triggers unless game code throws.
- **Only under specific conditions:**
  - B13: AntShipWars uses world-state mode by default (`server_use_world_state_packet = true`), but the adapter sets the flag in `SetupServer`, before any plane spawns.
  - B6: only if a piloted plane is disabled and re-enabled without being despawned (not checked).
- **Degrades play but doesn't break it:** B3 (other players' planes jitter more), Q3 (up to ~133 ms extra delay after a lag burst), Q2 (brief stall after an ownership change), R4 (looser correction check for other players' planes).

### Status at a glance

| ID | Item | Status |
|---|---|---|
| R1 | Prediction off freezes the client | Fixed in `4ffb402` |
| R2 | Ursitoare-Mirror calls the now-private `UnsetOwnership(entity)` | Fixed in Ursitoare-Mirror `97d60c1` |
| R3 | Server swallows entity exceptions silently | Regression (`eff3bc5`); direction proposed |
| R4 | Precise follower checker needs a follower distance threshold | Regression or intended (`5c2c9a2`) |
| B2 | Client input records allocated at 2× size | Confirmed |
| B3 | `BROADCAST_INPUTS` doesn't do what it should | Confirmed |
| B4 | Adding or removing an entity during a tick | Confirmed |
| B5 | One throwing entity aborts the tick | Partly fixed (server only) |
| B6 | Removing an owned entity doesn't tell the client | Partly fixed; no longer worked around in the binding |
| B7 | `INCREMENT_TICK_WHEN_NO_INPUT` never takes effect | Confirmed |
| B8 | `GAP_IN_SERVER_STREAM` never fires | Confirmed |
| B9 | Metrics never updated | Confirmed |
| B10 | RTT measured from time 0 | Confirmed |
| B11 | Catch-up counted as packet loss | Confirmed |
| B12 | CATCHUP desync event every tick | Confirmed |
| B13 | World-state mode turned on after registration | Confirmed, now silent (R3) |
| B14 | Rewind ignores history length and new bodies | Confirmed |
| B15 | Server state history | Partly fixed |
| B16 | Client `GetServerTickId()` always 0 per entity | Confirmed |
| B17 | Ownership messages: unwrapped send, spurious messages | Confirmed |
| B18 | Interpolators stuck on oldest state without smoothing | Confirmed |
| B19 | Minor buffer correctness | Confirmed |
| T1 | Client guard against future-stamped states | Task, not started; has a concrete trigger in AntShipWars |
| T2 | Update ownership tests to the new model | Task, in progress: 4 tests still expect the old model |
| T3 | Run the test suite | Task |
| Q1 | Snapping followers without input components | Resolved |
| Q2–Q8 | See [Decision needed](#decision-needed) | Decision needed |
| L1–L3, L5–L7 | See [Latent](#latent) | Latent |
| L4 | Unowned entities allocate an input record per tick | No longer reachable |
| P1–P12 | See [Performance](#performance) | Perf, unchanged |
| C1–C3 | See [Cleanup](#cleanup) | Cleanup |

---

## Decisions recorded

| # | Question | Your answer |
|---|---|---|
| D1 | Should non-predicted followers without input components snap to the newest server state? | Only if `PREDICT_FOLLOWERS` is false; later confirmed as a real gap. Implemented in `5c2c9a2` with the per-entity condition `!predictAsFollower`, which covers all three cases in [Q1](#q1-snapping-followers-without-input-components-resolved). |
| D2 | What does `BROADCAST_INPUTS = false` mean? | The server does not send the input it used to the clients. |
| D3 | Is fail-fast on component exceptions intended? | No. Concern: the cost of wrapping each entity in try/catch. See [B5](#b5-one-throwing-entity-aborts-the-whole-tick), and [R3](#r3-server-exceptions-are-swallowed-silently) for the proposed handling. |
| D4 | Should spawning/despawning predicted entities inside the tick work? | Yes. It's a server action and it doesn't matter when it happens. |
| D5 | Can the server stop sending an entity's state while clients keep it registered? | No. The server only stops when the entity is destroyed (deregistered), and that deregistration reaches the client too. |
| D6 | Is buffering only once (never re-buffering after the queue drains) intended? | Yes. No extra delay wanted. |
| D7 | Catch-up starts at 17 queued inputs against a target of 3: does it matter? | "So what?" Explanation given, **pending your view**, see [Q3](#q3-catch-up-threshold). |
| D8 | (Earlier, 2026-09-29) | Client ownership is a server fact about an entity id. It survives client deregister/re-register; only a server revoke or `Clear()` drops it. |

---

## Regressions

### R1. Turning prediction off froze the client
- **Status: fixed in `4ffb402`** (by reading). Introduced in `5c2c9a2`.
- **The bug:** with `PREDICTION_ENABLED = false`, a locally controlled entity fell into the follower branch of `ClientPreSimTick`, so it no longer sampled or sent input. No heartbeat was sent either, because the client still had a locally controlled entity.
  - The server's label for the connection stopped at the last tick it applied, so every later state carried the same stamp.
  - `AddServerState` pinned the local entity to its last server state (`ClientPredictedEntity.cs:407-411`), and followers never saw a newer end tick (`ClientPredictedEntity.cs:215`), so the whole client froze.
  - Live in AntShipWars: its debug toggle flips the flag at runtime (`CUtil.cs:488`; adapter `:485`, `:673`).
- **The fix** (`ClientPredictionManager.cs:590-630`): a locally controlled entity always sends input; with prediction off it calls `SampleInput(tickId)` instead of `ClientSimulationTick` (`:596-604`). It then also runs the follower tick (`:622-630`), so it snaps to each new server state and applies forces with the input the server echoes back.
  - Behaviour change from `b4d52f8`, where the entity only sampled and sent input: it now also applies forces locally between server states, like an unpredicted follower. That seems the better behaviour for this debug mode.
- **Still to do:** `ClientPredictedEntityTest.DisablingPredictionSnapsLocalEntityToServerState` drives the entity directly, so nothing covers the manager path. Add a manager-level test: with prediction off, a local entity still sends input every tick and snaps to new server states.

### R2. Ursitoare-Mirror no longer compiled
- **Status: fixed in Ursitoare-Mirror `97d60c1`.** Introduced in `4ffb402`, which made `UnsetOwnership(ServerPredictedEntity)` private (`ServerPredictionManager.cs:222`).
- **The bug:** `AbstractPredictedNetworkBehaviour.OnStopServer` called that method to tell the owning client it lost ownership before deregistering. That call was the binding's workaround for B6.
- **The fix:** `97d60c1` removed the call; `OnStopServer` now only deregisters. It also added `autoSetOwnership` (default true): with it off, an entity stays server-owned until the game calls `SetEntityOwner`.
- **Side effects:**
  - B6 is no longer worked around in the binding; see B6.
  - The comment above the removed line still says "release it first ... and tell the owning client" (C1).
- AntShipWars only uses the two-argument overload (`NetworkPlayerController.cs:353, 371`), so it was never affected.

### R3. Server exceptions are swallowed silently
- **Introduced in:** `eff3bc5`.
- **Where:** the per-entity `try/catch` in `PreSimTick` (`ServerPredictionManager.cs:85-108`) and `PostSimTick` (`:118-146`). The catch only logs when `LOG_ENTITY_PROCESSING_EXCEPTIONS` is on (off by default). There's no event, no counter, and an entity that throws every tick keeps throwing.
- **Effects:**
  - B13 changes from "aborts every server tick" to "silently sends an empty world state every tick": `WorldStateRecord.Set` throws, the catch swallows it, and `SendWorldState` (`:150`) still runs with `fill = 0`.
  - B4 is unaffected: "collection was modified" is thrown by the `foreach` enumerator, outside the try.
- **Direction (proposed 2026-10-10):** keep the per-entity try/catch (D3: no fail-fast), but never fail silently, and bound the cost of an entity that keeps throwing.
  - **Always report.** Move `EntityProcessingError` from `ClientPredictionManager` to `PredictionManager`, add the stage (`PreSim`, `PostSim`, and on the client `Resim`) and the consecutive-failure count, and dispatch it from an `onEntityProcessingError` event on both managers. Add a total counter next to `clientSendErrors`.
  - **Log the first exception per entity unconditionally** with `Debug.LogException`. An exception is a bug, not debug noise, so it shouldn't depend on a flag that's off by default. Log again only when the entity is quarantined, so a persistent fault doesn't flood the log.
  - **Quarantine after N consecutive failures** (a static setting, e.g. 3). Keep the counter on the entity and reset it on a tick that succeeds. A quarantined entity is skipped by both loops, so it costs nothing more and stops sending states. Dispatch `onEntityFaulted` so the integration decides what to do (despawn it, kick its owner, report it). The library shouldn't destroy game objects itself.
  - **Fix B13 directly** (resize the world-state record lazily), so it isn't "handled" by the catch.
  - **B4 isn't covered** by any catch, because the enumerator throws outside it; defer adds and removes instead.
  - **Optional:** a `RETHROW_ENTITY_EXCEPTIONS` setting for development, to stop on the first fault in the debugger. Off by default.
  - **Cost:** the try blocks already exist. The rest is one int per entity and a branch per loop step; the expensive part (the throw itself) stops after N ticks.
- **Client side:** the same pattern is B5's direction, plus `finally` in `Resimulate`.

### R4. The precise follower checker needs a follower distance threshold
- **Introduced in:** `5c2c9a2`. May be intended.
- **Where:** `ConfigureFollowerResimulation` (`ClientPredictionManager.cs:297-317`). When `RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD == 0` it sets `usePreciseResimChecker = false` and returns (`:307-312`) before the precise threshold is read.
- **Effect in AntShipWars:** its defaults are 0 and 3 m (`MasterConfig.cs:217-218`), so the 3 m precise radius is ignored.
  - At `b4d52f8` every AntShipWars follower used the precise checker, because the distance was always 0.
  - Now none do: all followers use `FOLLOWER_INSTANCE_RESIM_CHECKER`, as set by `AdjustFollowerResimPrecision`.
- **Also changed:** `usePreciseResimChecker` is no longer left at a stale value (part of Q7).
- **Open:** intended? If not, compute the distance whenever either threshold is > 0.

---

## Ownership model

Several items depend on how the server tracks ownership now. This describes `4ffb402`.

- **Every registered entity has an owner.** `AddPredictedEntity` makes it server-owned (connection 0, no message sent) unless it already has an owner (`ServerPredictionManager.cs:259-262`).
- **`SetEntityOwner(entity, c)`** (`:172-187`): the private `UnsetOwnership` drops the old owner from both maps, resets the entity and sends `false` to the old owner (`:222-245`). `SetOwnership` then records the new owner, resets again and sends `true` (`:189-207`).
- **`UnsetOwnership(entity, ownerId)`** (`:209-219`): if `ownerId` owns the entity, hands it back to the server and sends `true` to connection 0. This matches `b4d52f8`.
- **No public way to leave a registered entity without an owner.** `RemovePredictedEntity` is the only way out (`:270-303`).
- **Fixed by this:** see [Fixed since earlier reviews](#fixed-since-earlier-reviews) (client-owned entities also listed under the server, the leak on removal, owners set before registration being overwritten, `UnsetOwnership` registering untracked entities).
- **Behaviour changes:**
  - An entity nobody controls runs `ServerOwnedSimulationTick`, which samples the server's own input sources. Unowned entities used to coast on their last input through `ServerSimulationTick`. This is why L4 can no longer happen.
  - New messages to connection 0 and to the invalid id: see B17.
- **Tests:** see T2.

---

## Confirmed bugs

### B2. Client input records are allocated at twice their declared size
- **Where:** `AbstractPredictedEntity.cs:36-40` sums the input counts; `ClientPredictedEntity.cs:127-131` sums them again into the same fields. This has been there since the initial commit.
- **Impact:**
  - `GetFloatInputCount()`/`GetBinaryInputCount()` report 2× on the client.
  - Every input record a client sends is 2× (Mirror serializes whole arrays).
  - The server re-broadcasts that record as the entity's input, so state messages carry 2× input to every client.
  - Values are still correct (components read sequentially; the extra half is zeros).
  - It would also break any future "component wrote exactly the declared count" check (the test `ComponentWritingFewerInputsThanDeclaredIsRejected` expects one).
  - The new server check `PredictionInputRecord.Valid()` (`ServerPredictionManager.cs:353-357`) doesn't compare sizes with the entity's declared counts, so it doesn't catch this.
- **Direction:** delete the loop in the `ClientPredictedEntity` constructor.

### B3. BROADCAST_INPUTS doesn't do what it should (per D2)
- **false still sends input.** `ServerPredictionManager.cs:125` assigns `state.input = entity.GetLastInput()` whatever the flag is.
- **true doesn't always send the input that was used (old H7).**
  - Line 125 makes the reused `serverStateRecord.input` point at the record received from the client.
  - On the next tick, `ServerPredictedEntity.SamplePhysicsState` (`:275-283`) re-samples the server's own input sources **into that object**.
  - If no new client input arrived that tick, line 125 sends the same, now overwritten, object.
  - Followers then load the server's sampled input (usually empty) instead of the owner's. This is the cause behind the AntShipWars "gunner never sees the pilot boost" analysis.
- **Input kept across ownership changes.** `ServerPredictedEntity.Reset` (`:398-409`) keeps `lastAppliedInput`, so after a handover from one client to another, the previous owner's input is reported as the input used until the new owner's first input. The aliasing above happens to mask this. A handover to the server is not affected, because `ServerOwnedSimulationTick` replaces `lastAppliedInput` on its first tick.
- **Direction:** false → `state.input = null`. True → send the input applied this tick (own copy, never re-sample into a received record). Clear `lastAppliedInput` on `Reset`.
- **Tests:** `ServerPredictedEntityTest.TestAppliedInputStaysAttachedWhileNoNewInputArrives`.

### B4. Adding or removing a predicted entity during a tick breaks the tick (per D4)
- **Where:** `ServerPredictionManager.PreSimTick` (`:83-109`) and `PostSimTick` (`:111-152`) iterate `_serverEntityToId` while running component code (`LoadInput`, `ApplyForces`, `SampleInput`, `SampleComponentState`, the entity events).
- **Spawning:** spawning a predicted entity from there goes `NetworkServer.Spawn` → `OnStartServer` → `AddPredictedEntity` (`:247`), which adds to that dictionary. The next loop step throws "collection was modified", and the rest of the server tick (other entities, state sending, `tickId++`) is skipped. The per-entity try/catch added in `eff3bc5` doesn't help: the enumerator throws outside it.
- **Despawning:** removing goes `RemovePredictedEntity` (`:270-303`). Whether removing while iterating throws depends on the .NET version inside Unity; it can't be relied on either way.
- **World-state mode:** in `useServerWorldStateMessage` mode, `_worldStateRecord.Resize` (`:264-267`, `:297-300`) mid-`PostSimTick` also discards the states already gathered that tick.
- **Client:** the same applies to any event listener (`newStateReached`, `onPostResimTick`, ...) that registers/deregisters during a client tick.
- **Direction:** while a tick is running, queue adds and removes and apply them when the tick ends.

### B5. One throwing entity aborts the whole tick
- **Status: partly fixed.**
  - **Server: done in `eff3bc5`,** without reporting. See [R3](#r3-server-exceptions-are-swallowed-silently).
  - **`resimulating`** is now a read-only property, reset at the start of `ClientPreSimTick` and `ClientPostSimTick` (`ClientPredictionManager.cs:579, 659`). It no longer stays true after a throw, but `AfterResimulate` and `resimulation.Dispatch(false)` are still skipped.
  - **Client: open.** There's no per-entity try/catch in:
    - `ClientPreSimTick` (`ClientPredictionManager.cs:577-642`; only the network send is wrapped)
    - `ClientPostSimTick` (`:657-671`)
    - the `Resimulate` loops (`:453-512`)
- **Impact (client):**
  - An exception from any component skips the remaining entities and `tickId++`.
  - A persistent fault freezes the simulation (the earlier rocket freeze was this class of bug).
- **Cost (D3):**
  - A try block that doesn't throw is close to free on Mono and IL2CPP desktop builds.
  - A throw is expensive (stack trace capture), so an entity that throws every tick costs a lot: count consecutive failures and disable the entity after N.
  - Mono won't inline a method that contains a try, so put the try in the per-entity loop body, not in small hot helpers.
  - If you target WebGL, measure, because exceptions work differently there.
  - Scale: about one try per entity per loop, i.e. 3 per tick on the client plus 2 per resim step, and 2 per tick on the server. `PredictionBudgetTracker` can measure before/after.
- **Direction:** the pattern proposed in [R3](#r3-server-exceptions-are-swallowed-silently) (catch per entity, always report, quarantine after N consecutive failures), applied to the client loops, plus `finally` in `Resimulate` so `AfterResimulate` always runs.
- **Tests:** `PredictionManagerInteropTest.ClientTickSurvivesEntityThatThrows` (should still fail), `ServerTickSurvivesEntityThatThrows` (should now pass, by reading).

### B6. Removing an owned entity on the server doesn't tell the client (old H5)
- **Status: partly fixed in `eff3bc5`.** `RemovePredictedEntity` (`ServerPredictionManager.cs:280-285`) now drops the entity from its owner's map and list. It still doesn't send `false` to the owning client.
- **No workaround anymore:** the Mirror binding used to release ownership itself in `OnStopServer`; `97d60c1` removed that call (R2). The AntShipWars adapter never released before deregistering (`NetworkAntPlane.OnDisable`).
- **Impact:**
  - **On despawn (both integrations):** small. The client's own despawn removes the entity from its set of controlled entities, so it goes back to sending heartbeats. Only the stale id stays in `localEntityIds` (D8: ownership survives client deregister/re-register) until `Clear()`, and Mirror doesn't normally reuse net ids within a session.
  - **On deregister and re-register of a live entity:** larger. The server forgets the owner, and `AddPredictedEntity` makes the entity server-owned again, while the client still thinks it owns it. The server rejects the client's input as `POTENTIAL_EXPLOIT_ATTEMPT` and drives the entity itself, so it snaps back for the player until the game calls `SetEntityOwner` again. AntShipWars hits this only if a piloted plane is disabled and re-enabled without being despawned (not checked). This was the same at `b4d52f8`.
- **Direction:** in `RemovePredictedEntity`, send `false` to the owner when it's a client.
- **Tests:** `ServerRemovingOwnedEntityRevokesClientOwnership`: the owner-list assert should now pass; the client asserts still fail.

### B7. INCREMENT_TICK_WHEN_NO_INPUT can never take effect
- **Where:** `ServerPredictedEntity.cs:213-240`. The if-branch runs when not buffering and sets `tickShouldUpdate = false`. The else-branch only runs while buffering, where `!isBuffering && INCREMENT_TICK_WHEN_NO_INPUT` is always false. So the increment at `:299-301` never runs.
- **The comment at `:237-239`** describes the intended behaviour (advance the tick when there's no input). `docs/scripting-api/ServerPredictedEntity.md:20` documents the flag as working.
- **Direction:** implement it in the no-input path, or remove the flag. If implemented, the server could stamp ticks the client hasn't reached; design it together with T1.

### B8. GAP_IN_SERVER_STREAM can never fire
- **Where:** `ClientPredictedEntity.cs:350` sets `lastCheckedServerTickId = serverState.tickId`. Line 359 then tests `serverState.tickId > lastCheckedServerTickId`, which is always false.
- **Tests:** `ClientPredictedEntityTest.GapInServerStreamIsReported`.

### B9. Metrics that are never updated
- `totalResimulationsTriggeredByLocalAuthority`, `...ByFollowers`, `...ByBoth` (`PredictionManager.cs:64-66`): never written.
- `totalDesyncToSnapCount` (`PredictionManager.cs:62`): never written.
- `totalResimulationsDueToBoth`: `totalResimulationDecisions` is a local incremented at most once (`ClientPredictionManager.cs:327, 367-378`), so `> 1` never holds.
- `ServerPredictedEntity.catchupBufferWipes` (`:55`): never written.
- `ClientPredictedEntity.inputUsed` event (`:622`): never dispatched.

### B10. RTT is computed from time 0 when the record is missing (old M2)
- **Where:** `ClientPredictionManager.cs:717-725`. `clientTickRTTBuffer.Remove` (`:719`) returns the empty record (`sentTime = 0`) for a tick that is no longer in the buffer, so the RTT reads as time since startup.
- **When:** the buffer holds `CLIENT_RTT_MEASUREMENTS_BUFFER_SIZE = 20` ticks (`ClientPredictionManager.cs:24`): 166 ms at 120 Hz, including server buffering. Also for any state stamped with a tick the client never sent (T1).
- **Direction:** check `Contains` first.

### B11. Server catch-up is counted as packet loss (old M4)
- **Where:** `ClientPredictionManager.cs:697-708`. During catch-up the server applies several inputs in one tick, so the stamp jumps by more than 1. The skipped ticks go into `missedTicksBuffer` and are never removed. `onPacketLoss` reports loss that didn't happen.

### B12. A CATCHUP desync event is dispatched every tick (old M8)
- **Where:** `ServerPredictedEntity.cs:221-225` dispatches before checking `catchup > 0`.

### B13. Turning on useServerWorldStateMessage after entities registered breaks world-state sending (old M11)
- **Where:** `WorldStateRecord` is only resized in `AddPredictedEntity`/`RemovePredictedEntity` while the flag is on (`ServerPredictionManager.cs:264-267, 297-300`). Otherwise its arrays have length 0 and `WorldStateRecord.Set` (`:33`) throws `IndexOutOfRangeException` in `PostSimTick`.
- **Since `eff3bc5`:** the exception is swallowed per entity (R3), so the server sends an empty world state every tick with nothing logged. Clients get no updates.
- `Validate()` (`:63`) only checks the flag at construction, before integrations set it.
- **AntShipWars:** world-state mode is on by default (`server_use_world_state_packet = true`). The adapter sets the flag in `ApplyConfig`, called from `SetupServer` right after creating the manager and before any plane spawns, so the normal flow isn't affected. Switching the mode on at runtime with planes already registered would trigger it.
- The record resizes on the next add or remove, so the fault ends at the next spawn or despawn.
- **Direction:** resize lazily in `PostSimTick`.

### B14. Rewind ignores its own history length and new bodies (old M1)
- **Rewind not bounded:** `RewindablePhysicsController.Rewind` (`:105`) only refuses a rewind past tick 1, not one longer than `bufferSize`. A longer rewind applies newer states that wrapped into the slots.
- **Zero-filled slots:** `Track` (`:151`) prefills every slot with a zero record (origin, identity, zero velocity). A body tracked less than N ticks ago is moved to the origin when the world is rewound N ticks. Entities are then usually re-snapped to the server state; non-entity tracked bodies are not.
- **Stale fallback:** `ClientPredictedEntity.SnapToServer` (`:463`) falls back to the local history slot without checking that the slot holds that tick.
- **Direction:** store the tick per slot and skip or keep bodies whose slot doesn't match. Refuse rewinds longer than the history.
- **Tests:** `RewindablePhysicsControllerTest.RewindBeyondHistoryIsRefused`, `RewindToBeforeBodyWasTrackedDoesNotMoveIt`; `ClientPredictedEntityTest.SnapToServerDoesNotUseOverwrittenHistorySlot`, `SnapToServerWithoutAnyHistoryForTickDoesNotMoveEntity`.

### B15. Server state history drops input and component state (old M9)
- **Status: partly fixed in `5c2c9a2`.** `PhysicsStateRecord.From` (`PhysicsStateRecord.cs:81-97`) now copies input and component state into the target's own records, and only when the target has them and they're large enough (`Fit`). It no longer points the target's `input` at the source's.
- **Still wrong:** history slots are created with `new PhysicsStateRecord()` (`ServerPredictedEntity.cs:82`), which has neither, so nothing is copied into them. `GetStateAtTick()` (lag compensation) returns positions correctly, but no input and no component state.
- **Direction:** allocate the slots with `PhysicsStateRecord.AllocWithComponentStateAndInput`, or copy with `FromAll` (`PhysicsStateRecord.cs:100-123`, not used anywhere yet).

### B16. Client GetServerTickId() is always 0 in per-entity mode
- **Where:** `reportedServerTickId` is only set in `OnServerWorldStateReceived` (`ClientPredictionManager.cs:678`). The Mirror binding uses per-entity messages by default. Fixed by the first step of [Q8](#q8-tick-model).

### B17. Ownership messages
- **`SetOwnership` send not wrapped:** `SetOwnership` calls the integration's ownership sender (`ServerPredictionManager.cs:205`) outside a try/catch, unlike the release in `UnsetOwnership` (`:234-241`). An exception there propagates to the caller after the ownership maps were already changed. Releasing to the server goes through it too (it sends `true` to connection 0), as it did at `b4d52f8`. The Mirror binding never throws here.
- **Spurious messages (since `4ffb402`):**
  - `false` to connection 0 whenever a server-owned entity is given to a client, which is the usual first grant after registration.
  - `false` to the invalid connection id when `SetEntityOwner` runs before `AddPredictedEntity`.
  - The Mirror binding drops both; other integrations would receive them.
- **Direction:** wrap the send in `SetOwnership`; skip the `false` message when the old owner is the server or invalid.

### B18. Interpolators show the oldest state forever when USE_SMOOTH_BUFFER = false
- **Where:**
  - `MovingAverageInterpolator.cs:228` and `CustomVisualInterpolator.cs:207` treat `time == 0` as a special case.
  - The only place `time` is synced to the buffer is the `fill == 1` branch (`:130-138` / `:106-114`).
- **When:** two states arrive before any `Update` sees one, i.e. two FixedUpdates in the spawn frame. That's normal at 120 Hz / 60 fps.
- **Impact:** `time` stays on a timeline starting at 0 while the buffer's tick times are far larger. The visual shows the oldest buffered state for good, up to `BUFFER_SIZE - 1` (59) ticks behind.
- **The default smoothed mode is not affected**, because its tick ids start at 0.

### B19. Minor buffer correctness
- **Debug-only crash:** `MovingAverageInterpolator.GetBufferEndAngle` guards `< 2` but needs 3 states (`:324`). `MovingAverageInterpolator.Add` (`:299`) calls the **unguarded** `CustomVisualInterpolator.GetBufferEndAngle` anyway. Only reachable with `LOG_POS` on.
- **Late packet evicts newer data:** `TickIndexedBuffer.Add` on a full buffer, given a tick older than its oldest entry, evicts a newer entry and keeps the older one (`TickIndexedBuffer.cs:74-91`). Can happen with a reordered late packet.

---

## Tasks

### T1. Client guard against future-stamped states
- **Status:** not started. `ClientPredictionManager.OnServerStateReceived` has a TODO for it (`:691-692`).
- **Why:** B1's server-side cause is fixed (see [Fixed](#fixed-since-earlier-reviews)), but the client still accepts any stamp. One state stamped ahead of the client's own tick still causes the full B1 failure:
  - It becomes the end of `serverStateBuffer` for good, because the end is the highest tick (`TickIndexedBuffer.cs:84-87`).
  - `GetPredictionDecision` then always returns `SERVER_AHEAD_OF_CLIENT` → NOOP (`ClientPredictedEntity.cs:326, 339-348`).
  - `ClientFollowerSimulationTick` sets `lastAppliedFollowerTick` to that tick, so the follower never snaps or loads input again (`ClientPredictedEntity.cs:215-217`).
  - `lastAckTickId` and `clientLastReceivedTickId` jump to it, which freezes packet-loss and RTT tracking (`ClientPredictionManager.cs:699-714`). The second jump happens even for entities the client doesn't have. The first such state also runs the missed-tick loop once per tick between the two values.
  - Recovery only comes with a `Reset()` of the entity (ownership change, `SIMULATION_FREEZE`) or the client's tick catching up. On a server that has been up for a while that takes minutes.
- **Why a valid stamp is always in the past:** the server stamps each state with a client tick it got from that client (last applied input, or last heartbeat). The client sends both during tick T in `ClientPreSimTick` and only then moves to T+1 (`PredictionManager.cs:190`). So a valid stamp is at most `tickId - 1` when it arrives.
- **Why the client is the right place:** only the client knows its own tick for sure. The server's copy is at least half a round trip old and can't notice a client tick restart until a new message arrives.
- **Why drop instead of wait:** `GetPredictionDecision` already waits for a stamp a tick or two ahead. A far-ahead stamp becomes the buffer's end for good and hides every later state.
- **The guard covers followers too:** every state sent to a connection carries that connection's own client tick, followers included ([Appendix A.1](#a1-dont-followers-run-on-the-server-tick)).
- **Remaining ways to get a future stamp:**
  - `_connIdToLatestTick` keeps a connection's last tick until a newer heartbeat or input replaces it, and is never pruned (P12). If a client's tick restarts while its connection stays up, its states carry the old, higher tick for about one round trip.
  - **AntShipWars session restart (concrete trigger).** On `ServerState.PreppingSession` the adapter's `ServerReset()` calls `Clear()` on the server and, through `RpcReset`, on every client, without disconnecting anyone (`NetworkPredictionManagerAdapter.cs:153-160, 225-237`):
    1. The server clears `_connIdToLatestTick` at once.
    2. A client without a plane (in the lobby, say) keeps sending heartbeats with its old, high tick until `RpcReset` reaches it. Heartbeats that arrive after the server's reset store that old tick again (`OnHeartbeatReceived` accepts any value).
    3. The client restarts at tick 1. Its new heartbeats reach the server about one round trip after the reset.
    4. In between, every state sent to that client carries the old tick. A plane that registers on the client in that window gets a stored server state far in the "future" and is never corrected again until the client's tick catches up, which takes minutes. `clientLastReceivedTickId` also jumps, freezing RTT tracking.
    - Whether planes register within that window depends on when the session spawns them; not checked. A pilot's input doesn't cause this, because the server ignores input for entities it no longer has after `Clear()`.
    - A server-side mitigation, besides the client guard: ignore heartbeats that are far above the connection's current tick right after a reset, or have the client acknowledge the reset before the server accepts its ticks again.
  - AntShipWars also resets the client tick with `PredictionManager.Instance.Clear()` in `GameManager.OnDestroy` (`GameManager.cs:172`). Same effect if that runs while still connected (not checked).
  - A transport that reuses a connection id for a new client.
  - A binding that queues the shared state record and serializes it later would send every client the last connection's stamp ([Appendix A.1](#a1-dont-followers-run-on-the-server-tick)).
- **Task:**
  - Client: in `OnServerStateReceived` (`ClientPredictionManager.cs:686`), drop the state if `stateRecord.tickId >= tickId`, before `BufferServerTick` and before the `lastAckTickId`/`clientLastReceivedTickId` bookkeeping. Strict, no tolerance. Count the drops and dispatch a desync event so the cause stays visible.
  - Server (optional): remove a connection's `_connIdToLatestTick` entry when it disconnects. Needs a disconnect hook from the integration.
- **Caveat:** the guard stops one bad state from poisoning an entity but doesn't restore corrections. If the server keeps stamping ahead, the client drops everything until the server catches up; the counter makes that visible.
- **Tests:** a future-stamped state followed by normal ones: the follower still snaps to the normal ones, and `lastAckTickId` doesn't jump.

### T2. Update the ownership tests to the server-owned-by-default model
- **Status:** in progress. In `4ffb402`, `UnsetOwnershipOnNotRegisteredEntity` and `UnsetOwnershipOnNotOwnerEntity` were removed and `TestOwnershipSetAndUnset` was updated (should pass).
- **Still expecting the old model at `4ffb402`** (traced by hand against the code):

| Test | Fails at | Why |
|---|---|---|
| `SetSameOwnerTwiceIsNoOpOnBothSides` | `AssertOwner(1, -1)` | Entity 1 was never given away, so it's server-owned (0) |
| `SetServerAsOwnerTwiceIsNoOp` | `MessagesTo(0, 0, true) == 1` | Registration makes the entity server-owned without a message, and setting the same owner again is a no-op, so no message is sent |
| `ReleaseOwnershipRepeatedlyStaysReleased` | `AssertOwner(0, -1)` in the last block | The block now calls `UnsetOwnership(entity0, 1)` on a server-owned entity, a no-op; there's no public way to leave a registered entity unowned |
| `MultipleEntitiesOwnedByOneClientReleasedOneByOne(Revoke, -1)` | `AssertOwner(r, -1)` | `ReleaseMode.Revoke` now makes the same call as `ReleaseToServer`, so the entity ends up server-owned. The other two cases should pass |

- **Expected to pass** (traced): `SetOwnerReflectedOnBothServerAndClient`, `SwapEntityOwnership`, `ReleaseOwnershipByNonOwnerIsIgnored`, `SomeEntitiesOfOneClientReassignedToAnotherClient`, `OwnershipPingPongBetweenClientsEndsConsistent`, and the `ReleaseToServer`/`GiveToServer` cases above.
- **Fails for a different reason:** `ServerRemovingOwnedEntityRevokesClientOwnership` (B6; the client asserts).
- **Direction:** expect owner 0 wherever the tests expect -1 for a registered entity, and drop `ReleaseMode.Revoke` together with its comment ("`UnsetOwnership(entity)`: entity is left without an owner"). Decide whether registration should notify connection 0; if not, change `SetServerAsOwnerTwiceIsNoOp` to expect no message.

### T3. Run the test suite
- Nothing in this review was run. Run at least:
  - `PredictionManagerServerTest` (the B1 expectations, `TestOwnershipSetAndUnset`)
  - `PredictionManagerInteropTest` (ownership, T2; `ServerTickSurvivesEntityThatThrows`, B5)
  - the `FOLLOWER SNAPPING` regions in `ClientPredictedEntityTest` and `PredictionManagerFollowerTest` (Q1)
  - the distance tests (`FollowerResimulationUsesDistanceToLocalEntities`) and the follower tests in C3.
- Running them needs Unity in batch mode on AntShipWars_URP, so the editor must be closed.

---

## Decision needed

### Q1. Snapping followers without input components (resolved)
- **Fixed in `5c2c9a2`:** `ClientFollowerSimulationTick` (`ClientPredictedEntity.cs:206-258`).
  - Every follower that isn't predicted now snaps once per newer server state, whether it has input components or not.
  - Only loading the server's input stays limited to controllable followers (`:230`).
  - The condition is `!predictAsFollower`, so all three cases below are covered. That settles the D1 question (global flag or per-entity state) in favour of the per-entity state.
- **Tests:** `#region FOLLOWER SNAPPING` in `ClientPredictedEntityTest` and `PredictionManagerFollowerTest`, each for a controllable and a non-controllable follower. Not run yet (T3).
- **Ursitoare-Mirror README:** `:363` now describes the behaviour correctly, but names `PredictionManager.PREDICT_FOLLOWERS` (C1).
- **Background:** a follower is not predicted (`predictAsFollower = false`, set in `ConfigureFollowerResimulation`, `ClientPredictionManager.cs:297-317`) in three cases, and in all three its resim requests are ignored (`:271-274`, `:341-345`):
  - **a.** `PREDICT_FOLLOWERS = false`
  - **b.** the client has no local entity (spectator, or between owned entities), regardless of `PREDICT_FOLLOWERS`
  - **c.** it's farther than `RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD` from every local entity (only when the threshold is > 0)
- **Since the distance fix:** case **c** is reachable for the first time. With a follower threshold > 0, far followers snap to each new server state instead of being predicted.

### Q2. Which tick a connection's states carry (old H4)
- **Background:** every state the server sends to a client carries one tick: the last client tick the server applied for that connection (`_connIdToLatestTick`). The client files the state under that tick and compares it with its own history at that tick.
- **Problem:** `PreSimTick` writes that number once per owned entity, and the last write wins (`ServerPredictionManager.cs:94, 98, 327`).
- **Example:** a client flies a ship (applied up to tick 1000) and takes a rocket that is still buffering (tick 0). If the rocket is processed after the ship, every state sent to that client is stamped 0, the ship's included, and the client drops them as duplicates.
  - This lasts while the rocket buffers (about 3 ticks plus the round trip).
  - If the client stops sending the rocket's input entirely (e.g. destroyed it locally while the server keeps it owned, see D8), the stamp stays stuck. Every entity on that client then stops being corrected, and after the local history fills up, `SIMULATION_FREEZE` snaps the whole world back periodically.
- **Proposal:** stamp with the highest tick among owned entities that actually applied an input on this server tick; leave it unchanged when none did. Not "the highest ever", because that breaks when a client's tick counter restarts (T1). This is also the second recommendation in [Q8](#q8-tick-model).
- **Open:** does that match what you expect?
- **Tests:** `StateTickSentToClientDoesNotRegressWhenSwitchingOwnedEntity`, `...WhenOwningSecondEntity`.

### Q3. Catch-up threshold
- **Where:** `ticksPerCatchupSection = floor(bufferSize / CATCHUP_SECTIONS) + 1` (`ServerPredictedEntity.cs:74`), and catch-up runs `floor(queueFill / ticksPerCatchupSection)` extra inputs per tick (`:263-268`). With the library and Mirror-binding defaults (50 / 3) catch-up starts at 17 queued inputs. AntShipWars (50 / 10) starts at 6, against a target of 5.
- **Why it matters:** the server applies exactly one input per tick and the client sends one per tick, so the queue never drains by itself; only catch-up shrinks it.
  - Every late burst raises the queue for good. After a 100 ms hiccup at 120 Hz, 12 delayed inputs arrive together and the queue settles around 15.
  - It can sit anywhere up to 16 queued inputs (133 ms at 120 Hz) instead of the 3 you buffer for. That delays the client for everyone else, delays its corrections, and lengthens rewinds.
  - This is the same kind of delay D6 avoids by buffering only once.
  - The ceiling depends on `bufferSize`, the history length, so changing the history silently changes the latency ceiling.
  - This assumes client and server tick at the same rate.
- **Direction if you want it changed:** start catch-up relative to `BUFFER_FULL_THRESHOLD` (e.g. fill > target + N).
- **Open:** is up to 16 ticks acceptable?

### Q4. Oversimulation protection and blocked resims (config conflicts #8–#10)
- **#8:** `CanResiumlate` (`ClientPredictionManager.cs:570-575`) uses either `minTicksBetweenResims` or `maxTickResimulationCount`. The defaults (`protectFromOversimulation = true`, interval mode, `minTicksBetweenResims = 0`) make protection a no-op that reports itself as on.
- **#9:** a blocked resim becomes NOOP, not SNAP (`:391-399`), so entities that asked to snap that tick aren't. The library's own deciders never return SNAP. `DO_RESIM` was removed in `5c2c9a2`, so that part of #9 is gone.
- **#10:** "use protectFromOversimulation or TRUST_ALREADY_RESIMULATED_TICKS, not both" (`PredictionManager.cs:52`) isn't enforced.
- **Open:** which of these do you want changed?

### Q5. Interpolator cursor has no cushion after the first starvation
- **Where:** `MovingAverageInterpolator.cs:122-128`, `CustomVisualInterpolator.cs:98-104`. When the cursor passes the newest state, it's set to that state's time and then `deltaTime` is added. From then on it hugs the newest state, so any frame without a new tick holds and then jumps.
- **Same pattern** as the AntShipWars memory note "visual interpolator overrun".
- **Open:** intended, or should the interpolator keep a target delay (clamp without the extra `deltaTime`, a larger start margin, or drift-based time scaling)?

### Q6. Spoofed client input only logs a warning
- **Where:** `ServerPredictionManager.cs:362-367`. Off by default (`LOG_ERRORS = false`), no event.
- `eff3bc5` added a counter for malformed records (`invalidClientStatesReceived`, `:353-357`), but input for an entity the sender doesn't own is still only logged.
- **Open:** do you want an event or a counter so the integration can react (kick, count)?

### Q7. Zero means opposite things for the two distance thresholds
- **Where:** `ClientPredictionManager.cs:307-316`.
  - `RESIMULATE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0` means "every follower is near", and since `5c2c9a2` also "never use the precise checker" (R4).
  - `RESIMULATE_PRECISE_FOLLOWERS_SQR_DISTANCE_THRESHOLD = 0` means "never use the precise checker".
- The other half of the old item (`usePreciseResimChecker` left at a stale value) is fixed.
- **Open:** pick one meaning for 0? Decide together with R4.

### Q8. Tick model
- **Background** ([Appendix A](#appendix-a-tick-model-discussion)): Ursitoare stamps states the Quake/Source way ("the last input of yours I applied") but consumes inputs the way shared-timeline engines do (buffered, one per tick, last input reused when missing). It pays the shared model's costs without its main benefit: one timeline for everything it rewinds. Most of the tick bugs above (Q2, T1, B11, B16, the stall behaviour) come from that mix.
- **Recommendation:** keep the current model for now and fix its weak points. Those are also the first steps toward a shared timeline, if that's ever wanted.
  - **Send the server tick with every state**, per-entity mode included (fixes B16). Prediction checks keep the first state per label; unpredicted followers and spectators snap to the newest by server tick. Removes the need for `ALLOW_SERVER_HISTORY_REWRITES`.
  - **Track the label per connection and advance it only on ticks where an input was applied** (the Q2 proposal). Fuller fix: one input queue per connection, so all of a client's entities apply the same tick together.
  - **T1.**
- **Revisit a shared timeline** if players start owning several entities at once (ship plus rockets or drones), or if input stalls turn out to be common.
- **Open:** do you agree with keeping the current model plus these three fixes?

---

## Latent

### L1. Follower comparisons have no history-window check
- **What:** only the local entity checks that the server tick is still inside its history (`ClientPredictedEntity.cs:367, 389`). If a predicted follower stopped getting states while staying registered, after `bufferSize` ticks it would compare against an overwritten slot and ask for a resim from its last server tick.
  - The rewind distance would then grow by one every tick, with no cap (B14).
  - The local entity's inputs would be replayed from overwritten slots.
- **Why latent:** per D5, the server only stops sending on deregistration, which reaches the client quickly. A lag spike long enough to matter also stalls the local entity, whose `SIMULATION_FREEZE` resets everything first.

### L2. The resim check doesn't verify the history slot's tick (old M10)
- **Where:** `_defaultResimulationEligibilityCheck` (`ClientPredictedEntity.cs:427`).
- **Impact:** small. Followers also sample every tick, so slots are current. It only matters for ticks older than the history, or right after registration (see L3).

### L3. Each spawned predicted follower costs one full-world resimulation
- **What:** a new follower's local history is prefilled with zero records (`ClientPredictedEntity.cs:136`). Its first server state is stamped with a client tick from before it spawned on the client (about one round trip earlier). That comparison fails, so every client with a local entity resimulates the whole world once per spawn.
- **Impact:** CPU only. Matters with frequent predicted spawns (projectiles). Derived from reading, not measured.

### L5. ServerPredictedEntity.Reset doesn't reset lastInputLoadedTick (old M5)
- **Where:** `ServerPredictedEntity.cs:170, 180, 398-409`. After an ownership change, `ValidateInput` gets a delta computed from the previous owner's ticks (can be negative).
- **Why latent:** every game `ValidateInput` currently returns true.

### L6. ClientPredictionManager.Clear() leaves isControlledLocally set on entity objects
- **Impact:** only if something reads the flag on an entity after `Clear()`. Re-registering resets it.

### L7. Whole-world correction snaps ignored followers (config #11)
- **Where:** `Resimulate` (`ClientPredictionManager.cs:462`) doesn't consult `ShouldIgnoreResimulationDecision`. Under the current model this is arguably what you want.

---

## Performance

| ID | What | Where |
|---|---|---|
| P1 | The default interpolator allocates about 12 objects per entity per tick: 2 records in `Add`, 10 `float[4]` in `QuaternionAverage.PowerIteration`. At 50 entities and 120 Hz that's about 72k allocations/s. | `MovingAverageInterpolator.cs:292, 371`; `QuaternionAverage.cs:66` |
| P2 | Received records are kept by reference, so the integration must allocate per message (Mirror deserializes new records per entity, per tick, per client). Copy into preallocated slots instead. | `ClientPredictedEntity.cs:402`; `ServerPredictedEntity.cs:349` |
| P3 | `IEnumerable<int>` connection iteration boxes an enumerator once per entity per tick (per-entity send mode). | `ServerPredictionManager.cs:377, 405` |
| P4 | `TickIndexedBuffer` scans all keys on every eviction. With the bounded `tickResimCounter`s this now runs on every resim step for every entity once full. | `TickIndexedBuffer.cs:180-204` |
| P5 | `GetPredictionDecision` runs again in `Snap()` and in `Resimulate` (when `correctWholeWorldWhenResimulating` is false), re-dispatching its events and stats. | `ClientPredictionManager.cs:462, 544` |
| P6 | The resim check re-runs every tick against the same server state until a new one arrives. | `ClientPredictedEntity.cs:317-373` |
| P7 | Bodies are sampled twice per resim step: by the physics controller and by each entity. | `RewindablePhysicsController.cs:126-134`; `ClientPredictedEntity.cs:545` |
| P8 | Per-tick loops iterate dictionaries 3–5 times per tick plus 2 per resim step; a dense array would be faster. | both managers |
| P9 | Finalizers on both entity types (an extra GC cycle each). | `ClientPredictedEntity.cs:103`; `ServerPredictedEntity.cs:60` |
| P10 | `Track` reallocates 60–120 records each time, even for a body already tracked (pooled entities). | `RewindablePhysicsController.cs:145-153` |
| P11 | `rotDiff.eulerAngles` is computed three times per entity per frame. | `PredictedEntityVisuals.cs:136-138` |
| P12 | Per-connection maps are never pruned for disconnected connections. | `ServerPredictionManager.cs:18-19` |

---

## Cleanup

### C1. Docs out of date
- **Moved or removed members.** `5c2c9a2` moved the client flags (`PREDICTION_ENABLED`, `PREDICT_FOLLOWERS`, `DO_SNAP`, the distance thresholds, packet-loss and RTT settings), the client events and `GetServerTickDelay` to `ClientPredictionManager`, moved `onServerStateSendError` to `ServerPredictionManager`, and removed `DO_RESIM` and `TRACK_TIMING_STATS`. Still documented on `PredictionManager`:
  - `docs/manual/prediction-manager.md:134, 158-162`, including the removed `IGNORE_NON_AUTH_RESIM_DECISIONS` / `IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS`.
  - `docs/scripting-api/PredictionManager.md:15-19, 26, 73-79, 259-262, 279-282, 330, 344`. There are no `ClientPredictionManager` or `ServerPredictionManager` pages.
  - `Ursitoare-Mirror/README.md:363` (`PredictionManager.PREDICT_FOLLOWERS`) and `:388` (`DO_RESIM`; the "raw prediction" option no longer exists).
- `docs/scripting-api/SimplePhysicsControllerKinematic.md` documents a class that was removed.
- `docs/scripting-api/ServerPredictedEntity.md:15, 17, 20` documents `APPLY_FORCES_TO_EACH_CATCHUP_INPUT`, `BUFFER_ONCE` and `INCREMENT_TICK_WHEN_NO_INPUT` as working.
- Ownership docs, wherever they describe `UnsetOwnership(entity)` or unowned entities, need the [new model](#ownership-model).
- `Ursitoare-Mirror/Runtime/Ursitoare/Mirror/AbstractPredictedNetworkBehaviour.cs`, `OnStopServer`: the comment "RemovePredictedEntity doesn't release ownership, so release it first ... and tell the owning client" no longer matches the code (`97d60c1` removed the release).
- `docs/configuration-conflicts.md`:
  - #1 and #2 are resolved (flags removed).
  - #5 is fixed.
  - #4 is overstated and #6 is wrong (see [Prior claims](#prior-claims-that-were-wrong-or-changed)).
  - #9's `DO_RESIM` part is gone (flag removed).
  - #12's second flag is fully dead.

### C2. Dead flags and code
- `APPLY_FORCES_TO_EACH_CATCHUP_INPUT` is never read.
- `BUFFER_ONCE` is never read (buffering once is intended, D6, so the flag can go).
- `INCREMENT_TICK_WHEN_NO_INPUT`: see B7.
- `APPLY_OLD_INPUTS_IN_CURRENT_TICK` is dead while `IGNORE_OLD_INPUT` is true.
- Unused in `ServerPredictedEntity`: `SnapToLatest()`/`totalSnapAheadCounter`, `noInputAvailableForTick`, `allInputsBehindTickId`, `inputsBehindBy`, `tickUpdatedFromQueue`.
- `RewindablePhysicsController.notSubjectToResim` is never applied.
- `ClientPredictedEntity` constructor `?? Array.Empty` (`:121-122`) is dead: the base constructor already dereferences the arrays.
- `PhysicsStateRecord.Fit` and `FromAll` are unused (see B15 for a use of `FromAll`).
- `PredictionInputRecord(PredictionInputRecord other)` looks like a copy constructor but only allocates arrays of the same size; it copies no values or fill counts.
- `PredictionInputRecord.Valid()` accepts negative fill counts and any array size (see B2).

### C3. Tests
- `PredictionManagerFollowerTest`: the fields `savedIgnoreControllableFollowerDecisions` / `savedIgnoreNonAuthResimDecisions` (`:26-27`) and the `TestIgnoredControllableFollower...` names and comments describe the removed `IGNORE_*` flags.
  - `TestIgnoredControllableFollowerIsSnappedInstead` and `...ConvergesToServerWithoutLocalResimulation` should pass (since `0fcef54`).
  - The distance tests should pass with the distance fix.
- `PredictionManagerServerTest.TestOwnershipSetAndUnset` calls `GetEntity(1)` expecting the entity owned by connection 1. It passes only because that entity's id is also 1.
- Ownership tests: T2. The `ReleaseMode.Revoke` comment in `PredictionManagerInteropTest` describes the removed behaviour.
- No manager-level test covers prediction being off (R1).

---

## Fixed since earlier reviews

| Earlier ID | Item | Now |
|---|---|---|
| B1 / M3 | Server stamped states with its own tick before hearing from a connection, so one future-stamped state stopped all correction of an entity | Fixed in `5c2c9a2`: `GetLatestAppliedTickForConnection` returns 0 for an unknown connection (`ServerPredictionManager.cs:336-341`). The client starts at tick 1, so a 0 stamp never counts as new: followers don't snap to it, `lastAckTickId`/`clientLastReceivedTickId` don't move, and a spectator ignores its followers' resim requests anyway. `PredictionManagerServerTest.CheckUpdates_WithClientInput_TicksDoesNotIncreaseWithoutClientInput_ExceptForServerOwnedObjects` updated. Client hardening is T1 |
| Q1 / D1 | Followers without input components never snapped | Fixed in `5c2c9a2`, with tests |
| H1 / config #5 | Follower distance always 0 | Fixed in `5c2c9a2` (side effect: R4) |
| L4 (2026-10-09) | Unowned entities allocate an input record every tick | No longer reachable: every registered entity has an owner |
| R1 (new in `5c2c9a2`) | Turning prediction off froze the client | Fixed in `4ffb402`; a manager-level test is still missing (C3) |
| R2 (new in `4ffb402`) | Ursitoare-Mirror called the now-private `UnsetOwnership(entity)` | Fixed in Ursitoare-Mirror `97d60c1` (call removed; side effect on B6) |
| (new in `eff3bc5`) | Entities given to a client stayed listed under the server; removing them while client-owned leaked them there | Fixed in `4ffb402` |
| (new in `eff3bc5`) | `AddPredictedEntity` overwrote an owner set before registration | Fixed in `4ffb402` |
| (new in `eff3bc5`) | `UnsetOwnership(entity)` on an untracked entity registered it as server-owned | Fixed in `4ffb402` (the method is now private) |
| (new in `eff3bc5`) | `using NUnit.Framework.Constraints;` in runtime code | Removed in `4ffb402` |
| (new in `5c2c9a2`) | Ursitoare-Mirror used `onPacketLoss` on the base class after the client events moved | Fixed in Ursitoare-Mirror (no longer referenced). AntShipWars was updated to the moved flags and events too |
| H3 | `tickResimCounter` unbounded (manager and entity) | Both are bounded `TickIndexedBuffer`s |
| H6, L7 (flags) | Logging and debug flags on by default | All off by default |
| L3 (eviction) | `TickIndexedBuffer` evicting on overwrite | Fixed |
| L5 | `SimplePhysicsControllerKinematic` crash | Class removed (its doc page remains, C1) |
| L6 | `WrapperHelpers` two-argument overload | Rewritten |
| L8 | Library declaring the game's namespace | `Sector0.Ursitoare.*` |
| Config #1, #2 | `IGNORE_*` flags vs `predictAsFollower` | Flags removed; `predictAsFollower` is the single answer |
| Memory (rocket freeze) | Client `RemovePredictedEntity` removed before unsetting local control | Fixed; ownership id kept per D8 |
| Memory | Ownership grant before client registration dropped | Fixed |
| Memory | Legacy `PREDICTION_ENABLED` read | Moot: only one flag exists, now on `ClientPredictionManager` |

## Prior claims that were wrong or changed

| Earlier claim | Correction |
|---|---|
| Config #6: `INCREMENT_TICK_WHEN_NO_INPUT` × `IGNORE_OLD_INPUT` causes guaranteed mispredictions | The flag can never take effect (B7), so the conflict doesn't exist |
| Config #12: `APPLY_OLD_INPUTS_IN_CURRENT_TICK` "can almost never do anything" | It can never do anything while `IGNORE_OLD_INPUT` is true |
| Config #4: the server sends its own re-sampled input instead of the client's | Only on ticks without new client input; otherwise the client's input is sent. H7 (now B3) was the accurate description |
| H4 severity High | Usually a brief stall after an ownership change; severe only when a client owns an entity it stops sending input for (Q2) |
| M3 "freezes RTT and packet-loss tracking" | Worse: it also stopped all correction of the affected entities (B1, now T1) |
| Follower tests "fail today" | Expected to pass now (C3) |
| `GetBufferEndAngle` NRE | Debug-only now (B19) |
| `TestOwnershipSetAndUnset` stale | Stale semantics, but passes by coincidence (C3) |
| Visuals not told about ownership changes | True, but both window sizes are the same, so no effect today |
| B13 "aborts every server tick" (2026-10-09) | Since `eff3bc5` it silently sends empty world states instead (R3), and it ends at the next spawn or despawn |
| R2 still open (2026-10-10, in the "critical bugs" answer) | Already fixed by Ursitoare-Mirror `97d60c1`; the binding wasn't re-checked before that answer |

---

## Appendix A: Tick model discussion

From the 2026-10-09 discussion. Its first two questions (was B1 fixed, should the client guard against future stamps) are now B1 in [Fixed](#fixed-since-earlier-reviews) and [T1](#t1-client-guard-against-future-stamped-states). Line numbers are updated to the current tree.

### A.1 Don't followers run on the server tick?

**No.** Every state the server sends to a connection carries that connection's own client tick, followers included.

- The server first samples each entity with its owner's tick (`ServerPredictedEntity.SamplePhysicsState` → `PopulatePhysicsStateRecord(GetClientTickId())`).
- `SendServerState` then overwrites it per receiving connection: `stateRecord.tickId = GetLatestAppliedTickForConnection(connId)` (`ServerPredictionManager.cs:380-383`). World-state mode does the same (`:409-412`), and the client copies the message's tick onto every entity (`ClientPredictionManager.cs:681`).
- The Mirror binding sends the record through a TargetRpc, which serializes at call time, so each client gets its own stamp.
- **Why follower checks depend on this:** the client compares a follower's server state with its own prediction at the same tick (`_defaultFollowerResimulationEligibilityCheck` reads `clientStates.Get(tickId)`) and rewinds the whole world to that tick. A server-tick stamp would point into the wrong history slot.
- **Where the server tick does appear:**
  - `WorldStateRecord.serverTickId`, world-state mode only, reported through `GetServerTickId()` and never used for buffering (always 0 in per-entity mode, B16).
  - Server-owned entities record the server tick (`MarkLatestAppliedTickId(tickId, entity)`, `ServerPredictionManager.cs:94`) under the server's own connection id (0). The Mirror binding never sends to it (`GetNetConn` returns null for 0), and host mode doesn't run a client manager.
- **Note:** the server reuses one record and changes its tick per connection. A binding that queued the record and serialized it later would send every client the last connection's tick. The T1 guard would catch the cases where that's ahead.

### A.2 Is it a good idea for the server to use client ticks?

**Yes for a player driving one entity; it's stretched past that, and most tick bugs in this review come from the stretch.**

- **What it gets right:** "the last input of yours I applied" is the Quake/Source model. The client knows exactly which prediction to compare against, and no clock sync is needed.
- **Where it breaks down** (the label only means something on ticks where that client's input was applied; it's tracked per entity but sent per connection):
  1. **Several owned entities share one label.** Each server entity has its own queue, buffering, catch-up and reset; the connection gets one stamp and the last entity processed decides it (Q2).
  2. **Input stalls repeat the label.** A late input leaves the entity's tick unchanged while the server keeps simulating (`ServerPredictedEntity.cs:160-167`). With `ALLOW_SERVER_HISTORY_REWRITES = false` (default) the client keeps the first state and drops the rest: correct match, but no corrections until inputs resume. With rewrites on, a later state is filed under an older tick and compared with the wrong prediction. AntShipWars sets this flag from config (value not checked).
  3. **Catch-up mislabels states.** Several inputs are loaded back to back and forces applied once (`APPLY_FORCES_TO_EACH_CATCHUP_INPUT` is never read). The label jumps by k while the world moved one step. B11 is the same jump seen from the client.
  4. **Followers and spectators depend on it.** Follower corrections pause whenever this client's input stream stalls; a spectator needs a heartbeat round trip before it gets a usable label (where B1 came from). R1 was the extreme case: with prediction off, the client sent neither input nor heartbeat, so the label never moved again.
  5. **The server's copy of a client's tick goes stale** when either side resets (ownership change → 0; client `Clear()` → 1, T1).
- **Recommendation:** see [Q8](#q8-tick-model).

### A.3 Is this approach inferior to a shared timeline?

**For what Ursitoare does, yes; as a general design, no.**

- **Where the ack model is right:** the client predicts only its own entity and shows everyone else interpolated in the past. The only question a state must answer is "which of my inputs does this include?". Simpler, no clock sync, works from the first packet.
- **Why it fits Ursitoare worse:** Ursitoare rewinds and resimulates the whole physics world, followers included, with other players' inputs. That's rollback netcode, and the engines built around it (Rocket League, Overwatch, Netcode for Entities, Photon Fusion, GGPO-style fighting games) use one shared tick, because everything resimulated together needs one label. In Ursitoare the server relabels other clients' states and inputs onto yours, and that conversion is where the edge cases come from.
- **What the ack model still does better:** no clock-offset estimation or time dilation; the server applies inputs as they arrive; the client's tick rate is never adjusted; the current code and tests are built around it.
- **Migration path (each step can ship on its own):**
  1. Server tick on every state (also the first fix in Q8).
  2. The client estimates the offset between its tick and the server's from those stamps.
  3. Inputs are tagged with the server tick they're meant for.
  4. Time dilation replaces catch-up.

### A.4 How do those games achieve a shared timeline?

**The server's tick is the only clock. Each client estimates it and runs slightly ahead, so its input for tick S reaches the server just before the server simulates S. A feedback loop keeps the lead right.**

1. **The server tick is the clock.** Every snapshot carries the server tick it was taken at.
2. **The client estimates the server's current tick and runs ahead of it.** Estimate = last snapshot's tick + time since it arrived + half the round trip. The client predicts at that estimate plus a slack of 1-2 ticks: about half a round trip plus slack ahead of the server's present, a full round trip plus slack ahead of its newest snapshot. On connect it jumps to the estimate.
3. **Inputs are tagged with the server tick they're for.** The server applies input S at S. If it hasn't arrived, the server repeats the last input and the client is corrected later.
4. **The server reports how early inputs arrive** (Netcode for Entities: command age, how long a command sat in the server's buffer).
5. **The client changes its speed, not its tick.** It scales its delta time within a set range (Netcode for Entities' default minimum is 0.9×) to push command age toward the target slack. Each tick keeps the same fixed physics step; only how often ticks run in real time changes. Overwatch reportedly speeds clients up on packet loss to build a bigger buffer.
6. **Inputs are sent redundantly**: every packet carries all inputs not yet acknowledged.
7. **Large errors snap**: after a hitch or a big latency change, the client jumps its tick.

- **Result:** tick S means the same moment for every entity on every client. A snapshot for S is compared with history at S, rolled back to S and resimulated, for all entities at once. Spectators just render snapshots slightly in the past.
- **GGPO (peer to peer):** both sides start at frame 0 after a handshake and tag inputs with frame numbers; when one side gets too far ahead, it waits a frame.
- **What it would take in Ursitoare** (fixed ticks, a server input buffer with a target depth, RTT measurement and whole-world rewind already exist):
  - Server tick on every state; input queues keyed by server tick.
  - A command-age field in server messages and a client-side lead controller; server catch-up goes away.
  - Variable-rate ticking: the Mirror adapter ticks in `FixedUpdate` at a fixed real-time rate, so the client would drive `Tick` from its own accumulator, keeping the physics step per tick fixed.
  - The per-connection stamp, spectator heartbeats and stamp special cases go away.

### A.5 Why is that superior to the Quake/Source model?

**It isn't across the board. It wins when what the client predicts depends on when different entities' inputs take effect relative to each other.**

- **Core difference:** in Quake/Source each player's commands have their own numbering and the server runs a command as soon as it arrives. The shared model fixes when each input takes effect: input S runs at server tick S, the tick the client predicted it at.
- **Why Source doesn't need that:** the client predicts only its own player; others are interpolated about 100 ms in the past; interactions are settled by the server, and shots use lag compensation. Your movement doesn't depend on when anyone else's commands ran.
- **Where it falls apart:** a predicted ship hitting a predicted ball, or two ships colliding. The client needs both bodies at the same moment and must step them together, and the server must apply your input at the tick you predicted. If jitter shifts it one tick, the contact happens on a different step and the client mispredicts. The shared model guarantees one label for every entity and a known tick for every input, which also enables predicting several entities per player plus server-owned ones, extrapolating others' inputs, and deterministic resimulation.
- **Where Source is better:** simpler; tolerates late inputs (a late command still runs, so your prediction stays right, whereas in the shared model a missed input is replaced by a guess and you get a correction); cheap (one predicted entity); no deliberate buffering delay.
- **Where Ursitoare sits:** it already behaves like the shared model in how it uses inputs (buffered, one per tick, last input reused when missing, catch-up). So it pays the shared model's costs but labels states the Source way, without the shared model's main benefit. Moving the labels to the server tick is the valuable part of a migration; the extra cost would mostly be the clock estimate and the speed adjustment.

### A.6 Pushback: the shared timeline won't line up well enough for same-tick inputs to really run on the same tick

**The timelines never line up exactly, and inputs do sometimes miss their tick. But two kinds of alignment get mixed together, and the shared model only promises one.**

- **1. Each input runs on the tick it's labelled with: exact.** The client picks the label; the server applies it at S, never S±1. If it arrives too late, it isn't applied at all: the server reuses the previous input, the snapshot for S shows that guess, and the client corrects. Late inputs aren't applied at the next tick instead (Ursitoare's `APPLY_OLD_INPUTS_IN_CURRENT_TICK` would), because that breaks the guarantee.
- **2. Inputs made at the same real-world moment get the same tick: never, in any model.** Example at 60 Hz with a slack of 2 ticks:
  - Client A has a 40 ms round trip and runs about 3 ticks ahead; client B has 120 ms and runs about 6 ahead.
  - When the server is at 1000, A is at 1003 and B at 1006. If both fire at that moment, A's shot is 1003 and B's is 1006.
  - In Source, A's shot also lands first, because it arrives 40 ms earlier. The higher-ping player is later in both models; only trusting client clocks could change that.
- **Rollback only needs the first kind:** client and server must agree on which inputs ran at which tick, not on when buttons were pressed. A miss is recorded in the snapshot and corrected, so both sides stay consistent.
- **The concern does apply to how often inputs miss.** That depends on whether the slack covers network jitter. If spikes regularly exceed it, players see a steady stream of corrections. The shared model fights this with the slack, redundant inputs and speeding the client up after misses, but it trades the occasional miss for a defined tick; Source never misses because it applies late inputs late.
- **It's worse in Ursitoare today:** client input T lands on server tick T plus an offset set by when buffering finished. The offset differs between clients and between entities of the same client, and shifts after every stall or catch-up, so there's no notion of "the same tick" across clients. Inputs also miss (the server reuses the last input when the buffer runs dry), but the label then stops advancing instead of recording what happened.
- **Summary:** the shared model doesn't make players' inputs simultaneous. It makes the tick an input runs on something both sides agree on in advance, instead of something the server decides on arrival and reports afterwards.

### Sources

- [Netcode for Entities: Time synchronization](https://docs.unity3d.com/Packages/com.unity.netcode@1.5/manual/time-synchronization.html)
- [Netcode for Entities: NetworkTimeSystem API](https://docs.unity3d.com/Packages/com.unity.netcode@1.5/api/Unity.NetCode.NetworkTimeSystem.html)
- [Netcode for Entities: ClientTickRate](https://docs.unity3d.com/Packages/com.unity.netcode@1.0/api/Unity.NetCode.ClientTickRate.html)
- [Netcode for Entities: Introduction to prediction](https://docs.unity3d.com/Packages/com.unity.netcode@1.4/manual/intro-to-prediction.html)
- [GDC Vault: "Overwatch" Gameplay Architecture and Netcode](https://gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and)
- [Edgegap: Overwatch netcode deep dive](https://edgegap.com/blog/game-backend-deep-dive-overwatch-2016-netcode-architecture-rollback)
- [GameDev.net: Command frames and tick synchronization](https://gamedev.net/forums/topic/696756-command-frames-and-tick-synchronization)
- [GameDev.net: Rollbacks and simulation replay performance](https://gamedev.net/forums/topic/713082-rollbacks-and-simulation-replay-performance)
- [Game Developer: The rocket science behind Rocket League's physics](https://gamedeveloper.com/design/video-the-rocket-science-behind-i-rocket-league-s-i-physics)
