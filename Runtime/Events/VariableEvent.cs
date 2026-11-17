using System;
using System.Collections.Generic;
using RonJames.Foundation.Values;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace RonJames.Foundation.Events
{
    /// <summary>
    /// A shared asset that holds a typed value and notifies its Listeners whenever that value is Set
    /// or Raised (ADR 0002). Each play session starts from the authored Initial Value; the Runtime
    /// Value is never saved back to the asset.
    /// </summary>
    /// <remarks>
    /// Unity cannot create assets of an open generic type, so each value type needs a one-line
    /// subclass, e.g. <c>public class IntVariableEvent : VariableEvent&lt;int&gt; { }</c>.
    /// Use "Assets/Create/RonJames Foundation/Variable Event Type…" to generate it.
    /// </remarks>
    public abstract class VariableEvent<T> : SerializedScriptableObject, IVariableEvent<T>, IPlayModeResettable
    {
        [SerializeField]
        [TextArea(2, 5)]
        [Tooltip("What this value means and who sets it. For people, not code.")]
        private string description = "";

        [SerializeField]
        [LabelText("Initial Value")]
        [Tooltip("The value every play session starts with.")]
        private T initialValue;

        [SerializeField]
        [Tooltip("When on, Set only Raises if the new value differs from the current one. Leave off for event-like values, " +
                 "where the same value Set twice (\"the same item was picked twice\") must still notify.")]
        private bool onlyRaiseOnChange;

        [SerializeField]
        [Tooltip("Editor only: keep the Runtime Value between play sessions instead of resetting it to the Initial Value. " +
                 "Builds always start from the Initial Value.")]
        private bool keepRuntimeValueInEditor = false;

        [NonSerialized]
        private readonly ListenerList listeners = new ListenerList();

        [NonSerialized]
        private T runtimeValue;

        [NonSerialized]
        private int raiseCount;

        /// <summary>What this value means, for people.</summary>
        public string Description => description;

        /// <summary>The value every play session starts with.</summary>
        public T InitialValue
        {
            get => initialValue;
            set => initialValue = value;
        }

        /// <summary>When true, Set only Raises if the value actually changes.</summary>
        public bool OnlyRaiseOnChange
        {
            get => onlyRaiseOnChange;
            set => onlyRaiseOnChange = value;
        }

        /// <summary>The Runtime Value.</summary>
        public T Value => runtimeValue;

        object IValueSource.Value => runtimeValue;

        /// <inheritdoc />
        public Type ValueType => typeof(T);

        /// <summary>The number of subscribed Listeners (for tests and debugging).</summary>
        [ShowInInspector]
        [ReadOnly]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        [PropertyOrder(10)]
        public int ListenerCount => listeners.Count;

        /// <summary>How many times this event has been Raised this play session.</summary>
        [ShowInInspector]
        [ReadOnly]
        [ShowIf("@UnityEngine.Application.isPlaying")]
        [PropertyOrder(10)]
        public int RaiseCount => raiseCount;

        [ShowInInspector]
        [LabelText("Runtime Value")]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        [Tooltip("The current value. Editing it in play mode Sets it (and Raises).")]
        [PropertyOrder(5)]
        private T RuntimeValueInspector
        {
            get => runtimeValue;
            set => Set(value);
        }

        /// <inheritdoc />
        public void Set(T value)
        {
            if (onlyRaiseOnChange && ValueEquality.AreEqual(runtimeValue, value))
            {
                return;
            }

            runtimeValue = value;
            StashForEditor();
            Raise();
        }

        /// <inheritdoc />
        public void SetSilently(T value)
        {
            runtimeValue = value;
            StashForEditor();
        }

        /// <summary>
        /// Raises the current value, falling back to the Initial Value when the current value is null
        /// (or a destroyed Unity object). The fallback is stored before Listeners run, so they read the
        /// same value they're given. For value types the current value is never null, so this is the
        /// same as <see cref="Raise"/>. Safe to wire to UnityEvents.
        /// </summary>
        [Button]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        [PropertyOrder(6)]
        public void RaiseCurrent()
        {
            if (UnityNull.IsNull(runtimeValue))
            {
                runtimeValue = CopyOf(initialValue);
                StashForEditor();
            }

            Raise();
        }

        /// <inheritdoc />
        public void SetValue(T value) => Set(value);

        void IWritableValueSource.SetValue(object value) => Set(ValueConversion.To<T>(value, this));

        /// <summary>
        /// Notifies every Listener with the current value. If a Listener Sets this event again, that
        /// nested Raise is delivered immediately, and Listeners later in the outer Raise read the newest
        /// value, so everyone ends up with the latest value.
        /// </summary>
        [Button]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        [PropertyOrder(6)]
        public void Raise()
        {
            object[] current = listeners.BeginRaise(this);
            if (current == null)
            {
                return;
            }

            raiseCount++;
            try
            {
                foreach (object listener in current)
                {
                    if (ListenerList.ShouldSkip(listener))
                    {
                        continue;
                    }

                    try
                    {
                        switch (listener)
                        {
                            case IVariableEventListener<T> typedListener:
                                typedListener.OnEventRaised(runtimeValue);
                                break;
                            case IEventListener eventListener:
                                eventListener.OnEventRaised();
                                break;
                            case Action<T> typedCallback:
                                typedCallback(runtimeValue);
                                break;
                            case Action callback:
                                callback();
                                break;
                        }
                    }
                    catch (Exception exception)
                    {
                        ListenerList.Report(exception, listener, this);
                    }
                }
            }
            finally
            {
                listeners.EndRaise();
            }
        }

        /// <inheritdoc />
        public void Subscribe(IVariableEventListener<T> listener) => listeners.Add(listener);

        /// <inheritdoc />
        public void Unsubscribe(IVariableEventListener<T> listener) => listeners.Remove(listener);

        /// <inheritdoc />
        public void Subscribe(Action<T> listener) => listeners.Add(listener);

        /// <inheritdoc />
        public void Unsubscribe(Action<T> listener) => listeners.Remove(listener);

        /// <inheritdoc />
        public void Subscribe(IEventListener listener) => listeners.Add(listener);

        /// <inheritdoc />
        public void Unsubscribe(IEventListener listener) => listeners.Remove(listener);

        /// <inheritdoc />
        public void Subscribe(Action listener) => listeners.Add(listener);

        /// <inheritdoc />
        public void Unsubscribe(Action listener) => listeners.Remove(listener);

        /// <inheritdoc />
        public event Action ValueChanged
        {
            add => listeners.Add(value);
            remove => listeners.Remove(value);
        }

        /// <summary>Puts the Runtime Value back to (a copy of) the Initial Value and drops every Listener.</summary>
        public void ResetForPlayMode()
        {
            listeners.Clear();
            raiseCount = 0;
            if (!TryRestoreFromEditor())
            {
                runtimeValue = CopyOf(initialValue);
            }
        }

        /// <summary>Starts from the Initial Value and registers for the play-mode reset.</summary>
        protected virtual void OnEnable()
        {
            if (!TryRestoreFromEditor())
            {
                runtimeValue = CopyOf(initialValue);
            }

            PlayModeReset.Register(this);
        }

        /// <summary>Unregisters from the play-mode reset.</summary>
        protected virtual void OnDisable()
        {
            PlayModeReset.Unregister(this);
        }

        /// <summary>Keeps the Runtime Value in step with the Initial Value while editing outside play mode.</summary>
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying)
            {
                runtimeValue = CopyOf(initialValue);
            }
#if UNITY_EDITOR
            if (!keepRuntimeValueInEditor)
            {
                UnityEditor.SessionState.EraseString(EditorStashKey);
            }
#endif
        }

        /// <summary>
        /// Copies reference-type values so play-mode changes to the Runtime Value can never mutate the
        /// authored Initial Value (for example adding to a list). Unity objects and value types are shared.
        /// </summary>
        private static T CopyOf(T value)
        {
            if (value == null)
            {
                return default;
            }

            if (value is UnityEngine.Object || value is string || value.GetType().IsValueType)
            {
                return value;
            }

            return (T)SerializationUtility.CreateCopy(value);
        }

