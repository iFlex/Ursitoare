# Ursitoare - Client-Side Prediction & Server Reconciliation Framework

Ursitoare is a Unity physics prediction and server reconciliation framework for networked multiplayer games. It implements the classic client-side prediction pattern: the client simulates physics locally for immediate responsiveness, the server runs the authoritative simulation, and when discrepancies are detected the client rewinds and resimulates to reconcile with the server's state.

## How It Works

1. **Client-side prediction**: The locally controlled entity samples player input, applies forces, and simulates physics immediately so the player sees instant feedback without waiting for the server round-trip.

2. **Server authority**: The server receives client inputs, validates them, runs its own physics simulation, and broadcasts the authoritative state back to all clients.

3. **Reconciliation**: Each client compares its locally predicted state against the server's authoritative state. When a discrepancy exceeds configurable thresholds, the client rewinds physics to the server's tick, re-applies all buffered inputs, and resimulates forward to the current tick, correcting the prediction error.

4. **Visual smoothing**: A detached visuals object interpolates between physics states so the player sees smooth motion even when the underlying simulation snaps or resimulates.

## Architecture Overview

```
PredictionManager (orchestrator)
  |
  |-- ClientPredictedEntity (one per predicted object on the client)
  |     |-- PredictableControllableComponent[] (input-driven behaviours)
  |     |-- PredictableComponent[] (force-applying behaviours)
  |     |-- PredictedEntityVisuals (visual smoothing)
  |           |-- CustomVisualInterpolator (sliding-window interpolation)
  |
  |-- ServerPredictedEntity (one per predicted object on the server)
  |     |-- PredictableControllableComponent[] (input-driven behaviours)
  |     |-- PredictableComponent[] (force-applying behaviours)
  |
  |-- PhysicsController (physics simulation backend)
  |
  |-- PhysicsStateRecord (snapshot of a rigid body at a tick)
  |-- PredictionInputRecord (snapshot of player input at a tick)
```

### Data Flow

```
Client                          Network                         Server
------                          -------                         ------
Sample input
Store input in buffer
Apply forces
Simulate physics
Store predicted state           -- input + tickId -->           Receive input
                                                                Buffer input
                                                                Load & validate input
                                                                Apply forces
                                                                Simulate physics
Compare local vs server         <-- state + tickId --           Send authoritative state
If desync: rewind, resim
Visual interpolation
```

## Key Components

| Component | Role |
|---|---|
| [PredictionManager](docs/PredictionManager.md) | Central orchestrator: manages ticks, entities, ownership, resimulation, and network hooks. |
| [PhysicsController](docs/PhysicsController.md) | Interface for physics simulation, rewind, and resimulation backends. |
| [PredictableComponent](docs/PredictableComponent.md) | Interface for any component that applies forces and optionally carries state. |
| [PredictableControllableComponent](docs/PredictableControllableComponent.md) | Interface for components that sample, validate, and load player input. |
| [ClientPredictedEntity](docs/ClientPredictedEntity.md) | Client-side representation of a predicted entity: stores local and server state history, makes resimulation decisions. |
| [ServerPredictedEntity](docs/ServerPredictedEntity.md) | Server-side representation: buffers incoming client input, runs authoritative simulation, handles catchup. |
| [PredictedEntityVisuals](docs/PredictedEntityVisuals.md) | MonoBehaviour that detaches a visual representation and interpolates it for smooth rendering. |
| [CustomVisualInterpolator](docs/CustomVisualInterpolator.md) | Sliding-window interpolation implementation for smoothing visual output. |
| [PhysicsStateRecord](docs/PhysicsStateRecord.md) | Data class holding a full physics snapshot (position, rotation, velocity, angular velocity, tick, component state). |
| [PredictionInputRecord](docs/PredictionInputRecord.md) | Data class holding player input (scalar + binary channels) for a single tick. |

## Getting Started

1. **Create a `PredictionManager`** and configure it with your networking callbacks (`clientStateSender`, `serverStateSender`, etc.).
2. **Implement `PredictableComponent`** on any MonoBehaviour that applies forces to a Rigidbody.
3. **Implement `PredictableControllableComponent`** on any MonoBehaviour that reads player input.
4. **Create `ClientPredictedEntity` / `ServerPredictedEntity`** wrappers for each networked Rigidbody, passing in the component arrays.
5. **Register entities** with `PredictionManager.AddPredictedEntity(...)`.
6. **Call `PredictionManager.Tick()`** each `FixedUpdate`.
7. **Attach `PredictedEntityVisuals`** to each predicted object for smooth rendering.
8. **Wire up your networking layer** to call `OnClientStateReceived`, `OnServerStateReceived`, `OnHeartbeatReceived`, and `OnEntityOwnershipChanged` when messages arrive.

See the individual component docs in the `docs/` folder for detailed API references and configuration flags.
