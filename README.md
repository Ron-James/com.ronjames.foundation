# RonJames Foundation

Reusable Unity infrastructure for designer-authored behaviour:

- **Commands**: build behaviour as Sequences of small, configurable Commands in the inspector. A `CommandSequence` component runs them over time, with nesting (Composites), Skip/Stop control and a live Run tree for debugging.
- **Events**: ScriptableObject **Events** (no value) and **Variable Events** (a typed value that notifies whenever it is Set), with one listener component for each and pluggable Responses.
- **Values**: `VariableReference<T>` fields that hold their own value or read from any **Value Source** (usually a global Variable Event), plus **Filters** and **Conditions** that combine with All / Any / None.

The vocabulary is defined in [`GLOSSARY.md`](GLOSSARY.md). The design decisions are in [`docs/adr/`](docs/adr) and [`docs/DESIGN-NOTES.md`](docs/DESIGN-NOTES.md).

## Requirements

- Unity 6 (6000.0) or newer.
- **Odin Inspector 3.3+ already in the project** (usually `Assets/Plugins/Sirenix`). The package doesn't include Odin; see ADR 0001 for why it's a hard dependency.

## Install

In `Packages/manifest.json`:

```json
"com.ronjames.foundation": "file:../../com.ronjames.foundation"
```

(a local path), or a git URL once the repo is hosted. To run the package's tests from a project, also add `"testables": ["com.ronjames.foundation"]`.

## Quick start

1. **Create a Variable Event**: *Create > RonJames Foundation > Variable Events > Bool*, e.g. `Has Key`.
2. **Set it from a Sequence**: add a **Command Sequence** to a GameObject and add Commands such as *Delay* and *Set Bool Variable* (target: `Has Key`, value: true).
3. **React to it**: add a **Bool Variable Event Listener**, put `Has Key` in *Events*, add an *Equality Filter* (true), and add a *Run Command Sequence Response* pointing at another Command Sequence.
4. Press Play and watch the Run tree on each Command Sequence.

Import the **Key and Door** sample (Package Manager > RonJames Foundation > Samples) to see all of this wired up, including a Race Composite and a custom Command.

## Writing a Command

```csharp
public class MoveTransformCommand : Command
{
    [SerializeField] private VariableReference<Transform> target = new VariableReference<Transform>();
    [SerializeField] private VariableReference<float> seconds = new VariableReference<float>(1f);

    public override async Awaitable Execute(CommandContext context)
    {
        Transform moving = target.Value;              // configuration in fields...
        for (float t = 0; t < seconds.Value; t += Time.deltaTime)
        {                                              // ...all running state in locals
            context.ReportProgress(t / seconds.Value); // shows in the Run tree
            await Awaitable.NextFrameAsync(context.Token);
        }
    }
}
```

Rules:

- **Commands are stateless** (ADR 0003): never store running state in fields. The same Command can run twice at once.
- **Pass `context.Token` to every await.** It is cancelled on Skip and Stop. Release anything you acquire in a `finally`; the Run guarantees it runs.
- Synchronous work: derive from `InstantCommand` and override `Run(context)`.
- From inside a Command:
  - `context.StopSequence()` ends the Sequence it's in.
  - `context.SkipParent()` ends its enclosing Composite.
  - `context.StopParent()` Stops the Sequence containing that Composite.
- A Command that throws is **Failed**: it is logged with its path and Stops its Sequence, unless that Sequence has *Continue On Failure*.

## Your own value types

Unity can't create assets or components of open generic types, so each value type needs three one-line subclasses. Use *Create > RonJames Foundation > Variable Event Type…* to write them, or write them yourself:

```csharp
[CreateAssetMenu(menuName = "RonJames Foundation/Variable Events/Item")]
public class ItemVariableEvent : VariableEvent<Item> { }
public class ItemVariableEventListener : VariableEventListener<Item> { }
public class SetItemVariableCommand : SetVariableCommand<Item> { }
```

Built in: `bool`, `int`, `float`, `string`, `Vector2`, `Vector3`, `Quaternion`, `Color`, `GameObject`, `Transform`.

## Things to know

- **Variable Events always Raise on Set**, even when the value is unchanged (it is an event). Turn on *Only Raise On Change* for state-like values; use `SetSilently` to change without notifying.
- **Each play session starts from the Initial Value.** The Runtime Value is never saved. This works with domain reload disabled. *Keep Runtime Value In Editor* opts a single asset out (editor only).
- **Variable Events are global.** Two copies of a prefab share one value. A Variable Reference in *Value* mode is private to its object; shared per-instance values are out of scope for v1.
- **Writing through a reference to a read-only source throws.** Nothing is silently ignored.
- **Commands and Listeners are Odin-serialized.** Host `VariableReference`, Commands, Filters and Conditions in Odin-serialized objects (`SerializedMonoBehaviour`, `SerializedScriptableObject`, or inside a Command).
- **Starting a Command Sequence while it is running queues another Run** by default. *Restart* instead Stops it and starts again on the next frame; several Restart requests in one frame collapse into one.
- **Find what uses an Event**: right-click the asset > *RonJames Foundation > Find References*.

## Development

`../com.ronjames.foundation.Sandbox` is a Unity project that references this package by local path, with the tests enabled. Run the tests from *Window > General > Test Runner*, or in batch mode:

```bash
Unity -batchmode -nographics -projectPath ../com.ronjames.foundation.Sandbox -runTests -testPlatform EditMode
```
