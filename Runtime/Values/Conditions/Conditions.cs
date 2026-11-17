using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace RonJames.Foundation.Values
{
    /// <summary>A yes/no question about the current state of the world that takes no input.</summary>
    public interface ICondition
    {
        /// <summary>True when the condition holds right now.</summary>
        bool IsMet();
    }

    /// <summary>
    /// A Condition that can tell you when its answer might have changed, so waiting code doesn't
    /// have to poll every frame.
    /// </summary>
    public interface IObservableCondition : ICondition
    {
        /// <summary>
        /// Subscribes to "this condition may have changed". Returns false (and subscribes nothing)
        /// when the condition cannot notify; callers should poll instead.
        /// </summary>
        bool TryAddChangedListener(Action listener);

        /// <summary>Removes a listener added with <see cref="TryAddChangedListener"/>.</summary>
        void RemoveChangedListener(Action listener);
    }

    /// <summary>Combines several Conditions with All / Any / None. Wrap a Condition in None to invert it.</summary>
    public class CompositeCondition : IObservableCondition
    {
        [SerializeField]
        private CombineMode mode = CombineMode.All;

        [OdinSerialize]
        [ListDrawerSettings(ShowFoldout = false)]
        private List<ICondition> conditions = new List<ICondition>();

        /// <summary>Creates an empty composite.</summary>
        public CompositeCondition()
        {
        }

        /// <summary>Creates a composite from code.</summary>
        public CompositeCondition(CombineMode mode, params ICondition[] conditions)
        {
            this.mode = mode;
            this.conditions = new List<ICondition>(conditions);
        }

        /// <inheritdoc />
        public bool IsMet() => Combine.Evaluate(conditions, mode, condition => condition.IsMet());

        /// <summary>Only notifies when every part can notify; otherwise it subscribes nothing.</summary>
        public bool TryAddChangedListener(Action listener)
        {
            foreach (ICondition condition in conditions)
            {
                if (!UnityNull.IsNull(condition) && !(condition is IObservableCondition))
                {
                    return false;
                }
            }

            var added = new List<IObservableCondition>();
            foreach (ICondition condition in conditions)
            {
                if (UnityNull.IsNull(condition))
                {
                    continue;
                }

                var observable = (IObservableCondition)condition;
                if (!observable.TryAddChangedListener(listener))
                {
                    foreach (IObservableCondition undo in added)
                    {
                        undo.RemoveChangedListener(listener);
                    }

                    return false;
                }

                added.Add(observable);
            }

            return true;
        }

        /// <inheritdoc />
        public void RemoveChangedListener(Action listener)
        {
            foreach (ICondition condition in conditions)
            {
                if (!UnityNull.IsNull(condition) && condition is IObservableCondition observable)
                {
                    observable.RemoveChangedListener(listener);
                }
            }
        }
    }

    /// <summary>Met when a bool value is true.</summary>
    public class BoolCondition : IObservableCondition
    {
        [SerializeField]
        [HideLabel]
        private VariableReference<bool> value = new VariableReference<bool>(true);

        /// <summary>Creates a condition that is always met.</summary>
        public BoolCondition()
        {
        }

        /// <summary>Creates a condition reading <paramref name="value"/>.</summary>
        public BoolCondition(VariableReference<bool> value)
        {
            this.value = value;
        }

        /// <inheritdoc />
        public bool IsMet() => value.Value;

        /// <inheritdoc />
        public bool TryAddChangedListener(Action listener) => value.TryAddChangedListener(listener);

        /// <inheritdoc />
        public void RemoveChangedListener(Action listener) => value.RemoveChangedListener(listener);
    }

    /// <summary>Met when a value passes a Filter. This bridges Filters (about a value) and Conditions (about the world).</summary>
    public class FilterCondition<T> : IObservableCondition
    {
        [SerializeField]
        private VariableReference<T> value = new VariableReference<T>();

        [OdinSerialize]
        [Required("A Filter Condition needs a Filter.")]
        private IFilter<T> filter;

        /// <summary>Creates an empty condition.</summary>
        public FilterCondition()
        {
        }

        /// <summary>Creates a condition from code.</summary>
        public FilterCondition(VariableReference<T> value, IFilter<T> filter)
        {
            this.value = value;
            this.filter = filter;
        }

        /// <summary>Throws when no Filter is assigned, so a mis-authored condition fails loudly.</summary>
        public bool IsMet()
        {
            if (filter == null)
            {
                throw new InvalidOperationException($"FilterCondition<{typeof(T).Name}> has no Filter assigned.");
            }

            return filter.Allows(value.Value);
        }

        /// <inheritdoc />
        public bool TryAddChangedListener(Action listener) => value.TryAddChangedListener(listener);

        /// <inheritdoc />
        public void RemoveChangedListener(Action listener) => value.RemoveChangedListener(listener);
    }
}
