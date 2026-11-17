using RonJames.Foundation.Commands;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEngine;

namespace RonJames.Foundation.Commands.Editor
{
    /// <summary>Draws Disabled Commands faded so they read as switched off at a glance.</summary>
    public sealed class CommandDrawer<T> : OdinValueDrawer<T> where T : Command
    {
        private static readonly Color Faded = new Color(1f, 1f, 1f, 0.45f);

        /// <inheritdoc />
        protected override void DrawPropertyLayout(GUIContent label)
        {
            T command = ValueEntry.SmartValue;
            bool disabled = command != null && !command.Enabled;
            if (disabled)
            {
                GUIHelper.PushColor(Faded);
            }

            CallNextDrawer(label);

            if (disabled)
            {
                GUIHelper.PopColor();
            }
        }
    }
}
