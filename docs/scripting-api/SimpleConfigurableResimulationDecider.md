# SimpleConfigurableResimulationDecider

**Namespace:** `Prediction.policies.singleInstance`
**Implements:** `SingleSnapshotInstanceResimChecker`

Threshold-based snapshot comparison. Returns `RESIMULATE` when the difference between a local and server snapshot exceeds any configured threshold. Accumulates statistics for diagnostics.

---

## Static Configuration

| Type | Name | Default | Description |
|---|---|---|---|
| `bool` | `LOG_RESIMULATIONS` | `false` | Log each check that triggers a resimulation. |
| `bool` | `LOG_ALL_CHECKS` | `false` | Log every check, regardless of result. |

---

## Properties

| Type | Name | Default | Description |
|---|---|---|---|
| `float` | `distResimThreshold` | `0.0001` | Position distance threshold. Set to `0` or below to disable. |
| `float` | `rotationResimThreshold` | `0.0001` | Rotation angle threshold (degrees). Set to `0` or below to disable. |
| `float` | `veloResimThreshold` | `0.001` | Linear velocity magnitude threshold. Set to `0` or below to disable. |
| `float` | `angVeloResimThreshold` | `0.001` | Angular velocity magnitude threshold. Set to `0` or below to disable. |

---

## Diagnostic Counters

| Name | Description |
|---|---|
| `_avgDistD` | Cumulative position distance delta across all checks. |
| `_avgRotD` | Cumulative rotation delta across all checks. |
| `_avgVeloD` | Cumulative velocity delta across all checks. |
| `_avgAVeloD` | Cumulative angular velocity delta across all checks. |
| `_checkCount` | Total number of checks performed. |
| `_MaxDistD` | Maximum observed position distance delta. |
| `_MaxRotD` | Maximum observed rotation delta. |
| `_MaxVeloD` | Maximum observed velocity delta. |
| `_MaxAVeloD` | Maximum observed angular velocity delta. |

---

## Constructors

### SimpleConfigurableResimulationDecider()

```csharp
public SimpleConfigurableResimulationDecider()
```

Creates a decider with default thresholds.

---

### SimpleConfigurableResimulationDecider(float, float, float, float)

```csharp
public SimpleConfigurableResimulationDecider(
    float distResimThreshold,
    float rotResimThreshold,
    float veloResimThreshold,
    float angVeloResimThreshold)
```

Creates a decider with custom thresholds. Pass a value ≤ 0 to disable checking for a field.

---

## Public Methods

### Check

```csharp
public virtual PredictionDecision Check(
    uint entityId,
    uint tickId,
    PhysicsStateRecord local,
    PhysicsStateRecord server)
```

Compares the four fields. Returns `RESIMULATE` if any enabled threshold is exceeded. Returns `NOOP` otherwise. Updates all diagnostic counters on every call.

---

### Log

```csharp
public void Log(uint entityId, uint tickId, float distD, float angD, float vdelta, float avdelta, bool isResim)
```

Writes a formatted log line for the given check result.

---

## See Also

- [SingleSnapshotInstanceResimChecker](SingleSnapshotInstanceResimChecker.md)
- [PredictionDecision](PredictionDecision.md)
- [Manual: Resimulation Policy](../manual/resimulation-policy.md)
