using System;
using RonJames.Foundation.Events;
using RonJames.Foundation.Values;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Events;

namespace RonJames.Foundation.Commands
{
    /// <summary>Raises an Event (or re-Raises a Variable Event with its current value).</summary>
    public class RaiseEventCommand : InstantCommand
    {
        [OdinSerialize]
        [Required]
        private IEvent target;

        /// <summary>Creates an empty command.</summary>
        public RaiseEventCommand()
        {
        }

        /// <summary>Creates the command from code.</summary>
        public RaiseEventCommand(IEvent target)
        {
            this.target = target;
        }

        /// <inheritdoc />
        protected override void Run(CommandContext context)
        {
            if (UnityNull.IsNull(target))
            {
                throw new InvalidOperationException("Raise Event has no Event.");
            }

            target.Raise();
        }
    }

    /// <summary>
    /// Writes a value into a writable Value Source. For a Variable Event this is a Set (which
    /// Raises), or a Set Silently when "silently" is on.
    /// </summary>
    public class SetVariableCommand<T> : InstantCommand
    {
        [OdinSerialize]
        [Required]
        private IWritableValueSource<T> target;

        [SerializeField]
        [LabelText("Value")]
        private VariableReference<T> value = new VariableReference<T>();

        [SerializeField]
        [Tooltip("Variable Events only: change the value without Raising.")]
        private bool silently;

        /// <summary>Creates an empty command.</summary>
        public SetVariableCommand()
        {
        }

        /// <summary>Creates the command from code.</summary>
        public SetVariableCommand(IWritableValueSource<T> target, T value, bool silently = false)
        {
            this.target = target;
            this.value = new VariableReference<T>(value);
            this.silently = silently;
        }

        /// <inheritdoc />
        protected override void Run(CommandContext context)
        {
            if (UnityNull.IsNull(target))
            {
                throw new InvalidOperationException($"Set Variable ({typeof(T).Name}) has no target.");
            }

            T newValue = value.Value;
            if (silently && target is IVariableEvent<T> variableEvent)
            {
                variableEvent.SetSilently(newValue);
            }
            else
            {
                target.SetValue(newValue);
            }
        }
    }

    /// <summary>Activates or deactivates a GameObject.</summary>
    public class SetActiveCommand : InstantCommand
    {
        [SerializeField]
        private VariableReference<GameObject> target = new VariableReference<GameObject>();

        [SerializeField]
        private VariableReference<bool> active = new VariableReference<bool>(true);

        /// <summary>Creates an empty command.</summary>
        public SetActiveCommand()
        {
        }

        /// <summary>Creates the command from code.</summary>
        public SetActiveCommand(GameObject target, bool active)
        {
            this.target = new VariableReference<GameObject>(target);
            this.active = new VariableReference<bool>(active);
        }

        /// <inheritdoc />
        protected override void Run(CommandContext context)
        {
            GameObject gameObject = target.Value;
            if (gameObject == null)
            {
                throw new InvalidOperationException("Set Active has no target GameObject (or it was destroyed).");
            }

            gameObject.SetActive(active.Value);
        }
    }

    /// <summary>Invokes a UnityEvent.</summary>
    public class InvokeUnityEventCommand : InstantCommand
    {
        [SerializeField]
        private UnityEvent unityEvent = new UnityEvent();

        /// <summary>The UnityEvent, exposed for code.</summary>
        public UnityEvent UnityEvent => unityEvent;

        /// <inheritdoc />
        protected override void Run(CommandContext context) => unityEvent.Invoke();
    }

    /// <summary>Writes a message to the Console.</summary>
    public class LogCommand : InstantCommand
    {
        [SerializeField]
        [TextArea(1, 4)]
        private string message = "";

        [SerializeField]
        private LogType level = LogType.Log;

        [SerializeField]
        [Tooltip("Optional. Clicking the message selects this object. Defaults to the Command Sequence.")]
        private UnityEngine.Object context = null;

        /// <summary>Creates an empty command.</summary>
        public LogCommand()
        {
        }

        /// <summary>Creates the command from code.</summary>
        public LogCommand(string message, LogType level = LogType.Log)
        {
            this.message = message;
            this.level = level;
        }

        /// <inheritdoc />
        protected override void Run(CommandContext commandContext)
        {
            UnityEngine.Object target = context != null ? context : commandContext.Owner;
            Debug.unityLogger.Log(level, (object)message, target);
        }
    }

    /// <summary>An Event Listener Response that starts a Run of a Command Sequence.</summary>
    public class RunCommandSequenceResponse : IResponse
    {
        [SerializeField]
        [Required]
        private CommandSequence target;

        /// <summary>Creates an empty response.</summary>
        public RunCommandSequenceResponse()
        {
        }

        /// <summary>Creates the response from code.</summary>
        public RunCommandSequenceResponse(CommandSequence target)
        {
            this.target = target;
        }

        /// <inheritdoc />
        public void Respond()
        {
            if (target == null)
            {
                throw new InvalidOperationException("Run Command Sequence response has no target.");
            }

            target.Run();
        }
    }

    /// <summary>A Variable Event Listener Response that starts a Run of a Command Sequence (the value is ignored).</summary>
    public class RunCommandSequenceResponse<T> : IResponse<T>
    {
        [SerializeField]
        [Required]
        private CommandSequence target;

        /// <summary>Creates an empty response.</summary>
        public RunCommandSequenceResponse()
        {
        }

        /// <summary>Creates the response from code.</summary>
        public RunCommandSequenceResponse(CommandSequence target)
        {
            this.target = target;
        }

        /// <inheritdoc />
        public void Respond(T value)
        {
            if (target == null)
            {
                throw new InvalidOperationException("Run Command Sequence response has no target.");
            }

            target.Run();
        }
    }
}
