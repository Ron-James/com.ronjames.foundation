# Odin serialization is a hard dependency

Polymorphic and interface-typed fields (Sequences of Commands, Variable References backed by any value source) are serialized with Odin (`[OdinSerialize]`, Odin's serialized base classes), not Unity's `[SerializeReference]`. Only Odin can store an interface whose value is a ScriptableObject or component, and that is what lets a Variable Reference or a Command point at any kind of source. The package does not bundle Odin: the consuming project must already have it. When it is missing, the package fails to compile rather than silently disabling itself: every assembly reports that `Sirenix` can't be found, and a warning names Odin as the cause. It's a warning, not an `#error`, because the first import of Odin and the package together compiles once before Odin defines `ODIN_INSPECTOR`.

## Considered Options

- **`[SerializeReference]` with Odin only for drawing:** prefab overrides and diffs behave natively, but a Unity object can't sit behind an interface without a wrapper type on every field.
- **Optional Odin with a fallback path:** two serialization paths to keep correct, which defeats a "rock solid" goal.

## Consequences

Prefab overrides of Sequences and Variable References live inside Odin's serialization data, so they merge less cleanly in version control than native fields. Every consuming project needs an Odin licence.
