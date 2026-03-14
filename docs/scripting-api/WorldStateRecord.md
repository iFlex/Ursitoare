# WorldStateRecord

**Namespace:** `Prediction.data`

Batched physics state for all entities in a single tick. Used when `PredictionManager.useServerWorldStateMessage = true`.

The server builds this record during the post-simulation step and sends it to each client. The client unpacks it by calling `PredictionManager.OnServerWorldStateReceived`.

---

## Fields

| Type | Name | Description |
|---|---|---|
| `uint` | `tickId` | The client's acknowledged tick ID. Used to timestamp the record relative to the receiving client. |
| `uint` | `serverTickId` | The server's own tick ID when this record was captured. |
| `uint[]` | `entityIDs` | Entity IDs for each state in this record. Length equals the allocated capacity. |
| `PhysicsStateRecord[]` | `states` | Physics states, one per entity. Length equals the allocated capacity. |
| `int` | `fill` | Number of valid entries in `entityIDs` and `states`. |

---

## Public Methods

### WriteReset

```csharp
public void WriteReset()
```

Resets `fill` to 0. Call at the start of each tick before accumulating states.

---

### Resize

```csharp
public void Resize(int totalSize)
```

Reallocates `entityIDs` and `states` arrays to the given size. Called automatically by the manager when entities are added or removed.

---

### Set

```csharp
public void Set(uint id, PhysicsStateRecord stateRecord)
```

Appends an entity ID and state at the current fill position and increments `fill`.

---

## Notes

When reading on the client, iterate `entityIDs[0..fill]` and `states[0..fill]`. Do not read beyond `fill`.

---

## See Also

- [PhysicsStateRecord](PhysicsStateRecord.md)
- [PredictionManager](PredictionManager.md)
