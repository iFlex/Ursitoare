# SimplePhysicsControllerKinematic

**Namespace:** `Prediction.Simulation`
**Implements:** `PhysicsController`

A single-entity resimulation controller. Before resimulating, it freezes all tracked bodies except the target entity by setting them kinematic. After resimulation, it restores them.

Use this when only one entity needs to resimulate and all other physics bodies should remain static during the replay.

---

## PhysicsController Implementation Notes

- **Setup.** Sets `Physics.simulationMode = SimulationMode.Script`.
- **Simulate.** Calls `Physics.Simulate(fixedDeltaTime)`.
- **Rewind.** Returns `true`. Does not restore any state.
- **BeforeResimulate.** Saves the state of all tracked bodies, sets them kinematic. Sets the target entity's body to non-kinematic.
- **Resimulate.** Calls `Physics.Simulate(fixedDeltaTime)`.
- **AfterResimulate.** Restores all tracked bodies except the target entity to their saved state and sets them non-kinematic.
- **Track.** Records a `PhysicsStateRecord` snapshot for the body.
- **Untrack.** Removes the body from tracking.
- **Clear.** Removes all tracking data.

---

## See Also

- [PhysicsController](PhysicsController.md)
- [RewindablePhysicsController](RewindablePhysicsController.md)
- [Manual: Physics Controllers](../manual/physics-controllers.md)
