using System;

namespace RonJames.Foundation.Values
{
    /// <summary>
    /// Anything that can provide a value: a Variable Event asset, a component, or a plain object.
    /// This is the untyped form; every typed <see cref="IValueSource{T}"/> is also one of these.
    /// </summary>
    public interface IValueSource
    {
        /// <summary>The current value as an object. For a typed source this is its typed value, boxed.</summary>
        object Value { get; }

        /// <summary>The type of value this source provides.</summary>
        Type ValueType { get; }
    }

    /// <summary>A Value Source that provides a value of type <typeparamref name="T"/>.</summary>
    public interface IValueSource<out T> : IValueSource
    {
        /// <summary>The current value.</summary>
        new T Value { get; }
    }

    /// <summary>An untyped Value Source that can also be written to.</summary>
    public interface IWritableValueSource : IValueSource
    {
        /// <summary>
        /// Writes an untyped value. It is converted to <see cref="IValueSource.ValueType"/> when the
        /// conversion is safe, otherwise an <see cref="InvalidCastException"/> is thrown.
        /// </summary>
        void SetValue(object value);
    }

    /// <summary>A typed Value Source that can also be written to.</summary>
    public interface IWritableValueSource<T> : IValueSource<T>, IWritableValueSource
    {
        /// <summary>Writes a new value.</summary>
        void SetValue(T value);
    }

    /// <summary>A Value Source that notifies when its value changes.</summary>
    public interface IObservableValueSource : IValueSource
    {
        /// <summary>Raised after the value has been written (or re-raised).</summary>
        event Action ValueChanged;
    }
}
