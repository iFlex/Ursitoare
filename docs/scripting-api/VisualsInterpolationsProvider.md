# VisualsInterpolationsProvider

**Namespace:** `Prediction.Interpolation`

Interface for visual interpolation. Drives a detached visual `Transform` each frame by consuming a stream of `PhysicsStateRecord` snapshots.

Assign a factory for instances to `PredictionManager.INTERPOLATION_PROVIDER`.

---

## Methods

### Add

```csharp
void Add(PhysicsStateRecord record)
```

Buffer an incoming physics state. Called once per tick by `PredictedEntityVisuals` when `ClientPredictedEntity.newStateReached` fires.

---

### Update

```csharp
void Update(float deltaTime, uint currentTick)
```

Advance the visual position. Called once per frame from `PredictedEntityVisuals.Update`. Move the interpolation target based on elapsed time and buffered states.

---

### SetInterpolationTarget

```csharp
void SetInterpolationTarget(Transform t)
```

Set the `Transform` to drive. Called once during `PredictedEntityVisuals.SetClientPredictedEntity`.

---

### Reset

```csharp
void Reset()
```

Clear all buffered state. Called when the entity resets (e.g., on ownership change).

---

### SetControlledLocally

```csharp
void SetControlledLocally(bool isLocalAuthority)
```

Inform the interpolator whether the entity is locally controlled. Implementations may adjust smoothing parameters.

---

### GetVisualDelay

```csharp
double GetVisualDelay()
```

How far behind the newest state passed to `Add` the visuals were drawn in the last `Update`, in seconds. Smoothing that trails the newest state counts as delay. Returns `NaN` until something has been drawn, or when the drawing mode has no single moment it shows.

`Add` is called once per simulated tick, so this is the delay behind the newest simulated tick. Add the time elapsed since that tick to get the delay against the present.

---

## See Also

- [MovingAverageInterpolator](MovingAverageInterpolator.md)
- [PredictedEntityVisuals](PredictedEntityVisuals.md)
- [Manual: Visual Interpolation](../manual/visual-interpolation.md)
