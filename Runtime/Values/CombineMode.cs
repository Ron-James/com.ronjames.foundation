using System;
using System.Collections.Generic;

namespace RonJames.Foundation.Values
{
    /// <summary>How a composite Filter or Condition combines its parts.</summary>
    public enum CombineMode
    {
        /// <summary>Passes when every part passes. Passes when empty.</summary>
        All,

        /// <summary>Passes when at least one part passes. Fails when empty.</summary>
        Any,

        /// <summary>Passes when no part passes. Passes when empty. Use this to invert.</summary>
        None
    }

    /// <summary>Evaluates a list of yes/no tests with a <see cref="CombineMode"/>.</summary>
    public static class Combine
    {
        /// <summary>
        /// Combines <paramref name="test"/> over <paramref name="parts"/>. Null parts are ignored, as if
        /// they were not in the list. Evaluation short-circuits.
        /// </summary>
        public static bool Evaluate<TPart>(IReadOnlyList<TPart> parts, CombineMode mode, Func<TPart, bool> test)
        {
            if (parts != null)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    TPart part = parts[i];
                    if (UnityNull.IsNull(part))
                    {
                        continue;
                    }

                    bool passed = test(part);
                    switch (mode)
                    {
                        case CombineMode.All when !passed:
                            return false;
                        case CombineMode.Any when passed:
                            return true;
                        case CombineMode.None when passed:
                            return false;
                    }
                }
            }

            return mode != CombineMode.Any;
        }
    }
}
