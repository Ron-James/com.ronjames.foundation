using System;
using System.Collections.Generic;
using RonJames.Foundation.Values;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace RonJames.Foundation.Events
{
    /// <summary>
    /// Listens to one or more Events (Variable Events work too; their value is ignored) and runs its
    /// Responses, optionally only when a Condition is met.
    /// </summary>
    [AddComponentMenu("RonJames Foundation/Event Listener")]
    public class EventListener : SerializedMonoBehaviour, IEventListener
    {
        [OdinSerialize]
        [Tooltip("The Events this listener responds to.")]
        private List<IEvent> events = new List<IEvent>();

        [OdinSerialize]
        [Tooltip("Optional. When set, the Responses run only if this Condition is met at the moment of the Raise.")]
        private ICondition condition;

        [OdinSerialize]
        private List<IResponse> responses = new List<IResponse>();

        /// <summary>The Events listened to. Changing this while enabled takes effect on the next enable.</summary>
        public List<IEvent> Events => events;

        /// <summary>The optional gate.</summary>
        public ICondition Condition
        {
            get => condition;
            set => condition = value;
        }

        /// <summary>The Responses run on each accepted Raise.</summary>
        public List<IResponse> Responses => responses;

        /// <summary>Subscribes to every Event.</summary>
        protected virtual void OnEnable()
        {
            foreach (IEvent target in events)
            {
                if (!UnityNull.IsNull(target))
                {
                    target.Subscribe(this);
                }
            }
        }

        /// <summary>Unsubscribes from every Event.</summary>
        protected virtual void OnDisable()
        {
            foreach (IEvent target in events)
            {
                if (!UnityNull.IsNull(target))
                {
                    target.Unsubscribe(this);
                }
            }
        }

        /// <inheritdoc />
        [Button("Respond (test)")]
        public void OnEventRaised()
        {
            if (condition != null && !condition.IsMet())
            {
                return;
            }

            foreach (IResponse response in responses)
            {
                if (response == null)
                {
                    continue;
                }

                try
                {
                    response.Respond();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
