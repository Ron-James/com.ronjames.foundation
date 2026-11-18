using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RonJames.Foundation.Events;
using RonJames.Foundation.Values;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RonJames.Foundation.Tests
{
    public class EventsTests
    {
        private EventAsset signal;
        private IntVariableEvent number;
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            signal = Create<EventAsset>("Signal");
            number = Create<IntVariableEvent>("Number");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.name = name;
            created.Add(instance);
            return instance;
        }

        [Test]
        public void Set_ChangesTheValueAndRaises_SetSilentlyDoesNot()
        {
            var received = new List<int>();
            number.Subscribe((Action<int>)received.Add);

            number.Set(1);
            number.SetSilently(2);
            number.Raise();

            Assert.AreEqual(new[] { 1, 2 }, received);
            Assert.AreEqual(2, number.Value);
        }

        [Test]
        public void RaiseCurrent_RaisesTheCurrentValue_OrTheInitialValueWhenNull()
        {
            var target = Create<GameObjectVariableEvent>("Target");
            var fallback = new GameObject("fallback");
            var current = new GameObject("current");
            try
            {
                target.InitialValue = fallback;
                target.ResetForPlayMode();
                var received = new List<GameObject>();
                target.Subscribe((Action<GameObject>)received.Add);

                target.Set(current);
                target.RaiseCurrent();
                Assert.AreSame(current, received[1], "A non-null current value is raised as is.");

                target.Set(null);
                target.RaiseCurrent();
                Assert.AreSame(fallback, received[3], "A null current value falls back to the Initial Value.");
                Assert.AreSame(fallback, target.Value, "The fallback is stored, so listeners read what they're given.");

                target.Set(current);
                Object.DestroyImmediate(current);
                target.RaiseCurrent();
                Assert.AreSame(fallback, received[5], "A destroyed object counts as null.");
            }
            finally
            {
                Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void RaiseCurrent_OnValueTypes_IsTheSameAsRaise()
        {
            var received = new List<int>();
            number.InitialValue = 10;
            number.ResetForPlayMode();
            number.Subscribe((Action<int>)received.Add);

            number.Set(0);
            number.RaiseCurrent();

            Assert.AreEqual(new[] { 0, 0 }, received, "0 is a value, not null, so there is no fallback.");
        }

        [Test]
        public void Set_SameValueStillRaises_UnlessOnlyRaiseOnChange()
        {
            int raises = 0;
            number.Subscribe((Action)(() => raises++));

            number.Set(5);
            number.Set(5);
            Assert.AreEqual(2, raises, "Event-like by default: the same value Set twice still notifies.");

            number.OnlyRaiseOnChange = true;
            number.Set(5);
            number.Set(6);
            Assert.AreEqual(3, raises);
        }

        [Test]
        public void Listeners_AreNotifiedInSubscriptionOrder_AndDuplicatesIgnored()
        {
            var order = new List<string>();
            Action first = () => order.Add("first");
            Action second = () => order.Add("second");
            signal.Subscribe(first);
            signal.Subscribe(second);
            signal.Subscribe(first);

            signal.Raise();

            Assert.AreEqual(new[] { "first", "second" }, order);
            Assert.AreEqual(2, signal.ListenerCount);
        }

        [Test]
        public void SubscribingDuringARaise_TakesEffectOnTheNextRaise()
        {
            int lateCalls = 0;
            Action late = () => lateCalls++;
            Action subscriber = null;
            subscriber = () =>
            {
                signal.Subscribe(late);
                signal.Unsubscribe(subscriber);
            };
            signal.Subscribe(subscriber);

            signal.Raise();
            Assert.AreEqual(0, lateCalls);

            signal.Raise();
            Assert.AreEqual(1, lateCalls);
        }

        [Test]
        public void NestedSet_RaisesImmediately_AndLaterListenersSeeTheNewestValue()
        {
            var seenByLast = new List<int>();
            number.Subscribe((Action<int>)(value =>
            {
                if (value == 1)
                {
                    number.Set(2);
                }
            }));
            number.Subscribe((Action<int>)seenByLast.Add);

            number.Set(1);

            Assert.AreEqual(new[] { 2, 2 }, seenByLast);
            Assert.AreEqual(2, number.Value);
        }

        [Test]
        public void RunawayNestedRaises_AreStoppedAtTheDepthLimit()
        {
            int raises = 0;
            signal.Subscribe((Action)(() =>
            {
                raises++;
                signal.Raise();
            }));

            LogAssert.Expect(LogType.Error, new Regex("Raised inside its own Raise"));
            signal.Raise();

            Assert.AreEqual(32, raises);
        }

        [Test]
        public void AThrowingListener_IsLogged_AndTheRestStillRun()
        {
            bool secondRan = false;
            signal.Subscribe((Action)(() => throw new InvalidOperationException("boom")));
            signal.Subscribe((Action)(() => secondRan = true));

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            signal.Raise();

            Assert.IsTrue(secondRan);
        }

        [Test]
        public void DestroyedUnityListeners_AreSkipped()
        {
            var alive = Create<TestListenerObject>("alive");
            var doomed = Create<TestListenerObject>("doomed");
            signal.Subscribe(doomed);
            signal.Subscribe(alive);
            Object.DestroyImmediate(doomed);

            signal.Raise();

            Assert.AreEqual(new[] { "alive" }, alive.Calls);
        }

        [Test]
        public void DelegatesTargetingDestroyedObjects_AreSkipped()
        {
            var alive = Create<TestListenerObject>("alive");
            var doomed = Create<TestListenerObject>("doomed");
            signal.Subscribe((Action)doomed.Record);
            signal.Subscribe((Action)alive.Record);
            Object.DestroyImmediate(doomed);

            signal.Raise();

            Assert.AreEqual(new[] { "alive" }, alive.Calls);
            Assert.IsEmpty(doomed.Calls, "A forgotten unsubscribe must not call into a destroyed object.");
        }

        [Test]
        public void VariableEvents_CanBeListenedToAsPlainEvents()
        {
            var listener = Create<TestListenerObject>("plain");
            IEvent asEvent = number;
            asEvent.Subscribe(listener);

            number.Set(3);

            Assert.AreEqual(1, listener.Calls.Count);
        }

        [Test]
        public void Reset_RestoresACopyOfTheInitialValue_AndDropsListeners()
        {
            var listEvent = Create<IntListVariableEvent>("List");
            listEvent.InitialValue = new List<int> { 1 };
            listEvent.ResetForPlayMode();
            listEvent.Subscribe((Action)(() => { }));

            listEvent.Value.Add(2);
            Assert.AreEqual(1, listEvent.InitialValue.Count, "Changing the Runtime Value must not change the authored Initial Value.");

            listEvent.ResetForPlayMode();
            Assert.AreEqual(new[] { 1 }, listEvent.Value);
            Assert.AreEqual(0, listEvent.ListenerCount);
        }

        [Test]
        public void NotifyOrderNewestFirst_NotifiesTheMostRecentSubscriberFirst()
        {
            var order = new List<string>();
            signal.Subscribe((Action)(() => order.Add("first")));
            signal.Subscribe((Action)(() => order.Add("second")));
            signal.Subscribe((Action)(() => order.Add("third")));

            signal.NotifyOrder = NotifyOrder.NewestFirst;
            signal.Raise();
            Assert.AreEqual(new[] { "third", "second", "first" }, order);

            order.Clear();
            signal.NotifyOrder = NotifyOrder.OldestFirst;
            signal.Raise();
            Assert.AreEqual(new[] { "first", "second", "third" }, order, "Switching back takes effect on the next Raise.");
        }

        [Test]
        public void NotifyOrderNewestFirst_OnAVariableEvent_NotifiesValueListenersBeforePlainOnes()
        {
            var order = new List<string>();
            number.Subscribe((Action)(() => order.Add("plain 1")));
            number.Subscribe((Action<int>)(value => order.Add("value 1")));
            number.Subscribe((Action)(() => order.Add("plain 2")));
            number.Subscribe((Action<int>)(value => order.Add("value 2")));

            number.NotifyOrder = NotifyOrder.NewestFirst;
            number.Set(5);

            Assert.AreEqual(new[] { "value 2", "value 1", "plain 2", "plain 1" }, order);
        }

        [Test]
        public void NotifyOrderNewestFirst_StillAppliesSubscriptionChangesOnTheNextRaise()
        {
            var order = new List<string>();
            Action late = () => order.Add("late");
            signal.NotifyOrder = NotifyOrder.NewestFirst;
            signal.Subscribe((Action)(() => { order.Add("first"); signal.Subscribe(late); }));

            signal.Raise();
            Assert.AreEqual(new[] { "first" }, order);

            order.Clear();
            signal.Raise();
            Assert.AreEqual(new[] { "late", "first" }, order);
        }

        [Test]
        public void VariableEvents_RegisterForThePlayModeReset()
        {
            number.InitialValue = 10;
            number.Set(99);

            PlayModeReset.ResetAll();

            Assert.AreEqual(10, number.Value);
        }
    }
}
