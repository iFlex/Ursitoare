# PredictableControllableComponent

`Prediction.PredictableControllableComponent` (interface)

## Overview

PredictableControllableComponent is the interface for components that handle player input in the prediction system. Any MonoBehaviour that reads user input (keyboard, mouse, gamepad, etc.) and translates it into data that affects the simulation must implement this interface. This ensures that input can be sampled, serialized for network transmission, validated on the server, and replayed during resimulation.

## How It Works With Other Components

- **AbstractPredictedEntity** holds an array of `PredictableControllableComponent[]` (called `controllablePredictionContributors`). The entity uses these to:
  - **Sample input** (`SampleInput`): Called on the client each tick to capture the current player input into a `PredictionInputRecord`.
  - **Load input** (`LoadInput`): Called on both client (during resimulation) and server (when processing buffered client ticks) to restore input state from a record.
  - **Validate input** (`ValidateInput`): Called on the server to check whether the received input is valid before applying it.
- The input counts (`GetFloatInputCount`, `GetBinaryInputCount`) are aggregated at entity construction time to size the `PredictionInputRecord` buffers correctly.
- The order in which components sample and load input must be identical on client and server.

## Usage

```csharp
public class MovementInput : MonoBehaviour, PredictableControllableComponent
{
    private float moveX, moveY;
    private bool jump;

    public int GetFloatInputCount() => 2;
    public int GetBinaryInputCount() => 1;

    public void SampleInput(PredictionInputRecord input)
    {
        input.WriteNextScalar(Input.GetAxis("Horizontal"));
        input.WriteNextScalar(Input.GetAxis("Vertical"));
        input.WriteNextBinary(Input.GetButtonDown("Jump"));
    }

    public void LoadInput(PredictionInputRecord input)
    {
        moveX = input.ReadNextScalar();
        moveY = input.ReadNextScalar();
        jump = input.ReadNextBool();
    }

    public bool ValidateInput(float deltaTime, PredictionInputRecord input)
    {
        // Validate on server: e.g. reject impossible values
        return true;
    }
}
```

**Important**: The number and order of `WriteNextScalar`/`WriteNextBinary` calls in `SampleInput` must exactly match the `ReadNextScalar`/`ReadNextBool` calls in `LoadInput`, and must match across all instances on client and server.

## Public Methods

### `GetFloatInputCount() -> int`
Returns the number of float (scalar) input values this component writes per tick. Must be constant.
- **Returns**: `int`

### `GetBinaryInputCount() -> int`
Returns the number of boolean input values this component writes per tick. Must be constant.
- **Returns**: `int`

### `SampleInput(PredictionInputRecord input)`
Called on the client to capture the current frame's input into the record. Use `input.WriteNextScalar(...)` and `input.WriteNextBinary(...)`. This method has side effects: it reads from the input system.
- **Parameters**: `input` - the record to write input into.
- **Returns**: void

### `ValidateInput(float deltaTime, PredictionInputRecord input) -> bool`
Called on the server to validate a received input record. Return `false` to reject the input (e.g., if values are out of range or physically impossible given the time delta).
- **Parameters**: `deltaTime` - the time delta since the last input; `input` - the input record to validate.
- **Returns**: `bool` - true if valid, false to reject.

### `LoadInput(PredictionInputRecord input)`
Called on both client (during resimulation) and server (during simulation) to load input values into the component's local state. Use `input.ReadNextScalar()` and `input.ReadNextBool()`.
- **Parameters**: `input` - the record to read input from.
- **Returns**: void

## Configuration

This is an interface; there are no configuration flags. Implementers define their own configuration as needed.
