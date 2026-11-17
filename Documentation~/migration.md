# Coming from the Bizar projects

This package is a **rewrite**, not a port, of the command, event and variable systems that evolved across the Bizar projects (CCS → TronoxHira → Checklist-Skelington → Sibanye-TruckSim → Internal_LOTO). The ideas are the same. The code, names and several behaviours are not.

Nothing here is source-compatible with the old code, and the existing projects are not being migrated. This page exists so that someone who knows the old systems can find their way around the new one quickly, and doesn't get caught by behaviour that changed.

The reference point throughout is the newest version, in **Internal_LOTO**.

## What changed, in one minute

- **Listeners:** the old `EventListener<T>` subclasses, `EventListenerComponent(<T>)` and `UnityEventListener(<T>)` became **two components**, the Event Listener and the Variable Event Listener. Each has a Filter, a Condition and a list of **Responses**.
- **Listen value:** the old built-in listen value, `inverseFilter` and `useFilter` became **one Filter slot**. An `EqualityFilter` replaces the listen value; a `None` Composite replaces "inverse".
- **Event assets:** `EventSO<T>` is now `VariableEvent<T>`, with a separate Initial and Runtime value that reset every play session. `VoidEventSo` is now `EventAsset`.
- **Value interfaces:** `IValueAsset<T>` / `IReferenceContainer` became **Value Sources**: read and write are separate, and the typed ones inherit the untyped one.
- **Variable Reference modes:** `Reference` mode is now **Source**, and `Collection` mode is now a `ListElementSource<T>`.
- **Commands:** they are **stateless**, return `Awaitable` instead of `Task`, and have **no Undo and no Init/Dispose**.
- **Cancelling:** "Cancel" / "CancelAndStop" / `CancelOnCompleteOption` became **Skip** / **Stop** / the three control requests, plus a **Control** Command for the inspector.
- **Executor:** `CommandListExecutor` is now **`CommandSequence`**, and `StepCoordinator` is now **`SequenceSwitcher`**.
- **Debugging:** debug state lives in the **Run tree** on the Command Sequence, not on each Command.

---

## Events

### Assets

| Old | New | Notes |
|---|---|---|
| `EventSO<T>` (e.g. `IntEventSO`) | `VariableEvent<T>` (e.g. `IntVariableEvent`) | Still "a variable that is an event" (ADR 0002). |
| `VoidEventSo` | `EventAsset` | Renamed to avoid clashing with `UnityEngine.Event`. |
| `StoredValue` (get) | `Value` | |
| `StoredValue = x` / `Raise(x)` | `Set(x)` | Same behaviour: changes the value and Raises. |
| `RaiseDry(x)` | `SetSilently(x)` | |
| `Raise()` (value-less) / `RaiseInternalPayload()` / `Refresh()` | `Raise()` | Raises with the current value. |
| `Raise(Object)`, `RaiseGeneral(object)` (with `GameObject.GetComponent` fallbacks) | **removed** | They existed to work around UnityEvent's argument limits. Use `IWritableValueSource.SetValue(object)` (converts safely or throws), or a Set Variable Command. |
| *Initial value* = whatever `_storedValue` held | **Initial Value** (serialized) + **Runtime Value** (never saved) | See "Reset" below. |
| `resetWithEditorStop` | always reset at play start; **Keep Runtime Value In Editor** to opt out | |
| `debugMessageOnRaise`, `raiseCounter` | **Raise Count** and **Listener Count** in the play-mode inspector | |
| `VoidEventSo._onRaise` (a UnityEvent on the asset) | **removed** | An asset can't reference scene objects reliably. Use an Event Listener. |
| `IntEventSO.Increment/Decrement` with limits | **not included** | Use a custom Command, or Set Variable with a computed value. |
| plain C# `Event` / `Event<T>` classes | internal | The subscriber list is now an internal detail. Per-instance C# events are out of scope in this version. |
| project types: `GuidEvent`, `BootstrapperEventSO`, `ReportEventSO`, `TransformDataCollectionEvent`… | **not included** | Generate the type you need with *Create > RonJames Foundation > Variable Event Type…*. |

