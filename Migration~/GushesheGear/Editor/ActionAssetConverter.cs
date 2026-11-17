using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RonJames.Foundation.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GushesheGearMigration
{
    /// <summary>
    /// Converts ActionSO assets into package assets in place: VoidActionSo becomes EventAsset, and
    /// ActionSO&lt;T&gt; becomes the VariableEvent&lt;T&gt; for the same T, with the old default value
    /// as its Initial Value. The asset's GUID is kept, so references through package interfaces
    /// (Listeners, Variable References, Commands, [OdinSerialize] IEvent fields) keep working.
    /// </summary>
    /// <remarks>
    /// An asset is NOT converted, and is reported, when:
    /// <list type="bullet">
    /// <item>its class declares members of its own (e.g. IntActionSO.AddAmount, ProductActionSO's dealer); port those first, or tick "convert anyway";</item>
    /// <item>no VariableEvent&lt;T&gt; exists for its value type (copy Runtime/Types into the project);</item>
    /// <item>something that depends on it still has a field of an old-system type (an ActionSO / BaseActionSO
    /// field, ActionReference, ActionSubscription, MessageSubscription…). That field would lose the reference after
    /// conversion, so migrate the field first (Listener Migrator / [MigrateFrom]).</item>
    /// </list>
    /// </remarks>
    public static class ActionAssetConverter
    {
        private static readonly string[] OldSystemTypeNames =
        {
            "BaseActionSO", "VoidActionSo", "ActionSO`1", "ActionReference`1", "ValueReference`2", "ActionSubscription",
            "MessageSubscription`1", "ActionSubscriptionSet", "MessageSubscriptionSet`1", "BaseActionListener", "ActionListener`1"
        };

        public static MigrationReport Run(bool dryRun, bool convertEvenWithOwnMembers)
        {
            var report = new MigrationReport("Action asset conversion", dryRun);
            Type baseAction = MigrationCommon.FindType("BaseActionSO");
            if (baseAction == null)
            {
                report.Error("BaseActionSO not found: nothing to convert (or the old system is already deleted).");
                return report;
            }

            List<string> assetPaths = AssetDatabase.FindAssets("t:" + baseAction.Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".asset"))
                .Distinct()
                .OrderBy(path => path)
                .ToList();
            Dictionary<string, List<string>> dependents = ReverseDependencies(assetPaths);

            foreach (string path in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null || !baseAction.IsInstanceOfType(asset))
                {
                    continue;
                }

                Type oldType = asset.GetType();
                Type valueType = MigrationCommon.GenericArgumentOf(oldType, "ActionSO`1");
                Type newType = valueType == null ? typeof(EventAsset) : FindVariableEventType(valueType);
                string label = $"{path} ({oldType.Name} -> {(newType != null ? newType.Name : "?")})";

                string reason = BlockReason(asset, oldType, valueType, newType, convertEvenWithOwnMembers, dependents);
                if (reason != null)
                {
                    report.Count($"not converted: {oldType.Name}");
                    report.Line($"SKIP    {label}: {reason}");
                    continue;
                }

                if (dryRun)
                {
                    report.Count($"would convert: {oldType.Name}");
                    report.Line($"OK      {label}");
                    continue;
                }

                try
                {
                    Convert(path, asset, valueType, newType);
                    report.Count($"converted: {oldType.Name}");
                    report.Line($"DONE    {label}");
                }
                catch (Exception exception)
                {
                    report.Error($"{label}: {exception.Message}");
                }
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
            }

            return report;
        }

        private static string BlockReason(ScriptableObject asset, Type oldType, Type valueType, Type newType, bool convertAnyway, Dictionary<string, List<string>> dependents)
        {
            if (newType == null)
            {
                return $"no VariableEvent<{valueType?.Name}> type exists (copy Runtime/Types into the project)";
            }

            if (!convertAnyway)
            {
                string[] own = OwnMembers(oldType);
                if (own.Length > 0)
                {
                    return $"{oldType.Name} declares its own members ({string.Join(", ", own)}); port them, or tick 'convert anyway'";
                }
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (dependents.TryGetValue(path, out List<string> users))
            {
                foreach (string user in users)
                {
                    string blocker = OldSystemFieldIn(user, asset);
                    if (blocker != null)
                    {
                        return $"still used through an old-system field: {user} > {blocker}";
                    }
                }
            }

            return null;
        }

        private static void Convert(string path, ScriptableObject asset, Type valueType, Type newType)
        {
            object initialValue = valueType != null ? MigrationCommon.Get(asset, "_defaultCallValue") : null;

            ScriptableObject template = ScriptableObject.CreateInstance(newType);
            MonoScript script = MonoScript.FromScriptableObject(template);
            Object.DestroyImmediate(template);
            if (script == null)
            {
                throw new InvalidOperationException($"No script asset found for {newType.Name}.");
            }

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Script").objectReferenceValue = script;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Object converted = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (converted == null || !newType.IsInstanceOfType(converted))
            {
                throw new InvalidOperationException($"After swapping the script the asset is a {converted?.GetType().Name ?? "null"}, not a {newType.Name}.");
            }

            if (valueType != null)
            {
                PropertyInfo initial = newType.GetProperty("InitialValue", BindingFlags.Public | BindingFlags.Instance);
                initial?.SetValue(converted, initialValue);
                newType.GetMethod("ResetForPlayMode", BindingFlags.Public | BindingFlags.Instance)?.Invoke(converted, null);
            }

            EditorUtility.SetDirty(converted);
        }

        private static Type FindVariableEventType(Type valueType)
        {
            Type baseType = typeof(VariableEvent<>).MakeGenericType(valueType);
            return TypeCache.GetTypesDerivedFrom(baseType).Where(type => !type.IsAbstract && !type.IsGenericType)
                .OrderBy(type => type.Assembly == baseType.Assembly ? 0 : 1)
                .FirstOrDefault();
        }

        /// <summary>Fields, properties and methods declared by the concrete class itself (not inherited).</summary>
        private static string[] OwnMembers(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return type.GetMembers(flags)
                .Where(member => !(member is ConstructorInfo) && !member.Name.StartsWith("<") &&
                                 !(member is MethodInfo method && (method.IsSpecialName || method.GetBaseDefinition() != method)) &&
                                 member.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() == null)
                .Select(member => member.Name)
                .Distinct()
                .ToArray();
        }

        /// <summary>For each asset, the scenes / prefabs / assets that reference it directly.</summary>
        private static Dictionary<string, List<string>> ReverseDependencies(List<string> assetPaths)
        {
            var wanted = new HashSet<string>(assetPaths);
            var result = new Dictionary<string, List<string>>();
            string[] extensions = { ".unity", ".prefab", ".asset", ".controller", ".playable" };
            string[] all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && extensions.Any(p.EndsWith)).ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                if (i % 200 == 0)
                {
                    EditorUtility.DisplayProgressBar("Action asset conversion", "Finding what uses each asset…", (float)i / all.Length);
                }

                foreach (string dependency in AssetDatabase.GetDependencies(all[i], false))
                {
                    if (wanted.Contains(dependency) && dependency != all[i])
                    {
                        if (!result.TryGetValue(dependency, out List<string> users))
                        {
                            result[dependency] = users = new List<string>();
                        }

                        users.Add(all[i]);
                    }
                }
            }

            EditorUtility.ClearProgressBar();
            return result;
        }

        /// <summary>
        /// Looks at the types of the objects in <paramref name="userPath"/> that could hold
        /// <paramref name="asset"/>: returns a description of the first one with a field of an
        /// old-system type, or null when everything referencing it is already on the package.
        /// </summary>
        private static string OldSystemFieldIn(string userPath, Object asset)
        {
            IEnumerable<Type> types;
            if (userPath.EndsWith(".unity"))
            {
                types = SceneComponentTypes(userPath);
            }
            else
            {
                types = AssetDatabase.LoadAllAssetsAtPath(userPath)
                    .Where(o => o != null)
                    .Select(o => o is GameObject go ? null : o.GetType())
                    .Concat(AssetDatabase.LoadAllAssetsAtPath(userPath).OfType<GameObject>().SelectMany(go => go.GetComponents<Component>()).Where(c => c != null).Select(c => c.GetType()))
                    .Where(t => t != null);
            }

            foreach (Type type in types.Distinct())
            {
                string field = OldSystemField(type);
                if (field != null)
                {
                    return $"{type.Name}.{field}";
                }
            }

            return null;
        }

        private static IEnumerable<Type> SceneComponentTypes(string scenePath)
        {
            // Read the scene's script GUIDs from the YAML instead of opening it.
            string text = System.IO.File.ReadAllText(scenePath);
            var guids = new HashSet<string>();
            int index = 0;
            const string marker = "m_Script: {fileID: 11500000, guid: ";
            while ((index = text.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
            {
                index += marker.Length;
                guids.Add(text.Substring(index, 32));
            }

            foreach (string guid in guids)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                Type type = script != null ? script.GetClass() : null;
                if (type != null)
                {
                    yield return type;
                }
            }
        }

        /// <summary>The first serialized field of <paramref name="type"/> (or of a nested serializable type) whose type belongs to the old system.</summary>
        internal static string OldSystemField(Type type)
        {
            if (type == null || IsOldSystemType(type))
            {
                return type != null && (MigrationCommon.DerivesFrom(type, "BaseActionListener") || MigrationCommon.DerivesFrom(type, "ActionListener`1"))
                    ? "(old listener component)"
                    : null;
            }

            return OldSystemField(type, 0);
        }

        private static string OldSystemField(Type type, int depth)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type current = type; current != null && current != typeof(MonoBehaviour) && current != typeof(ScriptableObject) && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    if (field.IsNotSerialized || field.IsStatic)
                    {
                        continue;
                    }

                    foreach (Type involved in Involved(field.FieldType))
                    {
                        if (IsOldSystemType(involved))
                        {
                            return field.Name;
                        }

                        if (depth < 2 && !involved.IsPrimitive && !typeof(Object).IsAssignableFrom(involved) &&
                            involved.Namespace?.StartsWith("System") != true && involved.Namespace?.StartsWith("UnityEngine") != true &&
                            involved.Namespace?.StartsWith("RonJames") != true)
                        {
                            string nested = OldSystemField(involved, depth + 1);
                            if (nested != null)
                            {
                                return $"{field.Name}.{nested}";
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static IEnumerable<Type> Involved(Type type)
        {
            yield return type;
            if (type.IsArray)
            {
                foreach (Type inner in Involved(type.GetElementType()))
                {
                    yield return inner;
                }
            }
            else if (type.IsGenericType)
            {
                foreach (Type argument in type.GetGenericArguments())
                {
                    foreach (Type inner in Involved(argument))
                    {
                        yield return inner;
                    }
                }
            }
        }

        private static bool IsOldSystemType(Type type) => OldSystemTypeNames.Any(name => MigrationCommon.DerivesFrom(type, name));
    }
}
