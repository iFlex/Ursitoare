# RingBuffer&lt;T&gt;

**Namespace:** `Prediction.utils`

Fixed-capacity circular buffer. When full, adding a new item silently overwrites the oldest.

Used internally for local state history (`ClientPredictedEntity.localStateBuffer`, `localInputBuffer`) and the physics world history in `RewindablePhysicsController`.

---

## Properties

| Type | Name | Description |
|---|---|---|
| `T` | `emptyValue` | Value returned when accessing an empty buffer. Default: `default(T)`. |

---

## Constructor

```csharp
public RingBuffer(int capacity)
```

Creates a buffer with the given capacity.

---

## Public Methods

### Add

```csharp
public void Add(T item)
```

Appends `item`. If the buffer is full, the oldest item is overwritten.

---

### Set

```csharp
public void Set(int index, T data)
```

Writes `data` directly at the given raw index (modulo capacity). Does not advance any cursor.

---

### Get

```csharp
public T Get(int index)
```

Returns the item at `index % capacity`. Throws if the buffer is empty or `index` is negative.

---

### GetWithLocalIndex

```csharp
public T GetWithLocalIndex(int localIndex)
```

Returns the item at `localIndex` positions from the start. Returns `emptyValue` if out of range.

---

### GetEnd

```csharp
public T GetEnd()
```

Returns the most recently added item, or `emptyValue` if empty.

---

### GetStart

```csharp
public T GetStart()
```

Returns the oldest item still in the buffer, or `emptyValue` if empty.

---

### PopStart

```csharp
public T PopStart()
```

Removes and returns the oldest item. Returns `emptyValue` if empty.

---

### PopEnd

```csharp
public T PopEnd()
```

Removes and returns the most recently added item. Returns `emptyValue` if empty.

---

### GetCapacity

```csharp
public int GetCapacity()
```

Returns the maximum number of items the buffer can hold.

---

### GetFill

```csharp
public int GetFill()
```

Returns the current number of items in the buffer.

---

### GetStartIndex

```csharp
public int GetStartIndex()
```

Returns the raw array index of the oldest item.

---

### GetEndIndex

```csharp
public int GetEndIndex()
```

Returns the raw array index of the next write position (one past the newest item).

---

### Clear

```csharp
public void Clear()
```

Resets start, end, and fill to 0. Does not zero the underlying array.

---

## See Also

- [TickIndexedBuffer&lt;T&gt;](TickIndexedBuffer.md)
