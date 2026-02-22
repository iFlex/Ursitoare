# PredictableComponent

`Prediction.PredictableComponent` (interface)

## Overview

PredictableComponent is the interface that must be implemented by any MonoBehaviour that applies forces to a Rigidbody and needs to participate in the prediction system. It provides hooks for force application and optional component state that should be included in physics snapshots for accurate resimulation.

## How It Works With Other Components

- **AbstractPredictedEntity** (base class of ClientPredictedEntity and ServerPredictedEntity) holds an array of `PredictableComponent[]` (called `predictionContributors`). Each tick, the entity iterates over them to:
  - Call `ApplyForces()` before physics simulation.
  - Call `SampleComponentState()` after simulation to capture any extra state.
  - Call `LoadComponentState()` during resimulation to restore that state.
- Components that report `HasState() == true` have their state float/bool counts aggregated to size the `componentState` field in `PhysicsStateRecord`.

## Usage

Implement this interface on any component that modifies a Rigidbody's velocity, applies forces, or carries simulation-relevant state.

```csharp
public class ThrusterComponent : MonoBehaviour, PredictableComponent
{
    private float currentThrust;

    public void ApplyForces()
    {
        GetComponent<Rigidbody>().AddForce(transform.forward * currentThrust);
    }

    public bool HasState() => true;

    public void SampleComponentState(PhysicsStateRecord record)
    {
        record.componentState.WriteNextScalar(currentThrust);
    }

    public void LoadComponentState(PhysicsStateRecord record)
    {
        currentThrust = record.componentState.ReadNextScalar();
    }

    public int GetStateFloatCount() => 1;
    public int GetStateBoolCount() => 0;
}
```

## Public Methods

### `ApplyForces()`
Called each tick (and each resimulation step) to apply physics forces. This is where you call `Rigidbody.AddForce`, set velocities, etc.
- **Returns**: void

### `HasState() -> bool`
Return `true` if this component carries additional state that must be saved/restored during resimulation. If `false`, `SampleComponentState` and `LoadComponentState` will not be called.
- **Returns**: `bool`

### `SampleComponentState(PhysicsStateRecord physicsStateRecord)`
Called after simulation to write this component's state into the record's `componentState` field. Use `physicsStateRecord.componentState.WriteNextScalar(...)` and `WriteNextBinary(...)`.
- **Parameters**: `physicsStateRecord` - the record to write state into.
- **Returns**: void

### `LoadComponentState(PhysicsStateRecord physicsStateRecord)`
Called during resimulation to restore this component's state from a record. Use `physicsStateRecord.componentState.ReadNextScalar()` and `ReadNextBool()`.
- **Parameters**: `physicsStateRecord` - the record to read state from.
- **Returns**: void

### `GetStateFloatCount() -> int`
Returns the number of float values this component writes to component state. Must be constant.
- **Returns**: `int`

### `GetStateBoolCount() -> int`
Returns the number of bool values this component writes to component state. Must be constant.
- **Returns**: `int`

## Configuration

This is an interface; there are no configuration flags. Implementers define their own configuration as needed.
