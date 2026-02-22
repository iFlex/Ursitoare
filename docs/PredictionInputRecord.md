# PredictionInputRecord

`Prediction.data.PredictionInputRecord`

## Overview

PredictionInputRecord is a data class that stores player input for a single simulation tick. It uses a dual-channel design: scalar (float) values for analog inputs like joystick axes, and binary (bool) values for discrete inputs like button presses. The same class is also used internally to store custom component state via `PhysicsStateRecord.componentState`.

The record uses a sequential write/read pattern: components write their values in order during sampling, and read them back in the same order during loading. This ensures deterministic input reconstruction on both client and server.

## How It Works With Other Components

- **PredictableControllableComponent**: Writes input using `WriteNextScalar` / `WriteNextBinary` during `SampleInput`, and reads it using `ReadNextScalar` / `ReadNextBool` during `LoadInput`.
- **PredictableComponent**: Uses the same read/write mechanism for component state via `PhysicsStateRecord.componentState`.
- **ClientPredictedEntity**: Maintains a `RingBuffer<PredictionInputRecord>` of local input history. During resimulation, stored inputs are replayed.
- **ServerPredictedEntity**: Maintains a `TickIndexedBuffer<PredictionInputRecord>` of received client inputs.
- **PredictionManager**: Serializes and transmits input records over the network.
- **PhysicsStateRecord**: Holds an optional `input` field (the input used during that tick) and a `componentState` field, both of which are PredictionInputRecord instances.

## Usage

```csharp
// In a PredictableControllableComponent:
public void SampleInput(PredictionInputRecord input)
{
    input.WriteNextScalar(horizontalAxis);  // float
    input.WriteNextScalar(verticalAxis);    // float
    input.WriteNextBinary(isFiring);        // bool
}

public void LoadInput(PredictionInputRecord input)
{
    horizontalAxis = input.ReadNextScalar();
    verticalAxis = input.ReadNextScalar();
    isFiring = input.ReadNextBool();
}
```

**Important**: The write and read order must be identical. Each component always reads/writes the same number of values.

## Fields

| Field | Type | Description |
|---|---|---|
| `scalarInput` | `float[]` | Array of float input values. |
| `binaryInput` | `bool[]` | Array of boolean input values. |
| `scalarFillIndex` | `int` | Current write position in the scalar array. |
| `binaryFillIndex` | `int` | Current write position in the binary array. |

## Public Methods

### Constructor
`PredictionInputRecord(int floatCapacity, int binaryCapacity)`
Creates a record with pre-allocated arrays.
- **Parameters**: `floatCapacity` - number of float slots; `binaryCapacity` - number of bool slots.

### `WriteReset()`
Resets both write and read cursors to the beginning. Call before writing a new set of values.
- **Returns**: void

### `ReadReset()`
Resets only the read cursors to the beginning. Call before reading values back.
- **Returns**: void

### `WriteNextScalar(float value)`
Writes a float value at the current write position and advances the cursor.
- **Parameters**: `value` - the float to write.
- **Returns**: void

### `WriteNextBinary(bool binary)`
Writes a bool value at the current write position and advances the cursor.
- **Parameters**: `binary` - the bool to write.
- **Returns**: void

### `ReadNextScalar() -> float`
Reads the next float value and advances the read cursor. Returns 0 if past the end.
- **Returns**: `float`

### `ReadNextBool() -> bool`
Reads the next bool value and advances the read cursor. Returns false if past the end.
- **Returns**: `bool`

### `From(PredictionInputRecord other)`
Copies all scalar and binary values from another record (element-wise, up to the smaller array size).
- **Parameters**: `other` - the source record.
- **Returns**: void

### `ToString() -> string`
Returns a formatted string showing all written values.
- **Returns**: `string`

## Configuration

PredictionInputRecord is a plain data class with no configuration flags.
