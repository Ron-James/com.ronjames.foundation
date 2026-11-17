using RonJames.Foundation.Events;
using UnityEngine;

/// <summary>A Variable Event holding a <c>CollisionData</c>. Replaces the matching GushesheGear ActionSO.</summary>
[CreateAssetMenu(menuName = "RonJames Foundation/Variable Events/Collision", fileName = "New Collision Variable Event", order = 110)]
public class CollisionVariableEvent : VariableEvent<CollisionData>
{
}
