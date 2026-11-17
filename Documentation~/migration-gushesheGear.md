# Migrating GushesheGear

GushesheGear (`F:\Unity\GushesheGear`, Unity 6000.3, Odin) has an older generation of the same ideas: **Action SOs** (events that hold a value), **listener MonoBehaviours**, and **Action References** (variable references). Unlike the Bizar projects ([migration.md](migration.md)), this project is meant to be **migrated onto the package**. This guide is the plan for doing that cleanly, without a big-bang rewrite.

The old code lives in `Assets/_Assets_James/Scripts/ActionSO/` (with the subscription structs in `SaveData/ActionSubscription.cs` and the reset in `SceneManagement/SceneLoadManager.cs`).

## What's there today

Measured across the project's scenes, prefabs, assets and scripts:

| Thing | Count | Becomes |
|---|---|---|
| `VoidActionSo` assets | 42 | `EventAsset` |
| typed `ActionSO<T>` assets (Int 12, Float 10, Vector2 8, Transform 6, Bool 6, game types ~55) | ~100 | `VariableEvent<T>` |
| `BaseActionListener` components | 81 | **Event Listener** + `UnityEventResponse` |
| typed `ActionListener<T>` components (Int 32, Product 17, Float 17, Mission 15, StuntState 12, Vector2 10…) | ~150 | **Variable Event Listener** of that type |
| scripts using `ActionReference<T>` | 22 | `VariableReference<T>` |
| scripts using `ValueAssetReference<T>` / `ValueAsset<T>` | 16 | `VariableReference<T>` / a Value Source |
| scripts using `ActionSubscription` / `MessageSubscription<T>` | 8 / 5 | `IEvent` / `IVariableEvent<T>` fields + `Subscribe` |

How much each old listener feature is actually used:

| Old feature | Used by | Plan |
|---|---|---|
| listen value (`_useListenValues`) | **55** listeners | `EqualityFilter` (see below) |
| `updateOnEnable` | **31** | Receive Current Value On Enable. **Timing differs**, see below. |
| `_invertListenValue` | 3 | `CompositeFilter(None)` |
| previous-value filter (`usePreviousValueFilter`) | **1** (`DriftMeterUIProgressBAr.prefab`) | no built-in equivalent; see below |
| `onTrue` / `onFalse` (BoolActionListener) | 4 / 5 wired | a small project Response (recipe below) |
| `onValueTransition`, `onActionRaised`, `onPreviousValueSet`, `onListenValueSet`, `onCleared` | **0** wired | drop |
| `checkNullValue` | 0 | **default flips**, see below |
| `AsyncRaise` / `ActionReference.RequestChange` | 0 | drop |
| `PreviousValue` / `PreviousStoredValue` in code | 0 outside the ActionSO folder | drop |
| `raiseOnLoad` | 0 assets | drop |
| `autoCleanOnRaise` turned off | 0 assets | automatic now |

So almost everything maps one-to-one. The few features with no equivalent are effectively unused.

---

## Concept map

