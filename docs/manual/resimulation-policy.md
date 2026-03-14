# Resimulation Policy

## Overview

Each tick, `PredictionManager` asks every registered `ClientPredictedEntity` whether it needs to resimulate. The entity delegates this question to a `SingleSnapshotInstanceResimChecker`. The checker compares the client's local physics snapshot against the server's snapshot **at the same tick ID** and returns a `PredictionDecision`.

This comparison is possible because the server tags every state it sends with the **client's tick ID**. The client looks up its own stored prediction at that tick and compares the two.

```
Server state arrives: tickId = 42, position = (3.1, 0, 2.8)
                                         │
Client looks up local state at tick 42:  │
  localStateBuffer[42] = (3.0, 0, 2.7)  │
                                         ▼
  Checker compares:
    position distance = 0.14  →  exceeds threshold (0.0001)
    → return RESIMULATE
```

## PredictionDecision

`PredictionDecision` is an enum with four values:

| Value | Meaning |
|---|---|
| `NOOP` | States match. No action needed. |
| `RESIMULATE` | Desync detected. Client rewinds and replays from the divergence tick. |
| `SNAP` | Desync is large enough to teleport directly to server state. No replay. |
| `SIMULATION_FREEZE` | Server snapshot is older than local history. Pause simulation until server catches up. |

The `PredictionManager` takes the highest-severity decision across all entities.

## SimpleConfigurableResimulationDecider

`SimpleConfigurableResimulationDecider` is the default checker. It compares four fields between the local and server states:

| Field | Default threshold |
|---|---|
| Position distance | 0.0001 |
| Rotation angle (degrees) | 0.0001 |
| Velocity magnitude delta | 0.001 |
| Angular velocity magnitude delta | 0.001 |

If any field exceeds its threshold, `RESIMULATE` is returned. A threshold of `0` or below disables checking for that field.

### Custom Thresholds

```csharp
PredictionManager.SNAPSHOT_INSTANCE_RESIM_CHECKER =
    new SimpleConfigurableResimulationDecider(
        distResimThreshold:    0.01f,
        rotResimThreshold:     0.1f,
        veloResimThreshold:    0.05f,
        angVeloResimThreshold: 0.05f
    );
```

### Diagnostics

The decider tracks running totals for all four fields:

```csharp
var decider = (SimpleConfigurableResimulationDecider)PredictionManager.SNAPSHOT_INSTANCE_RESIM_CHECKER;
float avgDistError = decider._avgDistD / decider._checkCount;
float maxDistError = decider._MaxDistD;
```

### Logging

```csharp
SimpleConfigurableResimulationDecider.LOG_RESIMULATIONS = true; // log every resim trigger
SimpleConfigurableResimulationDecider.LOG_ALL_CHECKS    = true; // log every check
```

## Custom Resimulation Policy

Implement `SingleSnapshotInstanceResimChecker` to write your own comparison logic:

```csharp
public class MyResimChecker : SingleSnapshotInstanceResimChecker
{
    public PredictionDecision Check(
        uint entityId,
        uint tickId,
        PhysicsStateRecord local,
        PhysicsStateRecord server)
    {
        if ((local.position - server.position).sqrMagnitude > 0.01f)
            return PredictionDecision.RESIMULATE;
        return PredictionDecision.NOOP;
    }
}

PredictionManager.SNAPSHOT_INSTANCE_RESIM_CHECKER = new MyResimChecker();
```

You can also override the checker at the per-entity level:

```csharp
clientEntity.SetSingleStateEligibilityCheckHandler(myChecker.Check);
clientEntity.SetCustomEligibilityCheckHandler(myFullHistoryHandler); // full buffer access
```

## Desync Events

`ClientPredictedEntity` fires a `potentialDesync` event for observable conditions that may indicate a desync, even when no resimulation is triggered:

| Reason | Meaning |
|---|---|
| `MISSING_SERVER_COMPARISON` | No server snapshot available yet |
| `GAP_IN_SERVER_STREAM` | Missing ticks in the server update stream |
| `SERVER_AHEAD_OF_CLIENT` | Server tick is ahead of client tick |
| `SNAP_TO_SERVER_NO_DATA` | Snap requested but no server state for that tick |

`ServerPredictedEntity` fires its own `potentialDesync` event for input-side conditions:

| Reason | Meaning |
|---|---|
| `NO_INPUT_FOR_SERVER_TICK` | No input arrived for this tick |
| `INPUT_BUFFERED` | Still buffering, not yet processing |
| `INPUT_JUMP` | Client tick ID jumped non-sequentially |
| `MULTIPLE_INPUTS_PER_FRAME` | More than one input dequeued in a tick |
| `INVALID_INPUT` | Input failed server-side validation |
| `LATE_TICK` | Input arrived after its tick was already processed |
| `TICK_OVERFLOW` | Input queue exceeded capacity |
| `CATCHUP` | Server is processing multiple inputs to catch up |

## See Also

- [Scripting API: SingleSnapshotInstanceResimChecker](../scripting-api/SingleSnapshotInstanceResimChecker.md)
- [Scripting API: SimpleConfigurableResimulationDecider](../scripting-api/SimpleConfigurableResimulationDecider.md)
- [Scripting API: PredictionDecision](../scripting-api/PredictionDecision.md)
- [Scripting API: ClientPredictedEntity](../scripting-api/ClientPredictedEntity.md)
