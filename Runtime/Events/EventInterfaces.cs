using System;
using RonJames.Foundation.Values;

namespace RonJames.Foundation.Events
{
    /// <summary>Something that responds when an Event (or a Variable Event, ignoring its value) is Raised.</summary>
    public interface IEventListener
    {
        /// <summary>Called when an Event this listener subscribed to is Raised.</summary>
        void OnEventRaised();
    }

    /// <summary>Something that responds with the value when a Variable Event is Raised.</summary>
    public interface IVariableEventListener<in T>
    {
        /// <summary>Called with the Variable Event's current value when it is Raised.</summary>
        void OnEventRaised(T value);
    }

    /// <summary>
    /// Signals that something happened. Both value-less Events and Variable Events are IEvents, so
    /// anything that only cares "it was Raised" can listen to either.
    /// </summary>
    public interface IEvent
    {
        /// <summary>Notifies every Listener, in subscription order.</summary>
        void Raise();

        /// <summary>Adds a Listener. Adding the same Listener twice has no effect.</summary>
        void Subscribe(IEventListener listener);

        /// <summary>Removes a Listener.</summary>
        void Unsubscribe(IEventListener listener);

        /// <summary>Adds a callback. Adding the same delegate twice has no effect.</summary>
        void Subscribe(Action listener);

        /// <summary>Removes a callback.</summary>
        void Unsubscribe(Action listener);
    }

    /// <summary>
    /// Holds a typed value and notifies its Listeners whenever that value is Set or Raised. It is
    /// both an Event and a writable, observable Value Source.
    /// </summary>
    public interface IVariableEvent<T> : IEvent, IWritableValueSource<T>, IObservableValueSource
    {
        /// <summary>Changes the value, then Raises (unless "only raise on change" applies).</summary>
        void Set(T value);

        /// <summary>Changes the value without Raising.</summary>
        void SetSilently(T value);

        /// <summary>Adds a typed Listener. Adding the same Listener twice has no effect.</summary>
        void Subscribe(IVariableEventListener<T> listener);

        /// <summary>Removes a typed Listener.</summary>
        void Unsubscribe(IVariableEventListener<T> listener);

        /// <summary>Adds a typed callback. Adding the same delegate twice has no effect.</summary>
        void Subscribe(Action<T> listener);

        /// <summary>Removes a typed callback.</summary>
        void Unsubscribe(Action<T> listener);
    }
}