### Reset behaviour (changed)

- **Old:** each `EventSO` cleared its listeners in `OnDisable` and `OnEditorStopped`, and reset its stored value if `resetWithEditorStop` was on. Runtime subscriptions were `[OdinSerialize]`d *into the asset*.
- **New:** one central `PlayModeReset` runs at the start of every play session (before scenes load, and also with domain reload disabled). It puts every Variable Event's Runtime Value back to a *copy* of its Initial Value and drops all Listeners. Subscriptions are never serialized.
- **The copy matters:** in the old code, adding to a `List` stored in an `EventSO` at runtime changed the same list object. In the new code, the Runtime Value of a reference type is a deep copy, so the authored Initial Value never changes.

### Raise behaviour (changed)

| | Old | New |
|---|---|---|
| Notification order | **reverse** subscription order (last subscribed first) | **subscription order** (first subscribed first) |
| Subscribing twice | added twice | ignored |
| (Un)subscribing during a Raise | took effect immediately (backwards iteration) | takes effect on the **next** Raise |
| A Listener that throws | message and stack logged as text | `Debug.LogException` with the listener (or asset) as context, so you can click it. The others still run. |
| Runaway recursion | stack overflow | refused after 32 nested Raises, with an error |
| Destroyed listeners | could throw `MissingReferenceException` | skipped |
| Value-less listener on a typed event | `IListener.OnEventRaised()` | same: `IEventListener.OnEventRaised()` |

### Interfaces

| Old | New |
|---|---|
| `IEvent` (`Raise`, `Subscribe(IListener)`, `Unsubscribe`, `RemoveAllListeners`, `INamedEntry`) | `IEvent` (`Raise`, `Subscribe`/`Unsubscribe` for `IEventListener` and `Action`). `RemoveAllListeners` and `Name` are gone; the reset clears listeners. |
| `IEvent<T>` | `IVariableEvent<T>`, which is also a writable, observable Value Source and adds `Set` / `SetSilently` |
| `IListener.OnEventRaised()` | `IEventListener.OnEventRaised()` |
| `IListener<T>.OnEventRaised(T)` | `IVariableEventListener<T>.OnEventRaised(T)` |

---

## Listeners → Listener components

The old code had three overlapping ways to listen. They're replaced by **one component per kind**, each with a **Filter**, a **Condition** and a list of **Responses**.

| Old | New |
|---|---|
| `EventListener<T>` and its concrete subclasses (`IntListener`, `BoolListener`, `StringListener`…) | `VariableEventListener<T>` (`IntVariableEventListener`, `BoolVariableEventListener`…) |
| `EventListenerComponent<T>` (events + `typedListeners` + `listeners` + `eventDataFilter`) | `VariableEventListener<T>` |
| `EventListenerComponent` (value-less: events + `listeners`) | `EventListener` |
| `UnityEventListener` / `UnityEventListener<T>` (used *inside* listener lists) | `UnityEventResponse` / `UnityEventResponse<T>` |
| `PlayerEventListenerComponent`, `ColorEventListenerComponent`, `UserProfileListenerComponent`… | `VariableEventListener<T>` of that type (generate it) |

### Field by field

| Old member | New equivalent |
|---|---|
| `events` (`IEvent[]` / `List<IEvent>`) | **Events** list (`List<IVariableEvent<T>>`, or `List<IEvent>` on the Event Listener) |
| `onEventRaised` (`UnityEvent<T>`) | a `UnityEventResponse<T>` in **Responses** |
| `onEventRaisedEmpty` (`UnityEvent`) | the *without value* event of `UnityEventResponse<T>`, or a separate Response |
| `typedListeners` (`List<IListener<T>>`) | **Responses** (`IResponse<T>`) |
| `listeners` (`List<IListener>`) | **Responses**: `UnityEventResponse(<T>)`, `RunCommandSequenceResponse(<T>)`, or your own `IResponse` |
| `TriggerEvents(T)` / `TriggerEvents()` overrides in subclasses | write an `IResponse<T>` / `IResponse` class instead of subclassing the listener |
| `CheckValueAssetsOnStart` / `checkValueAssetsOnStart` | **Receive Current Value On Enable**. Note it now runs in `OnEnable`, not `Start`. |
| `useFilter` + **`listenValue`** | **Filter** = `EqualityFilter<T>` comparing against that value |
| `filters` / `eventDataFilter` (`CompositeFilter<T>`) | **Filter** = `CompositeFilter<T>` |
| `inverseFilter` | wrap the Filter in a `CompositeFilter<T>` set to **None** |
| `StoredValue` / `SetListenValue` / `TriggerListenValue` / `Init(T)` on the listener | **removed**. A Listener is no longer a Value Source. |
| `isUIElement`, `Index`, `ElementUnused` (`IUIElement<T>`) | **removed**. UI binding isn't a Listener's job. |
| — | **new:** an optional **Condition** gate on both components |

