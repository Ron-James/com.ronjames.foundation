# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/).

## [0.3.0] - 2026-11-17

### Added
- **Raise Current** (`VariableEvent<T>.RaiseCurrent()`, also on `IVariableEvent<T>`): raises the current value, or, when it's null or a destroyed Unity object, puts the Initial Value back and raises that. It takes no parameters (so it can be wired from UnityEvents) and has a play-mode inspector button.

### Changed
- `IVariableEvent<T>` gained `RaiseCurrent()`. Custom implementations of the interface (such as the GushesheGear bridge) must add it; the bridge sketch in the GushesheGear migration guide includes it.

## [0.2.0] - 2026-11-17

### Added
- **Control** Command: makes a Stop Sequence, Skip Parent or Stop Parent request from the inspector. Designers can now end a Sequence or Composite early without code, replacing the old per-command "cancel on complete" option.
- **Wait For Event** can wait for several Raises (*Required Raises*, default 1), with the count shown as progress.
- Full documentation in `Documentation~/`: an overview, a guide per module (Values, Events, Commands), and a migration guide from the earlier Bizar projects.
- A GushesheGear migration guide: Action SOs, action listeners and Action References mapped to the package, plus a phased migration plan.
- This changelog, and `documentationUrl` / `changelogUrl` in `package.json`.

### Fixed
- Delegates whose target is a destroyed Unity object (a method subscribed without a matching unsubscribe) are now skipped when an Event is Raised, instead of calling into the destroyed object.

### Changed
- The ADRs and design notes moved from `docs/` to `Documentation~/`, so they are no longer imported into consuming projects as assets.

## [0.1.0] - 2026-11-17

First release.

### Added
- **Values:**
  - Value Sources: untyped and typed, readable and writable, observable.
  - `VariableReference<T>` with Value and Source modes.
  - Filters (Equality, Comparison, Type, Null, Composite All / Any / None).
  - Conditions (Bool, Filter, Composite), observable so waits don't poll.
  - `ListElementSource<T>`.
  - A central play-mode reset that works with domain reload disabled.
- **Events:**
  - `EventAsset` and `VariableEvent<T>` (Initial / Runtime value, Set / Set Silently / Raise, optional Only Raise On Change and Keep Runtime Value In Editor).
  - Event Listener and Variable Event Listener components with Filter, Condition and Responses.
  - Ten built-in value types.
- **Commands:**
  - Stateless `Awaitable` Commands run by the `CommandSequence` component, with Skip / Stop, the in-command requests Stop Sequence, Skip Parent and Stop Parent, failure propagation, and Queue / Restart policies.
  - Sequence, Parallel, Race and Hooks Composites.
  - Built-in Commands: Delay, Branch, Switch, Run Command Sequence, Wait For Event, Wait Until, Raise Event, Set Variable, Set Active, Invoke UnityEvent, Log.
  - The Sequence Switcher, and Run Command Sequence Responses.
- **Editor:** a live Run tree, faded Disabled Commands, a hierarchy badge, a reference finder, and the Variable Event Type generator.
- **Samples and tests:** the Key and Door sample, plus 45 EditMode / PlayMode tests.
