# Variable Events combine a value and an event

A typed event asset always stores its value, and setting the value Raises it: there is no separate "typed event that remembers nothing" or "variable that doesn't notify". Only the value-less Event exists alongside it. In practice, typed event assets were almost always read for their last value too. One asset that is both an event and a variable keeps the asset count and the wiring down, and it is the familiar "Variable Event" pattern for the people who will use the package.

## Considered Options

- **Separate Event<T> and Variable<T> types:** clearer semantics on paper, but it doubles the asset types and forces authors to pick between them for every value.

## Consequences

Set always Raises, even when the value is unchanged, so repeated signals ("the same item was picked twice") still reach Listeners. Assets that represent state can opt in to raising only on change, and Set Silently exists for changes that shouldn't notify.
