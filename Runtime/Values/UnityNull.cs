namespace RonJames.Foundation.Values
{
    /// <summary>Null checks that also treat destroyed ("missing") Unity objects as null.</summary>
    public static class UnityNull
    {
        /// <summary>
        /// True when <paramref name="value"/> is null, or is a Unity object that has been destroyed.
        /// A plain <c>== null</c> on an interface or <c>object</c> misses the destroyed case.
        /// </summary>
        public static bool IsNull(object value)
        {
            if (value == null)
            {
                return true;
            }

            return value is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
