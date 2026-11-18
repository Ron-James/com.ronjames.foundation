using System;
using System.Collections.Generic;
using RonJames.Foundation.Values;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RonJames.Foundation.Events
{
    /// <summary>
    /// The subscriber list behind every Event and Variable Event. It enforces the Raise rules:
    /// <list type="bullet">
    /// <item>Listeners are notified in subscription order (or newest first, see <see cref="NotifyOrder"/>), and adding the same one twice is ignored.</item>
    /// <item>Subscribing or unsubscribing during a Raise takes effect on the next Raise.</item>
    /// <item>A Raise started inside a Raise runs immediately (nested), up to <see cref="MaxRaiseDepth"/> deep.</item>
    /// <item>A Listener that throws is logged and the others still run.</item>
    /// <item>Destroyed Unity listeners, and delegates targeting destroyed Unity objects, are skipped.</item>
    /// </list>
    /// </summary>
    internal sealed class ListenerList
    {
        /// <summary>How deeply Raises may nest before one is refused as a probable infinite loop.</summary>
        public const int MaxRaiseDepth = 32;

        private readonly List<object> items = new List<object>();
        private object[] snapshot = Array.Empty<object>();
        private NotifyOrder snapshotOrder = NotifyOrder.OldestFirst;
        private bool dirty;
        private int depth;

        public int Count => items.Count;

        public IReadOnlyList<object> Items => items;

        public bool Add(object listener)
        {
            if (listener == null || items.Contains(listener))
            {
                return false;
            }

            items.Add(listener);
            dirty = true;
            return true;
        }

        public bool Remove(object listener)
        {
            if (listener == null || !items.Remove(listener))
            {
                return false;
            }

            dirty = true;
            return true;
        }

        public void Clear()
        {
            if (items.Count == 0)
            {
                return;
            }

            items.Clear();
            dirty = true;
        }

        /// <summary>
        /// Starts a Raise and returns the Listeners to notify, or null when the Raise is refused because
        /// it is nested too deeply. Every non-null return must be paired with <see cref="EndRaise"/>.
        /// With <see cref="NotifyOrder.NewestFirst"/> and a <paramref name="notifiedFirst"/> test, the Listeners
        /// that pass the test come first (newest first), then the rest (newest first).
        /// </summary>
        public object[] BeginRaise(Object context, NotifyOrder order = NotifyOrder.OldestFirst, Predicate<object> notifiedFirst = null)
        {
            if (depth >= MaxRaiseDepth)
            {
                Debug.LogError(
                    $"[RonJames Foundation] '{(context != null ? context.name : "event")}' was Raised inside its own Raise more than {MaxRaiseDepth} times. " +
                    "A Listener is probably Setting the event it listens to. This Raise was not delivered.",
                    context);
                return null;
            }

            if (dirty || order != snapshotOrder)
            {
                snapshot = Build(order, notifiedFirst);
                snapshotOrder = order;
                dirty = false;
            }

            depth++;
            return snapshot;
        }

        public void EndRaise()
        {
            depth--;
        }

        private object[] Build(NotifyOrder order, Predicate<object> notifiedFirst)
        {
            if (order == NotifyOrder.OldestFirst)
            {
                return items.ToArray();
            }

            var result = new object[items.Count];
            int next = 0;
            if (notifiedFirst != null)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (notifiedFirst(items[i]))
                    {
                        result[next++] = items[i];
                    }
                }
            }

            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (notifiedFirst == null || !notifiedFirst(items[i]))
                {
                    result[next++] = items[i];
                }
            }

            return result;
        }

        /// <summary>
        /// True for destroyed Unity listeners, and for delegates whose target is a destroyed Unity
        /// object (a method-group subscription like <c>Subscribe(OnRaised)</c> from a MonoBehaviour that
        /// was destroyed without unsubscribing). Lambdas capture a closure, not the object, so they
        /// can't be detected: always unsubscribe them.
        /// </summary>
        public static bool ShouldSkip(object listener)
        {
            if (UnityNull.IsNull(listener))
            {
                return true;
            }

            return listener is Delegate callback && callback.Target is Object target && target == null;
        }

        public static void Report(Exception exception, object listener, Object context)
        {
            Debug.LogException(exception, listener as Object ?? context);
        }
    }
}
