using UnityEngine;

namespace RonJames.Foundation.Commands
{
    // Concrete Set Variable commands for the built-in value types, so they are easy to find in the
    // Command picker. The generic SetVariableCommand<T> works for any other type.

    /// <summary>Sets a <c>bool</c> Value Source.</summary>
    public class SetBoolVariableCommand : SetVariableCommand<bool>
    {
    }

    /// <summary>Sets a <c>int</c> Value Source.</summary>
    public class SetIntVariableCommand : SetVariableCommand<int>
    {
    }

    /// <summary>Sets a <c>float</c> Value Source.</summary>
    public class SetFloatVariableCommand : SetVariableCommand<float>
    {
    }

    /// <summary>Sets a <c>string</c> Value Source.</summary>
    public class SetStringVariableCommand : SetVariableCommand<string>
    {
    }

    /// <summary>Sets a <c>Vector2</c> Value Source.</summary>
    public class SetVector2VariableCommand : SetVariableCommand<Vector2>
    {
    }

    /// <summary>Sets a <c>Vector3</c> Value Source.</summary>
    public class SetVector3VariableCommand : SetVariableCommand<Vector3>
    {
    }

    /// <summary>Sets a <c>Quaternion</c> Value Source.</summary>
    public class SetQuaternionVariableCommand : SetVariableCommand<Quaternion>
    {
    }

    /// <summary>Sets a <c>Color</c> Value Source.</summary>
    public class SetColorVariableCommand : SetVariableCommand<Color>
    {
    }

    /// <summary>Sets a <c>GameObject</c> Value Source.</summary>
    public class SetGameObjectVariableCommand : SetVariableCommand<GameObject>
    {
    }

    /// <summary>Sets a <c>Transform</c> Value Source.</summary>
    public class SetTransformVariableCommand : SetVariableCommand<Transform>
    {
    }
}
