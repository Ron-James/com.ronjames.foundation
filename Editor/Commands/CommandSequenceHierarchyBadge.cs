using RonJames.Foundation.Commands;
using UnityEditor;
using UnityEngine;

namespace RonJames.Foundation.Commands.Editor
{
    /// <summary>
    /// Marks GameObjects that have a Command Sequence in the Hierarchy. The badge is grey when idle,
    /// blue while running, and green / orange / red for how the last Run ended.
    /// </summary>
    [InitializeOnLoad]
    internal static class CommandSequenceHierarchyBadge
    {
        private static GUIStyle style;

        static CommandSequenceHierarchyBadge()
        {
            EditorApplication.hierarchyWindowItemOnGUI += Draw;
            EditorApplication.update += RepaintWhilePlaying;
        }

        private static void Draw(int instanceId, Rect rect)
        {
            // Unity 6.3 marks InstanceIDToObject obsolete in favour of EntityIdToObject, which doesn't
            // exist in 6000.0 (the package minimum), and the hierarchy callback still passes an int.
#pragma warning disable CS0618
            if (!(EditorUtility.InstanceIDToObject(instanceId) is GameObject gameObject))
#pragma warning restore CS0618
            {
                return;
            }

            if (!gameObject.TryGetComponent(out CommandSequence sequence))
            {
                return;
            }

            style ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };

            Color color = new Color(0.6f, 0.6f, 0.6f);
            if (sequence.IsRunning)
            {
                color = CommandRunDrawer.ColorOf(CommandStatus.Running);
            }
            else if (sequence.LastRun != null)
            {
                switch (sequence.LastRun.Status)
                {
                    case RunStatus.Completed:
                        color = CommandRunDrawer.ColorOf(CommandStatus.Completed);
                        break;
                    case RunStatus.Stopped:
                        color = CommandRunDrawer.ColorOf(CommandStatus.Stopped);
                        break;
                    case RunStatus.Failed:
                        color = CommandRunDrawer.ColorOf(CommandStatus.Failed);
                        break;
                }
            }

            Rect badge = new Rect(rect.xMax - 18f, rect.y, 18f, rect.height);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(badge, new GUIContent("▶", "Command Sequence"), style);
            GUI.color = previous;
        }

        private static double nextRepaint;

        // Keeps badge colours live during play mode without repainting every frame.
        private static void RepaintWhilePlaying()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextRepaint)
            {
                return;
            }

            nextRepaint = EditorApplication.timeSinceStartup + 0.25;
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
