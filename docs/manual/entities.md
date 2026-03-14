# Entities

## Overview

An entity is a physics object participating in the prediction system. Every predicted object has two representations:

- A `ClientPredictedEntity` on the client.
- A `ServerPredictedEntity` on the server.

Both are plain C# objects, not MonoBehaviours. You construct them manually and register them with `PredictionManager`.

## Prediction Components — The Rules

Before looking at entities, you must understand the interfaces that make prediction work. **This is the most important part of the integration.**

Any MonoBehaviour attached to a predicted entity that influences physics must implement at least one of:

- `PredictableComponent` — for components that apply forces (`ApplyForces`). Required for any force-applying logic.
- `PredictableControllableComponent` — for components that also read input (`SampleInput`, `LoadInput`). Implies `PredictableComponent`.

### Constraints You Must Follow

> **All forces must be applied inside `ApplyForces()`. All input must be sampled inside `SampleInput()` and loaded inside `LoadInput()`. Input must be stored as fields on the component and persist until the next `SampleInput()` or `LoadInput()` call. Applying forces outside of `ApplyForces()` guarantees a desync.**

These constraints exist because resimulation must reproduce the same behavior. During resimulation:
1. `LoadInput()` is called with stored past input.
2. `ApplyForces()` is called to reproduce the forces.
3. Physics is stepped.

If any force is applied outside `ApplyForces()`, it cannot be reproduced during resimulation.

```
Normal tick flow:
  SampleInput() → store input fields → LoadInput() → set fields → ApplyForces() → read fields

Resimulation flow (replaying a past tick):
                                        LoadInput() → set fields → ApplyForces() → read fields
                                        ▲
                                        │ uses stored input from that past tick
```

### Contributor Order

The order of contributors passed to the entity constructor determines the order they are called. **This order must be the same on client and server.** If the order differs, input will be read/written in a different sequence and forces will be applied differently, causing desyncs.

## ClientPredictedEntity

`ClientPredictedEntity` is the client's representation of a predicted object. It maintains:

