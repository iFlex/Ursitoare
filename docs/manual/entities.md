# Entities

## Overview

An entity is a physics object participating in the prediction system. Every predicted object has two representations:

- A `ClientPredictedEntity` on the client.
- A `ServerPredictedEntity` on the server.

Both are plain C# objects, not MonoBehaviours. You construct them manually and register them with `PredictionManager`.

## ClientPredictedEntity

`ClientPredictedEntity` is the client's representation of a predicted object. It maintains:

- A ring buffer of past input records.
- A ring buffer of past local physics states.
- A tick-indexed buffer of states received from the server.

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

## Prediction Components

Any MonoBehaviour attached to a predicted entity that influences physics must implement at least one of:

- `PredictableComponent` — for components that apply forces (`ApplyForces`). This is required.
- `PredictableControllableComponent` — for components that also read input (`SampleInput`, `LoadInput`). Implies `PredictableComponent`.

The order of contributors passed to the entity constructor determines the order they are called. **This order must be the same on client and server.**

## Detached Visuals

The visual representation of an entity can be separated from the physics body. Pass a `visuals` `GameObject` to the entity constructor. `PredictedEntityVisuals` then takes ownership of that object and drives it with interpolated positions, independent of the physics body.

See [Visual Interpolation](visual-interpolation.md).

## See Also

- [Scripting API: ClientPredictedEntity](../scripting-api/ClientPredictedEntity.md)
- [Scripting API: ServerPredictedEntity](../scripting-api/ServerPredictedEntity.md)
- [Scripting API: PredictableComponent](../scripting-api/PredictableComponent.md)
- [Scripting API: PredictableControllableComponent](../scripting-api/PredictableControllableComponent.md)
