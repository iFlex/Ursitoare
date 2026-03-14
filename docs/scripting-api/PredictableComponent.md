# PredictableComponent

**Namespace:** `Prediction`

Implement this interface on any MonoBehaviour that applies forces to a predicted entity's Rigidbody. The entity calls these methods every tick and during each resimulation step.

---

## Methods

### ApplyForces

```csharp
void ApplyForces()
```

Called once per tick and once per resimulation step. Apply forces, torques, or direct velocity changes to the Rigidbody here. Do not read or write game state that is not rewound during resimulation.

---

### HasState

```csharp
bool HasState()
```

Return `true` if this component has game state that must be saved and restored during resimulation. If `true`, the remaining state methods are called.

---

### SampleComponentState

```csharp
void SampleComponentState(PhysicsStateRecord physicsStateRecord)
```

Write this component's state into `physicsStateRecord.componentState`. Use `WriteNextScalar` and `WriteNextBinary` in a consistent order. Called after each tick and after each resimulation step.

---

### LoadComponentState

```csharp
void LoadComponentState(PhysicsStateRecord physicsStateRecord)
```

Restore this component's state from `physicsStateRecord.componentState`. Use `ReadNextScalar` and `ReadNextBool` in the same order as `SampleComponentState`. Called at the start of resimulation and before each replay step.

---

### GetStateFloatCount

```csharp
int GetStateFloatCount()
```

Return the number of float values this component writes in `SampleComponentState`. Must be constant.

---

### GetStateBoolCount

```csharp
int GetStateBoolCount()
```

Return the number of bool values this component writes in `SampleComponentState`. Must be constant.

---

## Notes

- The order in which contributors call `SampleComponentState` and `LoadComponentState` is fixed by the order they are passed to the entity constructor. This order must be the same on client and server.
- If `HasState` returns `false`, the state methods are never called and `GetStateFloatCount` / `GetStateBoolCount` can return `0`.

---

## Example

```csharp
public class BoostComponent : MonoBehaviour, PredictableComponent
{
    private bool _boosting;
    private float _boostCharge;

    public bool HasState() => true;
    public int GetStateFloatCount() => 1;
    public int GetStateBoolCount() => 1;

    public void SampleComponentState(PhysicsStateRecord psr)
    {
        psr.componentState.WriteNextScalar(_boostCharge);
        psr.componentState.WriteNextBinary(_boosting);
    }

    public void LoadComponentState(PhysicsStateRecord psr)
    {
        _boostCharge = psr.componentState.ReadNextScalar();
        _boosting    = psr.componentState.ReadNextBool();
    }

    public void ApplyForces()
    {
        if (_boosting)
            GetComponent<Rigidbody>().AddForce(transform.forward * 100f);
    }
}
```

---

## See Also

- [PredictableControllableComponent](PredictableControllableComponent.md)
- [AbstractPredictedEntity](AbstractPredictedEntity.md)
- [PhysicsStateRecord](PhysicsStateRecord.md)