| GushesheGear | Package |
|---|---|
| `BaseActionSO` / `VoidActionSo` ("Void Action") | `EventAsset` (an Event) |
| `ActionSO<T>` (`IntActionSO`, `BoolActionSO`, `ProductActionSO`…) | `VariableEvent<T>` (`IntVariableEvent`, `BoolVariableEvent`, generated types) |
| `_defaultCallValue` | **Initial Value** |
| `StoredValue` (get) | `Value` |
| `StoredValue = x` / `Raise(x)` | `Set(x)` |
| `SetStoredValueDirectly(x)` | `SetSilently(x)` |
| `Raise()` on a typed action | ⚠ **not `Raise()`**. Old `Raise()` raised the *default value*; new `Raise()` raises the *current* value. Use `Set(variable.InitialValue)` where "raise the default" was meant, or **`RaiseCurrent()`** where "raise what's there, or the default if nothing is" was meant. |
| `ResetToDefault()` | `ResetForPlayMode()` (also drops listeners), or `Set(InitialValue)` |
| `PreviousValue` / `PreviousStoredValue` | none |
| `HasRaised`, `RaiseCount` | `RaiseCount` (play-mode inspector) |
| `Subscribe(origin, UnityAction)` → `IGuidIdentifiable` | `Subscribe(Action)` or `Subscribe(IEventListener)`. Keep the delegate/listener and pass it to `Unsubscribe`. |
| `Subscribe(origin, UnityAction<T>)` | `Subscribe(Action<T>)` or `Subscribe(IVariableEventListener<T>)` |
| `RemoveByGuid`, `Unsubscribe(origin, guid)`, `UnsubscribeAll(origin)` | `Unsubscribe(sameDelegateOrListener)` |
| `RemoveNullSubscribers` / `autoCleanOnRaise` | automatic: destroyed listener objects, and delegates whose target object was destroyed, are skipped |
| `AsyncRaise(value, callback)` (raise next frame via `CommandQueue`) | none (unused). In a Command Sequence, use Delay + Set Variable. |
| `Raise(IEnumerable<T>)` (raise a random element) | none. Pick the element yourself and `Set` it. |
| `IntActionSO.AddAmount(n)` | `number.Set(number.Value + n)`, or a small Command |
| `OnEditorStopped` / `OnSceneStarted` reset via `SceneLoadManager` (`Resources.LoadAll`, so **only assets under `Resources/`**) | `PlayModeReset`: **every loaded** Event / Variable Event resets at play start, wherever it lives |
| `SaveableSO` base (`OnLoad`, `raiseOnLoad`) | not a save participant. No ActionSO has `[Save]` fields, so no saved state is lost. |
| `BaseActionListener` (one action, one UnityEvent) | **Event Listener** (any number of Events, optional Condition, Responses) |
| `ActionListener<T>` + `TriggerUnityEvents` subclasses | **Variable Event Listener** + Responses |
| `actions` (`List<MessageSubscription<T>>`) | **Events** (`List<IVariableEvent<T>>`) |
| `_response` (`UnityEvent<T>`) | `UnityEventResponse<T>` |
| listen value / `filterValues` / `_invertListenValue` | one **Filter** |
| `updateOnEnable` | **Receive Current Value On Enable** |
| `checkNullValue` | `NullFilter` (inside None to reject nulls) |
| listener as `IValueAsset<T>` / `IUIElement<T>`, `SetListenValue`, `SendData`, `UpdateData`, `ClearData`, `RaiseData` | none (a Listener isn't a value). `SendData(x)` ≈ the **Respond (test)** button. |
| `ActionReference<T>` | `VariableReference<T>` |
| `useValue` toggle (Value / Reference) | `Mode` (Value / **Source**) |
| `assetReference` (an `ActionSO<T>`) | `Source` (any `IValueSource<T>`) |
| `RaiseDefault()` | `((IVariableEvent<T>)reference.Source).Set(initial)` |
| `Subscribe(parent, action)` / `UnsubscribeALl(parent)` on the reference | `TryAddChangedListener(action)` / `RemoveChangedListener(action)` |
| `ValueAsset<T>` / `ValueAssetReference<T>` (a value with no event) | a `VariableEvent<T>` (notifying is harmless), or bridge `ValueAsset<T>` as a Value Source (below) |
| `ActionSubscription` / `MessageSubscription<T>` structs | a plain `[OdinSerialize] IEvent` / `IVariableEvent<T>` field + `Subscribe` / `Unsubscribe` |
| `MessageSubscription<T>.LoadAction(path)` (`Resources.Load`) | none. Reference the asset directly. |
| `GenericAssetSelector` dropdown | Odin's object picker on the interface field |

## Behaviour that changes

Each of these can change what a scene does. Check them while migrating.

1. **`Raise()` on a typed action raised its default value.** The new `Raise()` re-raises the *current* value. GushesheGear only calls `Raise()` on two (void) save/load actions in `SaveDataManagerSO`, but check anything that calls `Raise()` on a typed action through a UnityEvent in the inspector.
2. **The value is stored *before* listeners run.** Old `Raise(x)` notified listeners first and stored `x` afterwards, so a listener reading `StoredValue` saw the *old* value. New `Set(x)` stores first, so `Value` is already `x` inside listeners. Anything that read `StoredValue` inside a callback to get "the previous value" will now get the new one.
3. **Notification order.** Old: typed subscribers in **reverse** order, then value-less subscribers in **reverse** order. New: one list, in **subscription order**. Only matters where listeners depend on each other's side effects.
4. **Raising outside play mode.** Old `Raise` was ignored (with a warning) when not playing. New `Set` / `Raise` works in edit mode too. Inspector buttons are play-mode only, but editor scripts that raise would now notify.
5. **Null values.** Old listeners **ignored null values by default** (`checkNullValue` false). New listeners pass null through unless the Filter rejects it. For listeners of reference types (`Product`, `Vehicle`, `Mission`…) that must ignore null, add `CompositeFilter(None)[ NullFilter ]` (combine it with the listen-value Equality Filter in an `All` Composite).
6. **`updateOnEnable` vs Receive Current Value On Enable.** Old: delivered the current value on enable **only before `Start`** (first enable), and only if the action had one. New: delivers on **every** enable. For objects that are toggled on and off (UI panels), the Responses run again on each re-enable. That's usually what you want for UI, but check the 31 listeners that use it.
7. **Reset scope.** Old reset relied on `SceneLoadManager` finding assets with `Resources.LoadAll`, so action SOs outside `Resources/` kept stale state between editor sessions. New reset covers every loaded asset automatically. You can move Events and Variable Events out of `Resources/` freely.
8. **Writes through a reference to a missing asset.** Old `ActionReference` in Reference mode with no asset **read its fallback `_value`** and threw a `NullReferenceException` on write. New `VariableReference` in Source mode with no source reads `default` and throws `InvalidOperationException` (with a clear message) on write.
9. **`ActionReference<T>(value)` constructor bug.** It set `useValue = false` (Reference mode) and relied on the null-asset fallback to read `value`. `new VariableReference<T>(value)` is correctly in Value mode.

### The listen-value rule, reproduced exactly

The old typed listener passed a Raise when (simplified, ignoring the previous-value filter):

```
if value is null:            pass = checkNullValue            (false by default)
else if useListenValues:     pass = (value == listenValue OR value in filterValues), inverted if invertListenValue
else:                        pass = true
```

| Old setup | New Filter on the Variable Event Listener |
|---|---|
| no listen value | `Composite(None)[ NullFilter ]` (to keep "ignore nulls"), or no Filter |
| listen value | `Composite(All)[ EqualityFilter(listenValue), Composite(None)[NullFilter] ]` |
| listen value + `filterValues` | `Composite(Any)[ EqualityFilter(listenValue), EqualityFilter(v1), EqualityFilter(v2)… ]` (plus the null rule) |
| + `_invertListenValue` | wrap the listen-value part in `Composite(None)` |

For value types (`int`, `float`, `bool`, `Vector2`, enums like `StuntState`) the null rule doesn't apply; skip it.

### The one previous-value filter

`DriftMeterUIProgressBAr.prefab` uses `usePreviousValueFilter` ("only respond when the value *changes from* one of these"). The package has no previous value. Options:

- if it really means "only when the value changes", turn on **Only Raise On Change** on that Variable Event;
- otherwise write a small project-side component that remembers the last value and forwards transitions.

### Recipe: on-true / on-false

`BoolActionListener`'s `onTrue` / `onFalse` (9 wired) becomes a Response in the project:

```csharp
using RonJames.Foundation.Events;
using UnityEngine;
using UnityEngine.Events;

public class BoolSplitResponse : IResponse<bool>
{
    [SerializeField] private UnityEvent onTrue = new UnityEvent();
    [SerializeField] private UnityEvent onFalse = new UnityEvent();

    public void Respond(bool value) => (value ? onTrue : onFalse).Invoke();
}
```

The same pattern replaces any other `TriggerUnityEvents` override (e.g. `IntListener`'s int-to-string): make it a Response.

---

## The migration plan

The key is a **bridge**: make the existing Action SOs implement the package's interfaces. Then new Listeners, Variable References and Commands work with the **existing assets**, and you can migrate one prefab or scene at a time while everything keeps working. The assets themselves are converted last.

**Use the migration kit**: [`Migration~/GushesheGear/`](../Migration~/GushesheGear/README.md) in this repo. It contains:
- the bridge as a `git apply` patch;
- the Variable Event / Listener / Set types for every GushesheGear value type;
- Responses ported from the old listener subclasses;
- editor tools (dry run first) for phases 2–4.

The phases below explain what each step does. The kit's README is the step-by-step.

### Phase 0: install

- Add the package: `"com.ronjames.foundation": "https://github.com/Ron-James/com.ronjames.foundation.git#v0.3.1"` (Odin and Unity 6 are already there; check Odin is 3.3+).
- **Name clash:** GushesheGear has a global `ICommand` (`Scripts/Command/ICommand.cs`). In any file that also has `using RonJames.Foundation.Commands;`, refer to one of them by full name, or alias it: `using FoundationCommand = RonJames.Foundation.Commands.ICommand;`. Nothing else clashes.

### Phase 1: the bridge (no behaviour change)

Add the package interfaces to the old base classes: **apply `Migration~/GushesheGear/Bridge/bridge.patch`**. It puts the bridge on `BaseActionSO`, so `VoidActionSo` and every `ActionSO<T>` get it, and makes `ValueAsset<T>` a writable Value Source. The sketch below shows the idea; the patch is the tested version.

```csharp
using System;
using System.Collections.Generic;
using RonJames.Foundation.Events;
using RonJames.Foundation.Values;
using Object = UnityEngine.Object;

// ActionSO.cs: add the interface to the class declaration:
public abstract class ActionSO<T> : BaseActionSO, IValueAsset<T>, IVariableEvent<T>
{
    // ...existing members unchanged...

    private readonly Dictionary<object, string> bridged = new Dictionary<object, string>();

    T IValueSource<T>.Value => StoredValue;
    object IValueSource.Value => StoredValue;
    Type IValueSource.ValueType => typeof(T);
    void IWritableValueSource<T>.SetValue(T value) => Raise(value);
    void IWritableValueSource.SetValue(object value) => Raise(ValueConversion.To<T>(value, this));
    void IVariableEvent<T>.Set(T value) => Raise(value);
    void IVariableEvent<T>.SetSilently(T value) => SetStoredValueDirectly(value);
    void IEvent.Raise() => Raise(StoredValue);            // new meaning: re-raise the current value
    void IVariableEvent<T>.RaiseCurrent() => Raise(UnityNull.IsNull(StoredValue) ? _defaultCallValue : StoredValue);

    void IEvent.Subscribe(IEventListener l) => Bridge(l, l as Object, () => l.OnEventRaised());
    void IEvent.Unsubscribe(IEventListener l) => Unbridge(l);
    void IEvent.Subscribe(Action a) => Bridge(a, a.Target as Object, () => a());
    void IEvent.Unsubscribe(Action a) => Unbridge(a);
    void IVariableEvent<T>.Subscribe(IVariableEventListener<T> l) => BridgeTyped(l, l as Object, l.OnEventRaised);
    void IVariableEvent<T>.Unsubscribe(IVariableEventListener<T> l) => Unbridge(l);
    void IVariableEvent<T>.Subscribe(Action<T> a) => BridgeTyped(a, a.Target as Object, v => a(v));
    void IVariableEvent<T>.Unsubscribe(Action<T> a) => Unbridge(a);
    event Action IObservableValueSource.ValueChanged
    {
        add => Bridge(value, value.Target as Object, () => value());
        remove => Unbridge(value);
    }

    private void Bridge(object key, Object origin, UnityEngine.Events.UnityAction response)
    {
        if (key == null || bridged.ContainsKey(key)) return;
        bridged[key] = Subscribe(origin != null ? origin : this, response).Guid;
    }

    private void BridgeTyped(object key, Object origin, UnityEngine.Events.UnityAction<T> response)
    {
        if (key == null || bridged.ContainsKey(key)) return;
        bridged[key] = Subscribe(origin != null ? origin : this, response).Guid;
    }

    private void Unbridge(object key)
    {
        if (key != null && bridged.TryGetValue(key, out string guid)) { RemoveByGuid(guid); bridged.Remove(key); }
    }
}

// VoidEventSO.cs: VoidActionSo implements IEvent the same way (Bridge / Unbridge using Subscribe(origin, UnityAction)).
// ValueAsset.cs: ValueAsset<T> can implement IWritableValueSource<T> in four lines (Value => StoredValue, SetValue => StoredValue = v).
```

Also clear `bridged` wherever `OnEditorStopped` clears `_subscribers`. Once this compiles, a `BoolVariableEventListener` can list an existing `BoolActionSO` in its Events, and a `VariableReference<int>` in Source mode can point at an existing `IntActionSO`.

### Phase 2: listeners, one prefab or scene at a time

For each old listener component:

1. Add the matching new component (**Event Listener** for `BaseActionListener`, **`XVariableEventListener`** for `ActionListener<X>`). Generate missing types with *Create > RonJames Foundation > Variable Event Type…* (Product, Mission, StuntState, Level, ItemQuantity, CustomComponent, ComponentService…).
2. Put the same Action SO assets in **Events** (they're accepted thanks to the bridge).
3. Build the **Filter** from the listen-value table above.
4. Move the UnityEvent into a `UnityEventResponse` (`BaseActionListener._response`) or `UnityEventResponse<T>` (`ActionListener<T>._response`). Move `onTrue` / `onFalse` into the `BoolSplitResponse` recipe.
5. Turn on **Receive Current Value On Enable** where `updateOnEnable` was on, and check behaviour 6 above.
6. Remove the old component. Use **Find References** on the asset to confirm nothing still points at the old listener.

**The kit's Listener Migrator does all of this automatically** for the safe cases. It:
- adds the new component;
- copies the actions and enabled state;
- builds the Filter;
- moves the existing UnityEvent *objects* into Responses, so every wired call carries over;
- ports subclass behaviour into Responses (`BoolSplitResponse`, `IntToStringResponse`, `StuntStateResponse`, `FloatFormatResponse` / `FloatCompareResponse`, `TranslatedStringResponse`);
- removes the old component.

It reports, and leaves alone, any listener that:
- is a UI card (Product, Mission, Quest, ItemQuantity, ExperienceData, Level, ActiveModifier, Item, PaintProduct);
- is referenced by anything (a UnityEvent target, a field, Odin data);
- is overridden in a prefab variant or scene instance;
- uses a feature with no equivalent.

Port those by hand, using the recipe above. For the UI cards, the cleanest split is a small project component that holds the card logic and is fed by a Variable Event Listener + Response.

### Phase 3: Action References and subscriptions in code (22 + 13 files)

1. **The host must be Odin-serialized.** `ActionReference<T>` is a Unity-serialized `[Serializable]` class, but `VariableReference<T>` needs Odin. Change each `MonoBehaviour` / `ScriptableObject` that holds one to `SerializedMonoBehaviour` / `SerializedScriptableObject`. Existing Unity-serialized fields keep their data.
2. **Changing the field type loses its data.** For each field, add the new `VariableReference<T>` (or `IEvent` / `IVariableEvent<T>`) next to the old one, marked `[OdinSerialize, MigrateFrom(nameof(oldField))]`, and run the kit's **Field Migrator**.
   - **What it copies:** `useValue` → Value mode with `_value`; otherwise Source mode with `assetReference` (a bridged ActionSO is a Value Source); subscriptions → their Action asset.
   - **Where:** every prefab, scene and ScriptableObject asset. Prefab instances are written only where they overrode the old field.
   - **Then:** switch the code over and delete the old field.
3. Replace `reference.Subscribe(this, OnX)` / `UnsubscribeALl(this)` with `TryAddChangedListener(OnX)` / `RemoveChangedListener(OnX)` in `OnEnable` / `OnDisable`. For the value, read `reference.Value`.
4. Replace `ActionSubscription` / `MessageSubscription<T>` fields with `[OdinSerialize] IEvent` / `IVariableEvent<T>` fields and `Subscribe` / `Unsubscribe` with a stored delegate. Today they're used in:
   - `SaveDataManagerSO`, `LevelManager`, `AnimationStateManager`, `RoadSpawner`;
   - `UIBuilder`, `MultiSourceUIBuilder`, `ExperienceDataManager`;
   - the quests: `BaseActionQuest`, `CollectQuest`, `MessageQuest`, `TimedItemQuest`.

   The quests are `SaveableSO`s with `[Save]` fields. Their saving is unaffected; only how they subscribe changes.

### Phase 4: convert the assets

Once nothing references the old *types* directly (only the package interfaces), convert each asset from `XActionSO` to `XVariableEvent` / `EventAsset` with the kit's **Asset Converter**. For each asset it:
- reads `_defaultCallValue`;
- swaps the asset's script to the new class (the asset's GUID, and so every reference to it, is kept);
- writes the value into **Initial Value**.

It refuses, and reports, while any user of the asset still has an old-system field, and for ActionSO classes that declare their own members, until those are ported.

Code that used game-specific methods (`IntActionSO.AddAmount`, …) moves to extension methods or small Commands. Assets can move out of `Resources/`.

### Phase 5: delete the old system

Remove the following, after a final **Find References** pass on a few assets to confirm nothing is left:
- `ActionSO.cs`, `BaseActionListener.cs`, `ActionListener.cs` and the concrete action / listener classes;
- `ValueReference.cs`, `ActionSubscription.cs`, `MessageSubscriptionSet.cs`;
- the action-SO part of `SceneLoadManager`'s reset.

`GenericAssetSelector` and `ValueAsset<T>` may still be used elsewhere.

## Not part of this migration

- **GushesheGear's command code** (`Scripts/Command/`: `ICommand`, `CommandInvoker`, `CommandQueue`, `AsyncCommand`) is an early queue used by `AsyncRaise`. It isn't the command-list system the package replaces. Leave it, or replace uses case by case with Command Sequences.
- **`InputEventChannel`, `ColliderChannel`, `CollisionActionSO`** and other game-specific channels: migrate them like any other Action SO once their value types have Variable Event classes. Anything they do beyond raising stays project code.
