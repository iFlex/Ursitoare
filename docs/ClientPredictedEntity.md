# ClientPredictedEntity

`Prediction.ClientPredictedEntity`

## Overview

ClientPredictedEntity is the client-side representation of a networked predicted object. It maintains the local prediction state (input history and physics state history), buffers incoming server authoritative state, and decides whether a resimulation or snap is needed by comparing local predictions against server data.

It extends `AbstractPredictedEntity`, which provides common logic for input loading, force application, component state sampling, and physics state population.

## How It Works With Other Components

- **PredictionManager** drives the entity each tick:
  - `ClientSimulationTick()` for the locally controlled entity (samples input, loads it, applies forces).
  - `ClientFollowerSimulationTick()` for follower entities (snaps to latest server state, applies forces).
  - `SamplePhysicsState()` after physics simulation to record the resulting state.
  - During resimulation: `SnapToServer()`, `PreResimulationStep()`, `PostResimulationStep()`.
- **PredictableControllableComponent[]**: Input-producing components. Used during `SampleInput`, `LoadInput`.
- **PredictableComponent[]**: Force-applying components. Used during `ApplyForces`.
- **PredictedEntityVisuals**: Subscribes to `newStateReached` to feed states into the visual interpolation pipeline.
- **SingleSnapshotInstanceResimChecker**: The manager assigns a resimulation eligibility check handler that is called in `GetPredictionDecision`.

## Usage

```csharp
// Create the entity
var clientEntity = new ClientPredictedEntity(
    id: networkId,
    isServer: false,
    bufferSize: 128,
    rb: GetComponent<Rigidbody>(),
    visuals: visualsGameObject,
    controllablePredictionContributors: new PredictableControllableComponent[] { movementInput },
    predictionContributors: new PredictableComponent[] { thruster, gravity }
);

// Register with the manager
PredictionManager.Instance.AddPredictedEntity(clientEntity);
```

## Public Methods

### Constructor
`ClientPredictedEntity(uint id, bool isServer, int bufferSize, Rigidbody rb, GameObject visuals, PredictableControllableComponent[] controllablePredictionContributors, PredictableComponent[] predictionContributors)`
- **Parameters**: `id` - unique entity id; `isServer` - whether this runs on a host-server; `bufferSize` - ring buffer capacity for input and state history; `rb` - the Rigidbody; `visuals` - the detached visuals GameObject; `controllablePredictionContributors` - input components; `predictionContributors` - force components.

### `ClientSimulationTick(uint tickId) -> PredictionInputRecord`
Called by the manager on the locally controlled entity each tick. Samples input, loads it into components, applies forces.
- **Parameters**: `tickId` - the current tick.
- **Returns**: `PredictionInputRecord` - the sampled input (sent to the server).

### `ClientFollowerSimulationTick(uint tickId)`
Called by the manager on follower (non-locally-controlled) entities. Snaps to the latest server state, optionally loads server input, then applies forces.
- **Parameters**: `tickId` - the current tick.
- **Returns**: void

### `SampleInput(uint tickId) -> PredictionInputRecord`
Samples input from all controllable components and stores it in the local input buffer at the given tick index.
- **Parameters**: `tickId` - the tick to sample for.
- **Returns**: `PredictionInputRecord`

### `SamplePhysicsState(uint tickId)`
Called after physics simulation to record the entity's current Rigidbody state and component state into the local state buffer.
- **Parameters**: `tickId` - the current tick.
- **Returns**: void

### `GetPredictionDecision(uint lastAppliedTick, out uint fromTick) -> PredictionDecision`
Compares local predicted state against the latest server state and returns a decision: `NOOP` (no correction needed), `RESIMULATE` (rewind and resim), or `SNAP` (teleport to server state).
- **Parameters**: `lastAppliedTick` - the current tick; `fromTick` (out) - the tick to resimulate from.
- **Returns**: `PredictionDecision`

### `BufferServerTick(uint lastAppliedTick, PhysicsStateRecord serverState)`
Stores a received server state snapshot into the server state buffer.
- **Parameters**: `lastAppliedTick` - the client's current tick when the message arrived; `serverState` - the authoritative state.
- **Returns**: void

### `SnapToServer(uint tickId)`
Teleports the Rigidbody to the server's recorded state at the given tick. Loads component state as well.
- **Parameters**: `tickId` - the tick to snap to.
- **Returns**: void

### `SnapToServerIfExists(uint tickId)`
Like `SnapToServer`, but silently does nothing if no server state exists for that tick.
- **Parameters**: `tickId` - the tick to snap to.
- **Returns**: void

