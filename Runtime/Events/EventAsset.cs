using System;
using RonJames.Foundation.Values;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RonJames.Foundation.Events
{
    /// <summary>
    /// A shared asset that signals something happened and carries no value. Named EventAsset rather
    /// than Event so it never clashes with <c>UnityEngine.Event</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "RonJames Foundation/Event", fileName = "New Event", order = 0)]
    public class EventAsset : ScriptableObject, IEvent, IPlayModeResettable
    {
        [SerializeField]
        [TextArea(2, 5)]
        [Tooltip("What this event means and who raises it. For people, not code.")]
        private string description = "";

        [NonSerialized]
        private readonly ListenerList listeners = new ListenerList();

        [NonSerialized]
        private int raiseCount;

        /// <summary>What this event means, for people.</summary>
        public string Description => description;

        /// <summary>The number of subscribed Listeners (for tests and debugging).</summary>
        [ShowInInspector]
        [ReadOnly]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        public int ListenerCount => listeners.Count;

        /// <summary>How many times this event has been Raised this play session.</summary>
        [ShowInInspector]
        [ReadOnly]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        public int RaiseCount => raiseCount;

        /// <inheritdoc />
        [Button]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        public void Raise()
        {
            object[] current = listeners.BeginRaise(this);
            if (current == null)
            {
                return;
            }

            raiseCount++;
            try
            {
                foreach (object listener in current)
                {
                    if (ListenerList.ShouldSkip(listener))
                    {
                        continue;
                    }

                    try
                    {
                        switch (listener)
                        {
                            case IEventListener eventListener:
                                eventListener.OnEventRaised();
                                break;
                            case Action callback:
                                callback();
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        ListenerList.Report(exception, listener, this);
                    }
                }
            }
            finally
            {
                listeners.EndRaise();
            }
        }

        /// <inheritdoc />
        public void Subscribe(IEventListener listener) => listeners.Add(listener);

        /// <inheritdoc />
        public void Unsubscribe(IEventListener listener) => listeners.Remove(listener);

        /// <inheritdoc />
        public void Subscribe(Action listener) => listeners.Add(listener);

        /// <inheritdoc />
        public void Unsubscribe(Action listener) => listeners.Remove(listener);

        /// <inheritdoc />
        public void ResetForPlayMode()
        {
            listeners.Clear();
            raiseCount = 0;
        }

        /// <summary>Registers for the play-mode reset.</summary>
        protected virtual void OnEnable()
        {
            PlayModeReset.Register(this);
        }

        /// <summary>Unregisters from the play-mode reset.</summary>
        protected virtual void OnDisable()
        {
            PlayModeReset.Unregister(this);
        }
    }
}
