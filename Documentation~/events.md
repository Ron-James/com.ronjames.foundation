# Events

Namespace `RonJames.Foundation.Events`, assembly `RonJames.Foundation.Events` (depends on Values).

Events are shared ScriptableObject assets that let one system say "this happened" (or "this value is now X") without knowing who is listening.

## The two asset types

| | **Event** (`EventAsset`) | **Variable Event** (`VariableEvent<T>`) |
|---|---|---|
| Carries | nothing | a typed value |
| Remembers | nothing | its value (Runtime Value) |
| Raise | notifies Listeners | notifies Listeners with the current value |
| Is also | an `IEvent` | an `IEvent` *and* a writable, observable Value Source |
| Create | *Create > RonJames Foundation > Event* | *Create > RonJames Foundation > Variable Events > …* |

`EventAsset` is named that way only to avoid clashing with `UnityEngine.Event`; in prose it's just an Event.

A Variable Event deliberately combines "variable" and "event" (ADR 0002). In practice almost every typed event was also read for its last value. Because it is a Value Source, any `VariableReference<T>` can point at it, and because it is an `IEvent`, value-less listeners can listen to it too.

### Set, Set Silently, Raise

```csharp
hasKey.Set(true);          // change the value, then Raise
hasKey.SetSilently(true);  // change the value, don't Raise
hasKey.Raise();            // Raise again with the current value (even if it's null)
target.RaiseCurrent();     // Raise the current value, or the Initial Value if the current one is null
bool value = hasKey.Value; // read the Runtime Value
```

- **Raise Current** is for reference-type values (a `GameObject`, a `Product`…) that may have been cleared or destroyed. If the current value is null, including a destroyed Unity object, it puts (a copy of) the Initial Value back and Raises that. The fallback is stored first, so Listeners reading `Value` see what they were given. For value types (`int`, `bool`, `Vector3`…) the value is never null, so it's the same as Raise. It takes no parameters, so you can wire it from a UnityEvent, and it has a button in the play-mode inspector.

- **Set always Raises**, even when the value is unchanged: it's an event, and "the same item was picked twice" must still notify. For state-like values, turn on **Only Raise On Change** on the asset.
- Writing through a `VariableReference<T>` in Source mode, or through `IWritableValueSource`, is a **Set**.
- In play mode the inspector shows the Runtime Value (editable: editing it Sets it), a Raise button, and the Listener and Raise counts.

### Initial Value and Runtime Value

Each Variable Event has a serialized **Initial Value** (what you author) and a non-serialized **Runtime Value** (what Set changes).

- At the start of **every play session**, the Runtime Value resets to a *copy* of the Initial Value and every Listener is dropped (see `PlayModeReset` in [values.md](values.md)). This works with domain reload disabled.
- Reference-type values (lists, classes) are deep-copied with Odin, so adding to a list at runtime never changes the authored Initial Value. Unity objects and value types are shared, not copied.
- The Runtime Value is never written back to the asset. Builds and the editor behave the same.
- **Keep Runtime Value In Editor** opts one asset out in the editor only: its value carries over between play sessions (stored in the editor session, so it survives domain reloads). Builds always start from the Initial Value.

## Listening

### Rules every Raise follows

1. Listeners are notified **in subscription order**. Subscribing the same Listener twice is ignored.
2. Subscribing or unsubscribing **during a Raise** takes effect on the **next** Raise.
3. If a Listener **Sets the event it is listening to**, that nested Raise is delivered **immediately**. Listeners later in the outer Raise then read the newest value, so everyone ends up with the latest value. Nesting deeper than 32 levels is refused with an error: it's almost certainly an infinite loop.
4. A Listener that **throws** is logged (click the log to select it) and the remaining Listeners still run.
5. **Destroyed** Unity listeners are skipped, and so are delegates whose target is a destroyed Unity object (e.g. `Subscribe(OnRaised)` from a MonoBehaviour destroyed without unsubscribing). Lambdas capture a closure rather than the object, so they can't be detected: always unsubscribe them.

### From code

