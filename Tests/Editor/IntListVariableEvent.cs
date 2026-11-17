using System.Collections.Generic;
using RonJames.Foundation.Events;

namespace RonJames.Foundation.Tests
{
    /// <summary>A reference-type Variable Event, to test that the Runtime Value is a copy of the Initial Value.</summary>
    public class IntListVariableEvent : VariableEvent<List<int>>
    {
    }
}
