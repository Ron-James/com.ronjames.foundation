# Commands

Namespace `RonJames.Foundation.Commands`, assembly `RonJames.Foundation.Commands` (depends on Values and Events).

Commands let you build behaviour that happens over time ("wait 2 seconds, open the door, wait for the bell…") as a list in the inspector, instead of writing a coroutine for every case.

## The model

| Term | What it is | Type |
|---|---|---|
| **Command** | one configured unit of work. Pure configuration; it holds no state from any Run. | `ICommand`, base classes `Command` and `InstantCommand` |
| **Sequence** | an ordered list of Commands | `List<ICommand>` |
| **Composite** | a Command that runs its own Sequence or group of Commands, so Commands nest | `SequenceComposite`, `ParallelComposite`, `RaceComposite`, `HooksComposite`, plus `BranchCommand`, `SwitchCommand`, `RunCommandSequenceCommand` |
| **Command Sequence** | the component that holds a Sequence and starts Runs of it | `CommandSequence` |
| **Run** | one execution of the Sequence from start to finish. Owns *all* live state. | `CommandRun` |
| **Run tree** | the Run's live state: one node per Command, with status, timing and progress | `RunNode` |

### Commands are stateless (ADR 0003)

A Command's fields are configuration only. Everything that changes while it runs (elapsed time, counters, "am I running") lives in local variables inside `Execute`, or in the Run. That means:

- the same Command (or whole Command Sequence) can run more than once, even **at the same time**;
- nothing from one Run leaks into the next, or into the saved scene;
- debugging happens on the **Run tree** in the Command Sequence's inspector, not on each Command.

## Running a Command Sequence

Add a **Command Sequence** component, add Commands to its list, then:

- **Run on Start**, or call `Run()` (wire it to a UnityEvent, a Response, or the inspector button in play mode);
- `Skip()` ends the *current top-level* Command early and the Run continues (if that Command is a Composite, the whole Composite is Skipped);
- `Stop()` ends the Run and discards any Queued Runs;
- **disabling or destroying** the component Stops its Run.

Whatever ends a Command, its `finally` blocks run before the next Command starts or the Run reports that it ended.

### When it's already running

| *When Already Running* | Calling `Run()` while a Run is going… |
|---|---|
| **Queue** (default) | queues another Run. Each call queues one, and they run in order after the current one. |
| **Restart** | Stops the current Run, discards Queued Runs, and starts a fresh Run on the frame after the stopped one has cleaned up. Several Restart requests in the same frame collapse into one. |

### Events and code hooks

UnityEvents: *On Run Started*, *On Run Completed*, *On Run Stopped*, *On Run Failed*. C# events: `RunStarted` and `RunEnded` (with the `CommandRun`). Also `IsRunning`, `QueuedRunCount`, `CurrentRun`, `LastRun`, plus awaitable helpers `WaitUntilIdleAsync`, `StopAndWaitAsync` and `RunAndWaitAsync`. Build Sequences from code with `SetCommands(...)`. Most built-in Commands have constructors for this.

### The Run tree

The Command Sequence inspector shows the current Run, or the last one (kept until the next Run starts): every Command as a line with a coloured status dot, elapsed time, a progress bar when the Command reports progress, and nested children under Composites. Failed Commands show their error. The Hierarchy also shows a ▶ badge on objects with a Command Sequence: grey when idle, blue while running, and green / orange / red for how the last Run ended.

| Status | Meaning |
|---|---|
| Pending | not reached (or never reached, because its Sequence Stopped first) |
| Running | executing now |
| Completed | finished normally |
| Skipped | ended early; its Sequence carried on |
| Stopped | ended because its Sequence was Stopped |
| Failed | ended with an error |
| Disabled | passed over because it's switched off |

## Ending things early

Two outcomes, **Skip** and **Stop**, at three reaches. From outside, `Skip()` and `Stop()` on the Command Sequence. From inside a Command (via its context), or from the inspector with the **Control** Command:

| Request | What ends | What continues |
|---|---|---|
| **Stop Sequence** | the Sequence this Command is in | the Composite containing that Sequence finishes normally, and its parent carries on |
| **Skip Parent** | the Composite enclosing this Command | the Sequence that Composite is in |
| **Stop Parent** | the Sequence that contains the enclosing Composite | the level above that |

A Command ending *itself* early needs no request: it just returns. Requests reach exactly one level up. That's deliberate, so a Command inside nested Composites can end its immediate group or the one around it. Skip Parent or Stop Parent on a top-level Command (no enclosing Composite) Stops the Run and logs a warning.

```
Run
├─ Sequence Composite "Unlock"
│   ├─ Wait For Event "Bell"
│   ├─ Control: Skip Parent        ← ends "Unlock"; "Open" still runs
│   └─ Log "never"
├─ Move "Open"
└─ Control: Stop Sequence          ← ends the Run's own Sequence
```

## Failure

A Command that throws is **Failed**: its error is logged with its full path ("Door > Unlock > Wait For Event"), it shows red in the Run tree, and **its Sequence Stops**. The failure propagates up through each enclosing Composite, which also Fail. The Run ends as **Failed** and *On Run Failed* fires.

**Continue On Failure** (on the Command Sequence and on each Sequence Composite) logs the failure and carries on instead. The Run still ends as Failed so you know something went wrong.

Built-in Commands with nothing assigned (no Condition, no target, no Event…) **Fail with a clear message** rather than silently doing nothing.

## Composites

