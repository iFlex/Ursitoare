# PredictionManager

**Namespace:** `Prediction`

Central coordinator of the prediction system. Manages the tick loop, entity registration, ownership, and all network message dispatch.

---

## Static Properties

| Type | Name | Description |
|---|---|---|
| `bool` | `DEBUG` | Enable verbose debug logging. Default: `false`. |
| `bool` | `LOG_TIMING` | Log per-tick timing data. Default: `false`. |
| `bool` | `DO_RESIM` | Allow resimulation. Default: `true`. |
| `bool` | `DO_SNAP` | Allow snapping. Default: `true`. |
| `bool` | `PREDICTION_ENABLED` | Enable full client prediction. Default: `true`. |
| `bool` | `IGNORE_NON_AUTH_RESIM_DECISIONS` | Ignore resim requests from non-local entities. Default: `false`. |
| `bool` | `IGNORE_CONTROLLABLE_FOLLOWER_DECISIONS` | Ignore resim requests from controllable follower entities. Default: `true`. |
| `bool` | `LOG_PRE_SIM_STATE` | Log entity state before each simulation step. Default: `false`. |
| `int` | `INVALID_CONNECTION_ID` | Sentinel value for missing connection IDs. Value: `-1`. |
| `PredictionManager` | `Instance` | The active singleton instance. Set by the constructor. |
| `Func<VisualsInterpolationsProvider>` | `INTERPOLATION_PROVIDER` | Factory for interpolator instances. Default: `MovingAverageInterpolator`. |
| `SingleSnapshotInstanceResimChecker` | `SNAPSHOT_INSTANCE_RESIM_CHECKER` | Snapshot comparison policy. Default: `SimpleConfigurableResimulationDecider`. |
| `PhysicsController` | `PHYSICS_CONTROLLER` | Physics simulation backend. Default: `RewindablePhysicsController`. |
| `Func<double>` | `ROUND_TRIP_GETTER` | Returns current round-trip time in seconds. Assign before calling `GetServerTickDelay`. |

---

## Properties

| Type | Name | Description |
|---|---|---|
| `bool` | `isClient` | This instance is running as a client. |
| `bool` | `isServer` | This instance is running as a server. |
| `uint` | `tickId` | Current tick counter. |
| `uint` | `reportedServerTickId` | Last server tick ID reported in a received world state. Read-only. |
| `bool` | `autoTrackRigidbodies` | Automatically register entity Rigidbodies with the physics controller. Default: `true`. |
| `bool` | `useServerWorldStateMessage` | Use batched world state messages instead of per-entity messages. Default: `false`. |
| `bool` | `snapOnSimSkip` | Snap entities when a simulation step is skipped. Default: `false`. |
| `bool` | `protectFromOversimulation` | Prevent the same tick from being resimulated too many times. Default: `true`. |
| `bool` | `oversimProtectWithTickInterval` | Use tick-interval mode for oversimulation protection. Default: `true`. |
| `uint` | `maxTickResimulationCount` | Max times one tick ID can be resimulated (per-tick-count mode). Default: `1`. |
| `uint` | `minTicksBetweenResims` | Minimum ticks between resimulations (interval mode). Default: `0`. |
| `bool` | `resimUseAvailableServerTicks` | During resimulation, snap to server state when available. Default: `true`. |
| `bool` | `correctWholeWorldWhenResimulating` | Snap all entities (not just the one requesting resim) at the start of resimulation. Default: `true`. |
| `bool` | `resimulating` | True while a resimulation is in progress. |
| `bool` | `shouldResimThisTick` | True if a resimulation was requested this tick. |

---

## Outgoing Message Delegates

Assign these before calling `Setup`. The manager calls them to send data over the network.

