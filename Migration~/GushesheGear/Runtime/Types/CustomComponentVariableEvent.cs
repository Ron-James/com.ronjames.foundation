using RonJames.Foundation.Events;
using UnityEngine;

/// <summary>A Variable Event holding a <c>ICustomComponent</c>. Replaces the matching GushesheGear ActionSO.</summary>
[CreateAssetMenu(menuName = "RonJames Foundation/Variable Events/CustomComponent", fileName = "New CustomComponent Variable Event", order = 108)]
public class CustomComponentVariableEvent : VariableEvent<ICustomComponent>
{
}
