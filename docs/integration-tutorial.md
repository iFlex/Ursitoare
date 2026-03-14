# Integration Tutorial

This tutorial walks through integrating Ursitoare into a project from scratch.

## Prerequisites

- Unity project with a physics-driven multiplayer game.
- A networking library of your choice (Mirror, Netcode for GameObjects, Fish-Net, custom, etc.).
- At least one `Rigidbody`-based entity that the local player controls.

Ursitoare has no dependency on any networking library. It communicates through delegates you assign. You are responsible for serialising and sending the data structures Ursitoare provides. This means you can use **any** transport — the library is purely a prediction engine.

---

## Step 1: Structure Your Game Logic with Prediction Interfaces

**This is the most important step.** Ursitoare requires your game logic to follow a specific structure. All MonoBehaviours that affect a predicted Rigidbody must implement one or both of these interfaces:

| Interface | When to use |
|---|---|
| `PredictableComponent` | Any component that applies forces to the Rigidbody. |
| `PredictableControllableComponent` | Any component that reads player input **and** applies forces. |

### The Core Constraint

> **All forces must be applied inside `ApplyForces()`. All input must be sampled inside `SampleInput()` and loaded inside `LoadInput()`. No physics forces may be applied outside of these methods. Violating this constraint guarantees a desync.**

This is because Ursitoare must be able to **replay** your game logic during resimulation. It does this by:
1. Loading the stored input for a past tick via `LoadInput()`.
2. Calling `ApplyForces()` to re-apply the same forces.
3. Stepping the physics engine.

If any force is applied outside of `ApplyForces()` — for example, in `Update()`, `FixedUpdate()`, or a coroutine — that force will not be replayed during resimulation, and the replayed state will diverge from the original prediction.

### Input Persistence Rule

Input sampled in `SampleInput()` must be stored on the component (as fields) and persist until the next call to `SampleInput()` or `LoadInput()`. The `LoadInput()` method reads from a stored record and writes the values into the same fields that `ApplyForces()` reads from. This way, both normal simulation and resimulation use the same code path.

```
Normal tick:    SampleInput() → writes fields → LoadInput() → reads fields → ApplyForces() → uses fields
Resimulation:                                   LoadInput() → reads fields → ApplyForces() → uses fields
```

### Example: Player Movement Component

```csharp
public class PlayerMovement : MonoBehaviour,
    PredictableControllableComponent, PredictableComponent
{
    public float speed = 10f;

    // Input fields — persist until next SampleInput or LoadInput
    private Vector2 _moveInput;
    private bool _jump;

    // ──── PredictableControllableComponent ────

    public int GetFloatInputCount() => 2;  // x, y axes
    public int GetBinaryInputCount() => 1; // jump button

    public void SampleInput(PredictionInputRecord input)
    {
        // Read from Unity's Input system and write into the record.
        // The ORDER you write here MUST match LoadInput exactly.
        input.WriteNextScalar(Input.GetAxis("Horizontal"));
        input.WriteNextScalar(Input.GetAxis("Vertical"));
        input.WriteNextBinary(Input.GetButtonDown("Jump"));
    }

    public void LoadInput(PredictionInputRecord input)
    {
        // Read back in the SAME ORDER as SampleInput.
        // Store into the same fields that ApplyForces reads.
        _moveInput.x = input.ReadNextScalar();
        _moveInput.y = input.ReadNextScalar();
        _jump        = input.ReadNextBool();
    }

    public bool ValidateInput(float deltaTime, PredictionInputRecord input)
    {
        // Server-side: reject obviously invalid input.
        // Return false to discard the input for this tick.
        return true;
    }

    public void ClearInput()
    {
        _moveInput = Vector2.zero;
        _jump = false;
    }

    // ──── PredictableComponent ────

    public void ApplyForces()
    {
        // Called every tick AND during every resimulation step.
        // ALL physics forces MUST go here — nowhere else.
        var rb = GetComponent<Rigidbody>();
        rb.AddForce(new Vector3(_moveInput.x, 0, _moveInput.y) * speed);
        if (_jump)
            rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
    }

    public bool HasState() => false;
    public void SampleComponentState(PhysicsStateRecord psr) { }
    public void LoadComponentState(PhysicsStateRecord psr) { }
    public int GetStateFloatCount() => 0;
    public int GetStateBoolCount() => 0;
}
```

### Common Mistakes

| Mistake | Why it causes desync |
|---|---|
| Applying forces in `FixedUpdate()` | Not replayed during resimulation |
| Applying forces in `Update()` | Not replayed, also frame-rate dependent |
| Reading `Input.*` inside `ApplyForces()` | During resimulation, `ApplyForces()` is called with stored past input — live input would be wrong |
| Different write/read order in `SampleInput` vs `LoadInput` | Input values get swapped, producing wrong forces |
| Not persisting input fields between calls | `ApplyForces()` reads stale or zero values |