### `PreResimulationStep(uint tickId)`
Called during resimulation for each intermediate tick. For the local authority: loads stored input and applies forces. For followers: dispatches events only.
- **Parameters**: `tickId` - the tick being resimulated.
- **Returns**: void

### `PostResimulationStep(uint tickId)`
Called after each resimulation physics step. Records the new state into the local buffer. Optionally tracks discrepancies between consecutive resimulations of the same tick.
- **Parameters**: `tickId` - the tick that was just resimulated.
- **Returns**: void

### `GetLastInput() -> PredictionInputRecord`
Returns the input record from the last simulated tick.
- **Returns**: `PredictionInputRecord`

### `GetServerDelay() -> uint`
Returns the gap in ticks between the latest local tick and the latest server state tick.
- **Returns**: `uint`

### `SetControlledLocally(bool controlled)`
Sets whether this entity is controlled by the local player. Resets all buffers.
- **Parameters**: `controlled` - true if locally controlled.
- **Returns**: void

### `Reset()`
Clears all buffers (input, state, server state) and resets tick counters.
- **Returns**: void

### `SetCustomEligibilityCheckHandler(Func<uint, uint, RingBuffer<PhysicsStateRecord>, TickIndexedBuffer<PhysicsStateRecord>, PredictionDecision> handler)`
Sets a custom function for determining resimulation eligibility using the full history buffers.
- **Parameters**: `handler` - the custom check function.
- **Returns**: void

### `SetSingleStateEligibilityCheckHandler(Func<uint, uint, PhysicsStateRecord, PhysicsStateRecord, PredictionDecision> handler)`
Sets a simpler resimulation check that compares a single local state against a single server state.
- **Parameters**: `handler` - the check function.
- **Returns**: void

### `IsControllable() -> bool`
Returns true if the entity has any `PredictableControllableComponent` contributors.
- **Returns**: `bool`

## Configuration Flags

| Flag | Type | Default | Description |
|---|---|---|---|
| `DEBUG` | `static bool` | `false` | Enables verbose debug logging for this entity. |
| `LOG_ADDED_SERVER_STATES` | `static bool` | `true` | Logs each server state that is buffered. |
| `LOG_USED_INPUTS` | `static bool` | `false` | Logs input records when they are used (both normal sim and resim). |
| `TRUST_ALREADY_RESIMULATED_TICKS` | `static bool` | `false` | When true, skips resimulation checks for ticks that have already been resimulated. |
| `APPLY_SERVER_INPUT_TO_FOLLOWERS` | `static bool` | `true` | When true, follower entities load the server-reported input during `ClientFollowerSimulationTick`. |
| `LOG_VELOCITIES` | `static bool` | `false` | Logs velocity data for the locally controlled entity. |
| `LOG_VELOCITIES_ALL` | `static bool` | `false` | Logs velocity data for all entities. |
| `TRACK_RESIM_DISCREPANCIES` | `static bool` | `true` | Tracks and compares state between consecutive resimulations of the same tick. |
| `ALLOW_SERVER_HISTORY_REWRITES` | `static bool` | `false` | When true, allows overwriting previously buffered server states for the same tick. |
| `LOG_RESIMULATION_STEPS` | `static bool` | `true` | Logs state data during each resimulation step. |
| `predictionDisabled` | `bool` | `false` | Disables prediction for this entity entirely. The entity will not be simulated or resimulated. |

## Events

| Event | Type | Description |
|---|---|---|
| `newStateReached` | `SafeEventDispatcher<PhysicsStateRecord>` | Fired after each tick's state is sampled (both authoritative and speculative). |
| `newAuthoritativeStateReached` | `SafeEventDispatcher<PhysicsStateRecord>` | Fired only when the sampled state is authoritative (not speculative). |
| `preSampleState` | `SafeEventDispatcher<bool>` | Fired just before state is sampled. |
| `onReset` | `SafeEventDispatcher<bool>` | Fired when the entity is reset. |
| `resimulation` | `SafeEventDispatcher<bool>` | Fired at start/end of resimulation pass for this entity. |
| `resimulationStep` | `SafeEventDispatcher<bool>` | Fired at each resimulation step. |
| `potentialDesync` | `SafeEventDispatcher<DesyncEvent>` | Fired when a potential desync condition is detected (missing server data, gaps, etc.). |
| `inputUsed` | `SafeEventDispatcher<PredictionInputRecord>` | Fired when input is used during simulation. |
