# PhysicsStateRecord

`Prediction.data.PhysicsStateRecord`

## Overview

PhysicsStateRecord is a data class that captures a complete snapshot of a Rigidbody's physics state at a specific simulation tick. It stores position, rotation, linear velocity, angular velocity, and optionally player input and custom component state. These records are the fundamental unit of data exchanged between client and server, stored in history buffers, and compared to detect desyncs.

## How It Works With Other Components

- **ClientPredictedEntity**: Maintains two buffers of PhysicsStateRecords:
  - `localStateBuffer` (RingBuffer): Stores client-predicted states indexed by tick.
  - `serverStateBuffer` (TickIndexedBuffer): Stores authoritative server states indexed by tick.
  - Desync detection compares entries from these two buffers.
- **ServerPredictedEntity**: Samples a PhysicsStateRecord after each tick via `SamplePhysicsState()` and sends it to clients.
- **PredictionManager**: Routes PhysicsStateRecords between server and client entities via network hooks.
- **CustomVisualInterpolator**: Receives PhysicsStateRecords and interpolates between them for smooth visual rendering.
- **PredictableComponent**: Components with state write/read their data to/from the `componentState` field.

## Usage

Records are typically created and managed internally by the prediction system. You interact with them primarily when implementing `PredictableComponent.SampleComponentState` / `LoadComponentState`, or when subscribing to entity events.

```csharp
// Reading a state record from an event
entity.newStateReached.AddEventListener((PhysicsStateRecord state) => {
    Debug.Log($"Position: {state.position}, Tick: {state.tickId}");
});
```

## Fields

| Field | Type | Description |
|---|---|---|
| `tickId` | `uint` | The simulation tick this snapshot corresponds to. |
| `position` | `Vector3` | World-space position of the Rigidbody. |
| `rotation` | `Quaternion` | World-space rotation of the Rigidbody. |
| `velocity` | `Vector3` | Linear velocity. |
| `angularVelocity` | `Vector3` | Angular velocity. |
| `input` | `PredictionInputRecord` | The player input that was active during this tick (may be null). |
| `componentState` | `PredictionInputRecord` | Custom component state using the same read/write mechanism as input records (may be null). |

## Public Methods

### `AllocWithComponentState(int componentFloats, int componentBools) -> PhysicsStateRecord` (static)
Creates a new record with an allocated `componentState` field sized for the given number of float and bool values.
- **Parameters**: `componentFloats` - number of float state values; `componentBools` - number of bool state values.
- **Returns**: `PhysicsStateRecord`

### `Alloc() -> PhysicsStateRecord` (static)
Creates a new empty record without component state.
- **Returns**: `PhysicsStateRecord`

### `Empty() -> PhysicsStateRecord` (static)
Creates a new record with all values zeroed/default.
- **Returns**: `PhysicsStateRecord`

### `From(Rigidbody rigidbody)`
Populates this record from a Rigidbody's current state (position, rotation, velocity, angular velocity).
- **Parameters**: `rigidbody` - the source Rigidbody.
- **Returns**: void

### `From(PhysicsStateRecord record)`
Deep-copies all fields from another record, including input and component state if both are non-null.
- **Parameters**: `record` - the source record.
- **Returns**: void

### `From(PhysicsStateRecord record, uint tickOverride)`
Deep-copies from another record but overrides the tick id.
- **Parameters**: `record` - the source; `tickOverride` - the tick to assign.
- **Returns**: void

### `To(Rigidbody r)`
Applies this record's state to a Rigidbody (sets position, rotation, velocity, angular velocity).
- **Parameters**: `r` - the target Rigidbody.
- **Returns**: void

### `Equals(object obj) -> bool`
Compares two records by tick id, position, rotation, velocity, and angular velocity.
- **Parameters**: `obj` - the object to compare.
- **Returns**: `bool`

### `GetHashCode() -> int`
Hash based on tick id and physics values.
- **Returns**: `int`

### `ToString() -> string`
Returns a formatted string with all physics values.
- **Returns**: `string`

## Configuration

PhysicsStateRecord is a plain data class with no configuration flags.
