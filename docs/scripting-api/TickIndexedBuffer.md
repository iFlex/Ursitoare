# TickIndexedBuffer&lt;T&gt;

**Namespace:** `Prediction.utils`

Dictionary-backed buffer keyed by tick ID. Stores a bounded number of entries. When full, the oldest entry is evicted on the next `Add`.

Used internally for server state history (`ClientPredictedEntity.serverStateBuffer`) and client input queues (`ServerPredictedEntity.inputQueue`).

---

## Properties

| Type | Name | Description |
|---|---|---|
| `T` | `emptyValue` | Value returned when a lookup finds no entry. Default: `default(T)`. |

---

## Constructor

```csharp
public TickIndexedBuffer(int capacity)
```

Creates a buffer with the given capacity.

---

## Public Methods

### Add

```csharp
public void Add(uint tickId, T item)
```

Stores `item` at `tickId`. If the buffer is at capacity, the oldest entry is evicted first. Updates the tracked start and end tick IDs.

---

### Remove

```csharp
public T Remove(uint tickId)
```

Removes and returns the item at `tickId`. Updates start/end tick tracking. Returns `emptyValue` if not found.

---

### Get

```csharp
public T Get(uint tickId)
```

Returns the item at `tickId`, or `emptyValue` if not present.

---

### Contains

```csharp
public bool Contains(uint tickId)
```

Returns `true` if an entry exists for `tickId`.

---

### GetEnd

```csharp
public T GetEnd()
```

Returns the item with the highest tick ID, or `emptyValue` if empty.

---

### GetEndTick

```csharp
public uint GetEndTick()
```

Returns the highest tick ID in the buffer.

---

### GetStart

```csharp
public T GetStart()
```

Returns the item with the lowest tick ID, or `emptyValue` if empty.

---

### GetStartTick

```csharp
public uint GetStartTick()
```

Returns the lowest tick ID in the buffer.

---

### GetFill

```csharp
public int GetFill()
```

Returns the number of entries currently stored.

---

### GetCapacity

```csharp
public int GetCapacity()
```

Returns the maximum number of entries the buffer can hold.

---

### GetRange

```csharp
public uint GetRange()
```

Returns `end - start`, the tick span covered by the buffer.

---

### GetNextTick

```csharp
public uint GetNextTick(uint tickId)
```

Returns the smallest stored tick ID greater than `tickId`. Returns `0` if `tickId` is beyond the end.

---

### GetPrevTick

```csharp
public uint GetPrevTick(uint tickId)
```

Returns the largest stored tick ID less than `tickId`. Returns `0` if `tickId` is before the start.

---

### Clear

```csharp
public void Clear()
```

Removes all entries and resets start and end tick IDs to 0.

---

## See Also

- [RingBuffer&lt;T&gt;](RingBuffer.md)