```csharp
doorBell.Subscribe(OnBell);                    // Action
hasKey.Subscribe((Action<bool>)OnHasKey);      // Action<T>
hasKey.Subscribe(myListener);                  // IVariableEventListener<bool> or IEventListener
hasKey.ValueChanged += OnAnyChange;            // IObservableValueSource
// ...and the matching Unsubscribe in OnDisable / OnDestroy.
```

Implement `IEventListener.OnEventRaised()` or `IVariableEventListener<T>.OnEventRaised(T value)` to make your own class a listener.

### The Listener components

| Component | Listens to | Gates | Runs |
|---|---|---|---|
| **Event Listener** | one or more Events (Variable Events work too; their value is ignored) | an optional **Condition** | its **Responses** (`IResponse`) |
| **Variable Event Listener** (one per value type, e.g. `BoolVariableEventListener`) | one or more Variable Events of that type | a **Filter** on the value, then an optional **Condition** | its **Responses** with the value (`IResponse<T>`) |

- They subscribe in `OnEnable` and unsubscribe in `OnDisable`.
- **Receive Current Value On Enable** (Variable Event Listener, off by default) delivers each Variable Event's current value once on enable, as if it had just been Raised. Use it when the listener may enable after the value was Set.
- The **Respond (test)** button runs the Responses by hand.
- **Filter vs Condition:** the Filter is about the Raised *value* ("is it true?", "is it over 5?"). The Condition is about the *world* at that moment ("does the door have power?"). Either can be empty.

### Responses

A **Response** is one thing a Listener does. Listeners hold a list of them, so one Raise can do several things.

| Response | Does |
|---|---|
| `UnityEventResponse` | invokes a UnityEvent |
| `UnityEventResponse<T>` | invokes a `UnityEvent<T>` with the value, and a plain UnityEvent |
| `RunCommandSequenceResponse` / `<T>` (Commands assembly) | starts a Run of a Command Sequence |

Write your own by implementing `IResponse` (for Event Listeners) or `IResponse<T>` (for Variable Event Listeners). Don't mark it `[Serializable]`; it then appears in the Response picker.

```csharp
public class PlaySoundResponse : IResponse
{
    [SerializeField] private AudioSource source;
    public void Respond() => source.Play();
}
```

## Your own value types

Unity can't create assets or components of an open generic type, so each value type needs a Variable Event subclass, a Listener subclass and (for Commands) a Set Variable subclass, one line each. Built in: `bool`, `int`, `float`, `string`, `Vector2`, `Vector3`, `Quaternion`, `Color`, `GameObject`, `Transform`.

For anything else use **Assets > Create > RonJames Foundation > Variable Event Type…** (also under *Tools > RonJames Foundation*). Type the value type, pick a folder, and it writes:

```csharp
[CreateAssetMenu(menuName = "RonJames Foundation/Variable Events/Item")]
public class ItemVariableEvent : VariableEvent<Item> { }

[AddComponentMenu("RonJames Foundation/Variable Event Listeners/Item Variable Event Listener")]
public class ItemVariableEventListener : VariableEventListener<Item> { }

public class SetItemVariableCommand : SetVariableCommand<Item> { }
```

The Variable Event and Listener must each be in a file named after the class (Unity's rule for ScriptableObjects and components). The generator does this for you.

## Global by design

A Variable Event asset is one value for the whole game. Two instances of the same prefab that point at "Score" share one score. That's what you want for genuinely shared state (has the player got the key, which step are we on). For a value that belongs to one object, use a Variable Reference in **Value** mode. Values shared within one prefab instance are out of scope in this version.

## Finding what uses an event

Right-click an Event or Variable Event asset > **RonJames Foundation > Find References** (or *Tools > RonJames Foundation > Reference Finder*). It lists:

- **components in open scenes** (and the open prefab) that reference it: Listeners, Command Sequences, anything holding a Variable Reference to it;
- **project assets** (scenes, prefabs, ScriptableObjects) that depend on it.

It reads through Odin's serialized data, so references inside Commands and Variable References are found too.