| Type | Name | Description |
|---|---|---|
| `Action<uint>` | `clientHeartbeadSender` | Client: send a heartbeat tick ID to the server. Called when no entity is locally controlled. |
| `Action<uint, PredictionInputRecord>` | `clientStateSender` | Client: send a tick ID and input record to the server. |
| `Action<int, uint, PhysicsStateRecord>` | `serverStateSender` | Server: send entity state to a connection. Parameters: `connId, entityId, state`. |
| `Action<int, WorldStateRecord>` | `serverWorldStateSender` | Server: send batched world state to a connection. Used when `useServerWorldStateMessage = true`. |
| `Action<int, uint, bool>` | `serverSetControlledLocally` | Server: notify a client of entity ownership. Parameters: `connId, entityId, owned`. |
| `Func<IEnumerable<int>>` | `connectionsIterator` | Server: returns all active connection IDs. |

---

## Events

| Type | Name | Fires when |
|---|---|---|
| `SafeEventDispatcher<uint>` | `onPreTick` | Before each tick's simulation step. Payload: tick ID. |
| `SafeEventDispatcher<uint>` | `onPostTick` | After each tick's simulation step. Payload: tick ID. |
| `SafeEventDispatcher<uint>` | `onPreResimTick` | Before each resimulation step. Payload: tick ID being replayed. |
| `SafeEventDispatcher<uint>` | `onPostResimTick` | After each resimulation step. Payload: tick ID just replayed. |
| `SafeEventDispatcher<bool>` | `resimulation` | At the start (`true`) and end (`false`) of a resimulation pass. |
| `SafeEventDispatcher<bool>` | `resimulationStep` | At the start (`true`) and end (`false`) of each individual resimulation step. |
| `SafeEventDispatcher<uint>` | `onTickFrozen` | When a simulation freeze occurs. Payload: tick ID. |
| `SafeEventDispatcher<ServerUpdateSendError>` | `onServerStateSendError` | When a server state send throws an exception. |
| `SafeEventDispatcher<EntityProcessingError>` | `onClientStateSendError` | When a client state send throws an exception. |

---

## Public Methods

### Setup

```csharp
public void Setup(bool isServer, bool isClient)
```

Initialises the manager for the given role. Validates that all required delegates are assigned. Throws `Exception` if any required delegate is missing. Must be called before `Tick`.

---

### Tick

```csharp
public void Tick()
```

Advances the simulation by one tick. Call once per `FixedUpdate`.

---

### AddPredictedEntity

```csharp
public void AddPredictedEntity(ClientPredictedEntity entity)
public void AddPredictedEntity(ServerPredictedEntity entity)
```

Registers an entity with the manager. Call after constructing the entity and before the next `Tick`.

---

### RemovePredictedEntity

```csharp
public void RemovePredictedEntity(uint id)
```

Unregisters and unregisters all data for the entity with the given ID. Call when the entity is destroyed.

---

### SetEntityOwner

```csharp
public void SetEntityOwner(ServerPredictedEntity entity, int ownerId)
```

Server only. Assigns the entity to the given connection. Fires `serverSetControlledLocally`. Clears any previous ownership for both the entity and the connection.

---

### UnsetOwnership

```csharp
public void UnsetOwnership(ServerPredictedEntity entity)
public void UnsetOwnership(int ownerId)
public void UnsetOwnership(ServerPredictedEntity entity, int ownerId)
```

Server only. Removes ownership assignment for the given entity or connection ID.

---

### GetOwner

```csharp
public int GetOwner(ServerPredictedEntity entity)
```

Returns the connection ID of the entity's current owner, or `INVALID_CONNECTION_ID` if unowned.

---

### GetEntity

```csharp
public ServerPredictedEntity GetEntity(int ownerId)
```

Returns the entity owned by the given connection ID, or `null`.

---

### GetLocalEntity

```csharp
public ClientPredictedEntity GetLocalEntity()
```

Client only. Returns the locally controlled `ClientPredictedEntity`, or `null` if none.

---

### OnEntityOwnershipChanged

```csharp
public void OnEntityOwnershipChanged(uint entityId, bool owned)
```

