using RonJames.Foundation.Events;
using UnityEngine;

/// <summary>A Variable Event holding a <c>LevelState</c>. Replaces the matching GushesheGear ActionSO.</summary>
[CreateAssetMenu(menuName = "RonJames Foundation/Variable Events/Level", fileName = "New Level Variable Event", order = 113)]
public class LevelVariableEvent : VariableEvent<LevelState>
{
}
