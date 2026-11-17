using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RonJames.Foundation.Commands
{
    /// <summary>
    /// One configured unit of work in a Sequence. A Command is pure configuration: everything that
    /// changes while it runs lives in locals inside <see cref="Execute"/> or in the Run (ADR 0003),
    /// so the same Command can run more than once, even at the same time.
    /// </summary>
    public interface ICommand
    {
        /// <summary>The name shown in the inspector and in the Run tree.</summary>
        string Name { get; }

        /// <summary>A Disabled Command is passed over by Runs without executing.</summary>
        bool Enabled { get; }

        /// <summary>
        /// Does the work. Honour <see cref="CommandContext.Token"/>: pass it to every await, and release
        /// anything you acquire in a <c>finally</c>, which the Run guarantees runs on Skip and Stop.
        /// </summary>
        Awaitable Execute(CommandContext context);
    }

    /// <summary>Base class for Commands: an optional display name and the enabled toggle.</summary>
    public abstract class Command : ICommand
    {
        [SerializeField]
        [ToggleLeft]
        [HorizontalGroup("Header", Width = 74)]
        [LabelText("Enabled")]
        [PropertyOrder(-100)]
        private bool enabled = true;

        [SerializeField]
        [HorizontalGroup("Header")]
        [HideLabel]
        [SuffixLabel("name", Overlay = true)]
        [PropertyOrder(-99)]
        private string name;

        /// <inheritdoc />
        public virtual string Name => string.IsNullOrEmpty(name) ? DefaultName : name;

        /// <inheritdoc />
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        /// <summary>Sets the display name (handy when building Sequences from code).</summary>
        public Command Named(string displayName)
        {
            name = displayName;
            return this;
        }

        /// <summary>The name used when none is authored: the type name split into words, without "Command".</summary>
        protected virtual string DefaultName
        {
            get
            {
                string typeName = GetType().Name;
                int genericTick = typeName.IndexOf('`');
                if (genericTick >= 0)
                {
                    typeName = typeName.Substring(0, genericTick);
                }

                if (typeName.EndsWith("Command") && typeName.Length > "Command".Length)
                {
                    typeName = typeName.Substring(0, typeName.Length - "Command".Length);
                }

                return Regex.Replace(typeName, "(?<=[a-z0-9])(?=[A-Z])", " ");
            }
        }

        /// <inheritdoc />
        public abstract Awaitable Execute(CommandContext context);

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    /// <summary>Base class for Commands that finish immediately, so they needn't write an async method.</summary>
    public abstract class InstantCommand : Command
    {
        /// <summary>Does the work synchronously.</summary>
        protected abstract void Run(CommandContext context);

        /// <inheritdoc />
        public sealed override Awaitable Execute(CommandContext context)
        {
            Run(context);
            return AwaitableUtility.Completed();
        }
    }
}