### The old filter logic, and how to reproduce it exactly

The old typed listener passed a Raise when:

```
result = (value == listenValue) OR (all filters pass)      // only if useFilter
if inverseFilter: result = !result
```

The new Listener has one Filter slot, so you state the logic directly:

| Old setup | New Filter |
|---|---|
| listen value only | `EqualityFilter(listenValue)` |
| listen value + filters | `Composite(Any)[ EqualityFilter(listenValue), Composite(All)[filters…] ]` |
| …with `inverseFilter` | wrap the above in `Composite(None)` |
| filters only (`eventDataFilter`) | `Composite(All)[filters…]` |

### Filters

| Old | New | Watch out for |
|---|---|---|
| `IFilter<T>.FilterAllows(T)` | `IFilter<T>.Allows(T)` | Renamed. Port custom filters by renaming the method. |
| `CompositeFilter<T>` (AND only, `emptyFilterAllowsAll`) | `CompositeFilter<T>` with **All / Any / None** | Empty: All passes, Any fails, None passes. The old default "empty allows all" is All's behaviour. |
| `EqualityFilter<T>` (`invert`) | `EqualityFilter<T>` | No invert flag; use None. Destroyed objects now count as null. |
| `VariableCheckFilter<T>` | `EqualityFilter<T>` | Same thing. |
| `ComparisonFilter` (one class for `float` and `int`, compared against a `float`) | `ComparisonFilter<T>` (any `IComparable<T>`) | The old `int` path used `Mathf.Approximately`. New ints compare exactly; floats use a tolerance. |
| `TypeFilter<T>` (`invertFilter`, **default true = exclude** the selected types) | `TypeFilter<T>` (**include** the selected types) | The default flipped. To exclude, wrap in None. |
| — | `NullFilter<T>` | New. Treats destroyed ("missing") objects as null. |
| `PlayerHostFilter`, `LocalPlayerFilter`, `ColliderTagFilter`, `HighlightableFilter`, `UserReportFilter`, `MultipleOptionQuestionFilter`, `QuestionSubmittedFilter` | **not included** | Project-specific. Port them by implementing `IFilter<T>.Allows`. |

### Conditions

| Old | New |
|---|---|
| `ICondition.IsMet()` | `ICondition.IsMet()` (same) |
| `EqualityCondition<T>` / `PlayerEqualityCondition` | `FilterCondition<T>` with an `EqualityFilter<T>` |
| `BoolCondition` (`invert`) | `BoolCondition` (no invert; wrap in a `CompositeCondition` set to None) |
| `ConditionCommand`'s All / Any / None over a list of conditions | `CompositeCondition` (All / Any / None), used by `BranchCommand` |
| `IsHostCondition`, `NetworkConnectionCondition` | **not included** (networking is out of scope in this version) |
| — | **new:** `IObservableCondition`, which lets Wait Until react instead of polling |

---

## Values

