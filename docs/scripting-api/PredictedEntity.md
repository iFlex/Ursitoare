# PredictedEntity

**Namespace:** `Prediction.Wrappers`

Interface for a MonoBehaviour wrapper that ties together a `ClientPredictedEntity`, a `ServerPredictedEntity`, and their visual component. Provides a single point of access for registering and deregistering a predicted object.

---

## Methods

### GetId

```csharp
uint GetId()
```

Returns the unique entity ID.

---

### GetOwnerId

```csharp
int GetOwnerId()
```

Returns the connection ID of the current owner. Returns `PredictionManager.INVALID_CONNECTION_ID` if unowned.

---

### GetClientEntity

```csharp
ClientPredictedEntity GetClientEntity()
```

Returns the `ClientPredictedEntity` for this object. May be `null` on a server-only instance.

---

### GetServerEntity

```csharp
ServerPredictedEntity GetServerEntity()
```

Returns the `ServerPredictedEntity` for this object. May be `null` on a client-only instance.

---

### GetVisualsControlled

```csharp
PredictedEntityVisuals GetVisualsControlled()
```

Returns the `PredictedEntityVisuals` component driving this entity's visuals.

---

### GetRigidbody

```csharp
Rigidbody GetRigidbody()
```

Returns the Rigidbody for this entity.

---

### IsServer

```csharp
bool IsServer()
```

Returns `true` if this instance has a server entity.

---

### IsClient

```csharp
bool IsClient()
```

Returns `true` if this instance has a client entity.

---

### IsClientOnly (default implementation)

```csharp
bool IsClientOnly()
```

Returns `true` if `IsClient()` is true and `IsServer()` is false.

---

### Register (default implementation)

```csharp
void Register()
```

Registers the client and/or server entity with `PredictionManager.Instance`. Call after construction.

---

### Deregister (default implementation)

```csharp
void Deregister()
```

Calls `PredictionManager.Instance.RemovePredictedEntity(GetId())`. Call before destroying the GameObject.

---

### ApplyClientForce (default implementation)

```csharp
void ApplyClientForce(Action<Rigidbody> applier)
```

Applies a force to the Rigidbody, but only if this is a client-only instance. Prevents force application on the server.

---

## See Also

- [ClientPredictedEntity](ClientPredictedEntity.md)
- [ServerPredictedEntity](ServerPredictedEntity.md)
- [PredictedEntityVisuals](PredictedEntityVisuals.md)
- [PredictionManager](PredictionManager.md)
