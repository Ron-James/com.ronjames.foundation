using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace RonJames.Foundation.Values
{
    /// <summary>A yes/no test applied to a value.</summary>
    public interface IFilter<in T>
    {
        /// <summary>True when <paramref name="value"/> passes.</summary>
        bool Allows(T value);
    }

    /// <summary>Combines several Filters with All / Any / None. Wrap a Filter in None to invert it.</summary>
    public class CompositeFilter<T> : IFilter<T>
    {
        [SerializeField]
        private CombineMode mode = CombineMode.All;

        [OdinSerialize]
        [ListDrawerSettings(ShowFoldout = false)]
        private List<IFilter<T>> filters = new List<IFilter<T>>();

        /// <summary>Creates an empty composite.</summary>
        public CompositeFilter()
        {
        }

        /// <summary>Creates a composite from code.</summary>
        public CompositeFilter(CombineMode mode, params IFilter<T>[] filters)
        {
            this.mode = mode;
            this.filters = new List<IFilter<T>>(filters);
        }

        /// <inheritdoc />
        public bool Allows(T value) => Combine.Evaluate(filters, mode, filter => filter.Allows(value));
    }

    /// <summary>Passes when the value equals another value. Destroyed Unity objects count as null.</summary>
    public class EqualityFilter<T> : IFilter<T>
    {
        [SerializeField]
        [LabelText("Equals")]
        private VariableReference<T> compareTo = new VariableReference<T>();

        /// <summary>Creates a filter comparing against <c>default</c>.</summary>
        public EqualityFilter()
        {
        }

        /// <summary>Creates a filter comparing against <paramref name="compareTo"/>.</summary>
        public EqualityFilter(VariableReference<T> compareTo)
        {
            this.compareTo = compareTo;
        }

        /// <inheritdoc />
        public bool Allows(T value) => ValueEquality.AreEqual(value, compareTo.Value);
    }

    /// <summary>How a <see cref="ComparisonFilter{T}"/> compares.</summary>
    public enum Comparison
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterOrEqual,
        LessOrEqual
    }

    /// <summary>
    /// Compares the value against another value. For <c>float</c> and <c>double</c>, Equal and NotEqual
    /// use a small tolerance; every other type compares exactly.
    /// </summary>
    public class ComparisonFilter<T> : IFilter<T> where T : IComparable<T>
    {
        [SerializeField]
        [HorizontalGroup("Row", Width = 110)]
        [HideLabel]
        private Comparison comparison = Comparison.Equal;

        [SerializeField]
        [HorizontalGroup("Row")]
        [HideLabel]
        private VariableReference<T> compareTo = new VariableReference<T>();

        [SerializeField]
        [ShowIf(nameof(ShowTolerance))]
        [MinValue(0)]
        private double tolerance = 0.0001;

        /// <summary>Creates a filter for "equal to default".</summary>
        public ComparisonFilter()
        {
        }

        /// <summary>Creates a filter from code.</summary>
        public ComparisonFilter(Comparison comparison, T compareTo)
        {
            this.comparison = comparison;
            this.compareTo = new VariableReference<T>(compareTo);
        }

        /// <inheritdoc />
        public bool Allows(T value)
        {
            T other = compareTo.Value;
            if (IsFloatingPoint() && (comparison == Comparison.Equal || comparison == Comparison.NotEqual))
            {
                bool near = Math.Abs(Convert.ToDouble(value) - Convert.ToDouble(other)) <= tolerance;
                return comparison == Comparison.Equal ? near : !near;
            }

            int order = value == null ? (other == null ? 0 : -1) : value.CompareTo(other);
            switch (comparison)
            {
                case Comparison.Equal: return order == 0;
                case Comparison.NotEqual: return order != 0;
                case Comparison.GreaterThan: return order > 0;
                case Comparison.LessThan: return order < 0;
                case Comparison.GreaterOrEqual: return order >= 0;
                case Comparison.LessOrEqual: return order <= 0;
                default: return false;
            }
        }

        private static bool IsFloatingPoint() => typeof(T) == typeof(float) || typeof(T) == typeof(double);

        private bool ShowTolerance() => IsFloatingPoint();
    }

    /// <summary>Passes when the value is an instance of any of the selected types. Null never passes.</summary>
    public class TypeFilter<T> : IFilter<T>
    {
        [OdinSerialize]
        [ValueDropdown(nameof(GetTypeOptions), IsUniqueList = true, DrawDropdownForListElements = true)]
        private List<Type> types = new List<Type>();

        /// <summary>Creates an empty filter (nothing passes).</summary>
        public TypeFilter()
        {
        }

        /// <summary>Creates a filter from code.</summary>
        public TypeFilter(params Type[] types)
        {
            this.types = new List<Type>(types);
        }

        /// <inheritdoc />
        public bool Allows(T value)
        {
            if (UnityNull.IsNull(value) || types == null)
            {
                return false;
            }

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != null && types[i].IsInstanceOfType(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<ValueDropdownItem<Type>> GetTypeOptions()
        {
#if UNITY_EDITOR
            IEnumerable<Type> candidates = UnityEditor.TypeCache.GetTypesDerivedFrom<T>();
            if (!typeof(T).IsAbstract && !typeof(T).IsInterface)
            {
                candidates = candidates.Prepend(typeof(T));
            }

            return candidates
                .Where(type => !type.ContainsGenericParameters)
                .OrderBy(type => type.Name)
                .Select(type => new ValueDropdownItem<Type>($"{type.Name}  ({type.Namespace})", type));
#else
            return Enumerable.Empty<ValueDropdownItem<Type>>();
#endif
        }
    }

    /// <summary>Passes when the value is null, including a Unity object that has been destroyed ("missing").</summary>
    public class NullFilter<T> : IFilter<T>
    {
        /// <inheritdoc />
        public bool Allows(T value) => UnityNull.IsNull(value);
    }
}