| Old | New | Notes |
|---|---|---|
| `IValueAsset<T>` (`StoredValue { get; set; }`) | `IValueSource<T>` (`Value`) + `IWritableValueSource<T>` (`SetValue`) | Read and write are separate. Some sources are read-only. |
| `IReferenceContainer` (`object Reference { get; set; }`) | `IValueSource` / `IWritableValueSource` (untyped) | **Every typed source is also untyped.** The untyped value is the typed value, boxed. In the old UI elements, `Reference` was a *separate* "bound data item" from the typed values; there's no such separate slot now. |
| `ValueReference<TValue, TAsset>` / `VariableReference<T>` | `VariableReference<T>` | One type parameter. |
| `referenceSelection`: **Value** / **Reference** / **Collection** | `Mode`: **Value** / **Source** | "Reference" mode is now called **Source** mode. |
| Collection mode (`CollectionReference<T>`: `useFirstElement`, `indexReference`) | `ListElementSource<T>`, a Value Source a reference can point at | Writable now (in range), and indexing a `List` is O(1). |
| `editAsset` + `_assetReference` (inline asset editor) | **removed** | Two serialized copies of the asset could drift apart. |
| Writing in Collection mode or to a missing asset: **silently ignored** | **throws** `InvalidOperationException` | Use `CanWrite` / `IsMissing` to check first. |
| implicit conversion to `T` | same, but a null reference now reads as `default` instead of throwing | |
| — | **new:** `TryAddChangedListener` / `CanNotify` | Lets waiting code avoid polling. |
| `VariableReferenceCollection<T>` | **removed** | |
| `IRefreshable.Refresh()` | `Raise()` on the event | |
| `IEditorPlaymodeListener.OnEditorStopped()` | `IPlayModeResettable.ResetForPlayMode()` + `PlayModeReset.Register` | It now runs at play *start*, and works without domain reload. |

---

## Commands

### The contract

| Old (`ICommand`) | New (`ICommand`) |
|---|---|
| `Task Execute(CancellationToken)` | `Awaitable Execute(CommandContext context)`. The token is `context.Token`. |
| `Task Undo(CancellationToken)`: required on every command | **removed**. It was overridden in 3 commands and called from 2 places. |
| `Init()` / `Dispose()` (`IInitializable`): Init once on Start, Dispose after *every* execute | **removed**. Acquire inside `Execute`, release in `finally`. The Run guarantees `finally` runs on Skip and Stop. |
| `ICommandInvoker Context { get; set; }`: a back-pointer commands used to cancel their parent | `CommandContext`, given per execution, with `StopSequence()` / `SkipParent()` / `StopParent()` |
| `IEnabled.IsEnabled` | `Enabled` (same toggle; Disabled Commands are passed over and drawn faded) |
| `Name` | `Name` (optional authored name, otherwise derived from the type) |
| `ICommand<T>.Execute(T, token)` (typed payloads) | **removed**. There are no Run payloads. |

### The base class

| Old `Command` member | New |
|---|---|
| `ExecuteBody(token)` | override `Execute(context)`, or `Run(context)` on an `InstantCommand` |
| 7 UnityEvents on every command (`_onExecuteStart`, `_onExecuteComplete`, `_onUndoStart`, `_onUndoComplete`, `_onCancel`, `_onPlannedCancel`, `_onPlannedCancelAndStop`) | **none on the base**. Wrap a Command in a **Hooks Composite** for started / completed / skipped / stopped / failed events. |
| `_isRunning`, `_wasCancelled`, `IsInitialized` (state on the instance) | **gone**. Live state is in the Run tree (ADR 0003). |
| **`CancelOnCompleteOption`** on every command: | put a **Control** Command straight after it: |
| · `None` | nothing |
| · `CancelSelf` | no equivalent. At top level it only re-created a token and fired the executor's *On Cancelled* event. Inside a Sequential Composite it could leave the following commands starting already cancelled. If you relied on the event, use a Hooks Composite. |
| · `CancelSelfAndStopTrain` | **Control: Stop Sequence** |
| · `TryCancelRootTrain` | **Control: Skip Parent** |
| · `TryCancelAndStopRootTrain` | **Control: Stop Parent** |

### Executors and invokers