#if UNITY_EDITOR
        private string EditorStashKey => $"RonJames.Foundation.VariableEvent.{UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this)}";
#endif

        private void StashForEditor()
        {
#if UNITY_EDITOR
            if (!keepRuntimeValueInEditor || !Application.isPlaying)
            {
                return;
            }

            try
            {
                byte[] bytes = SerializationUtility.SerializeValue(runtimeValue, DataFormat.Binary, out List<UnityEngine.Object> unityObjects);
                var ids = new string[unityObjects?.Count ?? 0];
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = unityObjects[i] != null ? UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(unityObjects[i]).ToString() : string.Empty;
                }

                UnityEditor.SessionState.SetString(EditorStashKey, Convert.ToBase64String(bytes) + "\n" + string.Join("|", ids));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RonJames Foundation] Could not keep the value of '{name}' between play sessions: {exception.Message}", this);
            }
#endif
        }

        private bool TryRestoreFromEditor()
        {
#if UNITY_EDITOR
            if (!keepRuntimeValueInEditor)
            {
                return false;
            }

            string stash = UnityEditor.SessionState.GetString(EditorStashKey, string.Empty);
            if (string.IsNullOrEmpty(stash))
            {
                return false;
            }

            try
            {
                string[] parts = stash.Split('\n');
                var unityObjects = new List<UnityEngine.Object>();
                if (parts.Length > 1 && parts[1].Length > 0)
                {
                    foreach (string id in parts[1].Split('|'))
                    {
                        UnityEngine.Object resolved = null;
                        if (UnityEditor.GlobalObjectId.TryParse(id, out UnityEditor.GlobalObjectId globalId))
                        {
                            resolved = UnityEditor.GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                        }

                        unityObjects.Add(resolved);
                    }
                }

                runtimeValue = SerializationUtility.DeserializeValue<T>(Convert.FromBase64String(parts[0]), DataFormat.Binary, unityObjects);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[RonJames Foundation] Could not restore the kept value of '{name}': {exception.Message}", this);
                return false;
            }
#else
            return false;
#endif
        }
    }
}
