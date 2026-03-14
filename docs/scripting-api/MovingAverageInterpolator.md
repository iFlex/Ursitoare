# MovingAverageInterpolator

**Namespace:** `Prediction.Interpolation`
**Implements:** `VisualsInterpolationsProvider`

The default visual interpolator. Buffers incoming physics states and interpolates between them using a sliding window average to reduce positional noise. Uses proper quaternion averaging for rotation.

---

## Static Configuration

| Type | Name | Default | Description |
|---|---|---|---|
| `bool` | `DEBUG` | `true` | Log interpolation details each frame. |
| `bool` | `LOG_POS` | `true` | Log position and rotation on each `Add`. |
| `bool` | `DEEP_DEBUG` | `false` | Log detailed buffer state during smoothing. |
| `bool` | `USE_INTERPOLATION` | `true` | Lerp/slerp between states. If false, snap to target. |
| `bool` | `USE_SMOOTH_BUFFER` | `true` | Interpolate from the averaged buffer. If false, use raw states. |
| `bool` | `INTERPOLATE_MANUAL` | `false` | Use a custom lerp instead of the standard buffer interpolation path. |
| `int` | `BUFFER_SIZE` | `60` | Capacity of the raw state ring buffer. |
| `int` | `SMOOTH_BUFFER_SIZE` | `6` | Capacity of the averaged state ring buffer. |
| `int` | `FOLLOWER_SMOOTH_WINDOW` | `4` | Sliding window size. Applied to both locally controlled and follower entities. |
| `int` | `startAfterBfrTicks` | `2` | Minimum buffered frames before interpolation begins. |
| `float` | `ANGLE_THRESHOLD` | `120` | Angle change threshold for direction change logging (degrees). |

---

## Properties

| Type | Name | Default | Description |
|---|---|---|---|
| `int` | `slidingWindowTickSize` | `6` | Current sliding window size. |
| `bool` | `autosizeWindow` | `false` | Dynamically resize the window based on server latency. |
| `float` | `MinVisualDelay` | `0.5f` | Minimum interpolation delay when autosizing (seconds). |
| `uint` | `minVisualTickDelay` | Computed | Minimum delay in ticks (derived from `MinVisualDelay / fixedDeltaTime`). |
| `Func<uint>` | `GetServerTickLag` | `null` | Latency source for autosizing. |
| `RingBuffer<PhysicsStateRecord>` | `averagedBuffer` | — | The smoothed state buffer used for interpolation. |

---

## Constructor

```csharp
public MovingAverageInterpolator()
```

Creates an interpolator with default settings.

---

## Public Methods

### ConfigureWindowAutosizing

```csharp
public void ConfigureWindowAutosizing(Func<uint> serverLatencyFetcher)
```

Enables or disables automatic window sizing. Pass `null` to disable. When enabled, pass a function that returns the current server tick lag; the window size adjusts each frame. Note: the autosizing code is currently disabled by a comment.

---

### Add

```csharp
public void Add(PhysicsStateRecord record)
```

Deep-copies the record, appends it to the raw buffer, and computes a new averaged state for the smooth buffer. The averaged state is computed by `GetNextProcessedState`.

---

### Update

```csharp
public void Update(float deltaTime, uint currentTick)
```

Advances the visual position. Finds the two buffered states that bracket the current playback time and lerps/slerps between them. Waits until `startAfterBfrTicks` records have been buffered before beginning.

---

### GetNextProcessedState

```csharp
public PhysicsStateRecord GetNextProcessedState()
```

Computes the smoothed state for the latest raw entry by averaging position over the last `slidingWindowTickSize` raw states. Uses `QuaternionAverage` for rotation. Returns a new `PhysicsStateRecord`.

---

### SetInterpolationTarget

```csharp
public void SetInterpolationTarget(Transform t)
```

Sets the `Transform` to move.

---

### Reset

```csharp
public void Reset()
```

Clears the raw buffer.

---

### SetControlledLocally

```csharp
public void SetControlledLocally(bool isLocalAuthority)
```

Sets `slidingWindowTickSize` to `FOLLOWER_SMOOTH_WINDOW` in both modes.

---

### Static Utilities

#### GetDirVector

```csharp
public static Vector3 GetDirVector(PhysicsStateRecord from, PhysicsStateRecord to)
```

Returns `to.position - from.position`.

#### GetWithOffset

```csharp
public static PhysicsStateRecord GetWithOffset(RingBuffer<PhysicsStateRecord> bfr, int offset)
```

Returns the record at `endIndex + offset`, wrapping around the ring buffer.

#### GetBufferEndAngle

```csharp
public static float GetBufferEndAngle(RingBuffer<PhysicsStateRecord> bfr)
```

Returns the angle between the last two direction vectors in the buffer.

---

## See Also

- [VisualsInterpolationsProvider](VisualsInterpolationsProvider.md)
- [QuaternionAverage](QuaternionAverage.md)
- [PredictedEntityVisuals](PredictedEntityVisuals.md)
- [Manual: Visual Interpolation](../manual/visual-interpolation.md)