| Old | New |
|---|---|
| `CommandListExecutor` | `CommandSequence` |
| `initializeOnStart` | gone (nothing to initialize) |
| `queueOnStart` | **Run On Start** |
| `QueueAllCommands()` / `QueueCommands()` | `Run()` / `RunAndWaitAsync()` |
| `Cancel()` (cancel the current command, continue) | `Skip()` |
| `CancelAndStop()` (cancel and clear the queue) | `Stop()` |
| `StopAsync()` | `StopAndWaitAsync()` |
| `ForceCompleteCommands()` (fired "completed" without completing) | **removed** |
| `onQueueStart`, `onCommandsCompleted`, `onCancelled`, `onCancelledAndStopped` | *On Run Started*, *On Run Completed*, *On Run Stopped*, *On Run Failed* |
| `CurrentCommand`, `CurrentQueue`, `CompletedCommands` in the inspector | the **Run tree** |
| `CommandListExecutorTyped<T>` (and its data payload) | **removed** |
| `GuidCommandListExecutor` (TruckSim) | **not included** |
| `CommandInvoker`, `ICommandInvoker`, `ICommandQueue`, `ICommandExecutor`, undo history | internal `CommandRun` / `CommandScope`. There's no public invoker and no undo. |
| `StepCoordinator` (steps: name, step stack, cancel stack; `RequestStep(i)`, `ClearActiveStep()`, `restartWhenRequestingActiveStep`) | `SequenceSwitcher` (entries: name, sequence, cleanup; `Request(i)` / `Request(name)`, `Clear()`, *Restart When Requesting Active*) |
| `CommandListExecutorHierarchyBadge` | the Command Sequence hierarchy badge, now coloured by Run status |

### Command by command

