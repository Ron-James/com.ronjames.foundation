# Values

Namespace `RonJames.Foundation.Values`, assembly `RonJames.Foundation.Values`.

This module is the foundation the other two build on: a common way to say "something that provides a value", a field type that can either hold a value or point at one, and yes/no tests over values (Filters) and over the world (Conditions).

## Value Sources

A **Value Source** is anything that can provide a value: a Variable Event asset, a component, or a plain C# object.

```csharp
interface IValueSource                  { object Value { get; }  Type ValueType { get; } }   // untyped
interface IValueSource<out T>           : IValueSource { new T Value { get; } }              // typed
interface IWritableValueSource          : IValueSource { void SetValue(object value); }
interface IWritableValueSource<T>       : IValueSource<T>, IWritableValueSource { void SetValue(T value); }
interface IObservableValueSource        : IValueSource { event Action ValueChanged; }
```

- **Every typed source is also an untyped one.** Code that only needs "some value" (logging, binding, generic tools) can take an `IValueSource` and read `Value` as an `object`. The untyped value is the typed value, boxed. A class that implements several typed sources declares which one its untyped `Value` returns.
- **Reading and writing are separate.** Not every source can be written. Writing is only possible through `IWritableValueSource(<T>)`.
- **Untyped writes convert or fail.** `IWritableValueSource.SetValue(object)` converts when it's safe (numbers, bools, strings, enums, using the invariant culture), and throws `InvalidCastException` with the source named otherwise. Writing `null` into a value type throws. Nothing is silently ignored.
- **Observable sources** say when they changed, which lets waiting code avoid polling. Every Variable Event is observable.

Built-in Value Sources: every **Variable Event**; every **`VariableReference<T>`** (a reference is itself a source); and **`ListElementSource<T>`** (element *i* of a list, where both the list and the index are Variable References).

### Writing your own Value Source

Implement the typed interface and route the untyped members to the typed ones:

```csharp
public class HealthComponent : MonoBehaviour, IValueSource<float>
{
    [SerializeField] private float health = 100f;

    public float Value => health;
    object IValueSource.Value => Value;
    public Type ValueType => typeof(float);
}
```

Any `VariableReference<float>` in Source mode can now point at this component.

## Variable Reference

`VariableReference<T>` is the field type you use whenever a value might be a constant or might come from somewhere shared.

| Mode | What it is | Reads | Writes |
|---|---|---|---|
| **Value** | the reference holds its own value, private to the object it's on | that value | changes that value |
| **Source** | the reference points at a Value Source (usually a global Variable Event) | the source's value (`default` if missing) | the source's `SetValue`, which for a Variable Event is a **Set**, so it Raises |

```csharp
[SerializeField] private VariableReference<float> speed = new VariableReference<float>(2f);

float s = speed.Value;      // or just: float s = speed;  (implicit conversion, null-safe)
speed.Value = 3f;           // throws if the source is read-only or missing
```

Behaviour worth knowing:

- **Missing sources:** in Source mode, if the source is unassigned or destroyed, `IsMissing` is true, reads return `default` and the inspector shows an error. Writes throw `InvalidOperationException`.
- **Read-only sources:** `CanWrite` tells you whether a write will work. Writing to a source that isn't writable throws instead of doing nothing.
- **Change notification:** `TryAddChangedListener(action)` subscribes when the reference can notify (always in Value mode; in Source mode when the source is observable) and returns `false` otherwise, so you know to poll. Remove with `RemoveChangedListener`. `CanNotify` tells you in advance.
- **From code:** `new VariableReference<T>(value)` creates one in Value mode; `VariableReference<T>.FromSource(source)` creates one in Source mode.
- **Value mode is private.** Two objects in Value mode never share a value. To share, point both at the same Variable Event. Shared *per-instance* values (one value per prefab instance, shared by everything inside it) are out of scope in this version.

> **Host it in an Odin-serialized object.** `VariableReference<T>` is intentionally not `[Serializable]`, because its source is an interface only Odin can serialize. Put it on a `SerializedMonoBehaviour` or `SerializedScriptableObject`, or inside a Command, Filter, Condition or Response, which are always hosted by Odin. On a plain `MonoBehaviour` it won't be saved.

## Filters

A **Filter** is a yes/no test applied to a value: `IFilter<in T> { bool Allows(T value); }`. Variable Event Listeners use one to decide whether to respond to a Raise.

| Filter | Passes when |
|---|---|
| `EqualityFilter<T>` | the value equals another value (a Variable Reference). Destroyed Unity objects count as null. |
| `ComparisonFilter<T>` (`T : IComparable<T>`) | the value is `=`, `≠`, `>`, `<`, `≥` or `≤` another value. `float`/`double` equality uses a small tolerance; every other type is exact. |
| `TypeFilter<T>` | the value is an instance of one of the selected types. Null never passes. |
| `NullFilter<T>` | the value is null, **including a Unity object that has been destroyed ("missing")**. |
| `CompositeFilter<T>` | its parts pass under **All / Any / None**. |

**Combining:** `All` passes when every part passes (and when empty). `Any` passes when at least one passes (fails when empty). `None` passes when no part passes (and when empty). Null parts are ignored. To **invert** a Filter, wrap it in a Composite set to `None`. There is no separate invert flag anywhere.

Write your own by implementing `IFilter<T>`. Don't mark the class `[Serializable]`; Odin serializes it. It then appears in the Filter picker automatically.

## Conditions

A **Condition** is a yes/no question about the current state of the world that takes no input: `ICondition { bool IsMet(); }`. Branch Commands, Wait Until Commands and Listeners use them.

| Condition | Met when |
|---|---|
| `BoolCondition` | a `VariableReference<bool>` is true |
| `FilterCondition<T>` | a Variable Reference's value passes a Filter. This is the bridge between Filters (about a value) and Conditions (about the world). With no Filter assigned it throws, so a mis-authored condition fails loudly. |
| `CompositeCondition` | its parts are met under **All / Any / None** (same rules as Filters) |

**Observable Conditions:** a Condition can also implement `IObservableCondition` to say "my answer may have changed". `BoolCondition`, `FilterCondition<T>` and `CompositeCondition` do so whenever the references they read can notify. That lets *Wait Until* react to a Variable Event changing instead of polling every frame. A Composite only notifies if every part can.

## Play-mode reset

`PlayModeReset` is the single place that resets runtime state at the start of every play session. It runs at `SubsystemRegistration`, before any scene loads, and it works with *Enter Play Mode Options* (domain reload disabled).

Anything implementing `IPlayModeResettable` registers in `OnEnable` with `PlayModeReset.Register(this)`, unregisters in `OnDisable`, and gets `ResetForPlayMode()` called at play start. Events and Variable Events already do this. You only need it for your own ScriptableObjects or static state that hold runtime data.

## Small helpers

- `UnityNull.IsNull(object)`: true for `null` *and* for destroyed Unity objects. A plain `== null` on an interface or `object` misses the destroyed case.
- `ValueEquality.AreEqual<T>(a, b)`: equality with the same null rules.
- `ValueConversion.To<T>(object, target)`: the conversion used by untyped writes.
