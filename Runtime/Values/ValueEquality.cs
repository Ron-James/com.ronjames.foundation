using System.Collections.Generic;

namespace RonJames.Foundation.Values
{
    /// <summary>Equality that treats destroyed ("missing") Unity objects as null.</summary>
    public static class ValueEquality
    {
        /// <summary>True when both are null-or-missing, or both are present and equal.</summary>
        public static bool AreEqual<T>(T a, T b)
        {
            bool aNull = UnityNull.IsNull(a);
            bool bNull = UnityNull.IsNull(b);
            if (aNull || bNull)
            {
                return aNull && bNull;
            }

            return EqualityComparer<T>.Default.Equals(a, b);
        }
    }
}
