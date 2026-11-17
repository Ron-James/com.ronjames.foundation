using RonJames.Foundation.Events;
using UnityEngine;

/// <summary>A Variable Event holding a <c>IProduct</c>. Replaces the matching GushesheGear ActionSO.</summary>
[CreateAssetMenu(menuName = "RonJames Foundation/Variable Events/Product", fileName = "New Product Variable Event", order = 116)]
public class ProductVariableEvent : VariableEvent<IProduct>
{
}
