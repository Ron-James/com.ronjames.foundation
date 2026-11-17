# Design notes from the grill sessions

These are the agreed decisions that are too small or too easy to reverse for an ADR. The vocabulary is defined in `GLOSSARY.md`, and the hard-to-reverse decisions are in `docs/adr/`.

## Package
- UPM name `com.ronjames.foundation`, namespace `RonJames.Foundation.*`, display name "RonJames Foundation".
- The package is a git repo at `C:\Dev\Unity\com.ronjames.foundation`, with `package.json` at the root so it can be installed by git URL. A Sandbox project at `C:\Dev\Unity\com.ronjames.foundation.Sandbox` references it by local path and marks it `testable`.
- Unity 6 (6000.0) minimum.
- One asmdef per module, plus Editor and Test asmdefs.
- Odin is a hard dependency (ADR 0001). If it's missing, the package fails to compile ("Sirenix could not be found"), with a warning naming the cause.
- v1 scope: Commands, Events/Variable Events, Variable Reference. Networking and the entry point come later.
- Tests go in `Tests/Editor` and `Tests/Runtime`. They cover Run semantics (Skip, Stop, Stop Sequence, Skip Parent, Stop Parent, Failed, Queue/Restart, cleanup on Stop), Raise ordering and nesting, and the reset rules.

## Assemblies
- `RonJames.Foundation.Values`: Value Source, Variable Reference, Filters, Conditions and the play-mode reset. Depends on Odin.
- `RonJames.Foundation.Events`: Event, Variable Event, both Listeners and Responses. Depends on Values.
- `RonJames.Foundation.Commands`: Command, Run, Composites, Command Sequence, Sequence Switcher and the v1 Commands. Depends on Values and Events. The "run a Command Sequence" Response lives here, so Events never depends on Commands.
- Each assembly has an `.Editor` assembly. There are two test assemblies (Editor and Runtime).

## Built-in types and authoring
- Built-in value types are `bool`, `int`, `float`, `string`, `Vector2`, `Vector3`, `Quaternion`, `Color`, `GameObject` and `Transform`. Each gets a concrete Variable Event, a Variable Event Listener and a Set Variable command.
- A "Create Variable Event type…" editor menu writes those three subclasses for a type you pick. The hand-written pattern is documented in the README.
- The package ships a `README.md` and one `Samples~` sample (key and door). It uses a `bool` Variable Event, a Listener with a Condition, and a Command Sequence with Delay, a Race Composite and Wait For Event. The sample also serves as the manual test scene in the Sandbox.

## Commands
- A Command returns `Awaitable` (ADR 0004). It's stateless; the Run owns all live state (ADR 0003).
- Commands have no Undo and no Init/Dispose. A Command acquires resources inside its execution and releases them in `finally`. The Run guarantees that `finally` runs on Skip and Stop.
- The only in-command control requests are Stop Sequence, Skip Parent and Stop Parent. Reaching up exactly one level is deliberate and kept. Ending yourself early is just returning.
- External controls on a Command Sequence:
  - `Skip()` ends the current Command.
  - `Stop()` ends the current Run and discards Queued Runs.
- Starting a Run while one is active **queues** a Run by default. Each request queues its own Run, in order. The **Restart** option instead Stops the current Run, discards the Queued Runs and starts the new one immediately. There are no Ignore or Concurrent policies.
- Failure: the Command is marked Failed, its Sequence Stops, and the exception is logged with the path to the Command. Each Sequence has a "continue on failure" toggle.
- Composites: Sequence, Parallel, Race (the first to finish wins and the rest are Skipped), and Hooks (started / completed / skipped / failed events). There are no UnityEvents on the Command base.
- A Disabled Command is authored configuration. It's passed over and drawn greyed out.
- No Run payloads. The typed command runner is dropped entirely.
- A Command Sequence has optional Run on Start. Disabling or destroying it Stops its Run.
- Debugging: the Command Sequence inspector shows a live Run tree (status, elapsed time, reported progress, active path). It stays visible after the Run ends until the next Run. Commands report progress through their context.
- v1 Commands:
  - Composites: Sequence, Parallel, Race, Hooks;
  - Delay;
  - Raise Event, Set Variable;
  - Wait For Event, Wait Until (Condition);
  - Branch (if/else on a Condition), Switch;
  - Run Command Sequence;
  - Set Active, Invoke UnityEvent, Log.
- The Sequence Switcher ships in v1.

