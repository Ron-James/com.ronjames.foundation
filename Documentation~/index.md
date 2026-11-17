# RonJames Foundation: documentation

RonJames Foundation is three small systems that let designers build behaviour in the inspector, and let systems talk to each other without referencing each other.

| Module | What it gives you | Guide |
|---|---|---|
| **Values** | `VariableReference<T>` fields, Value Sources, Filters, Conditions, the play-mode reset | [values.md](values.md) |
| **Events** | Event and Variable Event assets, Listener components, Responses | [events.md](events.md) |
| **Commands** | Commands, Composites, the `CommandSequence` component and its Runs, the Sequence Switcher | [commands.md](commands.md) |

If you've used the command / event / variable systems from the earlier Bizar projects, read **[migration.md](migration.md)** first. It maps every old class and option to its replacement and lists behaviour that changed.

For **GushesheGear** (Action SOs, action listeners, Action References), see **[migration-gushesheGear.md](migration-gushesheGear.md)**: a concept map, the behaviour differences that matter there, and a phased plan for migrating that project onto the package.

## How the pieces fit

```
          Values                      Events                         Commands
 ┌──────────────────────┐   ┌──────────────────────────┐   ┌────────────────────────────┐
 │ IValueSource(<T>)    │◄──┤ VariableEvent<T> is a     │   │ CommandSequence (component)│
 │ VariableReference<T> │   │ writable, observable      │   │   └ Run ─ RunNode tree     │
 │ IFilter<T>           │   │ Value Source              │   │ ICommand / Command         │
 │ ICondition           │──►│ EventListener /           │──►│ RunCommandSequenceResponse │
 │ PlayModeReset        │   │ VariableEventListener<T>  │   │ Set Variable, Raise Event, │
 └──────────────────────┘   │   Filter, Condition,      │   │ Wait For Event, Wait Until │
                            │   Responses               │   └────────────────────────────┘
                            └──────────────────────────┘
```

Assemblies depend in one direction only: **Values ← Events ← Commands**. Events never knows about Commands. The "run a Command Sequence" Response lives in the Commands assembly.

A typical flow:

1. A **Command** in a **Command Sequence** Sets a **Variable Event** ("Has Key" = true).
2. The Variable Event **Raises**. Every **Listener** subscribed to it is notified, in subscription order.
3. A **Variable Event Listener** checks its **Filter** (is the value true?) and its **Condition** (does the door have power?), then runs its **Responses**.
4. One Response starts another **Command Sequence**, which opens the door. Its Commands read **Variable References** that point at other Variable Events.

The **Key and Door** sample (Package Manager > RonJames Foundation > Samples) is exactly this flow, wired up in a scene.

## Vocabulary

The words in these guides have precise meanings, defined in [`GLOSSARY.md`](../GLOSSARY.md). The ones to know first:

- **Sequence**: an ordered list of Commands.
- **Run**: one execution of a Sequence. It owns all live state.
- **Skip** ends the current Command and continues. **Stop** ends the Sequence.
- **Event**: no value. **Variable Event**: holds a value and is an event.
- **Value Source**: anything that provides a value.

"Cancel" deliberately isn't used anywhere: in the old code it meant three different things.

## Design records

- [`adr/`](adr): the four decisions that would be expensive to reverse:
  - Odin is a hard dependency;
  - Variable Events combine a value and an event;
  - Commands are stateless;
  - Commands use `Awaitable`.
- [`design-notes.md`](design-notes.md): every smaller decision, including the ones made during implementation.

## Requirements

Unity 6 (6000.0+) and **Odin Inspector 3.3+ in the project**. Everything that holds interfaces (Commands, Variable References, Filters, Conditions, Responses) is serialized by Odin. See ADR 0001.