Client only. Call when the server notifies you of an ownership change. Sets or clears the local entity.

---

### OnClientStateReceived

```csharp
public void OnClientStateReceived(int connId, uint clientTickId, PredictionInputRecord tickInputRecord)
```

Server only. Call when a client input packet arrives. Buffers the input on the corresponding `ServerPredictedEntity`.

---

### OnServerStateReceived

```csharp
public void OnServerStateReceived(uint entityId, PhysicsStateRecord stateRecord)
```

Client only. Call when a per-entity state update arrives from the server. Buffers the state on the corresponding `ClientPredictedEntity`.

---

### OnServerWorldStateReceived

```csharp
public void OnServerWorldStateReceived(WorldStateRecord wsr)
```

Client only. Call when a world state message arrives from the server. Distributes state to all registered client entities.

---

### OnHeartbeatReceived

```csharp
public void OnHeartbeatReceived(int connectionId, uint tid)
```

Server only. Call when a heartbeat arrives from a client. Updates the tick tracking record for that connection.

---

### IsPredicted

```csharp
public bool IsPredicted(GameObject entity)
public bool IsPredicted(Rigidbody entity)
```

Returns `true` if the given object is registered as a predicted entity.

---

### ComputePredictionDecision

```csharp
public PredictionDecision ComputePredictionDecision(out uint resimFromTickId)
```

Queries all registered client entities and returns the highest-severity `PredictionDecision`. `resimFromTickId` is set to the earliest divergence tick.

---

### GetServerTickId

```csharp
public uint GetServerTickId()
```

On the server, returns the current `tickId`. On the client, returns the last received `reportedServerTickId`.

---

### GetServerTickDelay

```csharp
public static uint GetServerTickDelay()
```

Estimates the server tick delay based on `ROUND_TRIP_GETTER` and `Time.fixedDeltaTime`.

---

### GetTotalTicks

```csharp
public uint GetTotalTicks()
```

Returns the current `tickId`.

---

### GetAverageResimPerTick

```csharp
public uint GetAverageResimPerTick()
```

Returns `totalResimulationSteps / tickId`.

---

### Clear

```csharp
public void Clear()
```

Resets tick ID to 1, clears all entity registrations, and resets the physics controller.

---

## Diagnostic Counters

| Name | Description |
|---|---|
| `totalResimulations` | Total number of resimulation passes. |
| `totalResimulationSteps` | Total number of individual steps replayed across all resimulations. |
| `totalTickFreezes` | Total number of simulation freezes. |
| `totalDesyncToSnapCount` | Total number of direct snaps. |
| `totalResimulationsDueToAuthority` | Resimulations triggered by the local entity alone. |
| `totalResimulationsDueToFollowers` | Resimulations triggered by follower entities only. |
| `totalResimulationsDueToBoth` | Resimulations triggered by both. |
| `totalResimulationsSkipped` | Resimulations requested but blocked by oversimulation protection. |
| `maxRewindDistance` | Maximum rewind distance observed across all resimulations, in ticks. |
| `totalRewindDistance` | Cumulative rewind distance across all resimulations, in ticks. |
| `clientStatesReceived` | Number of client input packets received (server). |
| `clientSendErrors` | Number of exceptions thrown by client send delegates. |

---

## Nested Types

### EntityProcessingError

```csharp
public struct EntityProcessingError
{
    public Exception exception;
    public uint entityId;
}
```

Payload for `onClientStateSendError`.

### ServerUpdateSendError

```csharp
public struct ServerUpdateSendError
{
    public Exception exception;
    public int connId;
    public uint entityId;
    public uint tickId;
}
```

Payload for `onServerStateSendError`.

---

## See Also

- [Manual: PredictionManager](../manual/prediction-manager.md)
- [Integration Tutorial](../integration-tutorial.md)
- [ClientPredictedEntity](ClientPredictedEntity.md)
- [ServerPredictedEntity](ServerPredictedEntity.md)