## Events
- Event (no value) and Variable Event (typed value, and is an event) (ADR 0002).
- Set always Raises. Set Silently changes the value without Raising. Each asset has an optional "only raise on change" toggle.
- Initial Value is serialized. Runtime Value is never saved. A central reset puts Runtime Value back to Initial Value and clears Listeners at the start of every play session, and works with domain reload disabled. An asset can opt out in the editor.
- Listeners are notified in subscription order. Subscribing or unsubscribing during a Raise takes effect on the next Raise. A Set during a Raise Raises again immediately (nested), with a depth limit that logs an error. A Listener that throws is logged and the others still run.
- Event Listener: listens to Events, gated by an optional Condition, and runs Responses.
- Variable Event Listener: one per value type. It has a Filter, an optional Condition and Responses. Its "receive current value on enable" option is off by default.
- Responses are pluggable: a UnityEvent response, a "run a Command Sequence" response, and custom responses.

## Filters and Conditions
- There is one Filter slot. The composite Filter and composite Condition both support All / Any / None. Inverting means wrapping in None. The built-in "listen value" and the inverse flag go away.
- v1 Filters:
  - Equality (compares against a Variable Reference);
  - Comparison (numeric; no `Mathf.Approximately` for ints);
  - Type;
  - Composite;
  - Null.
- The **Null** Filter treats Unity "missing" (destroyed) objects as null.
- A bridge Condition checks a Variable Reference against a Filter.

## Values
- Value Source: anything that provides a value. Readable and writable are separate. Writing to a read-only source is an error, not a silent no-op.
- There is an untyped Value Source (returns `object`), and every typed Value Source inherits it. The untyped value is the typed value as an object. A class that implements several typed sources declares which one its untyped value is.
- Writing an `object` into a writable typed source converts it when the type fits and fails with a clear error when it doesn't.
- The new package doesn't replace anything in the existing projects, so compatibility with the old code isn't a constraint. Bind Collection and the "unused" notification are left out.
- Variable Reference has two modes:
  - **Value**: a private value per object.
  - **Source**: points at a Value Source, typically a global Variable Event.

  Collection mode is dropped. "Element of a list at an index" becomes its own Value Source.
- A Variable Reference passes on "changed" when its source can notify, so wait Commands avoid polling. They fall back to polling when it can't.
- Per-instance *shared* state is out of scope for v1. Shared means global Variable Events; private means Value mode.

## Decided during implementation (review these)
- **Odin check:** a `#warning`, not an `#error`. When Odin and the package are imported together for the first time, the package compiles once before Odin adds `ODIN_INSPECTOR`, so an `#error` gave a false alarm. With Odin really missing, every assembly fails with "Sirenix could not be found" and the warning names the cause.
- **Restart** starts the new Run on the frame after the stopped Run has cleaned up. Several Restart requests in one frame collapse into one. Without this, the outcome depended on whether cleanup was instant.
- **No frame yield between Commands.** Instant Commands in a row all run in the same frame. The old runner waited a frame per Command.
- **A Run with Continue On Failure** still ends with status **Failed** if anything failed, so `onRunFailed` fires at the end.
- **Skip Parent / Stop Parent on a top-level Command** (no enclosing Composite) Stops the Run and logs a warning.
- **External `Skip()`** ends the current *top-level* Command. If that's a Composite, the whole Composite is Skipped, as before.
- **Run Command Sequence** runs the target's Commands *inline* as children of this Run, so they show in its tree and obey its Skip/Stop. It does not start a separate Run on the target, and the target's own events don't fire. Nesting deeper than 64 levels fails, which catches a Sequence that runs itself.
- **Race** ignores Disabled children, so they can't win instantly. A Race with no enabled children completes immediately.
- **Branch / Filter Condition / Raise Event / Set Variable / Set Active** with nothing assigned **Fail** with a clear message instead of doing nothing.
- **Nested Raise values:** if a Listener Sets the Variable Event it's listening to, Listeners later in the outer Raise read the newest value, so everyone ends with the latest value.
- **`EventAsset`** is the code name of an Event. It avoids clashing with `UnityEngine.Event`.
- **Types hosted inside Odin data are not `[Serializable]`:** `VariableReference`, Commands, Filters, Conditions, Responses. That makes Odin, not Unity, serialize them, which is what keeps interface fields alive.

## Editor tooling (v1)
- Live Run tree.
- Hierarchy badge for objects with a Command Sequence.
- Reference finder: every Listener, Command and Variable Reference that points at a given Event or Variable Event.
- A menu to create Event / Variable Event assets.
