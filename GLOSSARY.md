# RonJames Foundation

Reusable Unity infrastructure for building designer-authored behaviour: command sequences that run over time, and ScriptableObject-based events and values that connect systems without them knowing about each other.

## Commands

**Command**:
One configured unit of work in a Sequence, such as "wait 2 seconds" or "raise this event". A Command is pure configuration and holds no state from any particular Run.
_Avoid_: Action, step, task

**Sequence**:
An ordered list of Commands that runs one after another.
_Avoid_: Command list, train, queue

**Composite**:
A Command that contains its own Sequence or group of Commands, so Commands can be nested.
_Avoid_: Group command, macro

**Sequence Composite**:
A Composite that runs its Commands one after another.
_Avoid_: Sequential composite command

**Parallel Composite**:
A Composite that runs its Commands at the same time and finishes when all of them have finished.
_Avoid_: Composite command (unqualified)

**Race Composite**:
A Composite that runs its Commands at the same time, finishes as soon as one of them finishes, and Skips the rest.
_Avoid_: Any composite, first-wins

**Hooks Composite**:
A Composite that wraps a single Command so authors can respond when it starts, completes, is Skipped or fails.
_Avoid_: Command events

**Queued Run**:
A Run waiting to start, in order, once the current Run of the same Command Sequence has finished.
_Avoid_: Pending queue, appended commands

**Restart**:
Stopping a Command Sequence's current Run, and discarding its Queued Runs, so a new Run starts immediately.
_Avoid_: Rerun, reset

**Sequence Switcher**:
The scene component that switches between Command Sequences so only the most recently requested one runs. It Stops the active one and runs its cleanup Sequence before starting the next.
_Avoid_: Step coordinator, step manager

**Condition**:
A yes/no question about the current state of the world that takes no input, such as "is the door open?".
_Avoid_: Check, requirement

**Disabled Command**:
A Command that an author has switched off. Runs pass over it without executing it.
_Avoid_: Inactive command, muted command

**Failed**:
The outcome of a Command that ended with an error. By default it Stops its Sequence.
_Avoid_: Errored, crashed

**Command Sequence**:
The scene component that holds a Sequence and starts Runs of it.
_Avoid_: Command list executor, invoker, runner

**Run**:
One execution of a Sequence from start to finish. It owns all live state: the current Command, statuses, elapsed time and progress.
_Avoid_: Execution, queue, train

**Skip**:
Ending the current Command early so the Sequence continues with the next one.
_Avoid_: Cancel

**Stop**:
Ending a Sequence so none of its remaining Commands run.
_Avoid_: Cancel and stop, abort, halt

**Stop Sequence**:
A request from inside a Command to Stop the Sequence that Command is in.
_Avoid_: Cancel self and stop train

**Skip Parent**:
A request from inside a Command to Skip the Composite that encloses it, so the Composite's parent Sequence continues.
_Avoid_: Try cancel root train

**Stop Parent**:
A request from inside a Command to Stop the Sequence that contains its enclosing Composite.
_Avoid_: Try cancel and stop root train

## Events

**Event**:
A shared asset that signals that something happened and carries no value.
_Avoid_: Void event, event bus, game event

**Variable Event**:
A shared asset that holds a typed value and notifies its Listeners whenever that value is Set or Raised.
_Avoid_: EventSO, variable, typed event, value event

**Raise**:
Notifying an Event's or Variable Event's Listeners, with the current value in the case of a Variable Event.
_Avoid_: Fire, invoke, publish, trigger

**Set**:
Changing a Variable Event's value and then Raising it.
_Avoid_: Write, assign

**Set Silently**:
Changing a Variable Event's value without Raising it.
_Avoid_: Raise dry, set without notify

**Initial Value**:
The value a Variable Event starts each play session with, as authored.
_Avoid_: Default value

**Runtime Value**:
A Variable Event's current value during a play session. It is never saved back to the asset.
_Avoid_: Stored value

**Listener**:
Anything subscribed to an Event or Variable Event that responds when it is Raised.
_Avoid_: Subscriber, handler, observer

**Event Listener**:
The scene component that listens to one or more Events and runs its Responses.
_Avoid_: Void listener

**Variable Event Listener**:
The scene component, one per value type, that listens to one or more Variable Events and runs its Responses with the value when it passes the Filter.
_Avoid_: Typed listener, EventListenerComponent

**Response**:
One thing a Listener does when it hears a Raise, such as invoking a UnityEvent or starting a Command Sequence.
_Avoid_: Reaction, callback, handler

**Filter**:
A yes/no test applied to a Variable Event's value that decides whether a Listener responds to that Raise.
_Avoid_: Predicate, guard

## Values

**Value Source**:
Anything that can provide a value: a Variable Event asset, a component, or a plain object. A typed Value Source is also an untyped one, and some Value Sources can be written to.
_Avoid_: Value asset, IValueAsset, provider, reference container, reference source

**Variable Reference**:
A field that resolves to a typed value, in one of two modes: Value or Source.
_Avoid_: Value reference, reference

**Value mode**:
A Variable Reference that holds its own value, private to the object it is on.
_Avoid_: Constant, local value

**Source mode**:
A Variable Reference that reads from, and if allowed writes to, a Value Source such as a global Variable Event.
_Avoid_: Reference mode, asset mode
