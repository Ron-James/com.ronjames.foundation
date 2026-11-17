using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RonJames.Foundation.Events.Editor
{
    /// <summary>
    /// Writes the three one-line classes a new value type needs: a Variable Event, a Variable Event
    /// Listener and a Set Variable command. Unity can't create assets or components of open generic
    /// types, so these subclasses are required; this window removes the boilerplate.
    /// </summary>
    public sealed class VariableEventTypeGenerator : EditorWindow
    {
        private string typeName = "";
        private string prefix = "";
        private string extraUsing = "";
        private string targetNamespace = "";
        private string folder = "Assets";
        private string status = "";

        [MenuItem("Assets/Create/RonJames Foundation/Variable Event Type…", priority = 30)]
        [MenuItem("Tools/RonJames Foundation/Variable Event Type Generator")]
        private static void Open()
        {
            var window = GetWindow<VariableEventTypeGenerator>(true, "Variable Event Type", true);
            window.folder = SelectedFolder();
            window.minSize = new Vector2(420, 250);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Generates {Name}VariableEvent, {Name}VariableEventListener and Set{Name}VariableCommand for a value type of your own.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            typeName = EditorGUILayout.TextField(new GUIContent("Value type", "The C# type as you'd write it, e.g. IOption, Item, List<Item>."), typeName);
            if (EditorGUI.EndChangeCheck())
            {
                prefix = SuggestPrefix(typeName);
                Type resolved = Resolve(typeName);
                if (resolved != null && string.IsNullOrEmpty(extraUsing))
                {
                    extraUsing = resolved.Namespace ?? "";
                }
            }

            prefix = EditorGUILayout.TextField(new GUIContent("Name", "Used in the class names."), prefix);
            extraUsing = EditorGUILayout.TextField(new GUIContent("Type's namespace", "Added as a using, if the type lives in a namespace."), extraUsing);
            targetNamespace = EditorGUILayout.TextField(new GUIContent("Generated namespace", "Optional."), targetNamespace);

            EditorGUILayout.BeginHorizontal();
            folder = EditorGUILayout.TextField("Folder", folder);
            if (GUILayout.Button("…", GUILayout.Width(28)))
            {
                string picked = EditorUtility.OpenFolderPanel("Folder for generated scripts", folder, "");
                if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
                {
                    folder = "Assets" + picked.Substring(Application.dataPath.Length);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(typeName) && Resolve(typeName) == null)
            {
                EditorGUILayout.HelpBox($"Couldn't find a type named '{typeName}'. That's fine if it's generic or not compiled yet; check the namespace.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!IsValidIdentifier(prefix) || string.IsNullOrWhiteSpace(typeName)))
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28)))
                {
                    Generate();
                }
            }

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
        }

        private void Generate()
        {
            string type = typeName.Trim();
            string usings = "using RonJames.Foundation.Commands;\nusing RonJames.Foundation.Events;\nusing UnityEngine;\n" +
                            (string.IsNullOrWhiteSpace(extraUsing) ? "" : $"using {extraUsing.Trim()};\n");

            var files = new (string File, string Body)[]
            {
                ($"{prefix}VariableEvent.cs",
                    $"[CreateAssetMenu(menuName = \"RonJames Foundation/Variable Events/{prefix}\", fileName = \"New {prefix} Variable Event\")]\n" +
                    $"public class {prefix}VariableEvent : VariableEvent<{type}>\n{{\n}}\n"),
                ($"{prefix}VariableEventListener.cs",
                    $"[AddComponentMenu(\"RonJames Foundation/Variable Event Listeners/{prefix} Variable Event Listener\")]\n" +
                    $"public class {prefix}VariableEventListener : VariableEventListener<{type}>\n{{\n}}\n"),
                ($"Set{prefix}VariableCommand.cs",
                    $"public class Set{prefix}VariableCommand : SetVariableCommand<{type}>\n{{\n}}\n")
            };

            Directory.CreateDirectory(folder);
            var written = new System.Collections.Generic.List<string>();
            foreach ((string file, string body) in files)
            {
                string path = Path.Combine(folder, file).Replace('\\', '/');
                if (File.Exists(path))
                {
                    status = $"Nothing written: {path} already exists.";
                    return;
                }

                written.Add(path);
            }

            for (int i = 0; i < files.Length; i++)
            {
                File.WriteAllText(written[i], usings + "\n" + Wrap(files[i].Body));
            }

            AssetDatabase.Refresh();
            status = "Wrote:\n" + string.Join("\n", written);
        }

        private string Wrap(string body)
        {
            if (string.IsNullOrWhiteSpace(targetNamespace))
            {
                return body;
            }

            string indented = string.Join("\n", body.TrimEnd('\n').Split('\n').Select(line => line.Length > 0 ? "    " + line : line));
            return $"namespace {targetNamespace.Trim()}\n{{\n{indented}\n}}\n";
        }

        private static string SuggestPrefix(string type)
        {
            string cleaned = Regex.Replace(type ?? "", @"[^A-Za-z0-9]", " ");
            string[] words = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string joined = string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
            if (joined.Length > 1 && joined[0] == 'I' && char.IsUpper(joined[1]))
            {
                joined = joined.Substring(1);
            }

            return joined;
        }

        private static bool IsValidIdentifier(string value) => !string.IsNullOrEmpty(value) && Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$");

        private static Type Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            name = name.Trim();
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .FirstOrDefault(type => type.FullName == name || type.Name == name);
        }

        private static string SelectedFolder()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                return "Assets";
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        }
    }
}
