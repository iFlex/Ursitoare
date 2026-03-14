# PhysicsController

**Namespace:** `Prediction.Simulation`

Interface for the physics simulation backend. Controls when `Physics.Simulate` is called and how the physics world is rewound for resimulation.

Assign an implementation to `PredictionManager.PHYSICS_CONTROLLER` before calling `Setup`.

---

## Methods

### Setup

```csharp
void Setup(bool isServer)
```

Called once by `PredictionManager.Setup`. Configure physics settings here. Implementations typically set `Physics.simulationMode = SimulationMode.Script`.

---

### Simulate

```csharp
void Simulate()
```

Called once per tick to advance physics by one step.

---

### Rewind

```csharp
bool Rewind(uint ticks)
```

Restore the physics world to the state it was in `ticks` steps ago. Return `true` on success. Return `false` if the requested rewind distance exceeds available history; the manager will skip the resimulation.

---

### BeforeResimulate

```csharp
void BeforeResimulate(ClientPredictedEntity entity)
```

Called once before a resimulation pass begins. Use this to prepare any per-pass state.

---

### Resimulate

```csharp
void Resimulate(ClientPredictedEntity entity)
```

Called once per replay step during resimulation. Advance physics by one step.

---

### AfterResimulate

```csharp
void AfterResimulate(ClientPredictedEntity entity)
```

Called once after a resimulation pass completes. Use this to restore any state altered in `BeforeResimulate`.

---

### Track

```csharp
void Track(Rigidbody rigidbody)
```

Begin tracking the given `Rigidbody`. Called by the manager when an entity is registered and `autoTrackRigidbodies = true`.

---

### Untrack

```csharp
void Untrack(Rigidbody rigidbody)
```

Stop tracking the given `Rigidbody`. Called by the manager when an entity is removed.

---

### Clear

```csharp
void Clear()
```

Reset all tracking state. Called by `PredictionManager.Clear`.

---

## See Also

- [RewindablePhysicsController](RewindablePhysicsController.md)
- [SimplePhysicsController](SimplePhysicsController.md)
- [SimplePhysicsControllerKinematic](SimplePhysicsControllerKinematic.md)
- [Manual: Physics Controllers](../manual/physics-controllers.md)
