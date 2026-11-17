using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RonJames.Foundation.Events.Editor
{
    /// <summary>
    /// Finds everything that points at an asset, typically an Event or Variable Event: Listeners,
    /// Commands and Variable References in open scenes (component by component), and prefabs, scenes
    /// and assets in the project. It works through Odin-serialized data, because Odin stores Unity
    /// object references in a list Unity itself serializes.
    /// </summary>
    public sealed class ReferenceFinderWindow : EditorWindow
    {
        private Object target;
        private readonly List<Component> sceneHits = new List<Component>();
        private readonly List<string> assetHits = new List<string>();
        private Vector2 scroll;
        private bool searched;

        [MenuItem("Assets/RonJames Foundation/Find References", priority = 2000)]
        private static void FindForSelection()
        {
            Open(Selection.activeObject);
        }

        [MenuItem("Assets/RonJames Foundation/Find References", true)]
        private static bool CanFindForSelection() => Selection.activeObject != null && EditorUtility.IsPersistent(Selection.activeObject);

        [MenuItem("Tools/RonJames Foundation/Reference Finder")]
        private static void OpenEmpty()
        {
            Open(Selection.activeObject);
        }

        /// <summary>Opens the window and searches for references to <paramref name="asset"/>.</summary>
        public static void Open(Object asset)
        {
            var window = GetWindow<ReferenceFinderWindow>("References");
            window.target = asset;
            if (asset != null)
            {
                window.Search();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            target = EditorGUILayout.ObjectField("Find references to", target, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                searched = false;
            }

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("Search", GUILayout.Width(70)))
                {
                    Search();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!searched)
            {
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField($"In open scenes ({sceneHits.Count})", EditorStyles.boldLabel);
            foreach (Component component in sceneHits)
            {
                if (component == null)
                {
                    continue;
                }

                if (GUILayout.Button($"{HierarchyPath(component.transform)}  ·  {component.GetType().Name}", EditorStyles.linkLabel))
                {
                    Selection.activeObject = component.gameObject;
                    EditorGUIUtility.PingObject(component.gameObject);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"In project assets ({assetHits.Count})", EditorStyles.boldLabel);
            foreach (string path in assetHits)
            {
                if (GUILayout.Button(path, EditorStyles.linkLabel))
                {
                    Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void Search()
        {
            sceneHits.Clear();
            assetHits.Clear();
            searched = true;
            if (target == null)
            {
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component != null && References(component, target))
                        {
                            sceneHits.Add(component);
                        }
                    }
                }
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                foreach (Component component in prefabStage.prefabContentsRoot.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && References(component, target))
                    {
                        sceneHits.Add(component);
                    }
                }
            }

            string targetPath = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(targetPath))
            {
                string[] extensions = { ".prefab", ".unity", ".asset" };
                foreach (string path in AssetDatabase.GetAllAssetPaths())
                {
                    if (!path.StartsWith("Assets/") || path == targetPath || !extensions.Contains(Path.GetExtension(path)))
                    {
                        continue;
                    }

                    if (AssetDatabase.GetDependencies(path, false).Contains(targetPath))
                    {
                        assetHits.Add(path);
                    }
                }
            }

            assetHits.Sort();
            Repaint();
        }

        private static bool References(Object owner, Object target)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
