# CustomVisualInterpolator

`Adapters.Prediction.CustomVisualInterpolator`

Implements: `Prediction.Interpolation.VisualsInterpolationsProvider`

## Overview

CustomVisualInterpolator is a sliding-window interpolation implementation for smoothing predicted entity visuals. It receives physics state snapshots each tick, optionally smooths them using a moving average over a configurable window, and interpolates between frames using linear position interpolation (`Vector3.Lerp`) and spherical rotation interpolation (`Quaternion.Slerp`).

The interpolator maintains two internal buffers: a raw state buffer and a smoothed (averaged) buffer. The smoothed buffer applies a sliding window average over the most recent N states, which helps eliminate jitter from resimulation corrections. The interpolator then walks through whichever buffer is active to find two bounding frames and computes the interpolation factor based on elapsed time.

## How It Works With Other Components

- **PredictedEntityVisuals**: Creates the interpolator, calls `SetInterpolationTarget` with the detached visuals Transform, feeds states via `Add`, and calls `Update` each frame.
- **ClientPredictedEntity**: The entity's `newStateReached` event is connected to `PredictedEntityVisuals`, which forwards states to this interpolator via `Add`.
- **PhysicsStateRecord**: The interpolator stores and interpolates between PhysicsStateRecord instances.

## Usage

```csharp
// Typically created via the factory:
PredictionManager.INTERPOLATION_PROVIDER = () => new CustomVisualInterpolator();

// Or manually:
var interpolator = new CustomVisualInterpolator();
interpolator.SetInterpolationTarget(visualsTransform);
// States are added each tick:
interpolator.Add(physicsState);
// Called each Update():
interpolator.Update(Time.deltaTime, currentTick);
```

### Window Autosizing

The interpolator supports dynamic window sizing based on server latency:

```csharp
interpolator.ConfigureWindowAutosizing(() => entity.GetServerDelay());
```

When enabled, the sliding window size adapts to the network latency, providing more smoothing at higher latencies.

## Public Methods

### `Update(float deltaTime, uint currentTick)`
Advances the interpolation time and updates the target Transform's position and rotation. Called each `Update()` frame.
- **Parameters**: `deltaTime` - frame delta time; `currentTick` - the current simulation tick.
- **Returns**: void

### `Add(PhysicsStateRecord record)`
Adds a new physics state to the interpolation buffer. Deep-copies the record. Also computes and adds a smoothed entry to the averaged buffer.
- **Parameters**: `record` - the physics state to add.
- **Returns**: void

### `SetInterpolationTarget(Transform t)`
Sets the Transform that will be positioned/rotated by the interpolator.
- **Parameters**: `t` - the target Transform (typically the detached visuals object).
- **Returns**: void

### `Reset()`
Clears the raw state buffer, resetting interpolation. Call when the entity resets.
- **Returns**: void

### `SetControlledLocally(bool isLocalAuthority)`
Adjusts the sliding window size based on whether this entity is locally controlled.
- **Parameters**: `isLocalAuthority` - true if locally controlled.
- **Returns**: void

### `ConfigureWindowAutosizing(Func<uint> serverLatencyFetcher)`
Enables or disables dynamic window sizing. Pass null to disable.
- **Parameters**: `serverLatencyFetcher` - function returning the current server delay in ticks, or null.
- **Returns**: void

### `GetNextProcessedState() -> PhysicsStateRecord`
Computes the next smoothed state by averaging the last `slidingWindowTickSize` entries in the raw buffer.
- **Returns**: `PhysicsStateRecord` - the averaged state.

### `GetDirVector(PhysicsStateRecord from, PhysicsStateRecord to) -> Vector3` (static)
Returns the direction vector between two states' positions.
- **Parameters**: `from`, `to` - the two states.
- **Returns**: `Vector3`

### `GetBufferEndAngle(RingBuffer<PhysicsStateRecord> bfr) -> float` (static)
Computes the angle change at the end of a buffer (between the last three entries).
- **Parameters**: `bfr` - the buffer.
- **Returns**: `float` - angle in degrees.

### `GetWithOffset(RingBuffer<PhysicsStateRecord> bfr, int offset) -> PhysicsStateRecord` (static)
Gets a record from the buffer relative to the end position.
- **Parameters**: `bfr` - the buffer; `offset` - negative offset from the end.
- **Returns**: `PhysicsStateRecord`

### `AddToWindow(PhysicsStateRecord accumulator, PhysicsStateRecord newItem)` (static)
Accumulates a position into the sliding window accumulator and copies the rotation.
- **Parameters**: `accumulator` - running total; `newItem` - state to add.
- **Returns**: void

### `FinalizeWindow(PhysicsStateRecord accumulator, int count)` (static)
Divides the accumulated position by the window count to compute the average.
- **Parameters**: `accumulator` - the accumulated state; `count` - number of entries.
- **Returns**: void

## Configuration Flags

| Flag | Type | Default | Description |
|---|---|---|---|
| `DEBUG` | `static bool` | `true` | Enables debug logging for interpolation state. |
| `LOG_POS` | `static bool` | `true` | Logs position and rotation of each added state. |
| `DEEP_DEBUG` | `static bool` | `false` | Enables very verbose logging of buffer internals. |
| `FOLLOWER_SMOOTH_WINDOW` | `static int` | `4` | Default sliding window size for follower entities. |
| `USE_INTERPOLATION` | `static bool` | `true` | When true, uses Lerp/Slerp between frames. When false, snaps to the target frame. |
| `USE_SMOOTH_BUFFER` | `static bool` | `true` | When true, interpolates from the averaged buffer. When false, uses the raw buffer. |
| `BUFFER_SIZE` | `static int` | `60` | Capacity of the raw state buffer. |
| `SMOOTH_BUFFER_SIZE` | `static int` | `6` | Capacity of the smoothed/averaged buffer. |
| `INTERPOLATE_MANUAL` | `static bool` | `false` | Reserved for manual interpolation mode. |
| `startAfterBfrTicks` | `static int` | `2` | Minimum number of entries in the interpolation buffer before interpolation begins. |
| `slidingWindowTickSize` | `int` | `6` | Number of ticks in the sliding average window. Adjusted by `SetControlledLocally`. |
| `autosizeWindow` | `bool` | `false` | When true, the sliding window size adapts to server latency. |
| `MinVisualDelay` | `float` | `0.5` | Minimum visual delay in seconds (used for autosize lower bound). |
| `minVisualTickDelay` | `uint` | computed | Minimum visual delay in ticks (derived from `MinVisualDelay / fixedDeltaTime`). |
| `ANGLE_THRESHOLD` | `static float` | `120` | Angle threshold (degrees) for logging direction change breaches. |
