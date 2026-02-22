# PredictionManager

`Prediction.PredictionManager`

## Overview

PredictionManager is the central orchestrator of the prediction system. It manages the simulation tick loop, tracks all predicted entities (both client and server), handles entity ownership, coordinates resimulation and snap-to-server decisions, and provides the hooks that connect the prediction system to your networking layer.

There is a single PredictionManager instance per game session, accessible via `PredictionManager.Instance`.

## How It Works With Other Components

- **ClientPredictedEntity / ServerPredictedEntity**: The manager maintains registries of both. Each tick, it drives their pre-simulation and post-simulation steps.
- **PhysicsController**: The manager delegates all physics simulation, rewind, and resimulation calls to the configured `PHYSICS_CONTROLLER`.
- **SingleSnapshotInstanceResimChecker**: The configured `SNAPSHOT_INSTANCE_RESIM_CHECKER` is assigned to each client entity to determine when resimulation is needed.
- **VisualsInterpolationsProvider**: The configured `INTERPOLATION_PROVIDER` factory is used by PredictedEntityVisuals (not directly by the manager).

## Usage

```csharp
// 1. Configure static providers before creating the manager
PredictionManager.PHYSICS_CONTROLLER = new RewindablePhysicsController();
PredictionManager.INTERPOLATION_PROVIDER = () => new CustomVisualInterpolator();

// 2. Create the manager
var manager = new PredictionManager();

// 3. Wire up networking hooks
manager.clientStateSender = (tickId, input) => { /* send input to server */ };
manager.clientHeartbeadSender = (tickId) => { /* send heartbeat */ };
manager.serverStateSender = (connId, entityId, state) => { /* send state to client */ };
manager.serverSetControlledLocally = (connId, entityId, owned) => { /* notify client */ };
manager.connectionsIterator = () => myConnections;

// 4. Setup
manager.Setup(isServer: false, isClient: true);

// 5. Register entities
manager.AddPredictedEntity(clientEntity);

// 6. Call each FixedUpdate
manager.Tick();
```

## Tick Lifecycle

Each call to `Tick()` performs:

1. **Client pre-sim**: Checks for resimulation needs, runs `ClientSimulationTick` on the local authority entity (sample input, load input, apply forces) and `ClientFollowerSimulationTick` on followers. Sends input to the server.
2. **Server pre-sim**: Runs `ServerSimulationTick` on each server entity (consumes buffered client input, applies forces).
3. **Physics simulation**: `PHYSICS_CONTROLLER.Simulate()`.
4. **Client post-sim**: Samples and stores the physics state for each client entity.
5. **Server post-sim**: Samples physics state for each server entity and sends it to all connected clients.
6. Increments `tickId`.

## Resimulation Flow

When the client detects a desync (via the resimulation eligibility check on each entity):

1. The manager finds the earliest tick requiring correction.
2. It calls `PHYSICS_CONTROLLER.Rewind(distance)` to roll back physics.
3. For each entity, it snaps to the server state at the start tick.
4. It then loops from `startTick+1` to `currentTick`, calling `PreResimulationStep` (load stored input, apply forces) and `PHYSICS_CONTROLLER.Resimulate()` for each intermediate tick.
5. Optionally snaps to any available server state at intermediate ticks (`resimUseAvailableServerTicks`).

## Public Methods

### `Setup(bool isServer, bool isClient)`
Initializes the manager. Validates that all required hooks are configured for the given role. Sets up the physics controller.
- **Parameters**: `isServer` - whether this instance runs server logic; `isClient` - whether it runs client logic.
- **Returns**: void

### `Tick()`
Advances the simulation by one fixed tick. Must be called every `FixedUpdate`.
- **Returns**: void

### `Clear()`
Resets all internal state: entities, ownership maps, tick counters. Call when leaving a game session.
- **Returns**: void

