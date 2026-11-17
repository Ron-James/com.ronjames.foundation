using System;
using System.Collections.Generic;
using System.Globalization;

namespace RonJames.Foundation.Values
{
    /// <summary>Converts untyped values written into typed Value Sources.</summary>
    public static class ValueConversion
    {
        /// <summary>
        /// Converts <paramref name="value"/> to <typeparamref name="T"/>. Values that already are a
        /// <typeparamref name="T"/> pass straight through; null (or a destroyed Unity object) becomes
        /// <c>default</c> for reference and nullable types; primitive numbers, bools, strings and enums
        /// are converted with the invariant culture. Anything else throws, so a bad write is never
        /// silently ignored.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="target">What is being written to, used only in the error message.</param>
        public static T To<T>(object value, object target = null)
        {
            if (value is T typed)
            {
                return typed;
            }

            Type targetType = typeof(T);
            if (UnityNull.IsNull(value))
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                {
                    return default;
                }

                throw new InvalidCastException(
                    $"Cannot write null into a {targetType.Name} {Describe(target)}: {targetType.Name} is a value type.");
            }

            Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            try
            {
                if (underlying.IsEnum)
                {
                    if (value is string text)
                    {
                        return (T)Enum.Parse(underlying, text, ignoreCase: true);
                    }

                    if (IsIntegral(value.GetType()))
                    {
                        return (T)Enum.ToObject(underlying, value);
                    }
                }
                else if (value is IConvertible && IsConvertibleTarget(underlying))
                {
                    return (T)Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException ||
                                              exception is ArgumentException || exception is InvalidCastException)
            {
                throw new InvalidCastException(
                    $"Cannot convert '{value}' ({value.GetType().Name}) to {targetType.Name} {Describe(target)}.",
                    exception);
            }

            throw new InvalidCastException(
                $"Cannot write a {value.GetType().Name} into a {targetType.Name} {Describe(target)}.");
        }

        private static readonly HashSet<Type> ConvertibleTargets = new HashSet<Type>
        {
            typeof(bool), typeof(char), typeof(string),
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        };

        private static bool IsConvertibleTarget(Type type) => ConvertibleTargets.Contains(type);

        private static bool IsIntegral(Type type) =>
            type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);

        private static string Describe(object target)
        {
            if (target is UnityEngine.Object unityObject && unityObject != null)
            {
                return $"source '{unityObject.name}'";
            }

            return target == null ? "source" : $"source ({target.GetType().Name})";
        }
    }
}
