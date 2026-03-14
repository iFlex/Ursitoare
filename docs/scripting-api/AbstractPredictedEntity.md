# AbstractPredictedEntity

**Namespace:** `Prediction`

Base class for `ClientPredictedEntity` and `ServerPredictedEntity`. Manages contributor components, input, forces, and component state.

You do not instantiate this class directly. Use `ClientPredictedEntity` or `ServerPredictedEntity`.

---

## Properties

| Type | Name | Description |
|---|---|---|
| `Rigidbody` | `rigidbody` | The Rigidbody this entity represents. |
| `uint` | `id` | Unique identifier for this entity. Read-only. |

---

## Public Methods

### PopulatePhysicsStateRecord

```csharp
public void PopulatePhysicsStateRecord(uint tickId, PhysicsStateRecord stateData)
```

Copies the current Rigidbody state (position, rotation, velocity, angular velocity) into `stateData` and sets `stateData.tickId`.

---

### LoadInput

```csharp
public void LoadInput(PredictionInputRecord input)
```

Calls `LoadInput` on each `PredictableControllableComponent` contributor in registration order. Contributors read their input values from the record sequentially.

---

### ClearInput

```csharp
public void ClearInput()
```

Calls `ClearInput` on each `PredictableControllableComponent` contributor.

---

### ApplyForces

```csharp
public void ApplyForces()
```

Calls `ApplyForces` on each `PredictableComponent` contributor in registration order.

---

### ValidateState

```csharp
public bool ValidateState(float deltaTime, PredictionInputRecord input)
```

Calls `ValidateInput` on each `PredictableControllableComponent` contributor. Returns `false` as soon as any contributor rejects the input.

---

### SampleComponentState

```csharp
public void SampleComponentState(PhysicsStateRecord psr)
```

Calls `SampleComponentState` on each stateful `PredictableComponent` contributor, writing their state into `psr.componentState`.

---

### LoadComponentState

```csharp
public void LoadComponentState(PhysicsStateRecord psr)
```

Calls `LoadComponentState` on each stateful `PredictableComponent` contributor, restoring their state from `psr.componentState`.

---

### IsControllable

```csharp
public bool IsControllable()
```

Returns `true` if at least one `PredictableControllableComponent` contributor was registered.

---

### GetStateFloatCount

```csharp
public int GetStateFloatCount()
```

Returns the total number of float state values contributed by all stateful components.

---

### GetStateBoolCount

```csharp
public int GetStateBoolCount()
```

Returns the total number of bool state values contributed by all stateful components.

---

## See Also

- [ClientPredictedEntity](ClientPredictedEntity.md)
- [ServerPredictedEntity](ServerPredictedEntity.md)
- [PredictableComponent](PredictableComponent.md)
- [PredictableControllableComponent](PredictableControllableComponent.md)