### `AddPredictedEntity(ServerPredictedEntity entity)`
Registers a server-side predicted entity with the manager.
- **Parameters**: `entity` - the server entity to track.
- **Returns**: void

### `AddPredictedEntity(ClientPredictedEntity entity)`
Registers a client-side predicted entity. Assigns the resimulation eligibility checker and optionally tracks its Rigidbody with the physics controller.
- **Parameters**: `entity` - the client entity to track.
- **Returns**: void

### `RemovePredictedEntity(uint id)`
Unregisters a predicted entity by its id from both client and server registries. Untracks the Rigidbody if `autoTrackRigidbodies` is true.
- **Parameters**: `id` - the entity's unique identifier.
- **Returns**: void

### `SetEntityOwner(ServerPredictedEntity entity, int ownerId)`
Assigns a connection as the owner (controller) of a server entity. Server-only.
- **Parameters**: `entity` - the server entity; `ownerId` - the connection id of the owning player.
- **Returns**: void

### `UnsetOwnership(ServerPredictedEntity entity)`
Removes ownership from a server entity. Server-only.
- **Parameters**: `entity` - the entity to release.
- **Returns**: void

### `UnsetOwnership(int ownerId)`
Removes ownership by connection id. Server-only.
- **Parameters**: `ownerId` - the connection id.
- **Returns**: void

### `GetOwner(ServerPredictedEntity entity) -> int`
Returns the connection id that owns the given entity, or -1 if unowned.
- **Parameters**: `entity` - the server entity.
- **Returns**: `int` - the owner connection id.

### `GetEntity(int ownerId) -> ServerPredictedEntity`
Returns the server entity owned by the given connection, or null.
- **Parameters**: `ownerId` - the connection id.
- **Returns**: `ServerPredictedEntity`

### `GetLocalEntity() -> ClientPredictedEntity`
Returns the locally controlled client entity, or null if spectating.
- **Returns**: `ClientPredictedEntity`

### `IsPredicted(GameObject entity) -> bool`
Checks whether a GameObject is tracked by the prediction system.
- **Parameters**: `entity` - the GameObject to check.
- **Returns**: `bool`

### `IsPredicted(Rigidbody entity) -> bool`
Checks whether a Rigidbody's GameObject is tracked by the prediction system.
- **Parameters**: `entity` - the Rigidbody to check.
- **Returns**: `bool`

### `ComputePredictionDecision(out uint resimFromTickId) -> PredictionDecision`
Aggregates resimulation decisions from all client entities and returns the highest-priority decision (NOOP < SNAP < RESIMULATE) along with the earliest tick to resimulate from.
- **Parameters**: `resimFromTickId` (out) - the earliest tick requiring correction.
- **Returns**: `PredictionDecision`

### `OnServerStateReceived(uint entityId, PhysicsStateRecord stateRecord)`
Called when the client receives an authoritative state update from the server. Buffers it into the corresponding client entity.
- **Parameters**: `entityId` - which entity; `stateRecord` - the server's physics snapshot.
- **Returns**: void

### `OnServerWorldStateReceived(WorldStateRecord wsr)`
Called when the client receives a batched world state update containing multiple entity states.
- **Parameters**: `wsr` - the world state record.
- **Returns**: void

### `OnClientStateReceived(int connId, uint clientTickId, PredictionInputRecord tickInputRecord)`
Called when the server receives a client's input for a tick. Buffers it into the corresponding server entity.
- **Parameters**: `connId` - the sending client's connection id; `clientTickId` - the tick; `tickInputRecord` - the input data.
- **Returns**: void

### `OnHeartbeatReceived(int connectionId, uint tid)`
Called when the server receives a spectator heartbeat (no controlled entity). Updates the latest known tick for that connection.
- **Parameters**: `connectionId`; `tid` - the client's current tick.
- **Returns**: void

