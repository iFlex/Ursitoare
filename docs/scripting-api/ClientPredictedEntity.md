# ClientPredictedEntity

**Namespace:** `Prediction`
**Inherits:** `AbstractPredictedEntity`

Client-side representation of a predicted entity. Maintains input and state history. Compares against server snapshots to determine whether to resimulate.

---

## Static Configuration

| Type | Name | Default | Description |
|---|---|---|---|
| `bool` | `DEBUG` | `false` | Verbose logging for this entity. |
| `bool` | `LOG_ADDED_SERVER_STATES` | `true` | Log each server state as it is buffered. |
| `bool` | `LOG_USED_INPUTS` | `false` | Log each input record as it is used. |
| `bool` | `TRUST_ALREADY_RESIMULATED_TICKS` | `false` | Skip resimulation checks for ticks already resimulated. |
| `bool` | `APPLY_SERVER_INPUT_TO_FOLLOWERS` | `true` | Apply server-supplied input when simulating follower entities. |
| `bool` | `LOG_VELOCITIES` | `false` | Log velocity data for the locally controlled entity. |
| `bool` | `LOG_VELOCITIES_ALL` | `false` | Log velocity data for all entities. |
| `bool` | `TRACK_RESIM_DISCREPANCIES` | `true` | Track differences between resimulation passes. |
| `bool` | `ALLOW_SERVER_HISTORY_REWRITES` | `false` | Allow later server states to overwrite already-stored ticks. |
| `bool` | `LOG_RESIMULATION_STEPS` | `true` | Log state after each resimulation step. |

---

## Properties

| Type | Name | Description |
|---|---|---|
| `GameObject` | `gameObject` | The GameObject this entity is attached to. |
| `bool` | `isControlledLocally` | True if this is the locally controlled entity. Read-only. |
| `bool` | `predictionDisabled` | If true, `GetPredictionDecision` always returns `NOOP`. |
| `RingBuffer<PredictionInputRecord>` | `localInputBuffer` | Ring buffer of past input records. |
| `RingBuffer<PhysicsStateRecord>` | `localStateBuffer` | Ring buffer of past local physics states. |
| `TickIndexedBuffer<PhysicsStateRecord>` | `serverStateBuffer` | Buffer of physics states received from the server. |
| `uint` | `lastCheckedServerTickId` | Tick ID of the last server state checked for desync. |
| `uint` | `lastTick` | Last tick processed as local authority. |
| `uint` | `lastSvTickId` | Last server tick ID added to `serverStateBuffer`. |

---

## Diagnostic Counters

| Name | Description |
|---|---|
| `totalTicks` | Total ticks processed. |
| `ticksAsFollower` | Ticks simulated as a follower. |
| `ticksAsLocalAuthority` | Ticks simulated as local authority. |
| `resimTicks` | Total resimulation steps. |
| `resimTicksAsAuthority` | Resimulation steps as authority. |
| `resimTicksAsFollower` | Resimulation steps as follower. |
| `maxServerDelay` | Maximum observed server delay in ticks. |
| `resimChecksSkippedDueToLackOfServerData` | Resim checks skipped because no server snapshot was available. |
| `resimChecksSkippedDueToServerAheadOfClient` | Resim checks skipped because the server tick is ahead. |
| `oldServerTickCount` | Number of server states received out of order. |
| `countMissingServerHistory` | Snap attempts that found no server state. |

---

## Constructor

```csharp
public ClientPredictedEntity(
    uint id,
    bool isServer,
    int bufferSize,
    Rigidbody rb,
    GameObject visuals,
    PredictableControllableComponent[] controllablePredictionContributors,
    PredictableComponent[] predictionContributors)
```

| Parameter | Description |
|---|---|
| `id` | Unique entity ID. Must match the server's ID for this object. |
| `isServer` | Pass `true` if this instance also runs on a listen server. |
| `bufferSize` | Tick history depth. Must cover max round-trip latency in ticks, with margin. |
| `rb` | The Rigidbody representing this entity. |
| `visuals` | Optional visual child GameObject to detach for interpolation. |
| `controllablePredictionContributors` | Components that read input. |
| `predictionContributors` | All physics-affecting components, including controllable ones. |

---

## Public Methods

### SetSingleStateEligibilityCheckHandler

```csharp
public void SetSingleStateEligibilityCheckHandler(
    Func<uint, uint, PhysicsStateRecord, PhysicsStateRecord, PredictionDecision> handler)
```

Assigns a snapshot comparison function. The manager calls this during `GetPredictionDecision`. This is the standard way to customise desync detection. Signature: `(entityId, tickId, localState, serverState) => PredictionDecision`.

---

### SetCustomEligibilityCheckHandler

```csharp
public void SetCustomEligibilityCheckHandler(
    Func<uint, uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, PredictionDecision> handler)
```

Assigns a full-history comparison function. Provides access to both complete buffers. Use this when snapshot-level checks are insufficient.

---

### GetPredictionDecision

