# WrapperHelpers

**Namespace:** `Prediction.Wrappers`

Static helper methods for extracting prediction component interfaces from arrays of `MonoBehaviour` objects.

---

## Static Methods

### GetControllableComponents

```csharp
public static PredictableControllableComponent[] GetControllableComponents(MonoBehaviour[] objects)
```

Filters `objects` and returns all entries that implement `PredictableControllableComponent`.

---

### GetComponents

```csharp
public static PredictableComponent[] GetComponents(MonoBehaviour[] objects)
```

Filters `objects` and returns all entries that implement `PredictableComponent`.

---

## Usage

```csharp
var allComponents = GetComponentsInChildren<MonoBehaviour>();

var controllable = WrapperHelpers.GetControllableComponents(allComponents);
var predictable  = WrapperHelpers.GetComponents(allComponents);

var clientEntity = new ClientPredictedEntity(id, isServer, 64, rb, visuals, controllable, predictable);
```

---

## See Also

- [PredictableComponent](PredictableComponent.md)
- [PredictableControllableComponent](PredictableControllableComponent.md)
- [PredictedEntity](PredictedEntity.md)
