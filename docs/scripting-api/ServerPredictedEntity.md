# ServerPredictedEntity

**Namespace:** `Prediction`
**Inherits:** `AbstractPredictedEntity`

Server-side authoritative representation of a predicted entity. Buffers client inputs, applies them each tick, and provides state for broadcast.

---

## Static Configuration

| Type | Name | Default | Description |
|---|---|---|---|
| `bool` | `DEBUG` | `false` | Verbose logging. |
| `bool` | `APPLY_FORCES_TO_EACH_CATCHUP_INPUT` | `false` | Call `ApplyForces` for each catchup input, not just the last. |
| `bool` | `USE_BUFFERING` | `true` | Buffer inputs on first connection before processing. |
| `bool` | `BUFFER_ONCE` | `true` | Only apply buffering once per ownership assignment. |
| `int` | `BUFFER_FULL_THRESHOLD` | `3` | Number of buffered inputs required before processing begins. |
| `bool` | `CATCHUP` | `true` | Process multiple inputs per tick when the queue is large. |
| `bool` | `INCREMENT_TICK_WHEN_NO_INPUT` | `false` | Advance client tick ID when no input is available. |
| `bool` | `SERVER_LOG_VELOCITIES` | `false` | Log velocity data after each simulation step. |
| `bool` | `LOG_CLIENT_INPUTS` | `false` | Log each input as it arrives and is applied. |
| `bool` | `APPLY_OLD_INPUTS_IN_CURRENT_TICK` | `false` | Apply late inputs in the current tick rather than discarding. |
| `bool` | `KEEP_SERVER_STATE_HISTORY` | `true` | Store state history for lag compensation. |
| `bool` | `LOG_INPUT_QUEUE_SIZE` | `true` | Log input queue size each tick. |

---

## Properties

| Type | Name | Description |
|---|---|---|
| `GameObject` | `gameObject` | The GameObject this entity is attached to. |
| `TickIndexedBuffer<PredictionInputRecord>` | `inputQueue` | Incoming client inputs, keyed by client tick ID. |
| `int` | `catchupSections` | Number of sections used to determine catchup rate. Default: `3`. |
| `int` | `ticksPerCatchupSection` | Inputs per section before catchup triggers. Computed from buffer size and `catchupSections`. |

---

## Diagnostic Counters

| Name | Description |
|---|---|
| `invalidInputs` | Inputs rejected by validation. |
| `ticksWithoutInput` | Ticks where no input was available. |
| `lateTickCount` | Inputs that arrived after their tick was already processed. |
| `totalSnapAheadCounter` | Times the queue was cleared and snapped to the latest input. |
| `inputJumps` | Non-sequential jumps in client tick ID. |
| `catchupTicks` | Total extra ticks processed during catchup. |
| `catchupBufferWipes` | Times the catchup logic wiped the buffer. |
| `maxClientDelay` | Maximum observed input queue range in ticks. |
| `totalBufferingTicks` | Ticks spent in the initial buffering phase. |
| `totalMissingInputTicks` | Ticks where no input was queued at all. |
| `clUpdateCount` | Total input packets received. |
| `clAddedUpdateCount` | Total input packets added to the queue. |

---

## Constructor

```csharp
public ServerPredictedEntity(
    uint id,
    int bufferSize,
    Rigidbody rb,
    GameObject visuals,
    PredictableControllableComponent[] controllablePredictionContributors,
    PredictableComponent[] predictionContributors)
```

| Parameter | Description |
|---|---|
| `id` | Unique entity ID. Must match the client's ID for this object. |
| `bufferSize` | Input queue capacity. |
| `rb` | The Rigidbody representing this entity. |
| `visuals` | Optional visual child GameObject. |
| `controllablePredictionContributors` | Components that read input. |
| `predictionContributors` | All physics-affecting components. |

---

## Public Methods

### ServerSimulationTick

```csharp
public uint ServerSimulationTick()
```

Called by the manager each tick. Dequeues the next client input, validates it, applies it, and calls `ApplyForces`. Handles catchup if the queue is large. Returns the applied client tick ID.

---

### SamplePhysicsState

```csharp
public PhysicsStateRecord SamplePhysicsState(uint svTid)
```

Captures the current Rigidbody state and component state into a `PhysicsStateRecord`. The record includes the last applied input, for relay to clients as a follower hint. Returns the record.

---

### BufferClientTick

```csharp
public void BufferClientTick(uint tid, PredictionInputRecord inputRecord)
```

Adds an incoming input record to the queue. Called by the manager when `OnClientStateReceived` is processed.

---

### GetClientTickId

```csharp
public uint GetClientTickId()
```

Returns the client tick ID currently being processed.

---

### GetStateAtTick

```csharp
public PhysicsStateRecord GetStateAtTick(uint tick)
```

Returns the server's recorded state at the given server tick ID. Returns `null` if `KEEP_SERVER_STATE_HISTORY` is false or the tick is outside the buffer.

---

### BufferFill

```csharp
public int BufferFill()
```

Returns the number of inputs currently queued.

---

### BufferSize

```csharp
public uint BufferSize()
```

Returns the tick range spanned by the input queue (end tick minus start tick).

---

### Reset

```csharp
public void Reset()
```

Clears the input queue, resets the client tick ID, and resets buffering. Call when ownership changes. The manager calls this automatically via `SetEntityOwner`.

---

### ResetClientState

```csharp
public void ResetClientState()
```

Clears the input queue and resets buffering without resetting statistics. Use when changing the controller of an entity mid-game.

---

### TickDeltaToTimeDelta

```csharp
public float TickDeltaToTimeDelta(int delta)
```

Converts a tick count delta to seconds using `Time.fixedDeltaTime`.

---

## Events

| Type | Name | Fires when |
|---|---|---|
| `SafeEventDispatcher<bool>` | `preSampleState` | Immediately before `SamplePhysicsState` writes state. |
| `SafeEventDispatcher<bool>` | `stateSampled` | After `SamplePhysicsState` completes. |
| `SafeEventDispatcher<bool>` | `firstTickArrived` | When the first input arrives after a reset (queue was empty). |
| `SafeEventDispatcher<DesyncEvent>` | `potentialDesync` | When an anomalous input condition is detected. |
| `SafeEventDispatcher<ClientInput>` | `inputReceived` | When an input packet is added to the queue. Only fires when `LOG_CLIENT_INPUTS` is true. |

---

## Nested Types

### DesyncEvent

```csharp
public struct DesyncEvent
{
    public uint tickId;
    public DesyncReason reason;
}
```

### DesyncReason

```csharp
public enum DesyncReason
{
    NO_INPUT_FOR_SERVER_TICK  = 0,
    INPUT_BUFFERED            = 1,
    INPUT_JUMP                = 2,
    MULTIPLE_INPUTS_PER_FRAME = 3,
    INVALID_INPUT             = 4,
    LATE_TICK                 = 5,
    TICK_OVERFLOW             = 6,
    CATCHUP                   = 7,
}
```

### ClientInput

```csharp
public struct ClientInput
{
    public uint tickId;
    public PredictionInputRecord input;
}
```

---

## See Also

- [AbstractPredictedEntity](AbstractPredictedEntity.md)
- [ClientPredictedEntity](ClientPredictedEntity.md)
- [PredictionInputRecord](PredictionInputRecord.md)
- [PhysicsStateRecord](PhysicsStateRecord.md)
- [Manual: Entities](../manual/entities.md)