```csharp
public virtual PredictionDecision GetPredictionDecision(uint lastAppliedTick, out uint fromTick)
```

Compares the latest server state against the matching local state. Returns a `PredictionDecision`. `fromTick` is set to the server tick ID used for comparison.

Returns `NOOP` when:
- `predictionDisabled` is true.
- No server snapshot is available.
- The server tick is ahead of the client.

Returns `SIMULATION_FREEZE` when the target tick is older than the local history.

---

### ClientSimulationTick

```csharp
public PredictionInputRecord ClientSimulationTick(uint tickId)
```

Called by the manager each tick for the locally controlled entity. Samples input, loads it, and calls `ApplyForces`. Returns the input record for transmission to the server.

Throws if called on a non-locally-controlled entity.

---

### ClientFollowerSimulationTick

```csharp
public void ClientFollowerSimulationTick(uint tickId)
```

Called by the manager each tick for follower entities. Snaps to the latest server state and calls `ApplyForces`.

Throws if called on a locally controlled entity.

---

### SampleInput

```csharp
public PredictionInputRecord SampleInput(uint tickId)
```

Samples input from all contributors into the input buffer slot for this tick. Returns the populated record.

---

### SamplePhysicsState

```csharp
public void SamplePhysicsState(uint tickId)
```

Captures the current Rigidbody state into `localStateBuffer`. Fires `newStateReached` and, if the state is authoritative, `newAuthoritativeStateReached`.

---

### BufferServerTick

```csharp
public void BufferServerTick(uint lastAppliedTick, PhysicsStateRecord serverState)
```

Adds an incoming server state to `serverStateBuffer`. Called by the manager when `OnServerStateReceived` is processed.

---

### SnapToServer

```csharp
public void SnapToServer(uint tickId)
```

Immediately moves the Rigidbody to the server's recorded state for the given tick. Fires `potentialDesync` with `SNAP_TO_SERVER_NO_DATA` if the state is missing.

---

### SnapToServerIfExists

```csharp
public void SnapToServerIfExists(uint tickId)
```

Snaps to server state only if a record exists for that tick. No-op otherwise.

---

### PreResimulationStep

```csharp
public void PreResimulationStep(uint tickId)
```

Called by the manager before each physics step during resimulation. Loads and applies stored input for the given tick (authority mode), or does nothing (follower mode).

---

### PostResimulationStep

```csharp
public void PostResimulationStep(uint tickId)
```

Called by the manager after each physics step during resimulation. Samples and stores the resulting state.

---

### GetLastInput

```csharp
public PredictionInputRecord GetLastInput()
```

Returns the input record for the last tick processed.

---

### GetServerDelay

```csharp
public uint GetServerDelay()
```

Returns the difference between the last local tick and the last server tick received.

---

### SetControlledLocally

```csharp
public void SetControlledLocally(bool controlled)
```

Sets whether this entity is locally controlled. Resets buffers. Called by the manager via `OnEntityOwnershipChanged`.

---

### Reset

```csharp
public void Reset()
```

Clears all history buffers, counters, and input state. Fires `onReset`.

---

## Events

| Type | Name | Fires when |
|---|---|---|
| `SafeEventDispatcher<PhysicsStateRecord>` | `newStateReached` | After each tick's state is sampled. |
| `SafeEventDispatcher<PhysicsStateRecord>` | `newAuthoritativeStateReached` | After each non-speculative (authority) state is sampled. |
| `SafeEventDispatcher<bool>` | `preSampleState` | Immediately before state is sampled. |
| `SafeEventDispatcher<bool>` | `onReset` | When `Reset` is called. |
| `SafeEventDispatcher<bool>` | `resimulation` | At start (`true`) and end (`false`) of a resimulation. |
| `SafeEventDispatcher<bool>` | `resimulationStep` | At start (`true`) and end (`false`) of each resimulation step. |
| `SafeEventDispatcher<DesyncEvent>` | `potentialDesync` | When an anomaly is detected during state comparison. |
| `SafeEventDispatcher<PredictionInputRecord>` | `inputUsed` | Reserved for future use. |

---

## Nested Types

### DesyncEvent

```csharp
public struct DesyncEvent
{
    public uint tickId;
    public DesyncReason reason;
    public uint gapSize;
}
```

### DesyncReason

```csharp
public enum DesyncReason
{
    MISSING_SERVER_COMPARISON = 0,
    GAP_IN_SERVER_STREAM      = 1,
    SERVER_AHEAD_OF_CLIENT    = 3,
    SNAP_TO_SERVER_NO_DATA    = 4,
}
```

---

## See Also

- [AbstractPredictedEntity](AbstractPredictedEntity.md)
- [ServerPredictedEntity](ServerPredictedEntity.md)
- [PredictionDecision](PredictionDecision.md)
- [PhysicsStateRecord](PhysicsStateRecord.md)
- [PredictionInputRecord](PredictionInputRecord.md)
- [Manual: Entities](../manual/entities.md)
