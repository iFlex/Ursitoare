# PredictedEntityVisuals

`Prediction.PredictedEntityVisuals` (MonoBehaviour)

## Overview

PredictedEntityVisuals is a Unity MonoBehaviour that provides smooth visual rendering for predicted entities. It detaches a visual representation from the physics Rigidbody and interpolates it independently, so that the player sees smooth motion even when the underlying physics object snaps or resimulates.

Without this component, players would see the Rigidbody teleport whenever a resimulation corrects the prediction. The visuals object follows the physics object with smooth interpolation, masking these corrections.

## How It Works With Other Components

- **ClientPredictedEntity**: The visuals component subscribes to the entity's `newStateReached` event to receive physics states after each tick. It also subscribes to `onReset` to reset the interpolation when the entity is reset.
- **VisualsInterpolationsProvider** (e.g., `CustomVisualInterpolator`): The actual interpolation logic is delegated to a provider. The visuals component feeds states into it and calls its `Update()` each frame.
- **PredictionManager**: The `Update()` method reads `PredictionManager.Instance.tickId` to pass the current tick to the interpolation provider.

## Usage

1. Attach `PredictedEntityVisuals` to the same GameObject as the Rigidbody.
2. Assign the `visualsEntity` field to the child GameObject that contains the visual mesh/renderer.
3. After creating the `ClientPredictedEntity`, call:

```csharp
var visuals = GetComponent<PredictedEntityVisuals>();
var provider = PredictionManager.INTERPOLATION_PROVIDER();
visuals.SetClientPredictedEntity(clientEntity, provider);
```

The component will:
- Detach the visuals object from the Rigidbody's transform hierarchy (so physics snaps don't move it).
- Feed each new physics state into the interpolation provider.
- Each `Update()` frame, tell the provider to interpolate and position the visuals.

## Public Methods

### `SetClientPredictedEntity(ClientPredictedEntity clientPredictedEntity, VisualsInterpolationsProvider provider)`
Initializes the visuals component. Detaches the visual object from the physics hierarchy, subscribes to entity events, and sets up the interpolation provider.
- **Parameters**: `clientPredictedEntity` - the entity to track; `provider` - the interpolation implementation.
- **Returns**: void

### `Reset()`
Resets the interpolation provider, clearing all buffered states. Called automatically when the entity resets.
- **Returns**: void

### `SetControlledLocally(bool ctlLoc)`
Notifies the interpolation provider whether this entity is locally controlled, allowing it to adjust smoothing behaviour.
- **Parameters**: `ctlLoc` - true if locally controlled.
- **Returns**: void

## Configuration Flags

| Flag | Type | Default | Description |
|---|---|---|---|
| `SHOW_DBG` | `static bool` | `false` | When true and debug ghosts are instantiated, makes them visible. |
| `visualsEntity` | `GameObject` (serialized) | none | The child GameObject containing the visual mesh. This gets detached and interpolated. |
| `debug` | `bool` (serialized) | `false` | When true, instantiates server and client ghost prefabs for visual debugging. |
| `serverGhostPrefab` | `GameObject` (serialized) | none | Prefab shown at the server's reported position (debug only). |
| `clientGhostPrefab` | `GameObject` (serialized) | none | Prefab shown at the client's predicted position (debug only). |
| `artifficialDelay` | `double` | `1.0` | An artificial time offset subtracted from the interpolation start time. |
