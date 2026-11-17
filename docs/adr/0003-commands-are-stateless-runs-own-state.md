# Commands are stateless; Runs own all live state

A Command holds only its configuration. Everything that changes while it executes (elapsed time, running/finished flags, progress, the current Command) lives in local variables inside the Command's execution or in the Run that is executing it. This allows the same Sequence to run more than once, even at the same time, and stops runtime state from being serialized into scenes and prefabs. That was a source of bugs when Commands were re-run after being disposed, or reused while still running.

## Consequences

Debugging can no longer read state off each Command in the inspector. Instead, the Command Sequence shows a live tree of its current Run: each Command's status, elapsed time, reported progress, and the active path through nested Composites. Commands report progress through the context they are given, never by setting fields on themselves.
