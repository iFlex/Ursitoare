# Visual Interpolation

## Overview

Physics simulation runs at a fixed tick rate, but rendering runs at the display frame rate. Without interpolation, objects would visibly stutter. Ursitoare decouples visuals from physics by separating the visual `GameObject` from the physics body and driving it with an interpolator in `Update`.

## Detached Visuals

When `PredictedEntityVisuals.SetClientPredictedEntity` is called, it detaches the `visualsEntity` from its parent transform. From that point on, the visuals object moves independently of the physics body. The `VisualsInterpolationsProvider` updates its position and rotation each frame.

On the server you may also detach visuals using `SetServerPredictedEntity`. This is useful when visuals carry colliders that should not interact differently on client and server.

## PredictedEntityVisuals

`PredictedEntityVisuals` is a MonoBehaviour you add to your entity's GameObject. It holds a reference to the visual child object and drives it each `Update`.

Set it up after constructing the `ClientPredictedEntity`:

```csharp
var visuals = entityGameObject.GetComponent<PredictedEntityVisuals>();
var interpolator = PredictionManager.INTERPOLATION_PROVIDER();
visuals.SetClientPredictedEntity(clientEntity, interpolator);
```

`PredictionManager.INTERPOLATION_PROVIDER` is a factory `Func`. Set it before `Setup` to control which interpolator all entities use:

```csharp
PredictionManager.INTERPOLATION_PROVIDER = () => new MovingAverageInterpolator();
```

## MovingAverageInterpolator

`MovingAverageInterpolator` is the default interpolator. It buffers incoming physics states and interpolates between them using a sliding window average to smooth out positional noise.

### How It Works

Each time `ClientPredictedEntity.newStateReached` fires (once per tick), the interpolator receives the new `PhysicsStateRecord`. It applies a sliding window average over the last `slidingWindowTickSize` states and stores the result in a smoothed buffer. During each `Update`, it interpolates between the two closest states in the smoothed buffer based on elapsed time.

The visual object always runs slightly behind the simulation to ensure there are always at least two states to interpolate between.

### Configuration

| Property | Default | Description |
|---|---|---|
| `slidingWindowTickSize` | 6 | Number of ticks in the smoothing window |
| `USE_INTERPOLATION` | `true` | Lerp/slerp between states. If false, snap to target. |
| `USE_SMOOTH_BUFFER` | `true` | Use the averaged buffer. If false, use raw states. |
| `BUFFER_SIZE` | 60 | Capacity of the raw state buffer |
| `SMOOTH_BUFFER_SIZE` | 6 | Capacity of the averaged state buffer |
| `startAfterBfrTicks` | 2 | Minimum buffered frames before interpolation begins |
| `FOLLOWER_SMOOTH_WINDOW` | 4 | Window size used for locally controlled entities |

### Debug Ghosts

`PredictedEntityVisuals` supports optional debug ghost objects:

- **Server ghost.** Instantiated from `serverGhostPrefab`. Shows where the server believes the entity is. Visible when `PredictedEntityVisuals.SHOW_DBG = true`.
- **Client ghost.** Instantiated from `clientGhostPrefab`. Shows the raw physics body position.

Assign the prefabs in the Inspector.

## Implementing a Custom Interpolator

Implement `VisualsInterpolationsProvider`:

```csharp
public class MyInterpolator : VisualsInterpolationsProvider
{
    public void Add(PhysicsStateRecord record) { /* buffer incoming state */ }
    public void Update(float deltaTime, uint currentTick) { /* move the visual target */ }
    public void SetInterpolationTarget(Transform t) { /* store the transform to drive */ }
    public void Reset() { /* clear state, e.g. on ownership change */ }
    public void SetControlledLocally(bool isLocalAuthority) { /* adjust for local vs follower */ }
}

PredictionManager.INTERPOLATION_PROVIDER = () => new MyInterpolator();
```

## See Also

- [Scripting API: PredictedEntityVisuals](../scripting-api/PredictedEntityVisuals.md)
- [Scripting API: VisualsInterpolationsProvider](../scripting-api/VisualsInterpolationsProvider.md)
- [Scripting API: MovingAverageInterpolator](../scripting-api/MovingAverageInterpolator.md)
