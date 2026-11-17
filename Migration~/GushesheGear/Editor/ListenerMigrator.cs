using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RonJames.Foundation.Events;
using RonJames.Foundation.Values;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace GushesheGearMigration
{
    /// <summary>
    /// Replaces GushesheGear's listener MonoBehaviours with the package's Event Listener / Variable
    /// Event Listener components, in prefabs and scenes. Only listeners that are safe to replace are
    /// migrated; everything else is listed in the report with the reason.
    /// </summary>
    /// <remarks>
    /// A listener is left alone (reported as "manual") when:
    /// <list type="bullet">
    /// <item>its type is a UI card / has game logic beyond listening (Product, Mission, Quest, ItemQuantity, ExperienceData, Level, ActiveModifier, Item listeners), or has no rule here;</item>
    /// <item>anything references the component: a UnityEvent targets it, a field or pool points at it;</item>
    /// <item>it's overridden in a prefab variant or scene instance (the override would be lost);</item>
    /// <item>it uses a feature with no equivalent: the previous-value filter, a wired onValueTransition /
    /// onActionRaised / onPreviousValueSet / onListenValueSet / onCleared, or a Float multiplier other than 1.</item>
    /// </list>
    /// Requires the bridge patch: the new listeners subscribe to the existing ActionSO assets.
    /// </remarks>
    public static class ListenerMigrator
    {
        private sealed class Rule
        {
            public string NewListener;
            public Func<Component, string> Block;
            public Func<Component, Type, IEnumerable<object>> ExtraResponses;
            public bool FloatCompare;
        }

        private static readonly string[] ManualTypes =
        {
            "ProductListener", "MissionActionListener", "QuestListener", "ItemQuantityListener",
            "ExperienceDataListener", "LevelActionListener", "ActiveModifierListener", "ItemListener"
        };

        private static readonly string[] UnsupportedEvents =
        {
            "onValueTransition", "onActionRaised", "onPreviousValueSet", "onListenValueSet", "onCleared"
        };

        private static readonly Dictionary<string, Rule> Rules = new Dictionary<string, Rule>
        {
            ["BaseActionListener"] = new Rule { NewListener = "EventListener" },
            ["BoolActionListener"] = new Rule { NewListener = "BoolVariableEventListener", ExtraResponses = (old, t) => Ported(old, "BoolSplitResponse", "onTrue", "onFalse") },
            ["IntListener"] = new Rule { NewListener = "IntVariableEventListener", ExtraResponses = (old, t) => Ported(old, "IntToStringResponse", "onStringCasted") },
            ["StringActionListener"] = new Rule { NewListener = "StringVariableEventListener", ExtraResponses = (old, t) => Ported(old, "TranslatedStringResponse", "responseLocale") },
            ["StuntStateListener"] = new Rule
            {
                NewListener = "StuntStateVariableEventListener",
                ExtraResponses = (old, t) => Ported(old, "StuntStateResponse", "onVehicleStunt", "onCharacterStunt", "onAutoStunt", "onIdle", "onPerformingStunt", "onPerformingCharacterStunt")
            },
            ["FloatActionListener"] = new Rule
            {
                NewListener = "FloatVariableEventListener",
                Block = old => Math.Abs(MigrationCommon.Get<float>(old, "multiplier", 1f) - 1f) > 0.00001f
                    ? "uses a multiplier other than 1 (the multiplier applied before the listen-value filter)"
                    : null,
                ExtraResponses = (old, t) => Ported(old, "FloatFormatResponse", "timeInMinutes", "roundedValueProcessor"),
                FloatCompare = true
            },
            ["Vector2Listener"] = new Rule { NewListener = "Vector2VariableEventListener" },
            ["StuntListener"] = new Rule { NewListener = "StuntSequenceVariableEventListener" },
            ["VehicleListener"] = new Rule { NewListener = "VehicleVariableEventListener" },
            ["CinemachineCameraListener"] = new Rule { NewListener = "CinemachineCameraVariableEventListener" },
            ["ColliderActionListener"] = new Rule { NewListener = "ColliderVariableEventListener" },
            ["ComponentServiceListener"] = new Rule { NewListener = "ComponentServiceVariableEventListener" },
            ["CustomComponentListener"] = new Rule { NewListener = "CustomComponentVariableEventListener" },
            ["TransformListener"] = new Rule { NewListener = "TransformVariableEventListener" },
        };

        /// <summary>Runs over every prefab and scene. With <paramref name="dryRun"/> nothing is changed.</summary>
        public static MigrationReport Run(bool dryRun, bool removeOldComponents, IEnumerable<string> containerPaths = null)
        {
            var report = new MigrationReport("Listener migration", dryRun);
            if (!CheckBridge(report))
            {
                return report;
            }

            List<string> paths = (containerPaths ?? MigrationCommon.AllContainerPaths()).ToList();

            // Pass 1: find every listener that something references, or that a variant / instance overrides.
            var blocked = new Dictionary<string, string>();
            MigrationCommon.ForEachContainer(paths, true, container => CollectBlocks(container, blocked), "Listener migration: checking references");

            // Pass 2: migrate (or, in a dry run, report) every listener each container owns.
            MigrationCommon.ForEachContainer(paths, dryRun, container => MigrateContainer(container, blocked, dryRun, removeOldComponents, report),
                dryRun ? "Listener migration: dry run" : "Listener migration");

            report.Line("");
            report.Line("Behaviour notes for migrated listeners: they now receive values in subscription order; " +
                        "'Update On Enable' became 'Receive Current Value On Enable', which fires on every enable, not just the first; " +
                        "reference-type listeners still ignore nulls (via a Null Filter), as before.");
            return report;
        }

        private static bool CheckBridge(MigrationReport report)
        {
            Type actionSo = MigrationCommon.FindGenericDefinition("ActionSO`1");
            Type baseAction = MigrationCommon.FindType("BaseActionSO");
            if (actionSo == null || baseAction == null)
            {
                report.Error("Couldn't find ActionSO<T> / BaseActionSO. Run this in GushesheGear before deleting the old system.");
                return false;
            }

            bool bridged = typeof(IEvent).IsAssignableFrom(baseAction) &&
                           actionSo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVariableEvent<>));
            if (!bridged)
            {
                report.Error("The bridge patch isn't applied (BaseActionSO isn't an IEvent / ActionSO<T> isn't an IVariableEvent<T>). Apply Bridge/bridge.patch first.");
                return false;
            }

            return true;
        }

        private static bool IsOldListener(Component component, out string typeName)
        {
            typeName = component.GetType().Name;
            return MigrationCommon.DerivesFrom(component.GetType(), "ActionListener`1") || typeName == "BaseActionListener";
        }

        private static void CollectBlocks(Container container, Dictionary<string, string> blocked)
        {
            foreach (Component component in MigrationCommon.AllComponents(container))
            {
                // References from this component to any old listener (UnityEvent targets, fields, Odin data).
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    // Unity's own bookkeeping links (instance -> prefab source, owner GameObject) aren't references.
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.propertyPath.StartsWith("m_CorrespondingSourceObject") || property.propertyPath.StartsWith("m_PrefabInstance") ||
                        property.propertyPath.StartsWith("m_PrefabAsset") || property.propertyPath == "m_GameObject" ||
                        !(property.objectReferenceValue is Component target) || target == component ||
                        !IsOldListener(target, out _))
                    {
                        continue;
                    }

                    string key = MigrationCommon.KeyOf(target, container);
                    if (key != null && !blocked.ContainsKey(key))
                    {
                        blocked[key] = $"referenced by {MigrationCommon.Describe(component, container)} ({property.propertyPath})";
                    }
                }

                // Overrides of an old listener on a prefab instance or variant.
                if (IsOldListener(component, out _) && PrefabUtility.IsPartOfPrefabInstance(component))
                {
                    GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(component);
                    Component source = PrefabUtility.GetCorrespondingObjectFromSource(component);
                    PropertyModification[] modifications = instanceRoot != null ? PrefabUtility.GetPropertyModifications(instanceRoot) : null;
                    if (modifications != null && source != null && modifications.Any(m => m.target == source && m.propertyPath != "m_Script"))
                    {
                        string key = MigrationCommon.KeyOf(component, container);
                        if (key != null && !blocked.ContainsKey(key))
                        {
                            blocked[key] = $"overridden in {container.Path} (the override would be lost)";
                        }
                    }
                }
            }
        }

        private static void MigrateContainer(Container container, Dictionary<string, string> blocked, bool dryRun, bool removeOld, MigrationReport report)
        {
            foreach (Component component in MigrationCommon.AllComponents(container).ToList())
            {
                if (component == null || !IsOldListener(component, out string typeName))
                {
                    continue;
                }

                // Parts of a prefab instance are migrated in the prefab that owns them.
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                {
                    continue;
                }

                string where = MigrationCommon.Describe(component, container);
                string reason = BlockReason(component, typeName, MigrationCommon.KeyOf(component, container), blocked);
                if (reason != null)
                {
                    report.Count($"manual: {typeName}");
                    report.Line($"MANUAL  {where}: {reason}");
                    continue;
                }

                int calls = WiredCalls(component);
                int alreadyMissing = MissingActions(component, typeName);
                if (alreadyMissing > 0)
                {
                    report.Count("actions already missing before migration (asset deleted)", alreadyMissing);
                    report.Line($"WARN    {where}: {alreadyMissing} action(s) point at a deleted asset; the reference is kept as it is");
                }

                if (dryRun)
                {
                    report.Count($"would migrate: {typeName}");
                    report.Count("wired UnityEvent calls to move", calls);
                    report.Line($"OK      {where}");
                    continue;
                }

                try
                {
                    Migrate(component, typeName, removeOld);
                    container.Changed = true;
                    report.Count($"migrated: {typeName}");
                    report.Count("wired UnityEvent calls moved", calls);
                    report.Line($"DONE    {where}");
                }
                catch (Exception exception)
                {
                    report.Error($"{where}: {exception.Message}");
                }
            }
        }

        /// <summary>
        /// After a migration: reloads every prefab and scene from disk and counts the package listeners
        /// and the wired UnityEvent calls inside their Responses, proving the moved calls survived saving.
        /// </summary>
        public static MigrationReport Verify(IEnumerable<string> containerPaths = null)
        {
            var report = new MigrationReport("Listener migration check", true);
            MigrationCommon.ForEachContainer(containerPaths ?? MigrationCommon.AllContainerPaths(), true, container =>
            {
                foreach (Component component in MigrationCommon.AllComponents(container))
                {
                    // Count each component once, in the prefab or scene that owns it.
                    if (PrefabUtility.IsPartOfPrefabInstance(component))
                    {
                        continue;
                    }

                    IList events = null;
                    IList responses = null;
                    if (component is EventListener eventListener)
                    {
                        report.Count("Event Listeners");
                        events = eventListener.Events;
                        responses = eventListener.Responses;
                    }
                    else if (MigrationCommon.DerivesFrom(component.GetType(), "VariableEventListener`1"))
                    {
                        report.Count("Variable Event Listeners");
                        events = MigrationCommon.GetListProperty(component, "Events");
                        responses = MigrationCommon.GetListProperty(component, "Responses");
                    }

                    if (events != null)
                    {
                        report.Count("wired UnityEvent calls in Responses", responses.Cast<object>().Sum(WiredCalls));
                        report.Count("events referenced", events.Count);
                        int missing = events.Cast<object>().Count(UnityNull.IsNull);
                        if (missing > 0)
                        {
                            // The migrator copies references as they were, so a reference to a deleted asset stays
                            // missing; the migration report flags those as "already missing".
                            report.Count("events missing (asset deleted)", missing);
                            report.Line($"MISSING    {missing} event asset(s) on {MigrationCommon.Describe(component, container)}");
                        }

                        if (events.Count == 0)
                        {
                            report.Line($"NO EVENTS  {MigrationCommon.Describe(component, container)}");
                        }
                    }
                    else if (IsOldListener(component, out string typeName))
                    {
                        report.Count($"old listeners remaining: {typeName}");
                    }
                }
            }, "Listener migration: checking");
            return report;
        }

        /// <summary>Actions that reference an asset which no longer exists (a Unity "missing" reference, not an empty slot).</summary>
        private static int MissingActions(Component old, string typeName)
        {
            bool IsMissing(object value) => value != null && UnityNull.IsNull(value);
            if (typeName == "BaseActionListener")
            {
                return IsMissing(MigrationCommon.Get(old, "action")) ? 1 : 0;
            }

            return MigrationCommon.Get(old, "actions") is IEnumerable subscriptions
                ? subscriptions.Cast<object>().Count(subscription => IsMissing(MigrationCommon.Get(subscription, "Action")))
                : 0;
        }

        /// <summary>Wired (persistent) UnityEvent calls in an object's fields, including one level of nested structs/classes (e.g. StringEventProcessor).</summary>
        private static int WiredCalls(object target) => WiredCalls(target, 0);

        private static int WiredCalls(object target, int depth)
        {
            if (target == null || depth > 2)
            {
                return 0;
            }

            if (target is UnityEventBase unityEvent)
            {
                return unityEvent.GetPersistentEventCount();
            }

            int total = 0;
            for (Type type = target.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                                      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    Type fieldType = field.FieldType;
                    if (typeof(UnityEventBase).IsAssignableFrom(fieldType) ||
                        (!fieldType.IsPrimitive && !fieldType.IsEnum && fieldType != typeof(string) && !typeof(Object).IsAssignableFrom(fieldType) &&
                         fieldType.IsSerializable && !typeof(IEnumerable).IsAssignableFrom(fieldType)))
                    {
                        total += WiredCalls(field.GetValue(target), depth + 1);
                    }
                }
            }

            return total;
        }

        private static string BlockReason(Component old, string typeName, string key, Dictionary<string, string> blocked)
        {
            if (ManualTypes.Contains(typeName))
            {
                return "UI card / game logic beyond listening (bound by pools or called by buttons); port by hand";
            }

            if (!Rules.TryGetValue(typeName, out Rule rule))
            {
                return "no migration rule for this listener type";
            }

            if (MigrationCommon.FindType(rule.NewListener) == null)
            {
                return $"the new listener type {rule.NewListener} doesn't exist (copy Runtime/Types into the project)";
            }

            if (key != null && blocked.TryGetValue(key, out string blockedReason))
            {
                return blockedReason;
            }

            if (MigrationCommon.Get<bool>(old, "usePreviousValueFilter"))
            {
                return "uses the previous-value filter, which has no equivalent";
            }

            foreach (string eventName in UnsupportedEvents)
            {
                if (MigrationCommon.HasPersistentCalls(MigrationCommon.Get(old, eventName)))
                {
                    return $"has calls wired on {eventName}, which has no equivalent";
                }
            }

            return rule.Block?.Invoke(old);
        }

        private static void Migrate(Component old, string typeName, bool removeOld)
        {
            Rule rule = Rules[typeName];
            Type newType = MigrationCommon.FindType(rule.NewListener);
            GameObject gameObject = old.gameObject;
            bool wasEnabled = !(old is Behaviour behaviour) || behaviour.enabled;

            if (typeName == "BaseActionListener")
            {
                var listener = (EventListener)Undo.AddComponent(gameObject, newType);
                listener.enabled = wasEnabled;
                if (MigrationCommon.Get(old, "action") is IEvent action)
                {
                    listener.Events.Add(action);
                }

                listener.Responses.Add(EventResponse(MigrationCommon.Get(old, "_response") as UnityEvent));
                EditorUtility.SetDirty(listener);
            }
            else
            {
                Type valueType = MigrationCommon.GenericArgumentOf(old.GetType(), "ActionListener`1");
                List<object> events = OldActions(old, valueType);
                object filter = BuildFilter(old, valueType);

                Component listener = AddTypedListener(gameObject, newType, wasEnabled, events, filter, MigrationCommon.Get<bool>(old, "updateOnEnable"));
                IList responses = MigrationCommon.GetListProperty(listener, "Responses");
                object response = MigrationCommon.Get(old, "_response");
                if (MigrationCommon.HasPersistentCalls(response))
                {
                    responses.Add(TypedEventResponse(valueType, response));
                }

                if (rule.ExtraResponses != null)
                {
                    foreach (object extra in rule.ExtraResponses(old, valueType))
                    {
                        responses.Add(extra);
                    }
                }

                EditorUtility.SetDirty(listener);

                // FloatActionListener's greater/less events fired on every Raise, outside the filter.
                if (rule.FloatCompare &&
                    (MigrationCommon.HasPersistentCalls(MigrationCommon.Get(old, "isGreaterThanListenValue")) ||
                     MigrationCommon.HasPersistentCalls(MigrationCommon.Get(old, "isLessThanListenValue"))))
                {
                    bool useListen = MigrationCommon.Get<bool>(old, "_useListenValues");
                    float compareTo = useListen ? MigrationCommon.Get<float>(old, "_listenValue") : MigrationCommon.Get<float>(old, "compareValue");
                    object compare = Ported(old, "FloatCompareResponse", "isGreaterThanListenValue", "isLessThanListenValue").First();
                    MigrationCommon.Set(compare, "compareTo", compareTo);
                    Component compareListener = AddTypedListener(gameObject, newType, wasEnabled, events, null, MigrationCommon.Get<bool>(old, "updateOnEnable"));
                    MigrationCommon.GetListProperty(compareListener, "Responses").Add(compare);
                    EditorUtility.SetDirty(compareListener);
                }
            }

            if (removeOld)
            {
                Undo.DestroyObjectImmediate(old);
            }
            else if (old is Behaviour oldBehaviour)
            {
                oldBehaviour.enabled = false;
                EditorUtility.SetDirty(oldBehaviour);
            }
        }

        private static Component AddTypedListener(GameObject gameObject, Type newType, bool enabled, List<object> events, object filter, bool receiveCurrentValueOnEnable)
        {
            Component listener = Undo.AddComponent(gameObject, newType);
            ((Behaviour)listener).enabled = enabled;
            IList eventList = MigrationCommon.GetListProperty(listener, "Events");
            foreach (object action in events)
            {
                eventList.Add(action);
            }

            if (filter != null)
            {
                MigrationCommon.SetProperty(listener, "Filter", filter);
            }

            MigrationCommon.SetProperty(listener, "ReceiveCurrentValueOnEnable", receiveCurrentValueOnEnable);
            return listener;
        }

        /// <summary>The ActionSO assets in the old listener's "actions" list (MessageSubscription&lt;T&gt;.Action), as IVariableEvent&lt;T&gt;.</summary>
        private static List<object> OldActions(Component old, Type valueType)
        {
            Type expected = typeof(IVariableEvent<>).MakeGenericType(valueType);
            var result = new List<object>();
            if (MigrationCommon.Get(old, "actions") is IEnumerable subscriptions)
            {
                foreach (object subscription in subscriptions)
                {
                    // Empty slots are dropped; a reference to a deleted asset is kept (and reported), like BaseActionListener's.
                    object action = MigrationCommon.Get(subscription, "Action");
                    if (action == null)
                    {
                        continue;
                    }

                    if (!expected.IsInstanceOfType(action))
                    {
                        throw new InvalidOperationException($"{(UnityNull.IsNull(action) ? "A missing asset" : ((Object)action).name)} isn't an {expected.Name}. Is the bridge patch applied?");
                    }

                    result.Add(action);
                }
            }

            return result;
        }

        /// <summary>
        /// Rebuilds the old pass rule as one Filter:
        /// nulls are ignored (unless checkNullValue), and with listen values on, the value must equal the
        /// listen value or one of filterValues (inverted by _invertListenValue).
        /// </summary>
        private static object BuildFilter(Component old, Type valueType)
        {
            var parts = new List<object>();
            if (!valueType.IsValueType && !MigrationCommon.Get<bool>(old, "checkNullValue"))
            {
                parts.Add(Composite(valueType, CombineMode.None, NewGeneric(typeof(NullFilter<>), valueType)));
            }

            object listenValue = MigrationCommon.Get(old, "_listenValue");
            if (MigrationCommon.Get<bool>(old, "_useListenValues") && listenValue != null)
            {
                var equalities = new List<object> { Equality(valueType, listenValue) };
                if (MigrationCommon.Get(old, "filterValues") is IEnumerable filterValues)
                {
                    foreach (object value in filterValues)
                    {
                        if (value != null)
                        {
                            equalities.Add(Equality(valueType, value));
                        }
                    }
                }

                object listen = equalities.Count == 1 ? equalities[0] : Composite(valueType, CombineMode.Any, equalities.ToArray());
                if (MigrationCommon.Get<bool>(old, "_invertListenValue"))
                {
                    listen = Composite(valueType, CombineMode.None, listen);
                }

                parts.Add(listen);
            }

            if (parts.Count == 0)
            {
                return null;
            }

            return parts.Count == 1 ? parts[0] : Composite(valueType, CombineMode.All, parts.ToArray());
        }

        private static object Equality(Type valueType, object value)
        {
            object reference = Activator.CreateInstance(typeof(VariableReference<>).MakeGenericType(valueType), value);
            return Activator.CreateInstance(typeof(EqualityFilter<>).MakeGenericType(valueType), reference);
        }

        private static object Composite(Type valueType, CombineMode mode, params object[] parts)
        {
            Type filterInterface = typeof(IFilter<>).MakeGenericType(valueType);
            Array array = Array.CreateInstance(filterInterface, parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                array.SetValue(parts[i], i);
            }

            return Activator.CreateInstance(typeof(CompositeFilter<>).MakeGenericType(valueType), mode, array);
        }

        private static object NewGeneric(Type definition, Type argument) => Activator.CreateInstance(definition.MakeGenericType(argument));

        private static IResponse EventResponse(UnityEvent unityEvent)
        {
            var response = new UnityEventResponse();
            if (unityEvent != null)
            {
                MigrationCommon.Set(response, "onRaised", unityEvent);
            }

            return response;
        }

        private static object TypedEventResponse(Type valueType, object unityEvent)
        {
            object response = NewGeneric(typeof(UnityEventResponse<>), valueType);
            MigrationCommon.Set(response, "onRaised", unityEvent);
            return response;
        }

        /// <summary>Creates a ported Response and moves the same-named fields (UnityEvents, processors) across from the old listener.</summary>
        private static IEnumerable<object> Ported(Component old, string responseType, params string[] fields)
        {
            Type type = MigrationCommon.FindType(responseType);
            if (type == null)
            {
                throw new InvalidOperationException($"Response type {responseType} not found (copy Runtime/Responses into the project).");
            }

            object response = Activator.CreateInstance(type);
            foreach (string field in fields)
            {
                object value = MigrationCommon.Get(old, field);
                if (value != null && !MigrationCommon.Set(response, field, value))
                {
                    throw new InvalidOperationException($"Couldn't move {old.GetType().Name}.{field} into {responseType}.");
                }
            }

            yield return response;
        }
    }
}
