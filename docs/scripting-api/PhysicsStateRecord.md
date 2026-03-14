# PhysicsStateRecord

**Namespace:** `Prediction.data`

One snapshot of a `Rigidbody`'s physics state at a specific tick. Used for history storage, server-to-client broadcast, and resimulation.

Do not use the default constructor. Use the static factory methods.

---

## Fields

| Type | Name | Description |
|---|---|---|
| `uint` | `tickId` | The tick this record was captured at. |
| `Vector3` | `position` | Rigidbody position. |
| `Quaternion` | `rotation` | Rigidbody rotation. Default: `Quaternion.identity`. |
| `Vector3` | `velocity` | Rigidbody linear velocity. |
| `Vector3` | `angularVelocity` | Rigidbody angular velocity. |
| `PredictionInputRecord` | `input` | The input applied during this tick. May be `null`. |
| `PredictionInputRecord` | `componentState` | Serialised state of stateful `PredictableComponent` contributors. May be `null`. |

---

## Static Methods

### AllocWithComponentState

```csharp
public static PhysicsStateRecord AllocWithComponentState(int componentFloats, int componentBools)
```

Creates a record and allocates a `componentState` buffer with the given capacity. Use this when the entity has stateful components.

---

### Alloc

```csharp
public static PhysicsStateRecord Alloc()
```

Creates an empty record without a component state buffer.

---

### Empty

```csharp
public static PhysicsStateRecord Empty()
```

Creates a zeroed record with identity rotation and no buffers. Same as `Alloc`.

---

## Public Methods

### From(Rigidbody)

```csharp
public void From(Rigidbody rigidbody)
```

Copies position, rotation, velocity, and angular velocity from the given `Rigidbody` into this record.

---

### From(PhysicsStateRecord)

```csharp
public void From(PhysicsStateRecord record)
```

Deep-copies all fields from another record, including `input` and `componentState` arrays if present.

---

### From(PhysicsStateRecord, uint)

```csharp
public void From(PhysicsStateRecord record, uint tickOverride)
```

Same as `From(PhysicsStateRecord)` but overrides `tickId` with `tickOverride`.

---

### To

```csharp
public void To(Rigidbody r)
```

Applies position, rotation, velocity, and angular velocity from this record to the given `Rigidbody`.

---

## See Also

- [PredictionInputRecord](PredictionInputRecord.md)
- [WorldStateRecord](WorldStateRecord.md)
- [ClientPredictedEntity](ClientPredictedEntity.md)
- [ServerPredictedEntity](ServerPredictedEntity.md)
