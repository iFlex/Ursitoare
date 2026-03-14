# Physics Controllers

## Overview

A `PhysicsController` manages Unity's physics simulation. It controls when `Physics.Simulate` is called and how the physics world is rewound during resimulation. You assign a controller to `PredictionManager.PHYSICS_CONTROLLER` before calling `Setup`.

All controllers set `Physics.simulationMode = SimulationMode.Script` during `Setup`, taking manual control of when physics steps occur.

## RewindablePhysicsController (Default)

`RewindablePhysicsController` is the default and recommended controller. It records the state of every tracked `Rigidbody` after each tick. When resimulation requires rewinding, it restores the recorded state.

### How It Works

- **Tracking.** When `PredictionManager.AddPredictedEntity` is called with `autoTrackRigidbodies = true`, the entity's `Rigidbody` is automatically passed to `Track`. The controller allocates a `RingBuffer<PhysicsStateRecord>` for that body.
- **Simulate.** Each tick, `Physics.Simulate(fixedDeltaTime)` runs and then the controller snapshots all tracked bodies into their ring buffers.
- **Rewind.** Given a rewind distance in ticks, the controller restores all tracked bodies to their state at the target tick.
- **Resimulate.** During each replay step, `Physics.Simulate(fixedDeltaTime)` runs again, advancing the world.

### Configuration

```csharp
// Use a custom buffer size (default: 60)
PredictionManager.PHYSICS_CONTROLLER = new RewindablePhysicsController(bufferSize: 128);
```

The buffer size must be at least as large as the maximum resimulation distance.

### Excluding Bodies from Resimulation

Some Rigidbodies (such as static obstacles or server-driven objects) should not be rewound. Register them with `AddIgnoreDuringResim`:

```csharp
var controller = (RewindablePhysicsController)PredictionManager.PHYSICS_CONTROLLER;
controller.AddIgnoreDuringResim(staticRigidbody);
```

## SimplePhysicsController

`SimplePhysicsController` is a minimal controller with no rewind support. `Rewind` always returns `true` without restoring any state, and `Resimulate` simply runs `Physics.Simulate`.

Use this when you do not need resimulation, for example on a dedicated server that does not resimulate.

## SimplePhysicsControllerKinematic

`SimplePhysicsControllerKinematic` is a single-entity resimulation controller. During resimulation:

1. All tracked bodies except the target entity are saved and set kinematic (frozen).
2. The target entity simulates normally.
3. After resimulation, the frozen bodies are restored.

Use this when only one entity needs to resimulate and the rest of the world should remain static during the replay.

## Implementing a Custom Controller

Implement the `PhysicsController` interface:

```csharp
public class MyPhysicsController : PhysicsController
{
    public void Setup(bool isServer) { /* configure physics */ }
    public void Simulate() { /* advance physics one tick */ }
    public bool Rewind(uint ticks) { /* restore state; return false if unable */ }
    public void BeforeResimulate(ClientPredictedEntity entity) { /* pre-resim setup */ }
    public void Resimulate(ClientPredictedEntity entity) { /* advance physics one resim step */ }
    public void AfterResimulate(ClientPredictedEntity entity) { /* post-resim cleanup */ }
    public void Track(Rigidbody rigidbody) { /* begin tracking this body */ }
    public void Untrack(Rigidbody rigidbody) { /* stop tracking this body */ }
    public void Clear() { /* reset all state */ }
}

PredictionManager.PHYSICS_CONTROLLER = new MyPhysicsController();
```

Assign the controller before calling `PredictionManager.Setup`.

## See Also

- [Scripting API: PhysicsController](../scripting-api/PhysicsController.md)
- [Scripting API: RewindablePhysicsController](../scripting-api/RewindablePhysicsController.md)
- [Scripting API: SimplePhysicsController](../scripting-api/SimplePhysicsController.md)
- [Scripting API: SimplePhysicsControllerKinematic](../scripting-api/SimplePhysicsControllerKinematic.md)
