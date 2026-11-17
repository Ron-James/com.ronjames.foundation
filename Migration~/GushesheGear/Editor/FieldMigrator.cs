using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RonJames.Foundation.Events;
using RonJames.Foundation.Values;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GushesheGearMigration
{
    /// <summary>
    /// Fills every field marked <c>[MigrateFrom("oldField")]</c> from its old field, in every prefab,
    /// scene and ScriptableObject asset that has one. Workflow per script:
    /// <list type="number">
    /// <item>Make the class Odin-serialized (SerializedMonoBehaviour / SerializedScriptableObject).</item>
    /// <item>Add the new field next to the old one: <c>[OdinSerialize, MigrateFrom(nameof(speed))] private VariableReference&lt;float&gt; speedValue;</c></item>
    /// <item>Run this (dry run first), check the report, then switch the code to the new field and delete the old field and the attribute.</item>
    /// </list>
    /// </summary>
    public static class FieldMigrator
    {
        private sealed class Marked
        {
            public Type Host;
            public FieldInfo NewField;
            public FieldInfo OldField;
        }

        public static MigrationReport Run(bool dryRun, IEnumerable<string> containerPaths = null)
        {
            var report = new MigrationReport("Marked field migration", dryRun);
            List<Marked> marked = CollectMarkedFields(report);
            if (marked.Count == 0)
            {
                report.Line("No fields are marked with [MigrateFrom]. Mark the new fields first (see FieldMigrator's summary).");
                return report;
            }

            var hosts = marked.GroupBy(m => m.Host).ToDictionary(g => g.Key, g => g.ToList());

            // ScriptableObject assets.
            foreach (KeyValuePair<Type, List<Marked>> host in hosts.Where(h => typeof(ScriptableObject).IsAssignableFrom(h.Key)))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + host.Key.Name))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path).Where(a => a != null && host.Key.IsInstanceOfType(a)))
                    {
                        if (CopyFields(asset, null, host.Value, path, dryRun, report))
                        {
                            if (!dryRun)
                            {
                                EditorUtility.SetDirty(asset);
                            }
                        }
                    }
                }
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
            }

            // Components in prefabs and scenes.
            if (hosts.Keys.Any(type => typeof(Component).IsAssignableFrom(type)))
            {
                MigrationCommon.ForEachContainer(containerPaths ?? MigrationCommon.AllContainerPaths(), dryRun, container =>
                {
                    foreach (Component component in MigrationCommon.AllComponents(container))
                    {
                        List<Marked> fields = hosts.Where(h => h.Key.IsInstanceOfType(component)).SelectMany(h => h.Value).ToList();
                        if (fields.Count == 0)
                        {
                            continue;
                        }

                        Component source = PrefabUtility.IsPartOfPrefabInstance(component) ? PrefabUtility.GetCorrespondingObjectFromSource(component) : null;
                        if (CopyFields(component, source, fields, MigrationCommon.Describe(component, container), dryRun, report))
                        {
                            container.Changed = true;
                            if (!dryRun)
                            {
                                EditorUtility.SetDirty(component);
                            }
                        }
                    }
                }, dryRun ? "Field migration: dry run" : "Field migration");
            }

            return report;
        }

        private static List<Marked> CollectMarkedFields(MigrationReport report)
        {
            var result = new List<Marked>();
            foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<MigrateFromAttribute>())
            {
                var attribute = field.GetCustomAttribute<MigrateFromAttribute>();
                Type host = field.DeclaringType;
                FieldInfo oldField = MigrationCommon.FindField(host, attribute.OldFieldName);
                string name = $"{host.Name}.{field.Name}";
                if (oldField == null)
                {
                    report.Error($"{name}: old field '{attribute.OldFieldName}' not found.");
                    continue;
                }

                if (!MigrationCommon.IsOdinSerialized(host))
                {
                    report.Error($"{name}: {host.Name} isn't Odin-serialized, so the new field won't be saved. Make it a SerializedMonoBehaviour / SerializedScriptableObject.");
                    continue;
                }

                if (field.GetCustomAttribute<OdinSerializeAttribute>() == null && field.GetCustomAttribute<SerializeField>() == null && !field.IsPublic)
                {
                    report.Error($"{name}: mark the new field [OdinSerialize] so it's saved.");
                    continue;
                }

                if (!IsSupportedTarget(field.FieldType))
                {
                    report.Error($"{name}: can't migrate into a {field.FieldType.Name}. Supported: VariableReference<T>, IEvent, IVariableEvent<T>, and lists / arrays of the event interfaces.");
                    continue;
                }

                result.Add(new Marked { Host = host, NewField = field, OldField = oldField });
                report.Count("marked fields");
            }

            return result;
        }

        /// <summary>Copies each marked field; returns true when anything was (or, in a dry run, would be) written.</summary>
        private static bool CopyFields(Object target, Object prefabSource, List<Marked> fields, string where, bool dryRun, MigrationReport report)
        {
            bool changed = false;
            foreach (Marked marked in fields)
            {
                object oldValue = marked.OldField.GetValue(target);

                // A prefab instance inherits the prefab's (already migrated) value unless it overrode the old field.
                if (prefabSource != null && SameSerializedValue(oldValue, marked.OldField.GetValue(prefabSource)))
                {
                    continue;
                }

                string label = $"{where} : {marked.Host.Name}.{marked.OldField.Name} -> {marked.NewField.Name}";
                try
                {
                    object newValue = Convert(oldValue, marked.NewField.FieldType);
                    if (!dryRun)
                    {
                        marked.NewField.SetValue(target, newValue);
                    }

                    changed = true;
                    report.Count(dryRun ? "would copy" : "copied");
                    report.Line($"{(dryRun ? "OK  " : "DONE")}  {label}{(prefabSource != null ? " (instance override)" : "")}");
                }
                catch (Exception exception)
                {
                    report.Error($"{label}: {exception.Message}");
                }
            }

            return changed;
        }

        private static bool IsSupportedTarget(Type type)
        {
            if (IsVariableReference(type) || IsEventInterface(type))
            {
                return true;
            }

            Type element = ElementType(type);
            return element != null && IsEventInterface(element);
        }

        private static bool IsVariableReference(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(VariableReference<>);

        private static bool IsEventInterface(Type type) =>
            type == typeof(IEvent) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IVariableEvent<>));

        private static Type ElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : null;
        }

        /// <summary>Converts an old value into the new field's type, or throws with the reason.</summary>
        internal static object Convert(object oldValue, Type target)
        {
            if (IsVariableReference(target))
            {
                Type valueType = target.GetGenericArguments()[0];
                return ConvertReference(oldValue, target, valueType);
            }

            if (IsEventInterface(target))
            {
                object single = ExtractEvents(oldValue).FirstOrDefault();
                if (single != null && !target.IsInstanceOfType(single))
                {
                    throw new InvalidOperationException($"{Name(single)} isn't an {target.Name}. Is the bridge patch applied?");
                }

                return single;
            }

            Type element = ElementType(target);
            List<object> events = ExtractEvents(oldValue).ToList();
            foreach (object item in events.Where(item => !element.IsInstanceOfType(item)))
            {
                throw new InvalidOperationException($"{Name(item)} isn't an {element.Name}. Is the bridge patch applied?");
            }

            if (target.IsArray)
            {
                Array array = Array.CreateInstance(element, events.Count);
                for (int i = 0; i < events.Count; i++)
                {
                    array.SetValue(events[i], i);
                }

                return array;
            }

            var list = (IList)Activator.CreateInstance(target);
            foreach (object item in events)
            {
                list.Add(item);
            }

            return list;
        }

        private static object ConvertReference(object oldValue, Type target, Type valueType)
        {
            if (oldValue == null)
            {
                return Activator.CreateInstance(target);
            }

            // ActionReference<T> / ValueAssetReference<T> / ValueReference<T, TAsset>.
            FieldInfo useValueField = MigrationCommon.FindField(oldValue.GetType(), "useValue");
            if (useValueField != null)
            {
                bool useValue = (bool)useValueField.GetValue(oldValue);
                object value = MigrationCommon.Get(oldValue, "_value");
                object asset = MigrationCommon.Get(oldValue, "assetReference");

                // The old reference read its fallback _value when no asset was assigned, so keep that.
                if (useValue || UnityNull.IsNull(asset))
                {
                    return Activator.CreateInstance(target, value);
                }

                Type sourceType = typeof(IValueSource<>).MakeGenericType(valueType);
                if (!sourceType.IsInstanceOfType(asset))
                {
                    throw new InvalidOperationException($"{Name(asset)} isn't an IValueSource<{valueType.Name}>. Is the bridge patch applied?");
                }

                return target.GetMethod(nameof(VariableReference<int>.FromSource), BindingFlags.Public | BindingFlags.Static).Invoke(null, new[] { asset });
            }

            if (valueType.IsInstanceOfType(oldValue))
            {
                return Activator.CreateInstance(target, oldValue);
            }

            throw new InvalidOperationException($"Don't know how to turn a {oldValue.GetType().Name} into a VariableReference<{valueType.Name}>.");
        }

        /// <summary>The event assets inside an old value: an ActionSO itself, a subscription's Action, a set's Subscriptions, or any list of those.</summary>
        private static IEnumerable<object> ExtractEvents(object oldValue)
        {
            if (oldValue == null)
            {
                yield break;
            }

            if (oldValue is Object unityObject)
            {
                if (unityObject != null)
                {
                    yield return unityObject;
                }

                yield break;
            }

            if (MigrationCommon.FindField(oldValue.GetType(), "Action") != null)
            {
                object action = MigrationCommon.Get(oldValue, "Action");
                if (!UnityNull.IsNull(action))
                {
                    yield return action;
                }

                yield break;
            }

            if (MigrationCommon.FindField(oldValue.GetType(), "Subscriptions") != null)
            {
                foreach (object item in ExtractEvents(MigrationCommon.Get(oldValue, "Subscriptions")))
                {
                    yield return item;
                }

                yield break;
            }

            if (oldValue is IEnumerable sequence && !(oldValue is string))
            {
                foreach (object entry in sequence)
                {
                    foreach (object item in ExtractEvents(entry))
                    {
                        yield return item;
                    }
                }
            }
        }

        private static bool SameSerializedValue(object a, object b)
        {
            if (a == null || b == null)
            {
                return a == null && b == null;
            }

            byte[] bytesA = Sirenix.Serialization.SerializationUtility.SerializeValue(a, DataFormat.Binary, out List<Object> refsA);
            byte[] bytesB = Sirenix.Serialization.SerializationUtility.SerializeValue(b, DataFormat.Binary, out List<Object> refsB);
            return bytesA.SequenceEqual(bytesB) && (refsA ?? new List<Object>()).SequenceEqual(refsB ?? new List<Object>());
        }

        private static string Name(object value) => value is Object unityObject ? unityObject.name : value?.GetType().Name ?? "null";
    }
}
