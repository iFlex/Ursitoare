# Overview and Architecture

## What Ursitoare Does

Ursitoare implements client-side prediction with server reconciliation for physics-driven multiplayer games in Unity. The client simulates the local player's entity immediately, without waiting for a server round-trip. When the server sends back its authoritative state, the **client** checks for divergence. If the states differ beyond a configurable threshold, the **client** rewinds physics and replays from the point of divergence.

The server is purely authoritative. It receives client input, simulates, and broadcasts the result. It never rewinds or resimulates. All reconciliation logic runs on the client.

## Network Agnosticism

Ursitoare has **no dependency on any networking library**. It does not import, reference, or assume any specific transport. This is a deliberate design choice that gives you full freedom to use whatever networking stack fits your project:

- Mirror, Netcode for GameObjects, Fish-Net, LiteNetLib, custom UDP, WebSocket — all work.
- You supply function delegates for sending data out and call methods on `PredictionManager` when data arrives.
- You are responsible for serialization and transport. Ursitoare gives you the data structures (`PredictionInputRecord`, `PhysicsStateRecord`, `WorldStateRecord`); you decide how to put them on the wire.

This means Ursitoare is purely a prediction engine. It can be dropped into any Unity project with any networking setup, as long as you bridge the two by wiring up the delegates described in the [Integration Tutorial](../integration-tutorial.md).

## Roles

A game instance can run as a client, a server, or both simultaneously (listen server / host).

- **Client.** Simulates the locally controlled entity ahead of the server. Receives server state, detects desyncs, and resimulates when needed. Tracks follower entities (other players) by applying the latest server state.
- **Server.** Receives input from clients, validates and applies it, simulates physics, and broadcasts the resulting authoritative state back to all clients.

You configure the role by calling `PredictionManager.Setup(isServer, isClient)`.

## Client Tick IDs: The Shared Language

The most important concept in Ursitoare is how **client tick IDs** connect predictions to authoritative state.

Every tick, the client:
1. Samples input and stores it locally (keyed by tick ID).
2. Simulates physics and stores the resulting state locally (keyed by tick ID).
3. Sends input to the server **tagged with the client's tick ID**.

The server:
1. Receives the input tagged with the client's tick ID.
2. Applies the input, simulates physics.
3. Sends the resulting state back to the client, **tagged with the same client tick ID**.

When the client receives the server's state, it uses the tick ID to look up its own prediction for that exact tick. If they differ, the client knows precisely:
- **Which tick to rewind to** — the tick ID on the server's state.
- **Which inputs to replay** — everything from that tick forward to the current tick, all stored in the local input buffer.

```
CLIENT                                             SERVER
──────                                             ──────

Tick 5:  Input sampled, stored at [5]
         State predicted, stored at [5]
         Input sent ──── (tickId=5) ────────►   Receives input for tick 5
                                                Applies input
Tick 6:  Input sampled, stored at [6]            Simulates physics
         State predicted, stored at [6]          Samples authoritative state
         Input sent ──── (tickId=6) ────────►   Sends state ◄── (tickId=5)
                                                  │
Tick 7:  Input sampled, stored at [7]              │
         State predicted, stored at [7]            │
         ◄─── Server state arrives (tickId=5) ─────┘
         │
         ├─ Look up local state at [5]
         ├─ Compare against server state
         │
         ├─ MATCH?  → Continue normally
         │
         └─ MISMATCH?
            ├─ Rewind physics to tick 5
            ├─ Snap to server state at tick 5
            ├─ Load input [6], apply forces, simulate  (replay tick 6)
            ├─ Load input [7], apply forces, simulate  (replay tick 7)
            └─ Now at tick 7 with corrected trajectory
```

This tick-tagging mechanism is what makes client-side prediction and reconciliation work correctly regardless of network latency. The higher the latency, the further back the rewind goes, and the more ticks must be replayed — but the mechanism is the same.

## The Tick Loop

The game world advances one tick per `FixedUpdate`. You are responsible for calling `PredictionManager.Tick()` from your `FixedUpdate` method.

Each tick proceeds in this order:

