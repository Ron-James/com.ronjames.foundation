using RonJames.Foundation.Commands;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace RonJames.Foundation.Commands.Editor
{
    /// <summary>
    /// Draws a Run as a live tree: one line per Command with its status, elapsed time and reported
    /// progress, nested under its Composites. This replaces reading state off each Command (ADR 0003).
    /// </summary>
    public sealed class CommandRunDrawer : OdinValueDrawer<CommandRun>
    {
        private const float LineHeight = 18f;
        private const float Indent = 14f;

        /// <inheritdoc />
        protected override void DrawPropertyLayout(GUIContent label)
        {
            CommandRun run = ValueEntry.SmartValue;
            if (run == null)
            {
                return;
            }

            SirenixEditorGUI.BeginBox();
            SirenixEditorGUI.BeginBoxHeader();
            GUILayout.Label($"Run #{run.Number}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            DrawStatusLabel(StatusOf(run.Status), $"{run.Status}  {run.Root.Elapsed:0.00}s");
            SirenixEditorGUI.EndBoxHeader();

            foreach (RunNode child in run.Root.Children)
            {
                DrawNode(child);
            }

            if (run.Root.Children.Count == 0)
            {
                GUILayout.Label("No Commands.", SirenixGUIStyles.CenteredGreyMiniLabel);
            }

            SirenixEditorGUI.EndBox();

            if (Application.isPlaying && run.IsRunning)
            {
                GUIHelper.RequestRepaint();
            }
        }

        private static void DrawNode(RunNode node)
        {
            Rect line = EditorGUILayout.GetControlRect(false, LineHeight);
            line.xMin += (node.Depth - 1) * Indent;

            Rect dot = new Rect(line.x, line.y + 5f, 8f, 8f);
            EditorGUI.DrawRect(dot, ColorOf(node.Status));

            Rect nameRect = new Rect(dot.xMax + 6f, line.y, line.width * 0.5f, line.height);
            GUI.Label(nameRect, node.Name, node.Status == CommandStatus.Running ? EditorStyles.boldLabel : EditorStyles.label);

            Rect right = new Rect(nameRect.xMax, line.y + 1f, line.xMax - nameRect.xMax, line.height - 2f);
            if (node.Status == CommandStatus.Running && node.Progress >= 0f)
            {
                EditorGUI.ProgressBar(right, node.Progress, node.ProgressLabel ?? $"{node.Progress:P0}");
            }
            else
            {
                string text = node.Status == CommandStatus.Pending || node.Status == CommandStatus.Disabled
                    ? node.Status.ToString()
                    : $"{node.Status}  {node.Elapsed:0.00}s";
                GUI.Label(right, text, SirenixGUIStyles.RightAlignedGreyMiniLabel);
            }

            if (node.Status == CommandStatus.Failed && node.Exception != null)
            {
                Rect error = EditorGUILayout.GetControlRect(false, LineHeight);
                error.xMin += node.Depth * Indent + 14f;
                GUI.Label(error, node.Exception.Message, SirenixGUIStyles.RichTextLabel);
            }

            foreach (RunNode child in node.Children)
            {
                DrawNode(child);
            }
        }

        private static void DrawStatusLabel(CommandStatus status, string text)
        {
            Color previous = GUI.color;
            GUI.color = ColorOf(status);
            GUILayout.Label(text, EditorStyles.miniBoldLabel);
            GUI.color = previous;
        }

        private static CommandStatus StatusOf(RunStatus status)
        {
            switch (status)
            {
                case RunStatus.Completed: return CommandStatus.Completed;
                case RunStatus.Stopped: return CommandStatus.Stopped;
                case RunStatus.Failed: return CommandStatus.Failed;
                default: return CommandStatus.Running;
            }
        }

        internal static Color ColorOf(CommandStatus status)
        {
            switch (status)
            {
                case CommandStatus.Running: return new Color(0.30f, 0.65f, 1.00f);
                case CommandStatus.Completed: return new Color(0.35f, 0.80f, 0.40f);
                case CommandStatus.Skipped: return new Color(0.95f, 0.75f, 0.25f);
                case CommandStatus.Stopped: return new Color(0.90f, 0.55f, 0.20f);
                case CommandStatus.Failed: return new Color(0.95f, 0.30f, 0.30f);
                case CommandStatus.Disabled: return new Color(0.45f, 0.45f, 0.45f);
                default: return new Color(0.60f, 0.60f, 0.60f);
            }
        }
    }
}
