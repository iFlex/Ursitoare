# SingleSnapshotInstanceResimChecker

**Namespace:** `Prediction.policies.singleInstance`

Interface for snapshot comparison logic. Compares one local `PhysicsStateRecord` against one server `PhysicsStateRecord` and returns a `PredictionDecision`.

Assign an implementation to `PredictionManager.SNAPSHOT_INSTANCE_RESIM_CHECKER`.

---

## Methods

### Check

```csharp
PredictionDecision Check(
    uint entityId,
    uint tickId,
    PhysicsStateRecord local,
    PhysicsStateRecord server)
```

Compare `local` and `server` and return the appropriate action.

| Parameter | Description |
|---|---|
| `entityId` | ID of the entity being checked. |
| `tickId` | The tick both states correspond to. |
| `local` | The client's predicted state at this tick. |
| `server` | The server's authoritative state at this tick. |

Return `PredictionDecision.NOOP` if the states are close enough. Return `PredictionDecision.RESIMULATE` if they diverge. Return `PredictionDecision.SNAP` for a large divergence that should skip resimulation.

---

## See Also

- [SimpleConfigurableResimulationDecider](SimpleConfigurableResimulationDecider.md)
- [PredictionDecision](PredictionDecision.md)
- [Manual: Resimulation Policy](../manual/resimulation-policy.md)
