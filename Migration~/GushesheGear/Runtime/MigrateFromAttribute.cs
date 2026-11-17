using System;

/// <summary>
/// Marks a new field that should be filled from an old field when you run
/// <i>Tools > GushesheGear Migration > Migrate Marked Fields</i>. Put it on the new field, name the
/// old field, run the tool, then delete the old field and this attribute.
/// </summary>
/// <remarks>
/// Supported copies:
/// <list type="bullet">
/// <item><c>VariableReference&lt;T&gt;</c> from <c>ActionReference&lt;T&gt;</c>, <c>ValueAssetReference&lt;T&gt;</c> or any <c>ValueReference&lt;T, TAsset&gt;</c>.</item>
/// <item><c>VariableReference&lt;T&gt;</c> from a plain <c>T</c> field (becomes Value mode).</item>
/// <item><c>IEvent</c> / <c>IVariableEvent&lt;T&gt;</c> (or a list / array of them) from a <c>BaseActionSO</c> / <c>ActionSO&lt;T&gt;</c> field,
/// an <c>ActionSubscription</c> / <c>MessageSubscription&lt;T&gt;</c>, an <c>ActionSubscriptionSet</c> / <c>MessageSubscriptionSet&lt;T&gt;</c>,
/// or a list / array of any of those.</item>
/// </list>
/// The class holding the new field must be Odin-serialized (SerializedMonoBehaviour /
/// SerializedScriptableObject), and the new field must be [OdinSerialize].
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class MigrateFromAttribute : Attribute
{
    public MigrateFromAttribute(string oldFieldName)
    {
        OldFieldName = oldFieldName;
    }

    public string OldFieldName { get; }
}
