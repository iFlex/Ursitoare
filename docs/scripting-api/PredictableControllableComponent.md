# PredictableControllableComponent

**Namespace:** `Prediction`

Implement this interface on any MonoBehaviour that reads player input for a predicted entity. Components implementing this interface also implement `PredictableComponent`.

---

## Methods

### GetFloatInputCount

```csharp
int GetFloatInputCount()
```

Return the number of float values this component writes in `SampleInput`. Must be constant.

---

### GetBinaryInputCount

```csharp
int GetBinaryInputCount()
```

Return the number of bool values this component writes in `SampleInput`. Must be constant.

---

### SampleInput

```csharp
void SampleInput(PredictionInputRecord input)
```

Read the current player input (from Unity's Input system or equivalent) and write it into `input` using `WriteNextScalar` and `WriteNextBinary`. Called once per tick on the client for the locally controlled entity.

The order of writes must match the order of reads in `LoadInput` exactly. This order must also be the same on client and server.

---

### LoadInput

```csharp
void LoadInput(PredictionInputRecord input)
```

Read input values from `input` using `ReadNextScalar` and `ReadNextBool` in the same order as `SampleInput`. Store them in component fields. Called on both client and server before `ApplyForces`.

---

### ValidateInput

```csharp
bool ValidateInput(float deltaTime, PredictionInputRecord input)
```

Server-side validation. Check whether the input is within acceptable bounds. Return `false` to reject the input. `deltaTime` is the time delta for the tick this input covers. This method does not advance the read cursor; call `ReadReset` manually if you need to inspect values.

---

### ClearInput

```csharp
void ClearInput()
```

Reset all cached input state to zero/default. Called when ownership changes or the entity is reset.

---

## Notes

- `SampleInput` is called on the client only.
- `LoadInput` is called on both client (during prediction and resimulation) and server.
- `ValidateInput` is called on the server only.
- Input is serialised as flat arrays of floats and bools. The indices are positional — the order of writes and reads must never change between versions. Add new fields at the end.

---

## Example

```csharp
public class PlayerMovement : MonoBehaviour,
    PredictableControllableComponent, PredictableComponent
{
    private float _horizontal;
    private float _vertical;
    private bool  _jump;

    public int GetFloatInputCount()  => 2;
    public int GetBinaryInputCount() => 1;

    public void SampleInput(PredictionInputRecord input)
    {
        input.WriteNextScalar(Input.GetAxis("Horizontal"));
        input.WriteNextScalar(Input.GetAxis("Vertical"));
        input.WriteNextBinary(Input.GetButtonDown("Jump"));
    }

    public void LoadInput(PredictionInputRecord input)
    {
        _horizontal = input.ReadNextScalar();
        _vertical   = input.ReadNextScalar();
        _jump       = input.ReadNextBool();
    }

    public bool ValidateInput(float deltaTime, PredictionInputRecord input)
    {
        return true; // add anti-cheat checks here
    }

    public void ClearInput()
    {
        _horizontal = 0;
        _vertical   = 0;
        _jump       = false;
    }

    public void ApplyForces()
    {
        var rb = GetComponent<Rigidbody>();
        rb.AddForce(new Vector3(_horizontal, 0, _vertical) * speed, ForceMode.Acceleration);
        if (_jump) rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        _jump = false;
    }

    public bool HasState()                                         => false;
    public void SampleComponentState(PhysicsStateRecord psr)      { }
    public void LoadComponentState(PhysicsStateRecord psr)        { }
    public int  GetStateFloatCount()                               => 0;
    public int  GetStateBoolCount()                                => 0;

    private float speed     = 10f;
    private float jumpForce = 5f;
}
```

---

## See Also

- [PredictableComponent](PredictableComponent.md)
- [PredictionInputRecord](PredictionInputRecord.md)
- [AbstractPredictedEntity](AbstractPredictedEntity.md)
