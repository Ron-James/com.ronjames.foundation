# Commands use Unity's Awaitable, not UniTask or Task

Command execution returns Unity's built-in `Awaitable` (Unity 6 minimum), so the package adds no async dependency beyond Odin. Its two limitations are acceptable here. An `Awaitable` can be awaited only once, but only the Run ever awaits a Command. There's no built-in `WhenAll`/`WhenAny`, but the package ships its own small helpers for parallel Composites.

## Considered Options

- **UniTask:** more mature, zero-allocation, with `WhenAll`/`WhenAny` built in, but it would be a second third-party dependency in every consuming project.
- **`System.Threading.Tasks.Task`:** what earlier projects used. It allocates, and fire-and-forget Tasks silently swallow exceptions.

## Consequences

Command authors must not await the same `Awaitable` twice or store it for later. Changing async primitive later would touch every Command, so this is effectively fixed.
