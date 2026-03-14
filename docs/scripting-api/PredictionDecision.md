# PredictionDecision

**Namespace:** `Prediction`

Returned by `ClientPredictedEntity.GetPredictionDecision` and `SingleSnapshotInstanceResimChecker.Check`. Describes the action the `PredictionManager` should take in response to a desync check.

---

## Values

| Value | Int | Description |
|---|---|---|
| `NOOP` | `0` | No desync detected. No action needed. |
| `RESIMULATE` | `1` | Desync detected. Rewind to the divergence tick and replay. |
| `SNAP` | `2` | Desync is too large to resimulate. Teleport directly to the server state. |
| `SIMULATION_FREEZE` | `3` | The target tick is older than local history. Pause simulation until the server catches up. |

The `PredictionManager` takes the highest-severity decision across all entities. Severity order: `NOOP` < `RESIMULATE` < `SNAP` < `SIMULATION_FREEZE`.

---

## See Also

- [SingleSnapshotInstanceResimChecker](SingleSnapshotInstanceResimChecker.md)
- [SimpleConfigurableResimulationDecider](SimpleConfigurableResimulationDecider.md)
- [ClientPredictedEntity](ClientPredictedEntity.md)
- [Manual: Resimulation Policy](../manual/resimulation-policy.md)