| Composite | Runs its children | Finishes when |
|---|---|---|
| **Sequence Composite** | one after another (has *Continue On Failure*) | the last finishes, or it's Stopped |
| **Parallel Composite** | all at once | all have finished |
| **Race Composite** | all enabled children at once | the **first** finishes. The rest are Skipped, and their cleanup runs before the Race completes. Disabled children never "win". |
| **Hooks Composite** | one wrapped Command | the Command ends. Fires *On Started*, then one of *On Completed / On Skipped / On Stopped / On Failed*. |

Every Composite is the "parent" its children's Skip Parent ends, and owns the scope its children's Stop Sequence ends.

## Built-in Commands

| Command | Does |
|---|---|
| **Delay** | waits N seconds (a Variable Reference), scaled or unscaled time, reporting progress |
| **Wait For Event** | waits until an Event or Variable Event has been Raised N times (default 1). Only Raises during the wait count. |
| **Wait Until** | waits until a Condition is met: event-driven if the Condition can notify, otherwise checked every frame |
| **Branch** | runs *Then* or *Else* depending on a Condition |
| **Switch** | runs the Command at an index (a Variable Reference), or *Default* |
| **Run Command Sequence** | runs another Command Sequence's Commands *inline*, as part of this Run: they appear nested in this Run's tree and obey its Skip/Stop. The target's own Run events don't fire. |
| **Control** | makes a Stop Sequence / Skip Parent / Stop Parent request (see above) |
| **Raise Event** | Raises an Event (or re-Raises a Variable Event) |
| **Set Variable** (`Set Bool Variable`, `Set Int Variable`, …, generic `SetVariableCommand<T>`) | writes a value into a writable Value Source. For a Variable Event that's a Set, or Set Silently with *silently* on. |
| **Set Active** | activates or deactivates a GameObject |
| **Invoke UnityEvent** | invokes a UnityEvent |
| **Log** | writes to the Console at a chosen level |

Every Command has an **Enabled** toggle (Disabled Commands are passed over and drawn faded) and an optional **name** shown in the inspector and the Run tree.

## Writing a Command

```csharp
public class MoveTransformCommand : Command
{
    [SerializeField] private VariableReference<Transform> target = new VariableReference<Transform>();
    [SerializeField] private Vector3 offset = Vector3.up;
    [SerializeField] private VariableReference<float> seconds = new VariableReference<float>(1f);

    public override async Awaitable Execute(CommandContext context)
    {
        Transform moving = target.Value;
        if (moving == null) throw new InvalidOperationException("Move Transform has no target.");

        Vector3 from = moving.position, to = from + offset;     // running state: locals only
        for (float t = 0; t < seconds.Value; t += Time.deltaTime)
        {
            moving.position = Vector3.Lerp(from, to, t / seconds.Value);
            context.ReportProgress(t / seconds.Value);          // shows in the Run tree
            await Awaitable.NextFrameAsync(context.Token);       // ends here on Skip/Stop
        }
        moving.position = to;
    }
}
```

The rules:

1. **No running state in fields.** Fields are configuration. Use locals.
2. **Pass `context.Token` to every await.** It's cancelled when this Command is Skipped or its Sequence Stops; the await then throws `OperationCanceledException`, which the Run expects. Don't catch it and carry on.
3. **Release what you acquire in `finally`.** Subscriptions, spawned objects, locks. The Run guarantees `finally` runs on Skip and Stop. There's no Init/Dispose.
4. **Throw on mis-configuration.** It becomes a clear Failed entry with the Command's path.
5. **Return an `Awaitable`, never `null`.** For synchronous work derive from **`InstantCommand`** and override `Run(context)`.
6. **Don't mark Commands `[Serializable]`.** Odin serializes them. Any public, non-abstract `ICommand` with a parameterless constructor appears in the Command picker.

What the context gives you:

| Member | Use |
|---|---|
| `Token` | cancellation for this Command |
| `ReportProgress(0..1, label)` | progress bar and label in the Run tree |
| `StopSequence()`, `SkipParent()`, `StopParent()` | the control requests |
| `IsSequenceStopping` | distinguishes "my Sequence is Stopping" from "only I was Skipped" |
| `Owner`, `GameObject`, `Run`, `Node` | the Command Sequence running this, its GameObject, the Run and this Command's node |
| `RunSequence(...)`, `RunParallel(...)`, `RunRace(...)`, `RunSingle(...)` | for **writing your own Composites**: run children as nested nodes with correct Skip/Stop/failure behaviour. `RunSingle` returns how the child ended. |

Nesting deeper than 64 levels fails, which catches a Sequence that runs itself through Run Command Sequence.

### Timing

- Commands that finish immediately run back-to-back **in the same frame**. A Sequence of ten instant Commands completes in one frame.
- `Awaitable` continuations run on the main thread. Don't await the same `Awaitable` twice, and don't store one for later (ADR 0004).

## Sequence Switcher

`SequenceSwitcher` switches between Command Sequences so only the most recently requested one runs. That fits a hotbar of steps where clicking a step should end whatever step was active. Each entry has a name, a Command Sequence and an optional *cleanup* Command Sequence.

`Request(index)` / `Request(name)`:

1. Stops the active entry and waits for it to clean up.
2. Runs that entry's cleanup Sequence and waits for it.
3. Starts the requested entry.

Requests arriving mid-switch collapse to the latest one. `Clear()` stops the active entry and starts nothing. With *Restart When Requesting Active*, requesting the already-active entry starts it again.
