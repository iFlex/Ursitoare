# PredictedEntityVisuals

**Namespace:** `Prediction`
**Inherits:** `MonoBehaviour`

Drives a detached visual child object using an interpolated stream of physics states. Add this component to the same GameObject as the physics body.

---

## Static Properties

| Type | Name | Default | Description |
|---|---|---|---|
| `bool` | `SHOW_DBG` | `false` | Show debug ghost objects when true. |

---

## Inspector Fields

| Type | Name | Description |
|---|---|---|
| `GameObject` | `visualsEntity` | The visual child object to detach and drive. Assign in the Inspector. |
| `GameObject` | `serverGhostPrefab` | Optional. Prefab instantiated to show the server's reported position. |
| `GameObject` | `clientGhostPrefab` | Optional. Prefab instantiated to show the raw physics body position. |

---

## Properties

| Type | Name | Description |
|---|---|---|
| `VisualsInterpolationsProvider` | `interpolationProvider` | The active interpolation provider. Set by `SetClientPredictedEntity`. Read-only. |
| `bool` | `hasVIP` | Indicates that a `VisualsInterpolationsProvider` is active. |
| `double` | `artifficialDelay` | Offset applied to the interpolation timeline on setup. Default: `1.0`. |

---

## Public Methods

### SetClientPredictedEntity

```csharp
public void SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity, VisualsInterpolationsProvider provider)
```

Connects this component to a `ClientPredictedEntity`. Detaches `visualsEntity` from its parent, assigns the interpolation provider, and subscribes to the entity's `newStateReached` event.

Do not call this on the server.

---

### SetServerPredictedEntity

```csharp
public void SetServerPredictedEntity(Transform serverPredictedEntity)
```

Connects this component to a server entity's transform. Detaches visuals and tracks the server transform position directly without interpolation.

Call this on the server when the visuals object carries colliders that must not interfere with the physics body.

---

### SetControlledLocally

```csharp
public void SetControlledLocally(bool ctlLoc)
```

Forwards the ownership flag to the interpolation provider. Called automatically when the entity's `onReset` fires.

---

### Reset

```csharp
public void Reset()
```

Resets the interpolation provider's buffer. Called automatically when the entity's `onReset` fires.

---

### GetInterpolationDistance

```csharp
public float GetInterpolationDistance()
```

Returns the distance between the visual object's current position and the physics body's position.

---

### Destroy

```csharp
public void Destroy(bool ignore)
```

Destroys the `visualsEntity` GameObject.

---

## Update Behaviour

Each frame, `Update` does the following:

1. Shows or hides debug ghosts based on `SHOW_DBG`.
2. If a server ghost is active, positions it at the latest server state.
3. Calls `interpolationProvider.Update(Time.deltaTime, currentTickId)` to advance the visual position.

---

## See Also

- [Manual: Visual Interpolation](../manual/visual-interpolation.md)
- [VisualsInterpolationsProvider](VisualsInterpolationsProvider.md)
- [MovingAverageInterpolator](MovingAverageInterpolator.md)
- [ClientPredictedEntity](ClientPredictedEntity.md)
