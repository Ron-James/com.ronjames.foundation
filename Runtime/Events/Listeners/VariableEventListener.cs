using System;
using System.Collections.Generic;
using RonJames.Foundation.Values;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace RonJames.Foundation.Events
{
    /// <summary>
    /// Listens to one or more Variable Events of one value type and runs its Responses with the value
    /// when it passes the Filter (and the Condition, if any).
    /// </summary>
    /// <remarks>Unity components can't be open generic: use a concrete subclass such as IntVariableEventListener.</remarks>
    public abstract class VariableEventListener<T> : SerializedMonoBehaviour, IVariableEventListener<T>
    {
        [OdinSerialize]
        [Tooltip("The Variable Events this listener responds to.")]
        private List<IVariableEvent<T>> events = new List<IVariableEvent<T>>();

        [SerializeField]
        [Tooltip("When on, each Variable Event's current value is delivered once on enable, as if it had just been Raised. " +
                 "Use it when this listener may enable after the value was Set.")]
        private bool receiveCurrentValueOnEnable;

        [OdinSerialize]
        [Tooltip("Optional. Raises whose value doesn't pass are ignored.")]
        private IFilter<T> filter;

        [OdinSerialize]
        [Tooltip("Optional. When set, the Responses run only if this Condition is met at the moment of the Raise.")]
        private ICondition condition;

        [OdinSerialize]
        private List<IResponse<T>> responses = new List<IResponse<T>>();

        /// <summary>The Variable Events listened to. Changing this while enabled takes effect on the next enable.</summary>
        public List<IVariableEvent<T>> Events => events;

        /// <summary>The optional Filter.</summary>
        public IFilter<T> Filter
        {
            get => filter;
            set => filter = value;
        }

        /// <summary>The optional gate.</summary>
        public ICondition Condition
        {
            get => condition;
            set => condition = value;
        }

        /// <summary>Whether current values are delivered on enable.</summary>
        public bool ReceiveCurrentValueOnEnable
        {
            get => receiveCurrentValueOnEnable;
            set => receiveCurrentValueOnEnable = value;
        }

        /// <summary>The Responses run on each accepted Raise.</summary>
        public List<IResponse<T>> Responses => responses;

        /// <summary>Subscribes, then optionally delivers each current value.</summary>
        protected virtual void OnEnable()
        {
            foreach (IVariableEvent<T> target in events)
            {
                if (!UnityNull.IsNull(target))
                {
                    target.Subscribe(this);
                }
            }

            if (!receiveCurrentValueOnEnable)
            {
                return;
            }

            foreach (IVariableEvent<T> target in events)
            {
                if (!UnityNull.IsNull(target))
                {
                    OnEventRaised(target.Value);
                }
            }
        }

        /// <summary>Unsubscribes from every Variable Event.</summary>
        protected virtual void OnDisable()
        {
            foreach (IVariableEvent<T> target in events)
            {
                if (!UnityNull.IsNull(target))
                {
                    target.Unsubscribe(this);
                }
            }
        }

        /// <inheritdoc />
        [Button("Respond (test)")]
        public void OnEventRaised(T value)
        {
            if (filter != null && !filter.Allows(value))
            {
                return;
            }

            if (condition != null && !condition.IsMet())
            {
                return;
            }

            foreach (IResponse<T> response in responses)
            {
                if (response == null)
                {
                    continue;
                }

                try
                {
                    response.Respond(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
