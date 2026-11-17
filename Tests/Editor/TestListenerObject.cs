using System.Collections.Generic;
using RonJames.Foundation.Events;
using UnityEngine;

namespace RonJames.Foundation.Tests
{
    /// <summary>A Unity-object listener that never unsubscribes, to prove destroyed listeners are skipped.</summary>
    public class TestListenerObject : ScriptableObject, IEventListener
    {
        public readonly List<string> Calls = new List<string>();

        public void OnEventRaised() => Calls.Add(name);

        /// <summary>A method to subscribe as a plain delegate (method group).</summary>
        public void Record() => Calls.Add(name);
    }
}
