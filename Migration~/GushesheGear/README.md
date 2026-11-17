# GushesheGear migration kit

Tools for moving GushesheGear from its Action SOs, listener MonoBehaviours and Action References onto RonJames Foundation. The *why* and the behaviour differences are in [`Documentation~/migration-gushesheGear.md`](../../Documentation~/migration-gushesheGear.md). This folder is the *how*.

This folder ends in `~` inside the package, so Unity never imports it. You copy parts of it into GushesheGear, use them, and delete them when the migration is done.

| Folder | What it is | Goes to |
|---|---|---|
| `Bridge/bridge.patch` | Makes `BaseActionSO` an `IEvent`, `ActionSO<T>` an `IVariableEvent<T>` and `ValueAsset<T>` a writable Value Source, so package components can use the existing assets | applied with `git apply` |
| `Runtime/Types/` | Variable Event, Variable Event Listener and Set command for each GushesheGear value type (Product, Mission, StuntState, Vehicle…) | `Assets/_Migration/Runtime/Types/`, then move wherever you like and keep them |
| `Runtime/Responses/` | Responses ported from the old listener subclasses' `TriggerUnityEvents` (Bool on-true/on-false, Int-to-string, translated string, StuntState, Float format / compare) | `Assets/_Migration/Runtime/Responses/` (keep) |
| `Runtime/MigrateFromAttribute.cs` | `[MigrateFrom("oldField")]` for the field migrator | `Assets/_Migration/Runtime/` (delete after) |
| `Editor/` | The migration tools: *Tools > GushesheGear Migration* | `Assets/_Migration/Editor/` (delete after) |

## Before you start

- **Work on a branch and commit before every real run.** The tools change many prefabs and scenes at once. There's no editor undo across them, so git is the undo.
- **Close nothing, but save everything.** The tools open each scene in turn and put your open scenes back afterwards.
- **Every tool has a Dry run.** It writes a report to `Logs/` and changes nothing. Read it before pressing Migrate.

## Steps

### 1. Install

1. Add the package to `Packages/manifest.json`:
   `"com.ronjames.foundation": "https://github.com/Ron-James/com.ronjames.foundation.git#v0.3.1"`
2. From the GushesheGear folder, apply the bridge: `git apply <path-to-this-kit>/Bridge/bridge.patch`. It changes `ActionSO.cs` and `ValueAsset.cs` only. If `git apply` refuses because those files changed since the patch was made, apply the same edits by hand; they're short and commented.
3. Copy `Runtime/` and `Editor/` into `Assets/_Migration/`.
4. Let Unity compile. Nothing behaves differently yet: the bridge only *adds* interfaces.

### 2. Listener components

*Tools > GushesheGear Migration > Open Migration Window > 2. Listener components > Dry run*, read `Logs/ListenerMigration-DryRun.txt`, then **Migrate**.

For each old listener it either migrates it or leaves it with a reason.

**Migrated** (`BaseActionListener`, and `Bool`, `Int`, `Float`, `String`, `Vector2`, `StuntState`, `Stunt`, `Vehicle`, `CinemachineCamera`, `Collider`, `ComponentService`, `CustomComponent`, `Transform` listeners):
- A new **Event Listener** or **Variable Event Listener** is added, with the same actions, the same enabled state, and *Receive Current Value On Enable* set from `updateOnEnable`.
- A **Filter** is built from the listen value, `filterValues`, `_invertListenValue` and the old "ignore nulls" default.
- The old **UnityEvents move into Responses** (the same UnityEvent objects, so every wired call carries over). Subclass behaviour becomes a ported Response.
- `FloatActionListener`'s greater / less events fired outside the filter, so they get a second, unfiltered listener.
- The old component is then removed, or disabled if you untick *Remove*.

**Left alone, with the reason in the report:**
- UI-card listeners (`Product`, `Mission`, `Quest`, `ItemQuantity`, `ExperienceData`, `Level`, `ActiveModifier`, `Item`). Pools bind data into them and buttons call their methods, so they need a hand port (see the guide).
- Any listener that **something references**: a UnityEvent targets it, or a field or Odin data points at it.
- Any listener that's **overridden** in a prefab variant or a scene instance.
- Listeners using the **previous-value filter**, any of the five unused extra events (`onValueTransition`…), or a **Float multiplier** other than 1.

Prefab-instance parts are migrated in the prefab that owns them, never in the scene.

### 3. Fields in code ([MigrateFrom])

For each script that holds an `ActionReference<T>`, `ValueAssetReference<T>`, `ActionSubscription`, `MessageSubscription<T>` or a direct `ActionSO` field:

1. Make the class Odin-serialized: `MonoBehaviour` → `SerializedMonoBehaviour`, `ScriptableObject` (or `SerializableScriptableObject`) → `SerializedScriptableObject`. Existing Unity-serialized fields keep their data.
2. Add the new field **next to** the old one, marked with the old field's name:

   ```csharp
   [SerializeField] private ActionReference<float> speed;                               // old, keep for now
   [OdinSerialize, MigrateFrom(nameof(speed))] private VariableReference<float> speedRef; // new

   [SerializeField] private ActionSubscription onSave;                                   // old
   [OdinSerialize, MigrateFrom(nameof(onSave))] private IEvent onSaveEvent;              // new
   ```

3. *3. Marked fields > Dry run*, then **Migrate**. It fills every new field in every prefab, scene and ScriptableObject asset. A prefab instance is only written where it overrode the old field.
4. Switch the code to the new field (`speedRef.Value`, `onSaveEvent.Subscribe(OnSave)`…), then delete the old field and the attribute.

Supported:
- `VariableReference<T>` from `ActionReference<T>`, `ValueAssetReference<T>`, any `ValueReference<T, TAsset>` or a plain `T`.
- `IEvent` / `IVariableEvent<T>` (or a `List` / array of them) from an ActionSO field, an `ActionSubscription` / `MessageSubscription<T>`, their `…Set` structs, or lists of any of those.

### 4. Assets

*4. Action assets > Dry run*, then **Migrate**. Each ActionSO asset becomes the package type **in place**: `VoidActionSo` becomes `EventAsset`, and `XActionSO` becomes the `VariableEvent<T>` for the same `T`. The old `_defaultCallValue` becomes the Initial Value. The GUID is kept, so everything that reaches it through package interfaces keeps working.

It won't convert an asset while:
- **its class declares its own members** (e.g. `IntActionSO.AddAmount`, `ProductActionSO`'s dealer, `TransformActionSO`). Port those to the Variable Event type, or to extension methods, then tick *Convert even if…*;
- **anything that uses it still has an old-system field** (an ActionSO field, `ActionReference`, a subscription struct, an old listener). That reference would be lost, so finish steps 2 and 3 for it first.

### 5. Clean up

When the reports show nothing left:
1. Delete `Assets/_Migration/Editor` and `Runtime/MigrateFromAttribute.cs`.
2. Revert the bridge, or delete the old ActionSO system entirely (the guide's Phase 5).
3. Keep `Runtime/Types` and `Runtime/Responses`.

## Running headless

```bash
Unity -batchmode -quit -projectPath . -executeMethod GushesheGearMigration.Batch.ListenersDryRun
```

Also available: `Batch.Listeners`, `Batch.FieldsDryRun`, `Batch.Fields`, `Batch.AssetsDryRun`, `Batch.Assets`. Reports go to `Logs/`.