| Old | New | Difference |
|---|---|---|
| `SequentialCompositeCommand` | **Sequence Composite** | Adds *Continue On Failure*. |
| `CompositeCommand` (all at once) | **Parallel Composite** | Children now get their own scope. See below. |
| — | **Race Composite** | New: the first to finish wins, the rest are Skipped. |
| — | **Hooks Composite** | New: replaces the per-command UnityEvents. |
| `ConditionCommand` (All/Any/None conditions, passed / failed commands, UnityEvents) | **Branch** with a `CompositeCondition` | A missing Condition now Fails instead of passing. No UnityEvents; use Hooks. |
| `SwitchCommand<T>` (a dictionary keyed by `T`, e.g. `ItemSwitchCommand`) | **Switch** (by **int index**) | Keyed switching isn't built in. Use a chain of Branches with `EqualityFilter` conditions, or write a keyed Switch Command. |
| `CommandExecutorCommand` (queues another executor) | **Run Command Sequence** | Runs the target's Commands *inline*, as part of this Run. Skip/Stop now reach it: the old nested executor ignored the parent's cancellation. |
| `TimedDelayCommand` (`onTimeElapsed`, `onTimeElapsedRatio`) | **Delay** | Progress shows in the Run tree. For per-frame UnityEvents, write a small Command. |
| `MessageWaitCommand` (`requiredCallCount`, `_onEventRaised`, `_callCountText`) | **Wait For Event** (*Required Raises*) | The count shows as the progress label. For UI text, listen to the Event. |
| `TypedMessageWaitCommand<T>` (wait for raises whose value passes a filter) | **Wait Until** + `FilterCondition<T>` over a Variable Reference to that event | Waits for the *value* to pass, re-checked on each Set. That's not the same as counting filtered raises. |
| `VariableWaitCommand<T>` / `IntVariableWaitCommand` (polled every 0.1 s) | **Wait Until** + `FilterCondition<T>` (e.g. `ComparisonFilter ≥`) | Event-driven when the value is a Variable Event. |
| `IntVariableWaitCommand`'s string outputs | **not included** | Listen to the Variable Event instead. |
| `EventRaiseCommand<T>` | **Set Variable** (to set a value) or **Raise Event** (to re-Raise) | |
| `ReferenceRaiseCommand<T>` / `StringRaiseCommand` (invoke a `UnityEvent<T>` with a reference's value) | **not included** | Set a Variable Event and respond with a `UnityEventResponse<T>`, or write a small Command. |
| `UnityEventCommand` (UnityEvent + a list of events to Raise) | **Invoke UnityEvent** + **Raise Event** | |
| `ActivateCommand` (`IActivatable`) | **Set Active** (GameObject, via a Variable Reference) | |
| `CancelCommand` | **Control** | |
| `BlankCommand` | not needed | Use a named, empty Sequence Composite or a Log as a placeholder. |
| `CommandComponent` (a command on a GameObject, as `IValueAsset<ICommand>`) | **not included** | Put the Commands in a Command Sequence and use **Run Command Sequence**. |
| `EventRaiseSequenceCommand<T>`, `CollectionRaiseCommand(<T>)`, `CollectionBindCommand` | **not included** | Bind Collection was deliberately left out. |
| `LayerCommand`, `AudioPlayCommand`, `VideoWaitCommand`, `TeleportCommand`, `TextCommand`, `ColorChangeCommand`, `FadeCommand`, `SaveCommand`, `NextModuleCommand`, `LoadSceneNetworkCommand`, `NetworkedCommand`, `SetSynchronizerCommand`, slot / report / bootstrapper commands… | **not included** | Project-specific or networking. Port them as Commands (see "Writing a Command" in [commands.md](commands.md)). |

### Run behaviour (changed)

| | Old | New |
|---|---|---|
| A command throws (top level) | the invoker logged it and **carried on** with the next command | the Command is **Failed**, the Sequence **Stops** and the Run is Failed, unless *Continue On Failure* is on |
| A command throws (in a sequential composite) | rethrown, wrapped in a new exception | propagates as Failed through every enclosing Composite, logged **once** with the full path |
| Between commands | waited one frame after **every** command | **no** frame between commands. Instant Commands run back-to-back in one frame. |
| Calling Run again while running | the whole list was **appended** to the queue again | **Queue** policy: one whole Run queued per call. **Restart** policy: start over on the next frame. |
| Nested executor + parent cancelled | the external token was **ignored**, so the nested executor kept running | Skip/Stop always reach nested Commands |
| "Cancel" from inside a parallel composite | children shared the *parent's* context, so `CancelSelfAndStopTrain` stopped the **parent** list | each Composite owns a scope, so **Stop Sequence** ends the Parallel group itself (use **Stop Parent** for the level above) |
| Re-running a list | commands were Disposed after each execute but only Init-ed once | no lifecycle hooks, nothing to get out of step |
| Memory | undo history and completed-command lists grew forever | each Run is dropped when the next one starts |
| Debugging | read the fields of each command instance | the live **Run tree** |

---

## Where the old names went

A quick index of old name → new name:

- **Events:** `EventSO` → `VariableEvent` · `VoidEventSo` → `EventAsset` · `RaiseDry` → `SetSilently` · `IListener` → `IEventListener` · `IListener<T>` → `IVariableEventListener<T>`
- **Listeners:** `EventListener<T>` / `EventListenerComponent<T>` → `VariableEventListener<T>` · `EventListenerComponent` → `EventListener` · `UnityEventListener` → `UnityEventResponse` · `listenValue` → `EqualityFilter` · `inverseFilter` → `CompositeFilter(None)` · `CheckValueAssetsOnStart` → Receive Current Value On Enable
- **Filters:** `FilterAllows` → `Allows` · `VariableCheckFilter` → `EqualityFilter`
- **Values:** `IValueAsset<T>` / `StoredValue` → `IValueSource<T>` / `Value` · `IReferenceContainer` → `IValueSource` · Reference mode → Source mode · `CollectionReference` → `ListElementSource`
- **Command sequences:** `CommandListExecutor` → `CommandSequence` · `QueueAllCommands` → `Run` · `Cancel` → `Skip` · `CancelAndStop` → `Stop` · `StepCoordinator` → `SequenceSwitcher`
- **Commands:** `SequentialCompositeCommand` → `SequenceComposite` · `CompositeCommand` → `ParallelComposite` · `ConditionCommand` → `BranchCommand` · `CommandExecutorCommand` → `RunCommandSequenceCommand` · `TimedDelayCommand` → `DelayCommand` · `MessageWaitCommand` → `WaitForEventCommand` · `VariableWaitCommand` → `WaitUntilCommand` · `UnityEventCommand` → `InvokeUnityEventCommand` · `ActivateCommand` → `SetActiveCommand` · `CancelCommand` / `CancelOnCompleteOption` → `ControlCommand`
