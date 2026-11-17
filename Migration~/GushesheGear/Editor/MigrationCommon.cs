using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GushesheGearMigration
{
    /// <summary>A scene or prefab being scanned or migrated, with its root objects.</summary>
    internal sealed class Container
    {
        public string Path;
        public bool IsPrefab;
        public GameObject[] Roots;
        public bool Changed;
        public Scene Scene;
    }

    /// <summary>What a migration run found and did. Written to Logs/ when a run finishes.</summary>
    public sealed class MigrationReport
    {
        private readonly string title;
        private readonly List<string> lines = new List<string>();
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public MigrationReport(string title, bool dryRun)
        {
            this.title = title;
            DryRun = dryRun;
        }

        public bool DryRun { get; }
        public int Errors { get; private set; }

        public void Count(string key, int amount = 1)
        {
            counts.TryGetValue(key, out int current);
            counts[key] = current + amount;
        }

        public void Line(string text) => lines.Add(text);

        public void Error(string text)
        {
            Errors++;
            lines.Add("ERROR: " + text);
        }

        public string Build()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{title} ({(DryRun ? "DRY RUN: nothing was changed" : "changes applied")}) - {DateTime.Now:yyyy-MM-dd HH:mm}");
            builder.AppendLine();
            foreach (KeyValuePair<string, int> pair in counts.OrderBy(pair => pair.Key))
            {
                builder.AppendLine($"  {pair.Key}: {pair.Value}");
            }

            builder.AppendLine();
            foreach (string line in lines)
            {
                builder.AppendLine(line);
            }

            return builder.ToString();
        }

        public string Save(string fileName)
        {
            string directory = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, fileName);
            File.WriteAllText(path, Build());
            return path;
        }
    }

    internal static class MigrationCommon
    {
        /// <summary>Every prefab and scene under Assets/, prefabs first.</summary>
        public static List<string> AllContainerPaths(string folder = "Assets")
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath);
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath);
            return prefabs.Where(IsMigratable).OrderBy(path => path)
                .Concat(scenes.Where(IsMigratable).OrderBy(path => path))
                .ToList();
        }

        /// <summary>Third-party folders are left alone.</summary>
        private static bool IsMigratable(string path) =>
            path.StartsWith("Assets/") && !path.Contains("/Plugins/") && !path.Contains("/Samples/");

        /// <summary>
        /// Opens each container in turn, calls <paramref name="visit"/>, and saves it if visit marked it
        /// changed (and this isn't a dry run). Scenes are opened one at a time; the scenes that were
        /// open before are restored afterwards.
        /// </summary>
        public static void ForEachContainer(IEnumerable<string> paths, bool dryRun, Action<Container> visit, string progressTitle)
        {
            if (!dryRun && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Cancelled: open scenes have unsaved changes.");
            }

            string[] openScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(i => SceneManager.GetSceneAt(i).path)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();

            List<string> list = paths.ToList();
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    string path = list[i];
                    if (EditorUtility.DisplayCancelableProgressBar(progressTitle, path, (float)i / Mathf.Max(1, list.Count)))
                    {
                        throw new OperationCanceledException("Cancelled by user.");
                    }

                    if (path.EndsWith(".prefab"))
                    {
                        GameObject root = PrefabUtility.LoadPrefabContents(path);
                        try
                        {
                            var container = new Container { Path = path, IsPrefab = true, Roots = new[] { root } };
                            visit(container);
                            if (container.Changed && !dryRun)
                            {
                                PrefabUtility.SaveAsPrefabAsset(root, path);
                            }
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                        }
                    }
                    else if (path.EndsWith(".unity"))
                    {
                        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        var container = new Container { Path = path, IsPrefab = false, Roots = scene.GetRootGameObjects(), Scene = scene };
                        visit(container);
                        if (container.Changed && !dryRun)
                        {
                            EditorSceneManager.SaveScene(scene);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (openScenes.Length > 0)
                {
                    EditorSceneManager.OpenScene(openScenes[0], OpenSceneMode.Single);
                    for (int i = 1; i < openScenes.Length; i++)
                    {
                        EditorSceneManager.OpenScene(openScenes[i], OpenSceneMode.Additive);
                    }
                }
            }
        }

        public static IEnumerable<Component> AllComponents(Container container)
        {
            foreach (GameObject root in container.Roots)
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null)
                    {
                        yield return component;
                    }
                }
            }
        }

        /// <summary>
        /// A stable key for a component, shared by every place it appears: a prefab-instance part is
        /// keyed by the prefab asset that owns it, so scene instances, variants and the prefab itself
        /// all agree on which component they mean.
        /// </summary>
        public static string KeyOf(Component component, Container container)
        {
            if (component == null)
            {
                return null;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(component))
            {
                Component source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
                if (source != null)
                {
                    return KeyOfAssetComponent(source);
                }
            }

            return Key(container.Path, component);
        }

        /// <summary>The key of a component that lives in a prefab asset (e.g. a property-modification target).</summary>
        public static string KeyOfAssetComponent(Component component)
        {
            return component == null ? null : Key(AssetDatabase.GetAssetPath(component), component);
        }

        private static string Key(string ownerPath, Component component)
        {
            Component[] sameType = component.GetComponents(component.GetType());
            int index = Array.IndexOf(sameType, component);
            return $"{ownerPath}|{HierarchyPath(component.transform)}|{component.GetType().FullName}#{index}";
        }

        public static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        public static string Describe(Component component, Container container) =>
            $"{container.Path} > {HierarchyPath(component.transform)} ({component.GetType().Name})";

        // ------------------------------------------------------------------------------------
        // Reflection helpers: the old system is reached only by name, so these tools keep
        // compiling after it has been deleted.
        // ------------------------------------------------------------------------------------

        public static Type FindType(string name)
        {
            return TypeCache.GetTypesDerivedFrom<object>().FirstOrDefault(type => type.Name == name && !type.IsGenericTypeDefinition)
                   ?? AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(type => type != null);
        }

        public static Type FindGenericDefinition(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .FirstOrDefault(type => type.IsGenericTypeDefinition && type.Name == name);
        }

        public static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        public static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        public static object Get(object target, string fieldName)
        {
            return target == null ? null : FindField(target.GetType(), fieldName)?.GetValue(target);
        }

        public static T Get<T>(object target, string fieldName, T fallback = default)
        {
            object value = Get(target, fieldName);
            return value is T typed ? typed : fallback;
        }

        public static bool Set(object target, string fieldName, object value)
        {
            FieldInfo field = target == null ? null : FindField(target.GetType(), fieldName);
            if (field == null || (value != null && !field.FieldType.IsInstanceOfType(value)))
            {
                return false;
            }

            field.SetValue(target, value);
            return true;
        }

        public static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite)
            {
                throw new MissingMemberException(target.GetType().Name, propertyName);
            }

            property.SetValue(target, value);
        }

        public static IList GetListProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(target) as IList;
        }

        /// <summary>True when a UnityEvent field holds at least one persistent (inspector-wired) call.</summary>
        public static bool HasPersistentCalls(object unityEvent) =>
            unityEvent is UnityEngine.Events.UnityEventBase eventBase && eventBase.GetPersistentEventCount() > 0;

        /// <summary>The generic argument T where <paramref name="type"/> derives from <paramref name="genericBaseName"/>&lt;T&gt;.</summary>
        public static Type GenericArgumentOf(Type type, string genericBaseName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition().Name == genericBaseName)
                {
                    return current.GetGenericArguments()[0];
                }
            }

            return null;
        }

        public static bool DerivesFrom(Type type, string baseName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                string name = current.IsGenericType ? current.GetGenericTypeDefinition().Name : current.Name;
                if (name == baseName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the type is serialized by Odin (a SerializedMonoBehaviour / SerializedScriptableObject / similar).</summary>
        public static bool IsOdinSerialized(Type type) =>
            typeof(Sirenix.Serialization.ISupportsPrefabSerialization).IsAssignableFrom(type) ||
            DerivesFrom(type, "SerializedMonoBehaviour") || DerivesFrom(type, "SerializedScriptableObject") ||
            DerivesFrom(type, "SerializedBehaviour");
    }
}
