using System;
using UnityEditor;
using UnityEngine;

namespace GushesheGearMigration
{
    /// <summary>
    /// Tools > GushesheGear Migration: runs each step as a dry run (report only) or for real. Every
    /// run writes its report to Logs/ in the project folder. Work on a branch: there's no undo across
    /// prefabs and scenes, so git is the undo.
    /// </summary>
    public sealed class MigrationWindow : EditorWindow
    {
        private bool removeOldListeners = true;
        private bool convertEvenWithOwnMembers;
        private string lastReport = "";
        private Vector2 scroll;

        [MenuItem("Tools/GushesheGear Migration/Open Migration Window")]
        private static void Open() => GetWindow<MigrationWindow>("GushesheGear Migration").minSize = new Vector2(520, 420);

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Order: 1) apply Bridge/bridge.patch and copy Runtime/ into the project. 2) Listeners. " +
                "3) Marked fields ([MigrateFrom]). 4) Assets. Always dry-run first and read the report. Commit before each real run.",
                MessageType.Info);

            Section("2. Listener components", "Replaces safe old listeners with Event Listener / Variable Event Listener components.");
            removeOldListeners = EditorGUILayout.ToggleLeft("Remove the old components (off: disable them instead)", removeOldListeners);
            Buttons(() => ListenerMigrator.Run(true, removeOldListeners), () => ListenerMigrator.Run(false, removeOldListeners), "ListenerMigration");

            Section("3. Marked fields", "Copies old fields into new fields marked [MigrateFrom(\"oldField\")].");
            Buttons(() => FieldMigrator.Run(true), () => FieldMigrator.Run(false), "FieldMigration");

            Section("4. Action assets", "Converts ActionSO assets into EventAsset / VariableEvent assets in place (GUIDs kept).");
            convertEvenWithOwnMembers = EditorGUILayout.ToggleLeft("Convert even if the old class declares its own members", convertEvenWithOwnMembers);
            Buttons(() => ActionAssetConverter.Run(true, convertEvenWithOwnMembers), () => ActionAssetConverter.Run(false, convertEvenWithOwnMembers), "AssetConversion");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title, string description)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        }

        private void Buttons(Func<MigrationReport> dryRun, Func<MigrationReport> run, string logName)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dry run"))
            {
                Execute(dryRun, logName + "-DryRun.txt");
            }

            if (GUILayout.Button("Migrate") &&
                EditorUtility.DisplayDialog("Run migration", "This changes prefabs, scenes or assets. Commit first so you can revert. Continue?", "Migrate", "Cancel"))
            {
                Execute(run, logName + ".txt");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void Execute(Func<MigrationReport> action, string logName)
        {
            try
            {
                MigrationReport report = action();
                string path = report.Save(logName);
                lastReport = report.Build();
                Debug.Log($"[GushesheGear Migration] Report written to {path}");
            }
            catch (OperationCanceledException cancelled)
            {
                lastReport = cancelled.Message;
            }
        }
    }

    /// <summary>
    /// Batch-mode entry points, e.g.
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod GushesheGearMigration.Batch.ListenersDryRun</c>.
    /// Reports go to Logs/.
    /// </summary>
    public static class Batch
    {
        public static void ListenersDryRun() => Save(ListenerMigrator.Run(true, true), "ListenerMigration-DryRun.txt");

        public static void Listeners() => Save(ListenerMigrator.Run(false, true), "ListenerMigration.txt");

        public static void VerifyListeners() => Save(ListenerMigrator.Verify(), "ListenerMigration-Check.txt");

        public static void FieldsDryRun() => Save(FieldMigrator.Run(true), "FieldMigration-DryRun.txt");

        public static void Fields() => Save(FieldMigrator.Run(false), "FieldMigration.txt");

        public static void AssetsDryRun() => Save(ActionAssetConverter.Run(true, false), "AssetConversion-DryRun.txt");

        public static void Assets() => Save(ActionAssetConverter.Run(false, false), "AssetConversion.txt");

        private static void Save(MigrationReport report, string fileName)
        {
            string path = report.Save(fileName);
            Debug.Log($"[GushesheGear Migration] Report written to {path} ({report.Errors} errors)");
        }
    }
}
