# Overview and Architecture

## What Ursitoare Does

Ursitoare implements client-side prediction with server reconciliation for physics-driven multiplayer games in Unity. The client simulates the local player's entity immediately, without waiting for a server round-trip. When the server sends back its authoritative state, the client checks for divergence. If the states differ beyond a configurable threshold, the client rewinds physics and replays from the point of divergence.

## Roles

A game instance can run as a client, a server, or both simultaneously (listen server / host).

- **Client.** Simulates the locally controlled entity ahead of the server. Receives server state, detects desyncs, and resimulates when needed. Tracks follower entities (other players) using server state.
- **Server.** Receives input from clients, applies it, simulates physics, and broadcasts the resulting state back to all clients.

You configure the role by calling `PredictionManager.Setup(isServer, isClient)`.

## The Tick Loop

The game world advances one tick per `FixedUpdate`. You are responsible for calling `PredictionManager.Tick()` from your `FixedUpdate` method.

Each tick proceeds in this order:

1. **Resimulation check (client only).** The manager queries every registered `ClientPredictedEntity` for its `PredictionDecision`. If any entity requests a resimulation, the manager rewinds physics and replays all ticks from the divergence point forward.
2. **Pre-simulation.** Each entity runs its pre-sim step: loading buffered input and applying forces to the `Rigidbody`.
3. **Physics step.** `PhysicsController.Simulate()` is called, advancing `Physics.Simulate` by one `fixedDeltaTime`.
4. **Post-simulation.** The resulting `Rigidbody` state is sampled and stored. The server sends state to clients.
5. **Tick ID advances.**

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

## Network Agnosticism

Ursitoare has no networking dependency. It communicates through function delegates you assign before calling `Setup`. These delegates are called by the manager to send and receive tick data. Your networking layer calls back into the manager's `On*` methods when messages arrive.

See the [Integration Tutorial](../integration-tutorial.md).

## Physics Simulation

Physics simulation is controlled by a `PhysicsController`. The default is `RewindablePhysicsController`, which records the state of every tracked `Rigidbody` each tick and can restore any historical state for resimulation. You can substitute a different implementation.

See [Physics Controllers](physics-controllers.md).

## See Also

- [PredictionManager](prediction-manager.md)
- [Entities](entities.md)
- [Integration Tutorial](../integration-tutorial.md)
