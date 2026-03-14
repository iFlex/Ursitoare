# QuaternionAverage

**Namespace:** *(global)*

Computes a weighted average of multiple quaternions using the outer-product matrix / power-iteration method. Used by `MovingAverageInterpolator` to average rotation across a sliding window.

---

## Constructor

```csharp
public QuaternionAverage()
```

Initialises the accumulation matrix to zero.

---

## Public Methods

### Reset

```csharp
public void Reset()
```

Clears the accumulation matrix. Call before starting a new window average.

---

### AccumulateRot

```csharp
public void AccumulateRot(Quaternion rotation)
```

Adds `rotation` to the accumulation matrix. Call once per quaternion in the window.

---

### GetAverageRotation

```csharp
public Quaternion GetAverageRotation(int rotationCount)
```

Computes and returns the average quaternion for all accumulated rotations. `rotationCount` must equal the number of `AccumulateRot` calls since the last `Reset`. Uses 10 iterations of power iteration.

---

## Usage

```csharp
var avg = new QuaternionAverage();

avg.AccumulateRot(q1);
avg.AccumulateRot(q2);
avg.AccumulateRot(q3);

Quaternion result = avg.GetAverageRotation(3);
avg.Reset();
```

---

## See Also

- [MovingAverageInterpolator](MovingAverageInterpolator.md)