### Component State

If your component carries state beyond what the Rigidbody tracks (e.g., a cooldown timer, a fuel gauge), implement `HasState() => true` and use `SampleComponentState` / `LoadComponentState` to save and restore it. This state is rewound alongside the physics state during resimulation.

### Multiple Components

A single entity can have multiple `PredictableComponent` and `PredictableControllableComponent` implementations. Pass them as arrays when constructing the entity. **The order of these arrays must be identical on client and server.** The system iterates them in order when loading input and applying forces.

---

## Step 2: Create the PredictionManager

`PredictionManager` is a plain C# object. Create it once when your network session starts — not in `Awake` or `Start`, but after your networking layer is ready and you know the local role.

```csharp
public class GameSessionManager : MonoBehaviour
{
    private PredictionManager predictionManager;

    public void OnSessionStarted(bool isServer, bool isClient)
    {
        predictionManager = new PredictionManager();
        // Configure delegates (see Steps 3–4)
        predictionManager.Setup(isServer, isClient);
    }
}
```

---

## Step 3: Wire Up Outgoing Message Callbacks

These delegates are called by the manager to send data over the network. Implement them using whatever your networking library provides.

### Client-side delegates

Assign these when running as a client.

```csharp
// Called each tick when the local player has no controlled entity (spectating).
// Send this tickId to the server so it can track your clock.
predictionManager.clientHeartbeadSender = (tickId) =>
{
    myNetwork.SendToServer(new HeartbeatMessage { tickId = tickId });
};

// Called each tick when the local player controls an entity.
// Sends the player's input for this tick, tagged with the client's tick ID.
// The server uses this tick ID to tag the resulting state back to the client.
predictionManager.clientStateSender = (tickId, inputRecord) =>
{
    myNetwork.SendToServer(new InputMessage
    {
        tickId = tickId,
        scalarInput = inputRecord.scalarInput,
        binaryInput = inputRecord.binaryInput,
    });
};
```

### Server-side delegates

Assign these when running as a server.

```csharp
// Called once per entity per tick (when useServerWorldStateMessage = false).
// The state's tickId is already set to the client's tick — this is what
// allows the client to match it against its local prediction.
predictionManager.serverStateSender = (connId, entityId, state) =>
{
    myNetwork.SendToClient(connId, new StateMessage
    {
        entityId = entityId,
        tickId   = state.tickId,    // ← This is the CLIENT's tick ID
        position = state.position,
        rotation = state.rotation,
        velocity = state.velocity,
        angularVelocity = state.angularVelocity,
    });
};

// Called when an entity gains or loses a controller.
// Tell the owning client whether they control this entity.
predictionManager.serverSetControlledLocally = (connId, entityId, owned) =>
{
    myNetwork.SendToClient(connId, new OwnershipMessage
    {
        entityId = entityId,
        owned    = owned,
    });
};

// Returns all active connection IDs. Used to broadcast state each tick.
predictionManager.connectionsIterator = () => myNetwork.GetAllConnectionIds();
```

**Optional: world state mode.** Instead of one message per entity, batch all entities into one message per tick:

```csharp
predictionManager.useServerWorldStateMessage = true;

predictionManager.serverWorldStateSender = (connId, worldState) =>
{
    myNetwork.SendToClient(connId, new WorldStateMessage
    {
        tickId       = worldState.tickId,
        serverTickId = worldState.serverTickId,
        entityIDs    = worldState.entityIDs[..worldState.fill],
        states       = worldState.states[..worldState.fill],
    });
};
```

---

## Step 4: Wire Up Incoming Message Callbacks

Call these methods when messages arrive from the network.

### On the server

```csharp
// When a client input packet arrives:
void OnInputMessageReceived(int connId, InputMessage msg)
{
    var input = new PredictionInputRecord(msg.scalarInput.Length, msg.binaryInput.Length);
    input.scalarInput = msg.scalarInput;
    input.binaryInput = msg.binaryInput;
    predictionManager.OnClientStateReceived(connId, msg.tickId, input);
}

// When a heartbeat arrives (client is spectating):
void OnHeartbeatReceived(int connId, HeartbeatMessage msg)
{
    predictionManager.OnHeartbeatReceived(connId, msg.tickId);
}
```

### On the client

```csharp
// When a per-entity state update arrives:
void OnStateMessageReceived(StateMessage msg)
{
    var state = new PhysicsStateRecord();
    state.tickId          = msg.tickId;
    state.position        = msg.position;
    state.rotation        = msg.rotation;
    state.velocity        = msg.velocity;
    state.angularVelocity = msg.angularVelocity;
    predictionManager.OnServerStateReceived(msg.entityId, state);
}

// When a world state update arrives:
void OnWorldStateMessageReceived(WorldStateMessage msg)
{
    var wsr = new WorldStateRecord();
    wsr.tickId       = msg.tickId;
    wsr.serverTickId = msg.serverTickId;
    wsr.entityIDs    = msg.entityIDs;
    wsr.states       = msg.states;
    wsr.fill         = msg.entityIDs.Length;
    predictionManager.OnServerWorldStateReceived(wsr);
}

// When the server tells you that you own (or no longer own) an entity:
void OnOwnershipMessageReceived(OwnershipMessage msg)
{
    predictionManager.OnEntityOwnershipChanged(msg.entityId, msg.owned);
}
```

