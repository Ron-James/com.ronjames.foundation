using System;
using NUnit.Framework;
using RonJames.Foundation.Events;
using RonJames.Foundation.Values;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RonJames.Foundation.Tests
{
    public class ValuesTests
    {
        private sealed class ReadOnlySource : IValueSource<int>
        {
            public int Value => 7;
            object IValueSource.Value => Value;
            public Type ValueType => typeof(int);
        }

        private IntVariableEvent intEvent;

        [SetUp]
        public void SetUp()
        {
            intEvent = ScriptableObject.CreateInstance<IntVariableEvent>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(intEvent);
        }

        [Test]
        public void ValueMode_ReadsAndWritesItsOwnValue()
        {
            var reference = new VariableReference<int>(3);
            Assert.AreEqual(3, reference.Value);
            reference.Value = 4;
            Assert.AreEqual(4, (int)reference);
            Assert.IsTrue(reference.CanWrite);
        }

        [Test]
        public void SourceMode_ReadsAndSetsAVariableEvent()
        {
            var reference = VariableReference<int>.FromSource(intEvent);
            int raisedWith = -1;
            intEvent.Subscribe((Action<int>)(value => raisedWith = value));

            reference.Value = 9;

            Assert.AreEqual(9, intEvent.Value);
            Assert.AreEqual(9, reference.Value);
            Assert.AreEqual(9, raisedWith, "Writing through a reference is a Set, which Raises.");
        }

        [Test]
        public void SourceMode_WritingToAReadOnlySource_Throws()
        {
            var reference = VariableReference<int>.FromSource(new ReadOnlySource());
            Assert.AreEqual(7, reference.Value);
            Assert.IsFalse(reference.CanWrite);
            Assert.Throws<InvalidOperationException>(() => reference.Value = 1);
        }

        [Test]
        public void SourceMode_MissingSource_ReadsDefaultAndRefusesWrites()
        {
            var reference = VariableReference<int>.FromSource(intEvent);
            Object.DestroyImmediate(intEvent);

            Assert.IsTrue(reference.IsMissing);
            Assert.AreEqual(0, reference.Value);
            Assert.Throws<InvalidOperationException>(() => reference.Value = 1);
        }

        [Test]
        public void UntypedWrite_ConvertsSafeValues_AndRejectsOthers()
        {
            IWritableValueSource untyped = new VariableReference<int>();
            untyped.SetValue("5");
            Assert.AreEqual(5, untyped.Value);
            untyped.SetValue(6L);
            Assert.AreEqual(6, untyped.Value);
            Assert.Throws<InvalidCastException>(() => untyped.SetValue(new object()));
            Assert.Throws<InvalidCastException>(() => untyped.SetValue("not a number"));
            Assert.Throws<InvalidCastException>(() => untyped.SetValue(null));
        }

        [Test]
        public void TypedSource_IsAlsoAnUntypedSource()
        {
            intEvent.Set(12);
            IValueSource untyped = intEvent;
            Assert.AreEqual(12, untyped.Value);
            Assert.AreEqual(typeof(int), untyped.ValueType);
        }

        [Test]
        public void ChangedListener_FiresInValueModeAndForObservableSources()
        {
            int calls = 0;
            void OnChanged() => calls++;

            var local = new VariableReference<int>();
            Assert.IsTrue(local.TryAddChangedListener(OnChanged));
            local.Value = 1;

            var shared = VariableReference<int>.FromSource(intEvent);
            Assert.IsTrue(shared.TryAddChangedListener(OnChanged));
            intEvent.Set(2);

            var readOnly = VariableReference<int>.FromSource(new ReadOnlySource());
            Assert.IsFalse(readOnly.TryAddChangedListener(OnChanged), "A source that can't notify must say so.");

            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Combine_EmptyLists()
        {
            var empty = Array.Empty<ICondition>();
            Assert.IsTrue(Combine.Evaluate(empty, CombineMode.All, c => c.IsMet()));
            Assert.IsFalse(Combine.Evaluate(empty, CombineMode.Any, c => c.IsMet()));
            Assert.IsTrue(Combine.Evaluate(empty, CombineMode.None, c => c.IsMet()));
        }

        [Test]
        public void CompositeFilter_NoneInverts()
        {
            var isFive = new EqualityFilter<int>(new VariableReference<int>(5));
            var notFive = new CompositeFilter<int>(CombineMode.None, isFive);
            Assert.IsTrue(isFive.Allows(5));
            Assert.IsFalse(notFive.Allows(5));
            Assert.IsTrue(notFive.Allows(4));
        }

        [Test]
        public void ComparisonFilter_IntsCompareExactly_FloatsUseTolerance()
        {
            Assert.IsTrue(new ComparisonFilter<int>(Comparison.GreaterThan, 3).Allows(4));
            Assert.IsFalse(new ComparisonFilter<int>(Comparison.Equal, 3).Allows(4));
            Assert.IsTrue(new ComparisonFilter<float>(Comparison.Equal, 0.3f).Allows(0.1f + 0.2f));
            Assert.IsTrue(new ComparisonFilter<float>(Comparison.LessOrEqual, 1f).Allows(1f));
        }

        [Test]
        public void TypeFilter_MatchesInstancesOfSelectedTypes()
        {
            var filter = new TypeFilter<object>(typeof(string));
            Assert.IsTrue(filter.Allows("text"));
            Assert.IsFalse(filter.Allows(3));
            Assert.IsFalse(filter.Allows(null));
        }

        [Test]
        public void NullFilter_TreatsDestroyedObjectsAsNull()
        {
            var filter = new NullFilter<GameObject>();
            var gameObject = new GameObject("destroyed");
            Assert.IsFalse(filter.Allows(gameObject));
            Object.DestroyImmediate(gameObject);
            Assert.IsTrue(filter.Allows(gameObject), "A destroyed (missing) object counts as null.");
            Assert.IsTrue(filter.Allows(null));
        }

        [Test]
        public void Conditions_BoolAndFilterBridge()
        {
            var flag = ScriptableObject.CreateInstance<BoolVariableEvent>();
            try
            {
                var isMet = new BoolCondition(VariableReference<bool>.FromSource(flag));
                Assert.IsFalse(isMet.IsMet());
                flag.Set(true);
                Assert.IsTrue(isMet.IsMet());

                var overThree = new FilterCondition<int>(VariableReference<int>.FromSource(intEvent),
                    new ComparisonFilter<int>(Comparison.GreaterThan, 3));
                intEvent.Set(4);
                Assert.IsTrue(overThree.IsMet());

                Assert.Throws<InvalidOperationException>(() => new FilterCondition<int>().IsMet());
            }
            finally
            {
                Object.DestroyImmediate(flag);
            }
        }

        [Test]
        public void ListElementSource_ReadsAndWritesInRange()
        {
            var source = new ListElementSource<int>();
            Assert.AreEqual(0, source.Value, "A missing list reads as default.");
            Assert.Throws<InvalidOperationException>(() => source.SetValue(1));
        }
    }
}