- A ring buffer of past input records (keyed by tick ID).
- A ring buffer of past local physics states (keyed by tick ID).
- A tick-indexed buffer of states received from the server (keyed by the client's tick ID that the server echoes back).

### Construction

```csharp
var entity = new ClientPredictedEntity(
    id: entityId,
    isServer: false,
    bufferSize: 64,
    rb: rigidbody,
    visuals: visualsGameObject,
    controllablePredictionContributors: controllableComponents,
    predictionContributors: allComponents
);
```

`bufferSize` controls how many ticks of history are stored. It must be large enough to cover the maximum expected resimulation distance (round-trip latency in ticks plus margin).

### Locally Controlled vs Follower

An entity is either locally controlled or a follower.

- **Locally controlled.** The player owns this entity. The manager calls `ClientSimulationTick` each tick: it samples input, applies forces, and sends the input to the server. The entity's local state is compared against server snapshots to detect desyncs.
- **Follower.** Another player owns this entity. The manager calls `ClientFollowerSimulationTick` each tick: it snaps to the latest server state and applies forces using the last known input.

Ownership is set by `PredictionManager.OnEntityOwnershipChanged`. You do not call `SetControlledLocally` directly.

### How Tick IDs Enable Reconciliation

The client stores both input and state keyed by tick ID. When the server sends state tagged with the client's tick ID:

```
Client local buffers:                  Server state arrives:
                                       ┌─────────────────────────┐
  Input buffer:                        │ tickId = 5              │
  [5] → (0.7, 0.3, false)             │ position = (3.1, 0, 2.8)│
  [6] → (0.8, 0.1, false)             │ velocity = (1.2, 0, 0.9)│
  [7] → (0.5, 0.5, true)              │ ...                     │
                                       └─────────────────────────┘
  State buffer:                                  │
  [5] → (3.0, 0, 2.7) ◄────── compare ──────────┘
  [6] → (3.5, 0, 3.1)         mismatch! Rewind to 5,
  [7] → (4.1, 0, 3.8)         then replay with input [6], [7]
```

The client:
1. Looks up local state at tick 5.
2. Compares it against server state at tick 5.
3. If different: rewinds to tick 5, snaps to server state, replays ticks 6 and 7 using stored inputs.

### Server State Buffering

When the server sends a state update, call:

```csharp
PredictionManager.Instance.OnServerStateReceived(entityId, stateRecord);
```

The entity stores the record in `serverStateBuffer`. Each tick, `GetPredictionDecision` compares the latest buffered server state against the matching local state. If the difference exceeds the configured threshold, it returns `PredictionDecision.RESIMULATE`.

### Simulation Freeze

If the server snapshot is older than the oldest entry in the local history buffer, the client cannot rewind far enough to resimulate. The entity returns `PredictionDecision.SIMULATION_FREEZE`. The manager skips simulation for that tick, effectively pausing the client until the server catches up.

### Component State

Entities can carry stateful components beyond the Rigidbody. If a `PredictableComponent` returns `HasState() == true`, its state is serialised into `PhysicsStateRecord.componentState` alongside the physics data. This is rewound and restored during resimulation.

## ServerPredictedEntity

`ServerPredictedEntity` is the server's authoritative representation of a predicted object. It maintains a tick-indexed queue of incoming client inputs.

### Construction

```csharp
var entity = new ServerPredictedEntity(
    id: entityId,
    bufferSize: 64,
    rb: rigidbody,
    visuals: visualsGameObject,
    controllablePredictionContributors: controllableComponents,
    predictionContributors: allComponents
);
```

### How the Server Uses Client Tick IDs

The server does not maintain its own tick counter per entity. Instead, it tracks the **client's** tick ID:

```
Client sends:               Server receives and buffers:
  Input (tickId=10) ──────►  inputQueue[10] = input
  Input (tickId=11) ──────►  inputQueue[11] = input
  Input (tickId=12) ──────►  inputQueue[12] = input

Each server tick:
  1. Dequeue next input from queue → clientTickId advances to match
  2. Validate input
  3. LoadInput + ApplyForces
  4. After Physics.Simulate():
     Sample state → tag with clientTickId → send to client
```

This is what makes reconciliation possible. The server's state is always tagged with the client's tick ID, so the client can look up the matching local prediction.

### Input Buffering

When a client's input packet arrives, call:

```csharp
PredictionManager.Instance.OnClientStateReceived(connId, clientTickId, inputRecord);
```

The entity queues the input in `inputQueue`. Each tick, `ServerSimulationTick` dequeues the next input and applies it.

**Initial buffering.** `USE_BUFFERING = true` holds off processing until `BUFFER_FULL_THRESHOLD` inputs have accumulated. This smooths out jitter from irregular network delivery.

**Catchup.** If the input queue grows large (the server has fallen behind), the entity processes multiple inputs per tick to catch up. The catchup rate is controlled by `catchupSections` and `ticksPerCatchupSection`.

### Input Validation

Before applying input, the server calls `ValidateInput` on each `PredictableControllableComponent`. If validation fails, the input is discarded and `invalidInputs` is incremented.

### Ownership Change

When a new player takes control of an entity, call `Reset()` to clear the input queue and prepare for a fresh stream of tick IDs. `PredictionManager.SetEntityOwner` calls this automatically.

### State History

When `KEEP_SERVER_STATE_HISTORY = true`, the server stores one `PhysicsStateRecord` per tick in a ring buffer. Retrieve past states with `GetStateAtTick(tick)`. This is used for lag compensation.

## Detached Visuals

The visual representation of an entity can be separated from the physics body. Pass a `visuals` `GameObject` to the entity constructor. `PredictedEntityVisuals` then takes ownership of that object and drives it with interpolated positions, independent of the physics body.

See [Visual Interpolation](visual-interpolation.md).

## See Also

- [Scripting API: ClientPredictedEntity](../scripting-api/ClientPredictedEntity.md)
- [Scripting API: ServerPredictedEntity](../scripting-api/ServerPredictedEntity.md)
- [Scripting API: PredictableComponent](../scripting-api/PredictableComponent.md)
- [Scripting API: PredictableControllableComponent](../scripting-api/PredictableControllableComponent.md)
