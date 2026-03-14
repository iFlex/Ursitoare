# RewindablePhysicsController

**Namespace:** `Prediction.Simulation`
**Implements:** `PhysicsController`

The default physics controller. Records the state of every tracked `Rigidbody` after each tick. Restores recorded state when rewinding for resimulation.

---

## Static Properties

| Type | Name | Default | Description |
|---|---|---|---|
| `bool` | `DEBUG_STEP` | `false` | Call `Debug.Break()` after each resimulation step. |
| `bool` | `LOG_STEP` | `false` | Log all tracked body states before and after each step. |
| `RewindablePhysicsController` | `Instance` | — | The most recently constructed instance. |

---

## Properties

| Type | Name | Default | Description |
|---|---|---|---|
| `int` | `bufferSize` | `60` | Number of ticks of state history to retain. |

---

## Constructors

### RewindablePhysicsController()

```csharp
public RewindablePhysicsController()
```

Creates a controller with the default buffer size of 60 ticks.

---

### RewindablePhysicsController(int)

```csharp
public RewindablePhysicsController(int bufferSize)
```

Creates a controller with a custom buffer size.

---

## Public Methods

### GetTick

```csharp
public uint GetTick()
```

Returns the controller's internal tick counter.

---

### AddIgnoreDuringResim

```csharp
public void AddIgnoreDuringResim(Rigidbody rigidbody)
```

Marks a tracked `Rigidbody` as excluded from resimulation. The body is still tracked for history, but is not rewound when `Rewind` is called. Use for static obstacles or server-driven objects that should not replay.

---

### RemoveIgnoreDuringResim

```csharp
public void RemoveIgnoreDuringResim(Rigidbody rigidbody)
```

Removes the exclusion set by `AddIgnoreDuringResim`.

---

## PhysicsController Implementation Notes

- **Setup.** Sets `Physics.simulationMode = SimulationMode.Script` and assigns `Instance`.
- **Simulate.** Runs `Physics.Simulate(fixedDeltaTime)`, then samples all tracked bodies into their ring buffers at the current tick.
- **Rewind.** Decrements the internal tick counter by `ticks`. Restores all tracked body states from the ring buffer at the target tick. Returns `false` if the rewind would exceed the available history.
- **Resimulate.** Runs `Physics.Simulate(fixedDeltaTime)` and samples state into the ring buffer.
- **Track.** Allocates a `RingBuffer<PhysicsStateRecord>` for the body and sets `rigidbody.interpolation = RigidbodyInterpolation.None`.
- **Untrack.** Removes the ring buffer for the body.
- **Clear.** Removes all tracking data and resets tick to 1.

---

## See Also

- [PhysicsController](PhysicsController.md)
- [SimplePhysicsController](SimplePhysicsController.md)
- [SimplePhysicsControllerKinematic](SimplePhysicsControllerKinematic.md)
- [Manual: Physics Controllers](../manual/physics-controllers.md)
