using UnityEngine;
using UnityEngine.Events;

namespace RonJames.Foundation.Events
{
    /// <summary>One thing an Event Listener does when it hears a Raise.</summary>
    public interface IResponse
    {
        /// <summary>Performs the response.</summary>
        void Respond();
    }

    /// <summary>One thing a Variable Event Listener does, with the value, when it hears a Raise.</summary>
    public interface IResponse<in T>
    {
        /// <summary>Performs the response with the Raised value.</summary>
        void Respond(T value);
    }

    /// <summary>Invokes a UnityEvent.</summary>
    public class UnityEventResponse : IResponse
    {
        [SerializeField]
        private UnityEvent onRaised = new UnityEvent();

        /// <inheritdoc />
        public void Respond() => onRaised.Invoke();
    }

    /// <summary>Invokes a UnityEvent with the value, and optionally one without it.</summary>
    public class UnityEventResponse<T> : IResponse<T>
    {
        [SerializeField]
        private UnityEvent<T> onRaised = new UnityEvent<T>();

        [SerializeField]
        private UnityEvent onRaisedWithoutValue = new UnityEvent();

        /// <inheritdoc />
        public void Respond(T value)
        {
            onRaised.Invoke(value);
            onRaisedWithoutValue.Invoke();
        }
    }
}