```
┌────────────────────────────────────────────────────────────┐
│                    PredictionManager.Tick()                 │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  1. RESIMULATION CHECK (client only)                       │
│     ┌──────────────────────────────────────────────────┐   │
│     │ For each ClientPredictedEntity:                  │   │
│     │   Compare local state vs server state            │   │
│     │   Return PredictionDecision:                     │   │
│     │     NOOP    → states match, continue             │   │
│     │     RESIM   → rewind + replay                    │   │
│     │     SNAP    → teleport to server state           │   │
│     │     FREEZE  → pause simulation                   │   │
│     └──────────────────────────────────────────────────┘   │
│                           │                                │
│  2. PRE-SIMULATION                                         │
│     Client: SampleInput → LoadInput → ApplyForces          │
│     Server: Dequeue input → Validate → LoadInput →         │
│             ApplyForces                                    │
│                           │                                │
│  3. PHYSICS STEP                                           │
│     PhysicsController.Simulate()                           │
│     → Physics.Simulate(fixedDeltaTime)                     │
│                           │                                │
│  4. POST-SIMULATION                                        │
│     Client: Sample Rigidbody state → store in buffer       │
│     Server: Sample Rigidbody state → send to all clients   │
│                           │                                │
│  5. TICK ID ADVANCES                                       │
│     tickId++                                               │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

1. **Resimulation check (client only).** The manager queries every registered `ClientPredictedEntity` for its `PredictionDecision`. If any entity requests a resimulation, the manager rewinds physics and replays all ticks from the divergence point forward.
2. **Pre-simulation.** Each entity runs its pre-sim step: loading buffered input and applying forces to the `Rigidbody`.
3. **Physics step.** `PhysicsController.Simulate()` is called, advancing `Physics.Simulate` by one `fixedDeltaTime`.
4. **Post-simulation.** The resulting `Rigidbody` state is sampled and stored. On the server, the state is sent to clients tagged with the client's tick ID.
5. **Tick ID advances.**

## Resimulation in Detail

When the client detects a desync (server state differs from local prediction at the same tick), it performs resimulation:

```
Before resimulation:
Local history:    [5]──[6]──[7]──[8]──[9]  ← current tick
Server says:      [5] differs from local [5]

Resimulation:
1. Rewind physics world to tick 5
2. Snap entity to server's authoritative state at tick 5
3. Replay:
   ┌─ Tick 6: Load stored input[6] → ApplyForces → Physics.Simulate
   ├─ Tick 7: Load stored input[7] → ApplyForces → Physics.Simulate
   ├─ Tick 8: Load stored input[8] → ApplyForces → Physics.Simulate
   └─ Tick 9: Load stored input[9] → ApplyForces → Physics.Simulate

After resimulation:
Entity is at tick 9 with a corrected trajectory that started
from the server's authoritative state at tick 5.
```

During replay, each intermediate tick also checks if the server has provided authoritative state for that tick. If so, it snaps to the server state before continuing the replay. This gives the most accurate correction possible.

## Entities

There are two entity types, both created manually (not as MonoBehaviours):

- `ClientPredictedEntity` — lives on the client. Maintains local input and state history. Compares against server state to decide whether to resimulate.
- `ServerPredictedEntity` — lives on the server. Buffers incoming client inputs, applies them in order, and samples state for broadcast.

A single physical object in the game corresponds to one entity on the client and one on the server. The `PredictedEntity` interface is a convenience wrapper that holds both.

## Components

Your game logic attaches to entities through two interfaces:

- `PredictableComponent` — any component that applies forces to the Rigidbody each tick.
- `PredictableControllableComponent` — any component that reads player input. Extends `PredictableComponent`.

The entity calls `ApplyForces()` on all contributors each tick and during resimulation steps. The order of contributors is fixed at construction time and must be identical on client and server.

See [Entities](entities.md) for details on these interfaces and the constraints they impose.

## Physics Simulation

Physics simulation is controlled by a `PhysicsController`. The default is `RewindablePhysicsController`, which records the state of every tracked `Rigidbody` each tick and can restore any historical state for resimulation. You can substitute a different implementation.

See [Physics Controllers](physics-controllers.md).

## See Also

- [PredictionManager](prediction-manager.md)
- [Entities](entities.md)
- [Integration Tutorial](../integration-tutorial.md)
