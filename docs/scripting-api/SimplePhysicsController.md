# SimplePhysicsController

**Namespace:** `Prediction.Simulation`
**Implements:** `PhysicsController`

A minimal physics controller with no rewind support. Every tick and every resimulation step simply calls `Physics.Simulate(fixedDeltaTime)`. `Rewind` always returns `true` without restoring any state.

Use this on a dedicated server that does not need to resimulate, or during development when you want to disable rewind.

---

## PhysicsController Implementation Notes

- **Setup.** Sets `Physics.simulationMode = SimulationMode.Script`.
- **Simulate.** Calls `Physics.Simulate(fixedDeltaTime)`.
- **Rewind.** Returns `true`. Does not restore any state.
- **Resimulate.** Calls `Physics.Simulate(fixedDeltaTime)`.
- **BeforeResimulate / AfterResimulate.** No-op.
- **Track / Untrack / Clear.** No-op.

---

## See Also

- [PhysicsController](PhysicsController.md)
- [RewindablePhysicsController](RewindablePhysicsController.md)
- [Manual: Physics Controllers](../manual/physics-controllers.md)
