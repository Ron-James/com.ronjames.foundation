using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>Small helpers Unity's <see cref="Awaitable"/> doesn't provide (ADR 0004).</summary>
    public static class AwaitableUtility
    {
        /// <summary>An Awaitable that has already completed. Each call returns a new one; await it once.</summary>
        public static Awaitable Completed()
        {
            var source = new AwaitableCompletionSource();
            source.SetResult();
            return source.Awaitable;
        }
    }
}
