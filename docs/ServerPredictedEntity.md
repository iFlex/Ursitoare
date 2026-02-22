# ServerPredictedEntity

`Prediction.ServerPredictedEntity`

## Overview

ServerPredictedEntity is the server-side representation of a networked predicted object. It receives and buffers client input, runs the authoritative physics simulation, handles input catchup when the server falls behind, and provides the physics state that is broadcast back to all clients.

It extends `AbstractPredictedEntity`, which provides common logic for input loading, force application, component state sampling, and physics state population.

## How It Works With Other Components

- **PredictionManager** drives the entity each tick:
  - `ServerSimulationTick()` during server pre-sim: consumes buffered input, validates and loads it, applies forces, handles catchup.
  - `SamplePhysicsState()` during server post-sim: captures the authoritative state and attaches the input record for replication to clients.
- **PredictableControllableComponent[]**: Used for `LoadInput` and `ValidateInput` when processing client input.
- **PredictableComponent[]**: Used for `ApplyForces` each tick.
- The manager calls `BufferClientTick()` when a client input message arrives via the network.

## Usage

```csharp
var serverEntity = new ServerPredictedEntity(
    id: networkId,
    bufferSize: 128,
    rb: GetComponent<Rigidbody>(),
    visuals: null,  // server typically has no visuals
    controllablePredictionContributors: new PredictableControllableComponent[] { movementInput },
    predictionContributors: new PredictableComponent[] { thruster, gravity }
);

PredictionManager.Instance.AddPredictedEntity(serverEntity);
PredictionManager.Instance.SetEntityOwner(serverEntity, connectionId);
```

## Input Processing Flow

Each `ServerSimulationTick()`:

1. If buffering is active and the buffer hasn't reached `BUFFER_FULL_THRESHOLD`, forces are applied without consuming input.
2. Otherwise, `HandleTickInput()` iterates the input queue:
   - Skips inputs with tick ids older than the server's current tick.
   - When it finds an input at or ahead of the current tick, it updates the server tick id to match, validates the input, loads it, and applies forces.
3. If `CATCHUP` is enabled and the input queue has grown large, additional simulation steps are taken in a single frame to reduce the queue.

## Public Methods

### Constructor
`ServerPredictedEntity(uint id, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors)`
- **Parameters**: `id` - unique entity id; `bufferSize` - capacity of the input queue; `rb` - the Rigidbody; `visuals` - optional visuals object; `controllablePredictionContributors` - input components; `predictionContributors` - force components.

### `ServerSimulationTick() -> uint`
Main simulation entry point called by the manager each tick. Processes buffered input, applies forces, handles catchup.
- **Returns**: `uint` - the tick id after processing.

### `SamplePhysicsState() -> PhysicsStateRecord`
Samples the current Rigidbody state and component state after physics simulation. Attaches the consumed input record.
- **Returns**: `PhysicsStateRecord` - the authoritative state to broadcast to clients.

### `BufferClientTick(uint clientTickId, PredictionInputRecord inputRecord)`
Called by the manager when a client input message arrives. Buffers the input in the tick-indexed queue.
- **Parameters**: `clientTickId` - the client's tick for this input; `inputRecord` - the input data.
- **Returns**: void

### `ResetClientState()`
Clears the input queue and resets the tick counter. Use when changing the controller of the entity.
- **Returns**: void

### `Reset()`
Clears all input queues and resets tick counters and stats. Use when changing entity ownership.
- **Returns**: void

### `GetTickId() -> uint`
Returns the server entity's current tick id.
- **Returns**: `uint`

### `BufferFill() -> int`
Returns the current number of buffered inputs.
- **Returns**: `int`

### `BufferSize() -> uint`
Returns the range of the input buffer (difference between newest and oldest tick).
- **Returns**: `uint`

### `TickDeltaToTimeDelta(int delta) -> float`
Converts a tick delta to a time delta in seconds.
- **Parameters**: `delta` - the tick difference.
- **Returns**: `float` - time in seconds.

### `IsControllable() -> bool`
Returns true if the entity has any `PredictableControllableComponent` contributors.
- **Returns**: `bool`

## Configuration Flags

| Flag | Type | Default | Description |
|---|---|---|---|
| `DEBUG` | `static bool` | `false` | Enables verbose debug logging. |
| `APPLY_FORCES_TO_EACH_CATCHUP_INPUT` | `static bool` | `false` | When true, forces are applied for each catchup input individually. |
| `USE_BUFFERING` | `static bool` | `true` | Enables input buffering: the server waits for a minimum number of inputs before starting to process them. |
| `BUFFER_ONCE` | `static bool` | `true` | If true, buffering only happens once at the start; once the buffer fills, it stays disabled. |
| `BUFFER_FULL_THRESHOLD` | `static int` | `3` | Number of buffered ticks required before the server starts consuming input. |
| `CATCHUP` | `static bool` | `true` | Enables catchup: when the input queue grows large, multiple ticks are simulated in one frame. |
| `INCREMENT_TICK_WHEN_NO_INPUT` | `static bool` | `false` | When true, the server tick id increments even when no client input is available. |
| `SERVER_LOG_VELOCITIES` | `static bool` | `false` | Logs velocity data after each tick. |
| `LOG_CLIENT_INUPTS` | `static bool` | `false` | Logs received client inputs. |
| `LOG_INPUT_QUEUE_SIZE` | `static bool` | `true` | Logs the input queue fill level each tick. |
| `APPLY_OLD_INPUTS_IN_CURRENT_TICK` | `static bool` | `false` | When true, inputs with tick ids behind the server's current tick are still applied. |
| `catchupSections` | `int` | `3` | Divides the buffer into this many sections; one extra tick is simulated per section of accumulated inputs. |
| `ticksPerCatchupSection` | `int` | computed | Number of ticks per catchup section (derived from `bufferSize / catchupSections`). |

## Events

| Event | Type | Description |
|---|---|---|
| `preSampleState` | `SafeEventDispatcher<bool>` | Fired just before physics state is sampled. |
| `firstTickArrived` | `SafeEventDispatcher<bool>` | Fired when the first client input arrives in an empty queue. |
| `potentialDesync` | `SafeEventDispatcher<DesyncEvent>` | Fired for various desync conditions (no input, late tick, catchup, invalid input, etc.). |
| `stateSampled` | `SafeEventDispatcher<bool>` | Fired after the physics state is sampled. |
| `inputReceived` | `SafeEventDispatcher<ClientInput>` | Fired when a client input is received (only when `LOG_CLIENT_INUPTS` is true). |

## DesyncReason Enum

| Value | Description |
|---|---|
| `NO_INPUT_FOR_SERVER_TICK` | No input was available when the server needed to advance. |
| `INPUT_BUFFERED` | Input is available but being buffered (not yet consumed). |
| `INPUT_JUMP` | The tick id of incoming input jumped forward by more than 1. |
| `MULTIPLE_INPUTS_PER_FRAME` | Multiple inputs were consumed in a single frame. |
| `INVALID_INPUT` | An input failed server-side validation. |
| `LATE_TICK` | Input arrived with a tick id behind the server's current tick. |
| `TICK_OVERFLOW` | The input buffer reached maximum capacity. |
| `CATCHUP` | Catchup simulation was triggered. |