### `OnEntityOwnershipChanged(uint entityId, bool owned)`
Called on the client when the server notifies that this client now owns (or no longer owns) an entity. Client-only.
- **Parameters**: `entityId` - the entity; `owned` - true if now owned.
- **Returns**: void

### `GetServerTickDelay() -> uint` (static)
Estimates the server tick delay based on round-trip time and fixed timestep.
- **Returns**: `uint` - delay in ticks.

### `GetTotalTicks() -> uint`
Returns the current tick count.
- **Returns**: `uint`

### `GetAverageResimPerTick() -> uint`
Returns average resimulation steps per tick.
- **Returns**: `uint`

## Configuration Flags

| Flag | Type | Default | Description |
|---|---|---|---|
| `DEBUG` | `static bool` | `false` | Enables verbose debug logging. |
| `LOG_TIMING` | `static bool` | `false` | Logs tick timing information (pre-sim, post-sim, inter-tick durations). |
| `DO_RESIM` | `static bool` | `true` | Master toggle for resimulation. When false, desync is never corrected. |
| `DO_SNAP` | `static bool` | `true` | Master toggle for snap-to-server correction. |
| `PREDICTION_ENABLED` | `static bool` | `true` | Master toggle for the entire prediction system. |
| `IGNORE_NON_AUTH_RESIM_DECISIONS` | `static bool` | `false` | When true, only the locally controlled entity's desync triggers resimulation. |
| `IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS` | `static bool` | `true` | When true, follower entities that are controllable do not trigger resimulation. |
| `LOG_PRE_SIM_STATE` | `static bool` | `false` | Logs entity position/rotation before simulation each tick. |
| `INTERPOLATION_PROVIDER` | `static Func<VisualsInterpolationsProvider>` | `MovingAverageInterpolator` | Factory for creating visual interpolation providers. |
| `SNAPSHOT_INSTANCE_RESIM_CHECKER` | `static SingleSnapshotInstanceResimChecker` | `SimpleConfigurableResimulationDecider` | Policy that compares local vs server state to decide resimulation. |
| `PHYSICS_CONTROLLER` | `static PhysicsController` | `RewindablePhysicsController` | The physics backend used for simulation and rewind. |
| `ROUND_TRIP_GETTER` | `static Func<double>` | `null` | Provider for the current network round-trip time in seconds. |
| `autoTrackRigidbodies` | `bool` | `true` | Automatically tracks/untracks Rigidbodies with the physics controller when entities are added/removed. |
| `useServerWorldStateMessage` | `bool` | `false` | When true, the server batches all entity states into a single world state message instead of individual messages. |
| `snapOnSimSkip` | `bool` | `false` | Snap entities when simulation is skipped. |
| `protectFromOversimulation` | `bool` | `true` | Enables protection against resimulating the same ticks too many times. |
| `maxTickResimulationCount` | `uint` | `1` | Maximum number of times a single tick can be resimulated (when not using tick interval protection). |
| `oversimProtectWithTickInterval` | `bool` | `true` | Use tick-interval-based oversimulation protection instead of per-tick counters. |
| `minTicksBetweenResims` | `uint` | `0` | Minimum number of ticks that must pass between resimulations. |
| `resimUseAvailableServerTicks` | `bool` | `true` | During resimulation, snap to any available server state at intermediate ticks. |
| `correctWholeWorldWhenResimulating` | `bool` | `true` | When true, all entities are snapped to server state during resimulation, not just the one that triggered it. |

## Events

| Event | Type | Description |
|---|---|---|
| `onServerStateSendError` | `SafeEventDispatcher<ServerUpdateSendError>` | Fired when sending a server state update fails. |
| `onClientStateSendError` | `SafeEventDispatcher<EntityProcessingError>` | Fired when sending a client input update fails. |
| `resimulation` | `SafeEventDispatcher<bool>` | Fired at the start (`true`) and end (`false`) of a resimulation pass. |
| `resimulationStep` | `SafeEventDispatcher<bool>` | Fired during each resimulation step. |
