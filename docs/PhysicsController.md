# PhysicsController

`Prediction.Simulation.PhysicsController` (interface)

## Overview

PhysicsController is the interface that abstracts the physics simulation backend. The prediction system never calls Unity's physics API directly; instead it goes through this interface, allowing the physics layer to support rewind and resimulation.

The default implementation is `RewindablePhysicsController`, which wraps Unity's physics scene and supports rewinding the simulation by a number of ticks.

## How It Works With Other Components

- **PredictionManager** holds a static reference (`PHYSICS_CONTROLLER`) and calls it each tick:
  - `Simulate()` during the normal tick.
  - `Rewind()` + `BeforeResimulate()` + `Resimulate()` (in a loop) + `AfterResimulate()` during a resimulation pass.
- **ClientPredictedEntity** Rigidbodies are optionally auto-tracked/untracked via `Track()` / `Untrack()` when entities are added to or removed from the PredictionManager.

## Usage

To use a custom physics backend, implement this interface and assign it before calling `PredictionManager.Setup()`:

```csharp
PredictionManager.PHYSICS_CONTROLLER = new MyCustomPhysicsController();
```

## Public Methods

### `Setup(bool isServer)`
Initializes the physics controller.
- **Parameters**: `isServer` - true if running on the server.
- **Returns**: void

### `Simulate()`
Advances the physics simulation by one fixed timestep. Called once per tick during normal simulation.
- **Returns**: void

### `BeforeResimulate(ClientPredictedEntity entity)`
Called once before a resimulation pass begins. Use this to prepare the physics scene (e.g., disable auto-simulation, save state).
- **Parameters**: `entity` - the entity triggering resimulation (may be null when the manager resimulates globally).
- **Returns**: void

### `Rewind(uint ticks) -> bool`
Rewinds the physics simulation by the specified number of ticks.
- **Parameters**: `ticks` - how many ticks to rewind.
- **Returns**: `bool` - true if the rewind succeeded, false if there is insufficient history.

### `Resimulate(ClientPredictedEntity entity)`
Steps the physics simulation forward by one tick during a resimulation pass.
- **Parameters**: `entity` - the entity being resimulated (may be null for global resimulation).
- **Returns**: void

### `AfterResimulate(ClientPredictedEntity entity)`
Called once after a resimulation pass completes. Use this to restore normal simulation settings.
- **Parameters**: `entity` - the entity that triggered resimulation (may be null).
- **Returns**: void

### `Track(Rigidbody rigidbody)`
Registers a Rigidbody with the physics controller so it is included in simulation, rewind, and resimulation.
- **Parameters**: `rigidbody` - the Rigidbody to track.
- **Returns**: void

### `Untrack(Rigidbody rigidbody)`
Removes a Rigidbody from the physics controller's tracking.
- **Parameters**: `rigidbody` - the Rigidbody to stop tracking.
- **Returns**: void

## Configuration

PhysicsController is an interface and has no configuration flags of its own. Configuration depends on the concrete implementation. See the implementation classes (`RewindablePhysicsController`, `ScenePhysicsController`, `SimplePhysicsController`) for their specific settings.
