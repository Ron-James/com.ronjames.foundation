using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace RonJames.Foundation.Values
{
    /// <summary>The two ways a <see cref="VariableReference{T}"/> can resolve its value.</summary>
    public enum ReferenceMode
    {
        /// <summary>The reference holds its own value, private to the object it is on.</summary>
        Value,

        /// <summary>The reference reads from (and, if allowed, writes to) a Value Source.</summary>
        Source
    }

    /// <summary>
    /// A field that resolves to a typed value, either held directly (Value mode) or taken from a
    /// Value Source such as a global Variable Event (Source mode).
    /// </summary>
    /// <remarks>
    /// The class is deliberately not <c>[Serializable]</c>: Odin must serialize it, because the
    /// source is an interface. Host it in an Odin-serialized object (a SerializedMonoBehaviour,
    /// SerializedScriptableObject, or a Command / Filter / Condition inside one).
    /// </remarks>
    [InlineProperty]
    [HideReferenceObjectPicker]
    public class VariableReference<T> : IWritableValueSource<T>
    {
        [HorizontalGroup("Row", Width = 64)]
        [HideLabel]
        [SerializeField]
        private ReferenceMode mode = ReferenceMode.Value;

        [HorizontalGroup("Row")]
        [HideLabel]
        [ShowIf(nameof(IsValueMode))]
        [SerializeField]
        private T value;

        [HorizontalGroup("Row")]
        [HideLabel]
        [ShowIf(nameof(IsSourceMode))]
        [InfoBox("No Value Source assigned (or it was destroyed). Reads return the default value.",
            InfoMessageType.Error, nameof(IsMissing))]
        [OdinSerialize]
        private IValueSource<T> source;

        [NonSerialized]
        private Action valueModeChanged;

        /// <summary>Creates a reference in Value mode holding <c>default</c>.</summary>
        public VariableReference()
        {
        }

        /// <summary>Creates a reference in Value mode holding <paramref name="initialValue"/>.</summary>
        public VariableReference(T initialValue)
        {
            value = initialValue;
        }

        /// <summary>Creates a reference in Source mode reading from <paramref name="valueSource"/>.</summary>
        public static VariableReference<T> FromSource(IValueSource<T> valueSource)
        {
            return new VariableReference<T> { mode = ReferenceMode.Source, source = valueSource };
        }

        /// <summary>Which mode the reference resolves its value with.</summary>
        public ReferenceMode Mode
        {
            get => mode;
            set => mode = value;
        }

        /// <summary>The Value Source used in Source mode.</summary>
        public IValueSource<T> Source
        {
            get => source;
            set => source = value;
        }

        /// <summary>True when the reference is in Source mode but has no usable source.</summary>
        public bool IsMissing => mode == ReferenceMode.Source && UnityNull.IsNull(source);

        /// <summary>True when writing through this reference is allowed.</summary>
        public bool CanWrite => mode == ReferenceMode.Value || (!IsMissing && source is IWritableValueSource<T>);

        /// <summary>True when this reference can tell you its value changed (see <see cref="TryAddChangedListener"/>).</summary>
        public bool CanNotify => mode == ReferenceMode.Value || (!IsMissing && source is IObservableValueSource);

        /// <summary>
        /// The resolved value. Reading a missing source returns <c>default</c>. Writing to a source that
        /// cannot be written throws <see cref="InvalidOperationException"/> instead of being ignored.
        /// </summary>
        public T Value
        {
            get
            {
                if (mode == ReferenceMode.Value)
                {
                    return value;
                }

                return UnityNull.IsNull(source) ? default : source.Value;
            }
            set => Write(value);
        }

        object IValueSource.Value => Value;

        /// <inheritdoc />
        public Type ValueType => typeof(T);

        void IWritableValueSource<T>.SetValue(T newValue) => Write(newValue);

        void IWritableValueSource.SetValue(object newValue) => Write(ValueConversion.To<T>(newValue, this));

        /// <summary>
        /// Subscribes to value changes. Returns false (and subscribes nothing) when the reference
        /// cannot notify, so callers know to poll instead.
        /// </summary>
        public bool TryAddChangedListener(Action listener)
        {
            if (listener == null)
            {
                return false;
            }

            if (mode == ReferenceMode.Value)
            {
                valueModeChanged -= listener;
                valueModeChanged += listener;
                return true;
            }

            if (!IsMissing && source is IObservableValueSource observable)
            {
                observable.ValueChanged -= listener;
                observable.ValueChanged += listener;
                return true;
            }

            return false;
        }

        /// <summary>Removes a listener added with <see cref="TryAddChangedListener"/>.</summary>
        public void RemoveChangedListener(Action listener)
        {
            if (listener == null)
            {
                return;
            }

            valueModeChanged -= listener;
            if (!UnityNull.IsNull(source) && source is IObservableValueSource observable)
            {
                observable.ValueChanged -= listener;
            }
        }

        private void Write(T newValue)
        {
            if (mode == ReferenceMode.Value)
            {
                value = newValue;
                valueModeChanged?.Invoke();
                return;
            }

            if (IsMissing)
            {
                throw new InvalidOperationException(
                    $"Cannot write to a VariableReference<{typeof(T).Name}>: it is in Source mode with no Value Source assigned.");
            }

            if (!(source is IWritableValueSource<T> writable))
            {
                throw new InvalidOperationException(
                    $"Cannot write to a VariableReference<{typeof(T).Name}>: its Value Source ({DescribeSource()}) is read-only.");
            }

            writable.SetValue(newValue);
        }

        private string DescribeSource()
        {
            return source is UnityEngine.Object unityObject ? unityObject.name : source.GetType().Name;
        }

        private bool IsValueMode() => mode == ReferenceMode.Value;

        private bool IsSourceMode() => mode == ReferenceMode.Source;

        /// <summary>Reads the reference like a plain variable. A null reference reads as <c>default</c>.</summary>
        public static implicit operator T(VariableReference<T> reference)
        {
            return reference == null ? default : reference.Value;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return mode == ReferenceMode.Value ? $"{value}" : $"{Value} (from {(IsMissing ? "missing source" : DescribeSource())})";
        }
    }
}
