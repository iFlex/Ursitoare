# PredictionInputRecord

**Namespace:** `Prediction.data`

Stores one tick's worth of player input as flat arrays of floats and bools. Used for both player input and component state serialisation.

Reading and writing use sequential cursors. The order of reads and writes must be consistent and identical on client and server.

---

## Fields

| Type | Name | Description |
|---|---|---|
| `float[]` | `scalarInput` | Float input values. |
| `bool[]` | `binaryInput` | Bool input values. |
| `int` | `scalarFillIndex` | Next write position for floats. |
| `int` | `binaryFillIndex` | Next write position for bools. |

---

## Constructors

### PredictionInputRecord(int, int)

```csharp
public PredictionInputRecord(int floatCapacity, int binaryCapacity)
```

Creates a record with the given float and bool array capacities. Use this for pre-allocated buffer slots.

### PredictionInputRecord()

```csharp
public PredictionInputRecord()
```

Serialisation constructor. Does not allocate arrays.

---

## Public Methods

### WriteReset

```csharp
public void WriteReset()
```

Resets both write cursors to 0. Call before writing a new input record.

---

### ReadReset

```csharp
public void ReadReset()
```

Resets both read cursors to 0. Call before reading from a record.

---

### WriteNextScalar

```csharp
public void WriteNextScalar(float value)
```

Writes `value` at the current float cursor and advances it. No-op if the array is full.

---

### WriteNextBinary

```csharp
public void WriteNextBinary(bool binary)
```

Writes `binary` at the current bool cursor and advances it. No-op if the array is full.

---

### ReadNextScalar

```csharp
public float ReadNextScalar()
```

Returns the float at the current cursor and advances it. Returns `0` if past the end.

---

### ReadNextBool

```csharp
public bool ReadNextBool()
```

Returns the bool at the current cursor and advances it. Returns `false` if past the end.

---

### From

```csharp
public void From(PredictionInputRecord other)
```

Copies float and bool values from `other` into this record.

---

## See Also

- [PredictableControllableComponent](PredictableControllableComponent.md)
- [PhysicsStateRecord](PhysicsStateRecord.md)
