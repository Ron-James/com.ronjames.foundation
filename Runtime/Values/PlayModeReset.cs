using System;
using System.Collections.Generic;
using UnityEngine;

namespace RonJames.Foundation.Values
{
    /// <summary>Something holding runtime state that must be reset at the start of every play session.</summary>
    public interface IPlayModeResettable
    {
        /// <summary>Restores the authored state and drops runtime-only state such as Listeners.</summary>
        void ResetForPlayMode();
    }

    /// <summary>
    /// The single place that resets runtime state at the start of every play session. It runs
    /// before any scene loads, and it also works when "Enter Play Mode Options" disables domain reload,
    /// where static fields and ScriptableObject state would otherwise survive between sessions.
    /// </summary>
    public static class PlayModeReset
    {
        private static readonly HashSet<IPlayModeResettable> Registered = new HashSet<IPlayModeResettable>();

        /// <summary>Registers an object to be reset. Call from <c>OnEnable</c>.</summary>
        public static void Register(IPlayModeResettable resettable)
        {
            if (resettable != null)
            {
                Registered.Add(resettable);
            }
        }

        /// <summary>Stops resetting an object. Call from <c>OnDisable</c>.</summary>
        public static void Unregister(IPlayModeResettable resettable)
        {
            if (resettable != null)
            {
                Registered.Remove(resettable);
            }
        }

        /// <summary>The number of registered objects (for tests and debugging).</summary>
        public static int RegisteredCount => Registered.Count;

        /// <summary>Resets every registered object now. Called automatically at play start.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetAll()
        {
            var snapshot = new IPlayModeResettable[Registered.Count];
            Registered.CopyTo(snapshot);
            foreach (IPlayModeResettable resettable in snapshot)
            {
                if (UnityNull.IsNull(resettable))
                {
                    Registered.Remove(resettable);
                    continue;
                }

                try
                {
                    resettable.ResetForPlayMode();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, resettable as UnityEngine.Object);
                }
            }
        }
    }
}
