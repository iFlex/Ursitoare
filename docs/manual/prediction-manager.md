# PredictionManager

## Overview

`PredictionManager` is the central coordinator of the prediction system. It is a plain C# class, not a MonoBehaviour. You create one instance, configure it, and call `Tick()` from `FixedUpdate` every frame.

## Setup

Create an instance and assign all required delegates before calling `Setup`.

```csharp
var manager = new PredictionManager();

// Client delegates
manager.clientHeartbeadSender = (tickId) => { /* send heartbeat to server */ };
manager.clientStateSender = (tickId, input) => { /* send input to server */ };

// Server delegates
manager.serverStateSender = (connId, entityId, state) => { /* send state to client */ };
manager.serverSetControlledLocally = (connId, entityId, owned) => { /* send ownership message to client */ };
manager.connectionsIterator = () => myNetworkManager.GetConnectionIds();

manager.Setup(isServer: false, isClient: true);
```

`Setup` validates all required delegates for the configured role and throws if any are missing.

## Tick Loop

Call `Tick()` once per `FixedUpdate`:

```csharp
void FixedUpdate()
{
    PredictionManager.Instance.Tick();
}
```

## Entity Registration

Register entities after construction:

```csharp
manager.AddPredictedEntity(clientEntity);   // client
manager.AddPredictedEntity(serverEntity);   // server
```

Remove entities when they are destroyed:

```csharp
manager.RemovePredictedEntity(entityId);
```

## Ownership

On the server, assign which connection controls which entity:

```csharp
manager.SetEntityOwner(serverEntity, connectionId);
manager.UnsetOwnership(serverEntity);
```

Setting ownership causes `serverSetControlledLocally` to fire, notifying the owning client.

On the client, call `OnEntityOwnershipChanged` when the server notifies you that you own an entity:

```csharp
manager.OnEntityOwnershipChanged(entityId, owned: true);
```

## Receiving Network Messages

Wire these into your networking layer's receive callbacks:

| Method | Call when |
|---|---|
| `OnClientStateReceived(connId, tickId, input)` | Server receives input from a client |
| `OnServerStateReceived(entityId, state)` | Client receives per-entity state from server |
| `OnServerWorldStateReceived(worldState)` | Client receives combined world state from server |
| `OnHeartbeatReceived(connId, tickId)` | Server receives a heartbeat from a client |
| `OnEntityOwnershipChanged(entityId, owned)` | Client learns it owns or loses an entity |

## Resimulation

The manager automatically handles resimulation. Each tick, it queries all registered `ClientPredictedEntity` instances for their `PredictionDecision`. If any entity requests `RESIMULATE`, the manager:

1. Rewinds the `PhysicsController` to the divergence tick.
2. Snaps each affected entity to the server's recorded state at that tick.
3. Replays each intermediate tick by re-applying stored inputs and simulating physics.

`DO_RESIM` and `DO_SNAP` flags can disable resimulation or snapping globally for debugging.

## World State vs Per-Entity State

The server can send state in two modes:

- **Per-entity (default).** One message per entity per tick, sent to all connections. Set `useServerWorldStateMessage = false` and assign `serverStateSender`.
- **World state.** All entities batched into one message per tick per connection. Set `useServerWorldStateMessage = true` and assign `serverWorldStateSender`.

## Oversimulation Protection

`protectFromOversimulation = true` prevents the same tick from being resimulated excessively. Two sub-modes are available:

- **Interval mode** (`oversimProtectWithTickInterval = true`): enforce a minimum number of ticks between resimulations (`minTicksBetweenResims`).
- **Per-tick count mode**: limit how many times each tick ID can be resimulated (`maxTickResimulationCount`).

## Static Configuration

These static flags affect all instances:

| Flag | Default | Effect |
|---|---|---|
| `DEBUG` | `false` | Verbose logging |
| `LOG_TIMING` | `false` | Per-tick timing output |
| `DO_RESIM` | `true` | Enable resimulation |
| `DO_SNAP` | `true` | Enable snapping |
| `PREDICTION_ENABLED` | `true` | Enable full prediction |
| `IGNORE_NON_AUTH_RESIM_DECISIONS` | `false` | Ignore resim requests from non-local entities |
| `IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS` | `true` | Ignore resim requests from controllable followers |

## Diagnostics

The manager exposes a set of counters for profiling and debugging:

```csharp
manager.totalResimulations
manager.totalResimulationSteps
manager.totalTickFreezes
manager.totalDesyncToSnapCount
```

## See Also

- [Scripting API: PredictionManager](../scripting-api/PredictionManager.md)
- [Integration Tutorial](../integration-tutorial.md)
- [Entities](entities.md)