---

## Step 5: Construct and Register Entities

Create entities when the corresponding game object spawns. Register them with the manager immediately.

```csharp
public class PlayerEntity : MonoBehaviour, PredictedEntity
{
    public uint entityId;
    private ClientPredictedEntity _clientEntity;
    private ServerPredictedEntity _serverEntity;

    void Spawn(uint id, bool isServer, bool isClient)
    {
        entityId = id;
        var rb = GetComponent<Rigidbody>();
        var movement = GetComponent<PlayerMovement>();

        var controllable = new PredictableControllableComponent[] { movement };
        var predictable  = new PredictableComponent[]             { movement };

        if (isClient)
        {
            _clientEntity = new ClientPredictedEntity(
                id, isServer, bufferSize: 64, rb,
                visuals: transform.Find("Visuals").gameObject,
                controllable, predictable);

            PredictionManager.Instance.AddPredictedEntity(_clientEntity);
        }

        if (isServer)
        {
            _serverEntity = new ServerPredictedEntity(
                id, bufferSize: 64, rb,
                visuals: null,
                controllable, predictable);

            PredictionManager.Instance.AddPredictedEntity(_serverEntity);
        }
    }

    void Despawn()
    {
        PredictionManager.Instance.RemovePredictedEntity(entityId);
    }

    // PredictedEntity interface
    public uint GetId()                         => entityId;
    public int  GetOwnerId()                    => PredictionManager.Instance.GetOwner(_serverEntity);
    public ClientPredictedEntity GetClientEntity() => _clientEntity;
    public ServerPredictedEntity GetServerEntity() => _serverEntity;
    public PredictedEntityVisuals GetVisualsControlled() => GetComponent<PredictedEntityVisuals>();
    public bool IsServer()                      => _serverEntity != null;
    public bool IsClient()                      => _clientEntity != null;
    public Rigidbody GetRigidbody()             => GetComponent<Rigidbody>();
}
```

### Assigning Ownership (Server)

When a client connects and spawns their player, tell the manager which entity they own:

```csharp
predictionManager.SetEntityOwner(serverEntity, connectionId);
```

When they disconnect:

```csharp
predictionManager.UnsetOwnership(connectionId);
```

---

## Step 6: Drive the Tick Loop

Call `Tick()` once per `FixedUpdate`:

```csharp
void FixedUpdate()
{
    PredictionManager.Instance.Tick();
}
```

This must be called on both client and server. On a listen server (host), one call drives both roles.

---

## Step 7: Set Up Visual Interpolation (Client Only)

Add `PredictedEntityVisuals` to the entity's GameObject. Assign the visual child object in the Inspector. Then call `SetClientPredictedEntity` after constructing the `ClientPredictedEntity`:

```csharp
if (isClient)
{
    var visuals      = GetComponent<PredictedEntityVisuals>();
    var interpolator = PredictionManager.INTERPOLATION_PROVIDER();
    visuals.SetClientPredictedEntity(_clientEntity, interpolator);
}
```

---

## Step 8: Clean Up on Session End

When the session ends, clear the manager:

```csharp
predictionManager.Clear();
```

If you are done entirely, set `PredictionManager.Instance = null`.

---

## Configuration Checklist

| Setting | Where | Notes |
|---|---|---|
| `PHYSICS_CONTROLLER` | `PredictionManager` (static) | Default: `RewindablePhysicsController` |
| `INTERPOLATION_PROVIDER` | `PredictionManager` (static) | Default: `MovingAverageInterpolator` |
| `SNAPSHOT_INSTANCE_RESIM_CHECKER` | `PredictionManager` (static) | Default: `SimpleConfigurableResimulationDecider` |
| `clientHeartbeadSender` | `PredictionManager` instance | Required on client |
| `clientStateSender` | `PredictionManager` instance | Required on client |
| `serverStateSender` | `PredictionManager` instance | Required on server (non-world-state mode) |
| `serverWorldStateSender` | `PredictionManager` instance | Required on server (world-state mode) |
| `serverSetControlledLocally` | `PredictionManager` instance | Required on server |
| `connectionsIterator` | `PredictionManager` instance | Required on server |

---

## See Also

- [Manual: Overview & Architecture](manual/overview.md)
- [Manual: PredictionManager](manual/prediction-manager.md)
- [Manual: Entities](manual/entities.md)
- [Scripting API: PredictionManager](scripting-api/PredictionManager.md)
