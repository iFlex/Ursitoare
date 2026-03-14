# Ursitoare

Ursitoare is a client-side prediction and server reconciliation library for Unity. It handles the full prediction loop: input sampling, physics simulation, server state buffering, desync detection, and resimulation. It does **not** include a networking layer — you wire in your own transport by assigning a small set of delegates.

## Key Concepts

- **Network library agnostic.** Ursitoare has zero dependency on any networking library. It communicates exclusively through function delegates you assign. Mirror, Netcode for GameObjects, Fish-Net, LiteNetLib, custom UDP — any transport works. You serialize, send, and receive; Ursitoare handles the prediction logic. See the [Integration Tutorial](docs/integration-tutorial.md).

- **Tick-based simulation.** The game world advances in fixed steps. Each step is a tick. Both client and server share this concept, but advance independently.

- **Client-side prediction.** The locally controlled entity simulates immediately, without waiting for a server round-trip. The client runs ahead of the server.

- **Client-side reconciliation.** When the server sends back its authoritative state, the **client** compares it against its own prediction. If they differ, the **client** rewinds physics and replays from the divergence point. The server is purely authoritative — it never rewinds or replays.

- **Tick-tagged updates.** Every input the client sends and every state the server returns is tagged with the client's tick ID. This shared tick ID is how the client knows exactly which local prediction to compare against, and exactly which tick to rewind to when a desync is detected. See [Overview & Architecture](docs/manual/overview.md) for details.

## How It Works (At a Glance)

```
  CLIENT                                          SERVER
  ──────                                          ──────
  Tick 10: Sample input ──────────────────────►  Receives input for Tick 10
           Apply forces                          Validates input
           Simulate physics                      Applies input
           Store prediction locally              Simulates physics
           Send input (tagged Tick 10)            Samples state
                                                 Sends state (tagged Tick 10)
  ...                                             │
  Tick 14: ◄──────────────────────────────────── State for Tick 10 arrives
           Compare local Tick 10
           vs server Tick 10
           ┌─────────────────────┐
           │ Desync detected?    │
           │  YES → Rewind to 10 │
           │        Snap to      │
           │        server state │
           │        Replay 11-14 │
           │  NO  → Continue     │
           └─────────────────────┘
```

## Documentation

### Manual

Conceptual guides explaining how the system works.

| Page | Description |
|---|---|
| [Overview & Architecture](docs/manual/overview.md) | System design, tick loop, tick tagging, roles |
| [PredictionManager](docs/manual/prediction-manager.md) | The central coordinator |
| [Entities](docs/manual/entities.md) | Client and server entity types |
| [Physics Controllers](docs/manual/physics-controllers.md) | Pluggable simulation backends |
| [Resimulation Policy](docs/manual/resimulation-policy.md) | How desync is detected and corrected |
| [Visual Interpolation](docs/manual/visual-interpolation.md) | Smoothing visuals over the physics tick |

### Integration Tutorial

Step-by-step guide to adding Ursitoare to your project.

- [Integration Tutorial](docs/integration-tutorial.md)

### Scripting API

Full reference for every public type.

#### Core

| Class | Description |
|---|---|
| [PredictionManager](docs/scripting-api/PredictionManager.md) | Central singleton. Drives the tick loop. |
| [PredictionDecision](docs/scripting-api/PredictionDecision.md) | Enum returned by resimulation checks. |

#### Entities

| Class | Description |
|---|---|
| [AbstractPredictedEntity](docs/scripting-api/AbstractPredictedEntity.md) | Base class for client and server entities. |
| [ClientPredictedEntity](docs/scripting-api/ClientPredictedEntity.md) | Client-side predicted entity. |
| [ServerPredictedEntity](docs/scripting-api/ServerPredictedEntity.md) | Server-side authoritative entity. |
| [PredictedEntityVisuals](docs/scripting-api/PredictedEntityVisuals.md) | MonoBehaviour that drives interpolated visuals. |

#### Interfaces

| Interface | Description |
|---|---|
| [PredictableComponent](docs/scripting-api/PredictableComponent.md) | Implement on any component that applies forces. |
| [PredictableControllableComponent](docs/scripting-api/PredictableControllableComponent.md) | Implement on any component that reads player input. |
| [PredictedEntity](docs/scripting-api/PredictedEntity.md) | Wrapper interface tying client and server entities together. |

#### Data

| Class | Description |
|---|---|
| [PhysicsStateRecord](docs/scripting-api/PhysicsStateRecord.md) | One snapshot of a Rigidbody's state. |
| [PredictionInputRecord](docs/scripting-api/PredictionInputRecord.md) | One snapshot of player input. |
| [WorldStateRecord](docs/scripting-api/WorldStateRecord.md) | Combined state snapshot for all entities in a tick. |

#### Physics Controllers

| Class | Description |
|---|---|
| [PhysicsController](docs/scripting-api/PhysicsController.md) | Interface for the physics simulation backend. |
| [RewindablePhysicsController](docs/scripting-api/RewindablePhysicsController.md) | Default controller. Tracks state history for rewinding. |
| [SimplePhysicsController](docs/scripting-api/SimplePhysicsController.md) | Minimal controller. No rewind support. |
| [SimplePhysicsControllerKinematic](docs/scripting-api/SimplePhysicsControllerKinematic.md) | Controller that freezes other bodies during resimulation. |

#### Resimulation Policy

| Class | Description |
|---|---|
| [SingleSnapshotInstanceResimChecker](docs/scripting-api/SingleSnapshotInstanceResimChecker.md) | Interface for snapshot comparison logic. |
| [SimpleConfigurableResimulationDecider](docs/scripting-api/SimpleConfigurableResimulationDecider.md) | Threshold-based desync checker. |

#### Interpolation

| Class | Description |
|---|---|
| [VisualsInterpolationsProvider](docs/scripting-api/VisualsInterpolationsProvider.md) | Interface for visual interpolation. |
| [MovingAverageInterpolator](docs/scripting-api/MovingAverageInterpolator.md) | Default interpolator. Smooths with a sliding window average. |
| [QuaternionAverage](docs/scripting-api/QuaternionAverage.md) | Utility for computing an average of multiple quaternions. |

#### Utilities

| Class | Description |
|---|---|
| [RingBuffer&lt;T&gt;](docs/scripting-api/RingBuffer.md) | Fixed-capacity circular buffer. |
| [TickIndexedBuffer&lt;T&gt;](docs/scripting-api/TickIndexedBuffer.md) | Dictionary-backed buffer keyed by tick ID. |
| [WrapperHelpers](docs/scripting-api/WrapperHelpers.md) | Helpers for extracting prediction components from MonoBehaviours. |
