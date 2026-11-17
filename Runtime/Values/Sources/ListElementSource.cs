using System;
using System.Collections.Generic;
using UnityEngine;

namespace RonJames.Foundation.Values
{
    /// <summary>
    /// A Value Source that is the element of a list at an index. Both the list and the index are
    /// Variable References, so either can come from a Variable Event.
    /// </summary>
    public class ListElementSource<T> : IWritableValueSource<T>
    {
        [SerializeField]
        private VariableReference<List<T>> list = new VariableReference<List<T>>();

        [SerializeField]
        private VariableReference<int> index = new VariableReference<int>();

        /// <summary>The element, or <c>default</c> when the list is missing or the index is out of range.</summary>
        public T Value
        {
            get
            {
                List<T> items = list.Value;
                int i = index.Value;
                return items != null && i >= 0 && i < items.Count ? items[i] : default;
            }
        }

        object IValueSource.Value => Value;

        /// <inheritdoc />
        public Type ValueType => typeof(T);

        /// <summary>Replaces the element. Throws when the list is missing or the index is out of range.</summary>
        public void SetValue(T value)
        {
            List<T> items = list.Value;
            int i = index.Value;
            if (items == null)
            {
                throw new InvalidOperationException("Cannot write a list element: the list is missing.");
            }

            if (i < 0 || i >= items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), i,
                    $"Cannot write a list element: index {i} is outside a list of {items.Count}.");
            }

            items[i] = value;
        }

        void IWritableValueSource.SetValue(object value) => SetValue(ValueConversion.To<T>(value, this));
    }
}
